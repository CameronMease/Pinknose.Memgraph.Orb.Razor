using System.Diagnostics;
using System.IO.Compression;
using System.Xml.Linq;

namespace Pinknose.Memgraph.Orb.Razor.Tests;

/// <summary>Packs the library and inspects the resulting .nupkg.</summary>
// A Razor class library's most important payload -- the vendored Orb bundle and the interop
// module -- gets into the package through the Razor SDK's static web assets machinery, not
// through anything visible in the csproj. That makes it quietly easy to ship a package that
// restores fine and then fails at run time with a 404 for orb.min.js. NuGet versions are
// immutable, so "we'll fix it in the next one" means a dead version on nuget.org forever.
[TestClass]
public class PackagingTests
{
    private static XDocument _nuspec = null!;
    private static string[] _entries = null!;

    [ClassInitialize]
    public static void PackAsync(TestContext _)
    {
        var repoRoot = RepoRoot();
        var output = Path.Combine(Path.GetTempPath(), "orb-pack-test");

        if (Directory.Exists(output))
        {
            Directory.Delete(output, recursive: true);
        }

        var project = Path.Combine(
            repoRoot,
            "Pinknose.Memgraph.Orb.Razor",
            "Pinknose.Memgraph.Orb.Razor.csproj");

        using var pack = Process.Start(new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"pack \"{project}\" -c Release -o \"{output}\"",
            WorkingDirectory = repoRoot,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        })!;

        var log = pack.StandardOutput.ReadToEnd() + pack.StandardError.ReadToEnd();
        pack.WaitForExit();

        var package = Directory.GetFiles(output, "*.nupkg").SingleOrDefault()
            ?? throw new InvalidOperationException($"pack produced no .nupkg.{Environment.NewLine}{log}");

        using var archive = ZipFile.OpenRead(package);

        _entries = [.. archive.Entries.Select(entry => entry.FullName)];

        var nuspecEntry = archive.Entries.Single(entry => entry.FullName.EndsWith(".nuspec"));
        using var nuspecStream = nuspecEntry.Open();
        _nuspec = XDocument.Load(nuspecStream);
    }

    [TestMethod]
    [DataRow("orb.min.js", "the vendored Orb bundle")]
    [DataRow("orbGraph.js", "the interop module")]
    public void Package_ShipsTheStaticWebAsset(string fileName, string what)
    {
        Assert.IsTrue(
            _entries.Any(entry => entry.EndsWith(fileName, StringComparison.OrdinalIgnoreCase)),
            $"{what} ({fileName}) is missing from the package, so consuming it would 404 at run "
                + $"time. Entries: {string.Join(", ", _entries)}");
    }

    [TestMethod]
    public void Package_ShipsTheXmlDocumentation()
    {
        // Without this file in the package, a consumer gets no IntelliSense for any of it --
        // no parameter descriptions, no summaries, nothing. It is produced by
        // GenerateDocumentationFile, which is easy to lose in a csproj cleanup.
        Assert.IsTrue(
            _entries.Any(entry =>
                entry.EndsWith("Pinknose.Memgraph.Orb.Razor.xml", StringComparison.OrdinalIgnoreCase)),
            $"the XML documentation is missing from the package. Entries: {string.Join(", ", _entries)}");
    }

    [TestMethod]
    public void Package_ShipsTheReadme()
    {
        Assert.IsTrue(
            _entries.Any(entry => entry.Equals("README.md", StringComparison.OrdinalIgnoreCase)),
            "the README is what a consumer reads on nuget.org before anything else, including "
                + "the type-argument trap that otherwise costs them a confusing CS1503");

        Assert.AreEqual("README.md", Metadata("readme"));
    }

    // The nuspec's own copy of these is what nuget.org displays and what a consumer sees in
    // their package details; LICENSE.txt in the repo does not travel with the assembly.
    [TestMethod]
    [DataRow("id", "Pinknose.Memgraph.Orb.Razor")]
    [DataRow("license", "MIT")]
    [DataRow("authors", "Cameron Mease")]
    [DataRow("copyright", "Copyright (c) 2026 Cameron Mease")]
    public void Package_DeclaresIts(string element, string expected)
    {
        Assert.AreEqual(expected, Metadata(element));
    }

    [TestMethod]
    public void Package_PointsBackAtItsSource()
    {
        var repository = _nuspec.Descendants()
            .SingleOrDefault(element => element.Name.LocalName == "repository");

        Assert.IsNotNull(repository, "no <repository> in the nuspec");
        StringAssert.Contains(
            repository.Attribute("url")?.Value ?? string.Empty,
            "Pinknose.Memgraph.Orb.Razor");
    }

    [TestMethod]
    public void Package_HasAVersion()
    {
        // Not a specific number: MinVer derives it from the nearest git tag, so it changes with
        // every release and is a pre-release on any untagged commit.
        Assert.IsFalse(string.IsNullOrWhiteSpace(Metadata("version")));
    }

    private static string? Metadata(string element)
        => _nuspec.Descendants()
            .FirstOrDefault(node => node.Name.LocalName == element)
            ?.Value;

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
            $"Could not locate the repo root above '{AppContext.BaseDirectory}'.");
    }
}
