using System.Text.RegularExpressions;
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

    // Every trim warning ILLink currently reports against the library, by file. They share a
    // root cause: OrbJson serializes reflectively (see OrbJsonContext), which the trimmer
    // cannot follow, so it warns that the types involved might not survive. The runtime tests
    // below are what establish that they do survive in practice.
    //
    // This is a baseline, not an endorsement. It exists so a NEW warning -- a new reflective
    // call site in the library, or one reached from it -- fails this test instead of blending
    // into known noise. Removing these four means moving to a source-generated
    // JsonSerializerContext; until then, changing this dictionary is a deliberate act.
    private static readonly Dictionary<string, int> ExpectedTrimWarnings = new()
    {
        ["OrbJsonContext.cs"] = 3,
        ["OrbLayoutConverter.cs"] = 1
    };

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

    [TestMethod]
    public void Publish_ReportsOnlyTheKnownTrimWarningsInTheLibrary()
    {
        TrimmedPublishFixture.RequireEnabled();

        var actual = LibraryTrimWarnings(TrimmedPublishFixture.PublishOutput);

        CollectionAssert.AreEquivalent(
            ExpectedTrimWarnings.ToList(),
            actual.ToList(),
            "ILLink's trim warnings against the library changed. Expected "
                + $"[{Describe(ExpectedTrimWarnings)}] but the publish reported "
                + $"[{Describe(actual)}]. A new warning means a new reflective path the "
                + "trimmer cannot follow: confirm the runtime tests below still pass, then "
                + "update the baseline deliberately.");
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

    // Counts distinct warning sites per library source file. Line numbers are deliberately not
    // part of the baseline: they move whenever the file is edited, which would fail this test
    // for reasons that have nothing to do with trimming.
    private static Dictionary<string, int> LibraryTrimWarnings(string publishOutput)
    {
        var libraryDirectory = Path.Combine(
            TrimmedPublishFixture.RepoRoot(),
            "Pinknose.Memgraph.Orb.Razor") + Path.DirectorySeparatorChar;

        var sites = new HashSet<string>();
        var counts = new Dictionary<string, int>();

        var pattern = "(?<path>[A-Za-z]:[^(\r\n]+\\.cs)\\((?<line>[0-9]+),[0-9]+\\): "
            + "Trim analysis warning (?<code>IL[0-9]+)";

        foreach (Match match in Regex.Matches(publishOutput, pattern))
        {
            var path = match.Groups["path"].Value;
            if (!path.StartsWith(libraryDirectory, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            // MSBuild repeats a warning once per referencing project; count each site once.
            if (!sites.Add($"{path}:{match.Groups["line"].Value}:{match.Groups["code"].Value}"))
            {
                continue;
            }

            var file = Path.GetFileName(path);
            counts[file] = counts.GetValueOrDefault(file) + 1;
        }

        return counts;
    }

    private static string Describe(Dictionary<string, int> warnings)
        => string.Join(
            ", ",
            warnings.OrderBy(pair => pair.Key).Select(pair => $"{pair.Key}={pair.Value}"));
}
