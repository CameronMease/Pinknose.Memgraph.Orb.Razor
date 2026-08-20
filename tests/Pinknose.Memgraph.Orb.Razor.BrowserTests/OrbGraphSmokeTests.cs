using System.Text.RegularExpressions;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace Pinknose.Memgraph.Orb.Razor.BrowserTests;

// Deviation from the brief: does not inherit Microsoft.Playwright.MSTest's PageTest. See the
// comment on the Microsoft.Playwright PackageReference in the .csproj for why -- in short,
// Microsoft.Playwright.MSTest 1.62.0 cannot coexist with MSTest 4.3.3 in the same project.
// This class replicates the slice of PageTest's behavior the tests need: one browser for the
// whole class, a fresh isolated context/page per test.
[TestClass]
public class OrbGraphSmokeTests
{
    private const string CountPaintedPixels = """
        () => {
            const c = document.querySelector('.orb-graph canvas');
            if (!c) return -1;
            const d = c.getContext('2d').getImageData(0, 0, c.width, c.height).data;
            let painted = 0;
            for (let i = 3; i < d.length; i += 4) { if (d[i] !== 0) painted++; }
            return painted;
        }
        """;

    private const string ReadNodePositions = """
        () => {
            const v = window.__orbTestView;
            return v ? JSON.stringify(v.data.getNodePositions()) : null;
        }
        """;

    // The brief's version reads n.getCenter() and treats it directly as a canvas point.
    // getCenter() actually returns the node's position in Orb's *graph* (simulation) space,
    // not canvas pixels: Orb's canvas renderer draws with
    //   ctx.translate(transform.x, transform.y); ctx.scale(transform.k, transform.k);
    //   ctx.translate(width / 2, height / 2)   // OrbView always calls translateOriginToCenter()
    // so a graph point (gx, gy) lands on the canvas at
    //   (transform.x + transform.k * (gx + width / 2), transform.y + transform.k * (gy + height / 2)).
    // view._renderer is not exposed via a public getter but is a plain (non-#private) field,
    // so it is reachable from test code the same way the brief already reaches into
    // window.__orbTestView. Verified against orb.min.js 1.0.2 (renderer's `_render`,
    // `getSimulationPosition`, and `translateOriginToCenter` implementations).
    private const string FindNodePoint = """
        () => {
            const v = window.__orbTestView;
            const n = v.data.getNodes()[0];
            const c = n.getCenter();
            const r = v._renderer;
            const t = r.transform;
            const x = t.x + t.k * (c.x + r.width / 2);
            const y = t.y + t.k * (c.y + r.height / 2);
            return JSON.stringify({ x, y });
        }
        """;

    private static IPlaywright _playwright = null!;
    private static IBrowser _browser = null!;

    private IBrowserContext _context = null!;
    private IPage _page = null!;

    [ClassInitialize]
    public static async Task LaunchBrowserAsync(TestContext _)
    {
        _playwright = await Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync();
    }

    [ClassCleanup]
    public static async Task CloseBrowserAsync()
    {
        await _browser.DisposeAsync();
        _playwright.Dispose();
    }

    [TestInitialize]
    public async Task GoToDemoAsync()
    {
        _context = await _browser.NewContextAsync();
        _page = await _context.NewPageAsync();

        await _page.GotoAsync($"{SampleHostFixture.BaseUrl}/orb-demo");
        await _page.WaitForFunctionAsync("() => !!document.querySelector('.orb-graph canvas')");
        // Let the force simulation settle so pixel and position reads are stable.
        await _page.WaitForTimeoutAsync(2000);
    }

    [TestCleanup]
    public async Task CloseContextAsync()
    {
        await _context.CloseAsync();
    }

    [TestMethod]
    public async Task Graph_RendersANonBlankCanvas()
    {
        var painted = await _page.EvaluateAsync<int>(CountPaintedPixels);

        Assert.IsGreaterThan(0, painted, "the canvas rendered nothing");
    }

    [TestMethod]
    public async Task ClickingANode_RaisesOnNodeClickWithTheOriginalInstance()
    {
        await ClickFirstNodeAsync();

        await Expect(_page.Locator("#selection-label")).Not.ToHaveTextAsync("none");
    }

    [TestMethod]
    public async Task HoveringANode_RaisesEnterThenLeave()
    {
        var box = await _page.Locator(".orb-graph canvas").BoundingBoxAsync();
        var position = await FindNodePointAsync();

        await _page.Mouse.MoveAsync(box!.X + position.X, box.Y + position.Y);
        await Expect(_page.Locator("#hover-label")).Not.ToHaveTextAsync("none");

        await _page.Mouse.MoveAsync(box.X + 5, box.Y + 5);
        await Expect(_page.Locator("#hover-label")).ToHaveTextAsync("none");
    }

    [TestMethod]
    public async Task RemovingANode_DropsItAndItsEdge()
    {
        var before = await _page.EvaluateAsync<int>(
            "() => window.__orbTestView.data.getNodeCount()");

        await _page.ClickAsync("#remove-btn");
        await _page.WaitForFunctionAsync(
            $"() => window.__orbTestView.data.getNodeCount() === {before - 1}");

        var edges = await _page.EvaluateAsync<int>(
            "() => window.__orbTestView.data.getEdgeCount()");

        Assert.AreEqual(1, edges, "removing a node must take its edges with it");
    }

    [TestMethod]
    public async Task UpdatingNodes_PreservesExistingPositions()
    {
        var before = await ReadPositionAsync("n1");

        var nodeCountBefore = await _page.EvaluateAsync<int>(
            "() => window.__orbTestView.data.getNodeCount()");

        await _page.ClickAsync("#remove-btn");
        // Wait for the actual state change (merge/remove has applied) rather than guessing
        // how long that takes; the fixed wait below is for a different, unavoidable reason.
        await _page.WaitForFunctionAsync(
            $"() => window.__orbTestView.data.getNodeCount() === {nodeCountBefore - 1}");

        // Orb's remove()/merge() both funnel into activateSimulation(), which pins or
        // unpins nodes depending on isPhysicsEnabled. This sample never sets
        // OrbForceLayout.IsPhysicsEnabled, so it's Orb's default: false -- meaning
        // activateSimulation() *pins* nodes at their current position instead of reheating
        // them. Measured directly (3 runs): n1 moved exactly 0.0000 units after a remove +
        // 500ms wait. A short wait is still kept below in case that default ever changes
        // (e.g. physics gets turned on) and a node legitimately drifts a little.
        await _page.WaitForTimeoutAsync(500);

        var after = await ReadPositionAsync("n1");
        var distance = Distance(before, after);

        // The regression this test exists to catch is updateData() calling setup() instead
        // of merge(): setup() clears all positions and lays the graph out from scratch.
        // Measured directly by making that exact swap and re-running this test unmodified:
        // n1 moved 22.7894 units, reproducibly (3 runs, bit-identical -- the fallback layout
        // here is deterministic, not randomized). 10 sits well above the observed jitter
        // (0, given IsPhysicsEnabled defaults to false -- see above) and well below the
        // observed from-scratch re-layout distance (~22.8), so it discriminates the two
        // without depending on exact-float equality against a physics simulation.
        Assert.IsLessThan(10.0, distance,
            $"merge must not reset simulated positions (n1 moved {distance:F2} units)");
    }

    [TestMethod]
    public async Task UnstyledNodesAndEdges_RenderWithOrbsDefaultStyleNotZeroed()
    {
        // This test navigates itself, unlike the others above which rely on the
        // [TestInitialize] navigation to /orb-demo: the regression under test only
        // reproduces for nodes/edges with neither Label nor Style set, and /orb-demo's
        // Person always sets Style while its Relationship always sets Label.
        await _page.GotoAsync($"{SampleHostFixture.BaseUrl}/orb-unstyled");
        await _page.WaitForFunctionAsync("() => !!document.querySelector('.orb-graph canvas')");
        await _page.WaitForTimeoutAsync(2000);

        // Regression: pushStyles() used to push {} for nodes/edges with no projected style,
        // wholesale-replacing the default OrbView's constructor had just applied via
        // setDefaultStyle()/_applyStyle(). That left getRadius() === 0 (invisible, unhittable)
        // and getWidth() === 0 (edge never drawn). Assert Orb's real default survived instead.
        var allNodesHaveRadius = await _page.EvaluateAsync<bool>(
            "() => window.__orbTestView.data.getNodes().every(n => n.getRadius() > 0)");
        Assert.IsTrue(allNodesHaveRadius, "every unstyled node must render at a non-zero radius");

        var allEdgesHaveWidth = await _page.EvaluateAsync<bool>(
            "() => window.__orbTestView.data.getEdges().every(e => e.getWidth() > 0)");
        Assert.IsTrue(allEdgesHaveWidth, "every unstyled edge must render at a non-zero width");

        var painted = await _page.EvaluateAsync<int>(CountPaintedPixels);
        Assert.IsGreaterThan(0, painted, "the canvas rendered nothing");
    }

    [TestMethod]
    public async Task NavigatingAway_DisposesWithoutServerError()
    {
        await _page.ClickAsync("a[href='counter']");
        await Expect(_page.Locator("h1")).ToHaveTextAsync("Counter");

        // Blazor Server shows this banner when a circuit dies. A teardown exception in
        // OrbGraph.DisposeAsync would kill the circuit even if nothing reached
        // console.error, so check it in addition to the server log below.
        await Expect(_page.Locator("#blazor-error-ui")).ToBeHiddenAsync();

        // Whether a teardown exception reaches the browser console depends on client-side
        // log wiring and DetailedErrors, neither of which the test controls. The server log
        // is where an unhandled exception in a component's DisposeAsync unconditionally
        // lands, so assert there instead of (only) on Page.Console.
        var output = SampleHostFixture.HostOutput;
        StringAssert.DoesNotMatch(output, new Regex("Unhandled exception", RegexOptions.IgnoreCase));
    }

    private async Task ClickFirstNodeAsync()
    {
        var box = await _page.Locator(".orb-graph canvas").BoundingBoxAsync();
        var position = await FindNodePointAsync();

        await _page.Mouse.ClickAsync(box!.X + position.X, box.Y + position.Y);
    }

    private async Task<(float X, float Y)> FindNodePointAsync()
    {
        var json = await _page.EvaluateAsync<string>(FindNodePoint);

        var point = System.Text.Json.JsonDocument.Parse(json).RootElement;
        return ((float)point.GetProperty("x").GetDouble(),
                (float)point.GetProperty("y").GetDouble());
    }

    private async Task<(double X, double Y)> ReadPositionAsync(string id)
    {
        var json = await _page.EvaluateAsync<string>(ReadNodePositions);
        return ExtractPosition(json, id);
    }

    private static (double X, double Y) ExtractPosition(string positionsJson, string id)
    {
        using var doc = System.Text.Json.JsonDocument.Parse(positionsJson);

        foreach (var element in doc.RootElement.EnumerateArray())
        {
            if (element.GetProperty("id").GetString() == id)
            {
                return (element.GetProperty("x").GetDouble(), element.GetProperty("y").GetDouble());
            }
        }

        throw new InvalidOperationException($"No node with id '{id}' in the position snapshot.");
    }

    private static double Distance((double X, double Y) a, (double X, double Y) b)
    {
        var dx = a.X - b.X;
        var dy = a.Y - b.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }
}
