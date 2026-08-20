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
        var before = await _page.EvaluateAsync<string>(ReadNodePositions);

        await _page.ClickAsync("#remove-btn");
        await _page.WaitForTimeoutAsync(500);

        var after = await _page.EvaluateAsync<string>(ReadNodePositions);

        // Alice and Bob keep their coordinates; only Carol disappears.
        var aliceBefore = ExtractPosition(before, "n1");
        var aliceAfter = ExtractPosition(after, "n1");

        Assert.AreEqual(aliceBefore, aliceAfter, "merge must not reset simulated positions");
    }

    [TestMethod]
    public async Task NavigatingAway_DisposesWithoutServerError()
    {
        var errors = new List<string>();
        _page.Console += (_, msg) => { if (msg.Type == "error") errors.Add(msg.Text); };

        await _page.ClickAsync("a[href='counter']");
        await Expect(_page.Locator("h1")).ToHaveTextAsync("Counter");

        Assert.AreEqual(0, errors.Count, string.Join("\n", errors));
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

    private static string ExtractPosition(string positionsJson, string id)
    {
        using var doc = System.Text.Json.JsonDocument.Parse(positionsJson);

        foreach (var element in doc.RootElement.EnumerateArray())
        {
            if (element.GetProperty("id").GetString() == id)
            {
                return element.ToString();
            }
        }

        return "";
    }
}
