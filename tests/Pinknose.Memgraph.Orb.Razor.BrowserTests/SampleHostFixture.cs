using System.Diagnostics;
using System.Net.Http;
using System.Text;

namespace Pinknose.Memgraph.Orb.Razor.BrowserTests;

/// <summary>Starts the sample host once for the whole assembly.</summary>
// MSTest 4.x's analyzer (MSTEST0012/MSTEST0013) only recognizes [AssemblyInitialize] and
// [AssemblyCleanup] inside a class carrying [TestClass] -- without it, the build otherwise
// clean warns that both method signatures are "invalid" even though they run correctly.
[TestClass]
public static class SampleHostFixture
{
    public const string BaseUrl = "http://localhost:5099";

    private static readonly object Sync = new();
    private static readonly StringBuilder Buffer = new();

    private static Process? _host;

    /// <summary>
    /// Everything the sample host has written to stdout/stderr so far. A teardown exception
    /// in Blazor Server kills the *circuit*, not necessarily the browser console -- whether
    /// it reaches console.error depends on client-side log wiring the test does not control.
    /// The server-side log is where that exception unconditionally lands, so tests assert
    /// against this instead of (or in addition to) the browser console.
    /// </summary>
    public static string HostOutput
    {
        get
        {
            lock (Sync)
            {
                return Buffer.ToString();
            }
        }
    }

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
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        });

        // Do not read these streams synchronously -- the child process's stdout/stderr
        // pipes have a small OS buffer, and a synchronous ReadToEnd (or alternating
        // reads) deadlocks as soon as the child writes enough to fill the pipe while we're
        // blocked writing to (or reading from) the other one.
        _host!.OutputDataReceived += (_, e) => Append(e.Data);
        _host.ErrorDataReceived += (_, e) => Append(e.Data);
        _host.BeginOutputReadLine();
        _host.BeginErrorReadLine();

        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };

        for (var attempt = 0; attempt < 60; attempt++)
        {
            try
            {
                var response = await client.GetAsync($"{BaseUrl}/");
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

    private static void Append(string? line)
    {
        if (line is null)
        {
            return;
        }

        lock (Sync)
        {
            Buffer.AppendLine(line);
        }
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
