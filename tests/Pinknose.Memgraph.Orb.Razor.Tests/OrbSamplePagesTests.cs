using System.Text.RegularExpressions;

namespace Pinknose.Memgraph.Orb.Razor.Tests;

/// <summary>
/// Enforces that the two sample pages differ only by render mode.
/// </summary>
// The sample host exists to show the same graph running under Blazor Server and under
// WebAssembly, which is only a valid comparison while both pages render the identical
// component. If a feature (a new button, a different node set, extra settings) is added to
// one page and not the other, a browser test that passes on one route and fails on the other
// no longer says anything about the library -- it just means the demos drifted. The
// structural rule that prevents that: everything lives in OrbDemoView, and each page is a
// wrapper containing nothing but <OrbDemoView />.
[TestClass]
public class OrbSamplePagesTests
{
    private static readonly string ServerPage = Path.Combine(
        RepoRoot(),
        "samples",
        "Pinknose.Memgraph.Orb.Razor.SampleHost",
        "Components",
        "Pages",
        "OrbServer.razor");

    private static readonly string WasmPage = Path.Combine(
        RepoRoot(),
        "samples",
        "Pinknose.Memgraph.Orb.Razor.SampleHost.Client",
        "Pages",
        "OrbWasm.razor");

    [TestMethod]
    [DataRow("InteractiveServer")]
    [DataRow("InteractiveWebAssembly")]
    public void EachSamplePage_DeclaresItsRenderMode(string renderMode)
    {
        var page = renderMode == "InteractiveServer" ? ServerPage : WasmPage;

        StringAssert.Contains(File.ReadAllText(page), $"@rendermode {renderMode}");
    }

    [TestMethod]
    [DataRow("OrbServer.razor")]
    [DataRow("OrbWasm.razor")]
    public void EachSamplePage_IsNothingButAnOrbDemoViewWrapper(string pageName)
    {
        var page = pageName == "OrbServer.razor" ? ServerPage : WasmPage;

        var markup = StripBoilerplate(File.ReadAllText(page));

        Assert.AreEqual(
            "<OrbDemoView />",
            markup,
            $"{pageName} has grown content of its own. Every demo feature belongs in "
                + "OrbDemoView so that both sample pages get it: a feature on one page and "
                + "not the other makes the Server-vs-WebAssembly comparison meaningless.");
    }

    // Leaves only the page's real content: directives, Razor comments, the page title and the
    // heading are all allowed to differ, since they are how a reader tells the two apart.
    private static string StripBoilerplate(string razor)
    {
        var stripped = Regex.Replace(razor, @"@\*.*?\*@", string.Empty, RegexOptions.Singleline);

        var lines = stripped
            .Split('\n')
            .Select(line => line.Trim())
            .Where(line => line.Length > 0)
            .Where(line => !line.StartsWith('@'))
            .Where(line => !line.StartsWith("<PageTitle>"))
            .Where(line => !line.StartsWith("<h1>"));

        return string.Join(Environment.NewLine, lines);
    }

    private static string RepoRoot()
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
