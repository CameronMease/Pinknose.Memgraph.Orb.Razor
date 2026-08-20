using System.Diagnostics;
using System.Text;

namespace Pinknose.Memgraph.Orb.Razor.TrimmedPublishTests;

/// <summary>
/// Publishes the sample host with trimming on, once for the whole assembly, and serves it.
/// </summary>
// This suite answers a question the dev-build suite structurally cannot: `dotnet run` never
// trims, so nothing there exercises what the ILLink trimmer keeps. The library serializes
// through a reflection-based JsonSerializer and receives events through [JSInvokable], and
// both are exactly the kind of thing a trimmer removes when no statically visible code path
// reaches them -- with no build error, only a graph that silently stops working.
//
// It is opt-in because a trimmed WebAssembly publish relinks the runtime through emscripten
// and takes minutes. Set ORB_TRIM_TESTS=1 to run it; without that every test reports
// inconclusive and nothing is published.
[TestClass]
public static class TrimmedPublishFixture
{
    public const string BaseUrl = "http://localhost:5101";

    private static readonly object Sync = new();
    private static readonly StringBuilder HostBuffer = new();

    private static Process? _host;

    /// <summary>Everything `dotnet publish` wrote, including ILLink's trim warnings.</summary>
    public static string PublishOutput { get; private set; } = string.Empty;

    public static string HostOutput
    {
        get
        {
            lock (Sync)
            {
                return HostBuffer.ToString();
            }
        }
    }

    public static bool Enabled =>
        Environment.GetEnvironmentVariable("ORB_TRIM_TESTS") is "1" or "true" or "True";

    /// <summary>Call first in every test: no-ops when enabled, skips the test when not.</summary>
    public static void RequireEnabled()
    {
        if (!Enabled)
        {
            Assert.Inconclusive(
                "Trimmed-publish tests are opt-in because publishing takes minutes. "
                    + "Set ORB_TRIM_TESTS=1 to run them.");
        }
    }

    [AssemblyInitialize]
    public static async Task PublishAndStartAsync(TestContext _)
    {
        if (!Enabled)
        {
            return;
        }

        var repoRoot = RepoRoot();
        var project = Path.Combine(
            repoRoot,
            "samples",
            "Pinknose.Memgraph.Orb.Razor.SampleHost",
            "Pinknose.Memgraph.Orb.Razor.SampleHost.csproj");
        var output = Path.Combine(Path.GetTempPath(), "orb-trimmed-publish");

        // ILLink's results are cached in obj/Release, and an incremental publish that skips
        // the trimming step prints no trim warnings at all. That is not a quiet optimisation
        // here: PublishOutput is what the warning baseline test reads, so a cached publish
        // makes it see zero warnings and report that the library is cleaner than it is. Wiping
        // the Release artifacts forces the trimmer to actually run every time. This is why the
        // suite costs minutes, and why it is opt-in. Debug outputs are untouched.
        ClearReleaseArtifacts(repoRoot, output);

        // TrimmerSingleWarn=false expands the trimmer's one-warning-per-assembly summary into
        // the individual call sites, and SuppressTrimAnalysisWarnings=false is what makes them
        // appear at all for a Blazor WebAssembly publish. Without both, the warning assertions
        // below would have nothing to read.
        PublishOutput = await RunAsync(
            "dotnet",
            $"publish \"{project}\" -c Release -o \"{output}\" "
                + "-p:TrimmerSingleWarn=false -p:SuppressTrimAnalysisWarnings=false",
            repoRoot);

        var hostDll = Path.Combine(output, "Pinknose.Memgraph.Orb.Razor.SampleHost.dll");
        if (!File.Exists(hostDll))
        {
            throw new InvalidOperationException(
                $"Trimmed publish did not produce '{hostDll}'.{Environment.NewLine}{PublishOutput}");
        }

        _host = Process.Start(new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"\"{hostDll}\" --urls {BaseUrl}",
            WorkingDirectory = output,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        });

        // Read asynchronously -- a synchronous read of either pipe deadlocks once the child
        // fills the other one.
        _host!.OutputDataReceived += (_, e) => AppendHost(e.Data);
        _host.ErrorDataReceived += (_, e) => AppendHost(e.Data);
        _host.BeginOutputReadLine();
        _host.BeginErrorReadLine();

        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };

        for (var attempt = 0; attempt < 60; attempt++)
        {
            try
            {
                if ((await client.GetAsync($"{BaseUrl}/")).IsSuccessStatusCode)
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

        throw new InvalidOperationException(
            $"Published host did not start within 60 seconds.{Environment.NewLine}{HostOutput}");
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

    public static string RepoRoot()
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
            $"Could not locate the repo root above '{AppContext.BaseDirectory}'.");
    }

    private static void ClearReleaseArtifacts(string repoRoot, string publishOutput)
    {
        string[] projects =
        [
            Path.Combine(repoRoot, "Pinknose.Memgraph.Orb.Razor"),
            Path.Combine(repoRoot, "samples", "Pinknose.Memgraph.Orb.Razor.SampleHost"),
            Path.Combine(repoRoot, "samples", "Pinknose.Memgraph.Orb.Razor.SampleHost.Client")
        ];

        foreach (var directory in projects
            .SelectMany(project => new[]
            {
                Path.Combine(project, "bin", "Release"),
                Path.Combine(project, "obj", "Release")
            })
            .Append(publishOutput))
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    private static async Task<string> RunAsync(string fileName, string arguments, string workingDirectory)
    {
        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        })!;

        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();

        await process.WaitForExitAsync();

        return await stdout + Environment.NewLine + await stderr;
    }

    private static void AppendHost(string? line)
    {
        if (line is null)
        {
            return;
        }

        lock (Sync)
        {
            HostBuffer.AppendLine(line);
        }
    }
}
