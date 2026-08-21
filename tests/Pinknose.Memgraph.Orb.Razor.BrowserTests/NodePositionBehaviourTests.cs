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
    private const string Route = "/orb-server";
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

    private async Task GoToAsync()
    {
        await _page.GotoAsync($"{SampleHostFixture.BaseUrl}{Route}");
        await _driver.WaitForGraphAsync();
    }

    private Task SetNodePositionAsync(string id, double x, double y)
        => _page.EvaluateAsync(
            "([id, x, y]) => window.__orbTestView.data.setNodePositions([{ id, x, y }])",
            new object[] { id, x, y });
}
