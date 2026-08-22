using System.Diagnostics;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace Pinknose.Memgraph.Orb.Razor.BrowserTests;

/// <summary>Smoke tests for the standalone WebAssembly demo published to GitHub Pages.</summary>
// Deliberately thin. The sample pages already prove the component's behaviour under both
// render modes; what is untested elsewhere is that the demo itself still works -- that its
// controls are wired to the API, and that a change to the library does not leave a broken page
// published where visitors see it first.
//
// Starts its own host, in ClassInitialize rather than an assembly fixture, because
// SampleHostFixture already owns [AssemblyInitialize] and a second one is not allowed.
[TestClass]
public class DemoSmokeTests
{
    private const string BaseUrl = "http://localhost:5098";

    private static Process? _host;
    private static IPlaywright _playwright = null!;
    private static IBrowser _browser = null!;

    private IBrowserContext _context = null!;
    private IPage _page = null!;
    private OrbPageDriver _driver = null!;

    [ClassInitialize]
    public static async Task StartAsync(TestContext _)
    {
        var project = Path.Combine(
            SampleHostFixture.RepoRoot(),
            "samples",
            "Pinknose.Memgraph.Orb.Razor.Demo",
            "Pinknose.Memgraph.Orb.Razor.Demo.csproj");

        _host = Process.Start(new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"run --project \"{project}\" --urls {BaseUrl}",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        });

        _host!.BeginOutputReadLine();
        _host.BeginErrorReadLine();

        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };

        for (var attempt = 0; attempt < 60; attempt++)
        {
            try
            {
                if ((await client.GetAsync(BaseUrl)).IsSuccessStatusCode)
                {
                    break;
                }
            }
            catch (HttpRequestException)
            {
            }
            catch (TaskCanceledException)
            {
            }

            await Task.Delay(1000);
        }

        _playwright = await Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync();
    }

    [ClassCleanup]
    public static async Task StopAsync()
    {
        await _browser.DisposeAsync();
        _playwright.Dispose();

        if (_host is { HasExited: false })
        {
            _host.Kill(entireProcessTree: true);
        }

        _host?.Dispose();
    }

    [TestInitialize]
    public async Task OpenAsync()
    {
        _context = await _browser.NewContextAsync();
        _page = await _context.NewPageAsync();
        _driver = new OrbPageDriver(_page);

        await _page.GotoAsync(BaseUrl);
        await _driver.WaitForGraphAsync();
    }

    [TestCleanup]
    public async Task CloseAsync() => await _context.CloseAsync();

    [TestMethod]
    public async Task Demo_RendersTheWholeSolutionGraph()
    {
        // The counts come from SolutionGraph, so this also catches the data being silently
        // truncated -- a graph that renders three nodes still "renders".
        Assert.AreEqual(35, await _driver.CountNodesAsync());
        Assert.AreEqual(65, await _driver.CountEdgesAsync());
        Assert.IsGreaterThan(0, await _driver.CountPaintedPixelsAsync());
    }

    [TestMethod]
    public async Task SwitchingLayout_MovesTheNodes()
    {
        var before = await _driver.ReadPositionAsync("core");

        await _page.GetByLabel("Grid").CheckAsync();
        await _page.WaitForTimeoutAsync(1500);

        var after = await _driver.ReadPositionAsync("core");

        // A grid layout puts every node somewhere different from where the force simulation
        // left it, so any real move proves the layout switch reached Orb rather than only
        // updating the radio button.
        Assert.IsGreaterThan(
            1.0,
            OrbPageDriver.Distance(before, after),
            "switching to the grid layout did not move the graph");
    }

    [TestMethod]
    public async Task ClickingANode_AppendsToTheEventLog()
    {
        await Expect(_page.Locator("#event-log")).ToContainTextAsync("Click or hover a node");

        await _driver.ClickFirstNodeAsync();

        // "Project" is the demo's own record type. Its presence is the point the log is
        // making: the event handed back the caller's instance, not a copy or an id.
        await Expect(_page.Locator("#event-log")).ToContainTextAsync("click");
        await Expect(_page.Locator("#event-log")).ToContainTextAsync("Project");
    }
}
