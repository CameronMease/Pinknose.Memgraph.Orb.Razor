using Microsoft.Playwright;
using Pinknose.Memgraph.Orb.Razor.BrowserTests;
using static Microsoft.Playwright.Assertions;

namespace Pinknose.Memgraph.Orb.Razor.TrimmedPublishTests;

/// <summary>Exercises the library in a trimmed WebAssembly publish.</summary>
// Only /orb-wasm is driven here. /orb-server runs on the host, whose assemblies a
// WebAssembly publish does not trim, so it would prove nothing this suite is about.
[TestClass]
public class TrimmedPublishTests
{
    private const string WasmRoute = "/orb-wasm";

    private static IPlaywright _playwright = null!;
    private static IBrowser _browser = null!;

    private IBrowserContext _context = null!;
    private IPage _page = null!;
    private OrbPageDriver _driver = null!;

    [ClassInitialize]
    public static async Task LaunchBrowserAsync(TestContext _)
    {
        if (!TrimmedPublishFixture.Enabled)
        {
            return;
        }

        _playwright = await Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync();
    }

    [ClassCleanup]
    public static async Task CloseBrowserAsync()
    {
        if (!TrimmedPublishFixture.Enabled)
        {
            return;
        }

        await _browser.DisposeAsync();
        _playwright.Dispose();
    }

    [TestInitialize]
    public async Task OpenPageAsync()
    {
        TrimmedPublishFixture.RequireEnabled();

        _context = await _browser.NewContextAsync();
        _page = await _context.NewPageAsync();
        _driver = new OrbPageDriver(_page);
    }

    [TestCleanup]
    public async Task CloseContextAsync()
    {
        if (_context is not null)
        {
            await _context.CloseAsync();
        }
    }

    // Guards the assertion below rather than the library. "No warnings from our code" and
    // "the trimmer never ran" produce identical output, and the second one is easy to cause
    // by accident: ILLink caches its results, so an incremental publish prints nothing at
    // all. The framework's own assemblies always produce trim warnings under these publish
    // flags, so their presence is the proof that trim analysis actually happened.
    [TestMethod]
    public void Publish_ActuallyRanTrimAnalysis()
    {
        TrimmedPublishFixture.RequireEnabled();

        StringAssert.Contains(
            TrimmedPublishFixture.PublishOutput,
            "Trim analysis warning",
            "the publish produced no trim analysis at all, so the warning assertion below "
                + "would pass vacuously. The usual cause is an incremental publish reusing "
                + "ILLink's cached results.");
    }

    [TestMethod]
    public void Publish_ReportsNoTrimWarningsInTheLibrary()
    {
        TrimmedPublishFixture.RequireEnabled();

        var warnings = TrimWarningParser.WarningsByFile(
            TrimmedPublishFixture.PublishOutput,
            Path.Combine(TrimmedPublishFixture.RepoRoot(), "Pinknose.Memgraph.Orb.Razor")
                + Path.DirectorySeparatorChar);

        // The library serializes through a source-generated JsonSerializerContext precisely so
        // this stays empty: the trimmer can follow generated metadata, and cannot follow the
        // reflective resolver this replaced (which cost four IL2026 warnings). A regression
        // here means something reintroduced a reflective path -- most likely a
        // JsonSerializer.Serialize overload that takes a Type or bare options instead of a
        // JsonTypeInfo.
        Assert.IsTrue(
            warnings.Count == 0,
            $"the trimmer warned about the library: [{Describe(warnings)}]. Every warning here "
                + "is a path the trimmer cannot prove safe, so the runtime tests below are the "
                + "only thing standing between it and a silently broken trimmed build.");
    }

    [TestMethod]
    public async Task TrimmedWasm_RendersTheGraph()
    {
        await GoToAsync(WasmRoute);

        var painted = await _driver.CountPaintedPixelsAsync();

        Assert.IsGreaterThan(0, painted, "the trimmed build rendered nothing");
    }

    [TestMethod]
    public async Task TrimmedWasm_SerializesStylesAndLabelsIntact()
    {
        await GoToAsync(WasmRoute);

        // Styles and labels only reach Orb through the reflective serializer, so a trimmer
        // that dropped the payload types' property metadata would show up here as defaults or
        // blanks rather than as an exception. The demo styles Alice at 12, everyone else at 8.
        var radii = await _driver.ReadRadiiAsync();
        var labels = await _driver.ReadLabelsAsync();

        CollectionAssert.AreEqual(new double[] { 12, 8, 8 }, radii);
        CollectionAssert.AreEqual(new[] { "Alice", "Bob", "Carol" }, labels);
    }

    [TestMethod]
    public async Task TrimmedWasm_RaisesNodeClickBackToDotNet()
    {
        await GoToAsync(WasmRoute);

        // The JS -> .NET direction: the event arrives through a [JSInvokable] method the
        // trimmer only keeps if it recognises the attribute, carrying a payload that is
        // deserialized reflectively. Nothing else in this suite covers that direction.
        await _driver.ClickFirstNodeAsync();

        await Expect(_page.Locator("#selection-label")).Not.ToHaveTextAsync("none");
    }

    [TestMethod]
    public async Task TrimmedWasm_PushesUpdatesAfterAParameterChange()
    {
        await GoToAsync(WasmRoute);

        var before = await _driver.CountNodesAsync();

        await _page.ClickAsync("#remove-btn");
        await _driver.WaitForNodeCountAsync(before - 1);

        Assert.AreEqual(1, await _driver.CountEdgesAsync());
    }

    [TestMethod]
    public async Task TrimmedWasm_LabelOnlyStylesKeepOrbsDefaults()
    {
        await GoToAsync($"{WasmRoute}?styling=labels");

        var radii = await _driver.ReadRadiiAsync();

        Assert.IsTrue(
            radii.All(radius => radius > 0),
            $"a label must not cost a node its default size, but the radii were [{string.Join(", ", radii)}]");
    }

    private async Task GoToAsync(string route)
    {
        await _page.GotoAsync($"{TrimmedPublishFixture.BaseUrl}{route}");
        await _driver.WaitForGraphAsync();
    }

    private static string Describe(Dictionary<string, int> warnings)
        => string.Join(
            ", ",
            warnings.OrderBy(pair => pair.Key).Select(pair => $"{pair.Key}={pair.Value}"));
}
