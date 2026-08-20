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

    // The two sample pages render the same OrbDemoView component and differ only by render
    // mode, so every test below runs against both: anything that passes on one and fails on
    // the other is a real Blazor Server vs WebAssembly difference in the library, not a
    // difference between two hand-maintained demo pages.
    private const string ServerRoute = "/orb-server";
    private const string WasmRoute = "/orb-wasm";

    private static IPlaywright _playwright = null!;
    private static IBrowser _browser = null!;

    private readonly List<string> _consoleErrors = [];

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
    public async Task OpenPageAsync()
    {
        _context = await _browser.NewContextAsync();
        _page = await _context.NewPageAsync();
        _page.Console += (_, message) =>
        {
            if (message.Type == "error")
            {
                lock (_consoleErrors)
                {
                    _consoleErrors.Add(message.Text);
                }
            }
        };
    }

    [TestCleanup]
    public async Task CloseContextAsync()
    {
        await _context.CloseAsync();
    }

    private async Task GoToAsync(string route)
    {
        await _page.GotoAsync($"{SampleHostFixture.BaseUrl}{route}");
        // WebAssembly has to download and start the runtime before it renders anything, which
        // is far slower than a Server circuit -- especially on the first test to hit it.
        await _page.WaitForFunctionAsync(
            "() => !!document.querySelector('.orb-graph canvas')",
            null,
            new PageWaitForFunctionOptions { Timeout = 60_000 });
        // Let the force simulation settle so pixel and position reads are stable.
        await _page.WaitForTimeoutAsync(2000);
    }

    // Everything else in this class assumes the two routes really are Server and WebAssembly.
    // Nothing else proves it: a page whose @rendermode silently stopped taking effect would
    // still render a graph (the other render mode would just pick it up) and every test here
    // would keep passing while quietly testing the same mode twice.
    [TestMethod]
    public async Task TheTwoRoutes_ActuallyRunInDifferentRenderModes()
    {
        await GoToAsync(WasmRoute);
        Assert.IsTrue(
            await LoadedTheWebAssemblyRuntimeAsync(),
            "/orb-wasm did not load the WebAssembly runtime, so it is not running client-side");

        await GoToAsync(ServerRoute);
        Assert.IsFalse(
            await LoadedTheWebAssemblyRuntimeAsync(),
            "/orb-server loaded the WebAssembly runtime, so it is not running on a circuit");
    }

    [TestMethod]
    [DataRow(ServerRoute)]
    [DataRow(WasmRoute)]
    public async Task Graph_RendersANonBlankCanvas(string route)
    {
        await GoToAsync(route);

        var painted = await _page.EvaluateAsync<int>(CountPaintedPixels);

        Assert.IsGreaterThan(0, painted, "the canvas rendered nothing");
    }

    [TestMethod]
    [DataRow(ServerRoute)]
    [DataRow(WasmRoute)]
    public async Task ClickingANode_RaisesOnNodeClickWithTheOriginalInstance(string route)
    {
        await GoToAsync(route);

        await ClickFirstNodeAsync();

        await Expect(_page.Locator("#selection-label")).Not.ToHaveTextAsync("none");
    }

    [TestMethod]
    [DataRow(ServerRoute)]
    [DataRow(WasmRoute)]
    public async Task HoveringANode_RaisesEnterThenLeave(string route)
    {
        await GoToAsync(route);

        var box = await _page.Locator(".orb-graph canvas").BoundingBoxAsync();
        var position = await FindNodePointAsync();

        await _page.Mouse.MoveAsync(box!.X + position.X, box.Y + position.Y);
        await Expect(_page.Locator("#hover-label")).Not.ToHaveTextAsync("none");

        await _page.Mouse.MoveAsync(box.X + 5, box.Y + 5);
        await Expect(_page.Locator("#hover-label")).ToHaveTextAsync("none");
    }

    [TestMethod]
    [DataRow(ServerRoute)]
    [DataRow(WasmRoute)]
    public async Task RemovingANode_DropsItAndItsEdge(string route)
    {
        await GoToAsync(route);

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
    [DataRow(ServerRoute)]
    [DataRow(WasmRoute)]
    public async Task UpdatingNodes_PreservesExistingPositions(string route)
    {
        await GoToAsync(route);

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
    [DataRow(ServerRoute)]
    [DataRow(WasmRoute)]
    public async Task UnstyledNodesAndEdges_RenderWithOrbsDefaultStyleNotZeroed(string route)
    {
        // ?styled=false makes OrbDemoView build its nodes and edges with neither Label nor
        // Style set, which is the only case that reproduces the regression below. It has to
        // be set on the initial navigation rather than by clicking the toggle, because the
        // regression is about the FIRST push for a node that never had a style.
        await GoToAsync($"{route}?styled=false");

        // Regression: pushStyles() used to push {} for nodes/edges with no projected style,
        // wholesale-replacing the default OrbView's constructor had just applied via
        // setDefaultStyle()/_applyStyle(). That left getRadius() === 0 (invisible, unhittable)
        // and getWidth() === 0 (edge never drawn). Assert Orb's real default survived instead.
        await AssertOrbDefaultStyleSurvivedAsync();

        var painted = await _page.EvaluateAsync<int>(CountPaintedPixels);
        Assert.IsGreaterThan(0, painted, "the canvas rendered nothing");
    }

    [TestMethod]
    [DataRow(ServerRoute)]
    [DataRow(WasmRoute)]
    public async Task ClearingAStyleAfterItWasApplied_FallsBackToOrbsDefault(string route)
    {
        await GoToAsync(route);

        // The styled graph gives Alice radius 12 and everyone else 8, so "all radii equal"
        // below is only true once the styles have actually been cleared.
        var styledRadii = await ReadRadiiAsync();
        Assert.AreNotEqual(
            1,
            styledRadii.Distinct().Count(),
            "precondition: the styled graph must not already have uniform radii");

        await _page.ClickAsync("#style-toggle-btn");
        await Expect(_page.Locator("#style-state")).ToHaveTextAsync("unstyled");
        await _page.WaitForTimeoutAsync(500);

        // Two failure modes at once: pushing {} would zero these (the radius-0 regression),
        // and skipping the push entirely would leave the old per-node style painted, which
        // the uniformity check below catches.
        await AssertOrbDefaultStyleSurvivedAsync();

        var clearedRadii = await ReadRadiiAsync();
        Assert.AreEqual(
            1,
            clearedRadii.Distinct().Count(),
            "clearing every style must leave every node on Orb's single default size, but "
                + $"the radii were [{string.Join(", ", clearedRadii)}]");
    }

    [TestMethod]
    [DataRow(ServerRoute)]
    [DataRow(WasmRoute)]
    public async Task NavigatingAway_DisposesWithoutError(string route)
    {
        await GoToAsync(route);

        await _page.ClickAsync("a.navbar-brand");
        await Expect(_page.Locator("h1")).ToHaveTextAsync("Orb sample host");

        // Blazor Server shows this banner when a circuit dies, and WebAssembly shows it on an
        // unhandled client exception. A teardown exception in OrbGraph.DisposeAsync trips it
        // either way, even if nothing reached console.error.
        await Expect(_page.Locator("#blazor-error-ui")).ToBeHiddenAsync();

        // The two render modes put a teardown exception in different places, so each route
        // asserts on the log that actually receives one. Under Server it is the host's log
        // (whether it also reaches the browser console depends on client-side log wiring and
        // DetailedErrors, neither of which the test controls); under WebAssembly the
        // component runs in the browser and there is no circuit, so the console is the only
        // place it can land.
        if (route == ServerRoute)
        {
            StringAssert.DoesNotMatch(
                SampleHostFixture.HostOutput,
                new Regex("Unhandled exception", RegexOptions.IgnoreCase));
        }
        else
        {
            lock (_consoleErrors)
            {
                Assert.IsEmpty(
                    _consoleErrors,
                    $"disposal logged to the browser console: {string.Join(" | ", _consoleErrors)}");
            }
        }
    }

    private async Task AssertOrbDefaultStyleSurvivedAsync()
    {
        var allNodesHaveRadius = await _page.EvaluateAsync<bool>(
            "() => window.__orbTestView.data.getNodes().every(n => n.getRadius() > 0)");
        Assert.IsTrue(allNodesHaveRadius, "every unstyled node must render at a non-zero radius");

        var allEdgesHaveWidth = await _page.EvaluateAsync<bool>(
            "() => window.__orbTestView.data.getEdges().every(e => e.getWidth() > 0)");
        Assert.IsTrue(allEdgesHaveWidth, "every unstyled edge must render at a non-zero width");
    }

    private async Task<double[]> ReadRadiiAsync()
    {
        var json = await _page.EvaluateAsync<string>(
            "() => JSON.stringify(window.__orbTestView.data.getNodes().map(n => n.getRadius()))");

        return System.Text.Json.JsonSerializer.Deserialize<double[]>(json)!;
    }

    private async Task<bool> LoadedTheWebAssemblyRuntimeAsync()
    {
        // dotnet.js is fetched only when Blazor boots the WebAssembly runtime for the page.
        return await _page.EvaluateAsync<bool>(
            """
            () => performance.getEntriesByType('resource')
                .some(e => /_framework\/dotnet\..*\.js/.test(e.name))
            """);
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
