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
    private OrbPageDriver _driver = null!;

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
        _driver = new OrbPageDriver(_page);
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
        await _driver.WaitForGraphAsync();
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

        var painted = await _driver.CountPaintedPixelsAsync();

        Assert.IsGreaterThan(0, painted, "the canvas rendered nothing");
    }

    [TestMethod]
    [DataRow(ServerRoute)]
    [DataRow(WasmRoute)]
    public async Task ClickingANode_RaisesOnNodeClickWithTheOriginalInstance(string route)
    {
        await GoToAsync(route);

        await _driver.ClickFirstNodeAsync();

        await Expect(_page.Locator("#selection-label")).Not.ToHaveTextAsync("none");
    }

    [TestMethod]
    [DataRow(ServerRoute)]
    [DataRow(WasmRoute)]
    public async Task HoveringANode_RaisesEnterThenLeave(string route)
    {
        await GoToAsync(route);

        var box = await _page.Locator(".orb-graph canvas").BoundingBoxAsync();
        var position = await _driver.FindNodePointAsync();

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

        var before = await _driver.CountNodesAsync();

        await _page.ClickAsync("#remove-btn");
        await _driver.WaitForNodeCountAsync(before - 1);

        var edges = await _driver.CountEdgesAsync();

        Assert.AreEqual(1, edges, "removing a node must take its edges with it");
    }

    [TestMethod]
    [DataRow(ServerRoute)]
    [DataRow(WasmRoute)]
    public async Task UpdatingNodes_PreservesExistingPositions(string route)
    {
        await GoToAsync(route);

        var before = await _driver.ReadPositionAsync("n1");

        var nodeCountBefore = await _driver.CountNodesAsync();

        await _page.ClickAsync("#remove-btn");
        // Wait for the actual state change (merge/remove has applied) rather than guessing
        // how long that takes; the fixed wait below is for a different, unavoidable reason.
        await _driver.WaitForNodeCountAsync(nodeCountBefore - 1);

        // Orb's remove()/merge() both funnel into activateSimulation(), which pins or
        // unpins nodes depending on isPhysicsEnabled. This sample never sets
        // OrbForceLayout.IsPhysicsEnabled, so it's Orb's default: false -- meaning
        // activateSimulation() *pins* nodes at their current position instead of reheating
        // them. Measured directly (3 runs): n1 moved exactly 0.0000 units after a remove +
        // 500ms wait. A short wait is still kept below in case that default ever changes
        // (e.g. physics gets turned on) and a node legitimately drifts a little.
        await _page.WaitForTimeoutAsync(500);

        var after = await _driver.ReadPositionAsync("n1");
        var distance = OrbPageDriver.Distance(before, after);

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
        // ?styling=none makes OrbDemoView build its nodes and edges with neither Label nor
        // Style set, so no style is sent for them at all. It has to be set on the initial
        // navigation rather than by clicking through the modes, because the regression is
        // about the FIRST push for a node that never had a style.
        await GoToAsync($"{route}?styling=none");

        // Regression: pushStyles() used to push {} for nodes/edges with no projected style,
        // wholesale-replacing the default OrbView's constructor had just applied via
        // setDefaultStyle()/_applyStyle(). That left getRadius() === 0 (invisible, unhittable)
        // and getWidth() === 0 (edge never drawn). Assert Orb's real default survived instead.
        await AssertOrbDefaultStyleSurvivedAsync();

        // Merging over Orb's defaults must not hand back a label the caller cleared: Orb keeps
        // a label inside the style object, so a default that carried one would reintroduce it.
        var labels = await _driver.ReadLabelsAsync();
        Assert.IsTrue(
            labels.All(string.IsNullOrEmpty),
            $"a node with no Label must not render one, but got [{string.Join(", ", labels)}]");

        var painted = await _driver.CountPaintedPixelsAsync();
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
        var styledRadii = await _driver.ReadRadiiAsync();
        Assert.AreNotEqual(
            1,
            styledRadii.Distinct().Count(),
            "precondition: the styled graph must not already have uniform radii");

        await _page.ClickAsync("#style-toggle-btn");
        await Expect(_page.Locator("#style-state")).ToHaveTextAsync("labels");
        await _page.ClickAsync("#style-toggle-btn");
        await Expect(_page.Locator("#style-state")).ToHaveTextAsync("none");
        await _page.WaitForTimeoutAsync(500);

        // Two failure modes at once: pushing {} would zero these (the radius-0 regression),
        // and skipping the push entirely would leave the old per-node style painted, which
        // the uniformity check below catches.
        await AssertOrbDefaultStyleSurvivedAsync();

        var clearedRadii = await _driver.ReadRadiiAsync();
        Assert.AreEqual(
            1,
            clearedRadii.Distinct().Count(),
            "clearing every style must leave every node on Orb's single default size, but "
                + $"the radii were [{string.Join(", ", clearedRadii)}]");
    }

    [TestMethod]
    [DataRow(ServerRoute)]
    [DataRow(WasmRoute)]
    public async Task NodesWithOnlyALabel_KeepOrbsDefaultSize(string route)
    {
        // ?styling=labels sets Label and leaves Style null. Orb carries a node's label inside
        // its style object, so the projector has to send a style for it -- but one that holds
        // nothing except the label. setStyle() replaces a node's style wholesale, so pushing
        // that partial style unmerged costs the node every default it had, and size defaulting
        // to 0 makes it invisible and unhittable.
        await GoToAsync($"{route}?styling=labels");

        var radii = await _driver.ReadRadiiAsync();

        Assert.IsTrue(
            radii.All(radius => radius > 0),
            $"a label must not cost a node its default size, but the radii were [{string.Join(", ", radii)}]");
    }

    [TestMethod]
    [DataRow(ServerRoute)]
    [DataRow(WasmRoute)]
    public async Task EdgesWithOnlyALabel_KeepOrbsDefaultWidth(string route)
    {
        // The demo's edges carry a Kind label and no style of their own, so this is the
        // default view of the sample -- and an edge whose width falls back to 0 is never
        // drawn at all (Orb's canvas edge renderer returns early on a falsy width).
        await GoToAsync(route);

        var widths = await _driver.ReadEdgeWidthsAsync();

        Assert.IsTrue(
            widths.All(width => width > 0),
            $"a label must not cost an edge its default width, but the widths were [{string.Join(", ", widths)}]");
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
        var radii = await _driver.ReadRadiiAsync();
        Assert.IsTrue(
            radii.All(radius => radius > 0),
            $"every unstyled node must render at a non-zero radius, but got [{string.Join(", ", radii)}]");

        var widths = await _driver.ReadEdgeWidthsAsync();
        Assert.IsTrue(
            widths.All(width => width > 0),
            $"every unstyled edge must render at a non-zero width, but got [{string.Join(", ", widths)}]");
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

}
