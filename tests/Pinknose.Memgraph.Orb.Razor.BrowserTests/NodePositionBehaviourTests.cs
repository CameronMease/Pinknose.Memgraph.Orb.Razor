using Microsoft.Playwright;

namespace Pinknose.Memgraph.Orb.Razor.BrowserTests;

/// <summary>
/// What Orb actually does with a position set while the force simulation is running.
///
/// <para>
/// The design (Revision B) reasoned from source that <c>setNodePositions</c> writes only the
/// rendered position and would be overwritten on the simulator's next report. That reasoning
/// had not been observed. Measured here: it does not happen. A position set through
/// <c>setNodePositions</c> is invisible to the simulator, running or not -- see task-1-report.md
/// for the full investigation, including the controls that rule out "physics silently isn't
/// running" as the explanation. The spec has been amended (Revision C) to match.
/// </para>
/// </summary>
// Same deviation from Microsoft.Playwright.MSTest's PageTest as OrbGraphSmokeTests, and for the
// same reason (see the comment on the Microsoft.Playwright PackageReference in the .csproj).
[TestClass]
public sealed class NodePositionBehaviourTests
{
    // ?seedTest=true opts into OrbDemoView's seed-position controls (see the markup comment
    // there): they are gated off by default, the same way orbGraph.js only creates
    // window.__orbTestView when the host carries data-orb-test, so this file has to ask for
    // them explicitly rather than finding them always present.
    private const string Route = "/orb-server?seedTest=true";
    private const string NodeId = "n1";

    private static IPlaywright _playwright = null!;
    private static IBrowser _browser = null!;

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
    }

    [TestCleanup]
    public async Task CloseContextAsync()
    {
        await _context.CloseAsync();
    }

    // Named for what was actually observed, not for what the design predicted before this task
    // measured it -- see the class doc comment.
    [TestMethod]
    public async Task SetNodePositions_WithPhysicsRunning_Holds()
    {
        await GoToAsync();

        // isPhysicsEnabled: true is not enough on its own: a one-shot alpha=1 restart decays
        // to alphaMin and stops ticking on its own, and can finish well inside the wait below,
        // which would silently turn this into the physics-disabled case. alphaTarget > 0 keeps
        // d3-force reheating forever instead of settling, so ticking is guaranteed to still be
        // live when the position is set and for the whole observation window after.
        await _page.EvaluateAsync(
            """
            () => window.__orbTestView.setSettings({
              layout: { type: 'force', isPhysicsEnabled: true,
                        alpha: { alpha: 1, alphaMin: 0.05, alphaDecay: 0.028, alphaTarget: 0.3 } }
            })
            """);
        await _page.WaitForTimeoutAsync(150);

        // Sanity check that the settings call above actually landed, rather than assuming it:
        // Orb's setSettings() only honours isPhysicsEnabled as a FLAT key under "layout" (a
        // nested { layout: { options: { isPhysicsEnabled } } } shape is silently ignored except
        // for useGPU -- see task-1-report.md's "Out-of-scope finding"). Without this, a future
        // regression in this call -- or in Orb itself -- would silently turn this test into a
        // duplicate of SetNodePositions_WithPhysicsDisabled_Holds and nothing would say so.
        var isPhysicsEnabled = await _page.EvaluateAsync<bool?>(
            "() => window.__orbTestView.getSettings().layout.isPhysicsEnabled");
        Assert.IsTrue(
            isPhysicsEnabled == true,
            "precondition: physics must be observably running (getSettings().layout."
                + $"isPhysicsEnabled) before placing the node, but it reads {isPhysicsEnabled}. "
                + "Without this, a stuck physics-disabled setting would make this test pass for "
                + "the same reason as SetNodePositions_WithPhysicsDisabled_Holds instead of "
                + "proving the claim it's named for.");

        var before = await _driver.ReadPositionAsync(NodeId);

        // Placed far outside the graph. If this write reached the simulator, n1's spring back
        // to n2 (default link distance 50 vs. an actual distance of ~7071 here) would pull it
        // back well within the wait below.
        var target = (X: before.X + 5000, Y: before.Y + 5000);
        await SetNodePositionAsync(NodeId, target.X, target.Y);

        // Measured directly (3 runs, bit-identical): the position held exactly, unmoved, for
        // the full 2s with physics continuously hot -- not "eventually snapped back", not
        // "drifted a little", exactly unmoved. A companion check (see task-1-report.md) forced
        // two nodes to the same coordinate under this same hot setup and got zero many-body
        // separation either, ruling out "physics silently isn't running" as the explanation.
        await _page.WaitForTimeoutAsync(2000);

        var after = await _driver.ReadPositionAsync(NodeId);
        var distanceFromTarget = OrbPageDriver.Distance(target, after);

        Assert.AreEqual(
            0.0,
            distanceFromTarget,
            0.01,
            $"a position held through 2s of running physics unexpectedly moved to "
                + $"({after.X:F2},{after.Y:F2})");
    }

    [TestMethod]
    public async Task SetNodePositions_WithPhysicsDisabled_Holds()
    {
        await GoToAsync();

        await _page.EvaluateAsync(
            "() => window.__orbTestView.setSettings({ layout: { type: 'force', isPhysicsEnabled: false } })");
        await _page.WaitForTimeoutAsync(150);

        var before = await _driver.ReadPositionAsync(NodeId);
        var target = (X: before.X + 5000, Y: before.Y + 5000);
        await SetNodePositionAsync(NodeId, target.X, target.Y);

        await _page.WaitForTimeoutAsync(300);

        var after = await _driver.ReadPositionAsync(NodeId);
        var distanceFromTarget = OrbPageDriver.Distance(target, after);

        Assert.AreEqual(
            0.0,
            distanceFromTarget,
            0.01,
            $"a position set with physics disabled unexpectedly moved to "
                + $"({after.X:F2},{after.Y:F2})");
    }

    // Placed thousands of units outside the demo's three-node cluster (which settles close to
    // the canvas origin), so a fallback/default entry position can never be mistaken for a
    // seeded one -- the two are separated by orders of magnitude, not by rounding error.
    private const double FarSeedOffset = 5000;

    [TestMethod]
    public async Task ANodeMergedAfterItsSeedWasSet_EntersAtThatCoordinate()
    {
        await GoToAsync();

        // Seed a coordinate for "n4", which does not exist in the graph yet.
        await SeedPositionAsync("n4", FarSeedOffset, FarSeedOffset);

        // Now add it. Orb's OrbView.onMergeData consults getPosition for newly merged nodes
        // and hands the result straight to simulator.mergeData, so n4 should start at the
        // seed rather than wherever Orb's fallback layout would otherwise drop it.
        await MergeNodesAsync("n4");

        // Read back immediately: physics is off in this demo (OrbForceLayout.IsPhysicsEnabled
        // defaults to false, per UpdatingNodes_PreservesExistingPositions above), so nothing
        // should move n4 after entry -- but the assertion is about where it entered, not about
        // where it settles, so a threshold well inside the 5000-unit seed offset is used
        // rather than exact equality.
        var after = await _driver.ReadPositionAsync("n4");
        var distanceFromSeed = OrbPageDriver.Distance((FarSeedOffset, FarSeedOffset), after);

        Assert.IsLessThan(
            50.0,
            distanceFromSeed,
            $"a node merged after its seed was set should enter near "
                + $"({FarSeedOffset},{FarSeedOffset}), but entered at ({after.X:F2},{after.Y:F2})");
    }

    [TestMethod]
    public async Task SeedingIsMergedNotReplaced()
    {
        await GoToAsync();

        var seedA = (X: FarSeedOffset, Y: FarSeedOffset);
        var seedB = (X: -FarSeedOffset, Y: -FarSeedOffset);

        // Two separate SetSeedPositionsAsync calls, not one batch -- this is the behaviour
        // under test: seeding "b" must merge into the map instead of replacing it, or "a"'s
        // seed set moments earlier would be gone by the time both nodes are added below.
        await SeedPositionAsync("a", seedA.X, seedA.Y);
        await SeedPositionAsync("b", seedB.X, seedB.Y);

        await MergeNodesAsync("a", "b");

        var afterA = await _driver.ReadPositionAsync("a");
        var afterB = await _driver.ReadPositionAsync("b");

        Assert.IsLessThan(
            50.0,
            OrbPageDriver.Distance(seedA, afterA),
            $"a's seed should have survived b's separate seed call, but a entered at "
                + $"({afterA.X:F2},{afterA.Y:F2})");
        Assert.IsLessThan(
            50.0,
            OrbPageDriver.Distance(seedB, afterB),
            $"b's own seed should have taken effect, but b entered at ({afterB.X:F2},{afterB.Y:F2})");
    }

    private async Task GoToAsync()
    {
        await _page.GotoAsync($"{SampleHostFixture.BaseUrl}{Route}");
        await _driver.WaitForGraphAsync();
    }

    private Task SetNodePositionAsync(string id, double x, double y)
        => _page.EvaluateAsync(
            "([id, x, y]) => window.__orbTestView.data.setNodePositions([{ id, x, y }])",
            new object[] { id, x, y });

    // Drives the sample's test-only seed control (see OrbDemoView.razor) rather than talking
    // to orbGraph.js directly: the position map getPosition reads lives on the JS "handle"
    // object the component holds, and nothing on window exposes that handle the way
    // window.__orbTestView exposes Orb's own view -- SetSeedPositionsAsync is the only route
    // to it, so this test drives the real component method through the UI.
    //
    // Each @bind:event="oninput" fill is a full SignalR round trip on a Blazor Server page --
    // unlike setting a JS-side value, Fill()'s dispatched "input" event only starts that trip,
    // it does not wait for the server to have applied it. Measured directly: without the waits
    // below, clicking #seed-btn raced ahead of the id fill's round trip, _seedId server-side
    // was still "", and SeedPositionAsync's guard silently no-op'd the whole call -- the merged
    // node then carried no seed at all (getNodePositions() reported bare {"id":"n4"}, no x/y).
    private async Task SeedPositionAsync(string id, double x, double y)
    {
        await _page.FillAsync("#seed-id-input", id);
        await _page.WaitForTimeoutAsync(200);
        await _page.FillAsync("#seed-x-input", $"{x}");
        await _page.WaitForTimeoutAsync(200);
        await _page.FillAsync("#seed-y-input", $"{y}");
        await _page.WaitForTimeoutAsync(200);
        await _page.ClickAsync("#seed-btn");
        await _page.WaitForTimeoutAsync(200);
    }

    // Adds nodes straight through Orb's own data.merge(), the same way SetNodePositionAsync
    // above talks straight to data.setNodePositions() -- this test is about what OrbView does
    // with a getPosition hit for a node it has never seen, not about the library's own
    // change-detection (UpdatingNodes_PreservesExistingPositions already covers that path).
    private Task MergeNodesAsync(params string[] ids)
        => _page.EvaluateAsync(
            "(ids) => window.__orbTestView.data.merge({ nodes: ids.map(id => ({ id })), edges: [] })",
            ids);
}
