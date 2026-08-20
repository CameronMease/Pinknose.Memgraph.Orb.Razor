using System.Diagnostics;
using System.Net.Http;

namespace Pinknose.Memgraph.Orb.Razor.BrowserTests;

/// <summary>Starts the sample host once for the whole assembly.</summary>
// MSTest 4.x's analyzer (MSTEST0012/MSTEST0013) only recognizes [AssemblyInitialize] and
// [AssemblyCleanup] inside a class carrying [TestClass] -- without it, the build otherwise
// clean warns that both method signatures are "invalid" even though they run correctly.
[TestClass]
public static class SampleHostFixture
{
    public const string BaseUrl = "http://localhost:5099";

    private static Process? _host;

    [AssemblyInitialize]
    public static async Task StartAsync(TestContext _)
    {
        var sampleHostProject = Path.Combine(
            FindRepoRoot(),
            "samples",
            "Pinknose.Memgraph.Orb.Razor.SampleHost",
            "Pinknose.Memgraph.Orb.Razor.SampleHost.csproj");

        _host = Process.Start(new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"run --project \"{sampleHostProject}\" --urls {BaseUrl}",
            UseShellExecute = false
        });

        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };

        for (var attempt = 0; attempt < 60; attempt++)
        {
            try
            {
                var response = await client.GetAsync($"{BaseUrl}/orb-demo");
                if (response.IsSuccessStatusCode)
                {
                    return;
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

        throw new InvalidOperationException("Sample host did not start within 60 seconds.");
    }

    [AssemblyCleanup]
    public static void Stop()
    {
        if (_host is { HasExited: false })
        {
            _host.Kill(entireProcessTree: true);
        }

        _host?.Dispose();
    }

    // The brief's fixed relative path (../../../../../samples/...) assumes a specific
    // bin/<config>/<tfm> nesting depth from the runtime working directory, which is fragile
    // across build configurations and test runners. Walk up from the test assembly's own
    // location instead, until the repo's solution file is found.
    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Pinknose.Memgraph.Orb.Razor.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException(
            $"Could not locate the repo root (Pinknose.Memgraph.Orb.Razor.slnx) above '{AppContext.BaseDirectory}'.");
    }
}
