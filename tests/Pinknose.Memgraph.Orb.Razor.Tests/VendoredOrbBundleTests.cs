using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace Pinknose.Memgraph.Orb.Razor.Tests;

/// <summary>
/// Guards the vendored Orb bundle against byte-level drift.
/// </summary>
// orbGraph.js loads orb.min.js with a Subresource Integrity hash, so the browser refuses to
// execute the bundle if a single byte differs -- and the failure is silent from C#'s point of
// view: the component renders its host div, no canvas ever appears, and only the browser
// console says why. That is expensive to diagnose from a timed-out Playwright run, so these
// tests catch the same corruption in milliseconds without a browser.
//
// The corruption this was written for: `.gitattributes` declares `* text=auto` and Windows
// clones default to `core.autocrlf=true`, so git rewrote the bundle's one `\n` (at offset 64,
// after the license banner) to `\r\n` on checkout. One byte, hash broken, graph never renders.
// `.gitattributes` now marks the vendor directory binary; this test is what proves it stays
// that way.
[TestClass]
public class VendoredOrbBundleTests
{
    private static readonly string BundlePath = Path.Combine(
        RepoRoot(),
        "Pinknose.Memgraph.Orb.Razor",
        "wwwroot",
        "vendor",
        "memgraph",
        "orb",
        "1.0.2",
        "orb.min.js");

    [TestMethod]
    public void VendoredBundle_MatchesTheIntegrityPinnedInOrbGraphJs()
    {
        var orbGraphJs = File.ReadAllText(
            Path.Combine(RepoRoot(), "Pinknose.Memgraph.Orb.Razor", "wwwroot", "orbGraph.js"));

        var match = Regex.Match(
            orbGraphJs,
            """const SCRIPT_INTEGRITY = "(?<integrity>sha384-[^"]+)";""");

        Assert.IsTrue(match.Success, "could not find SCRIPT_INTEGRITY in orbGraph.js");

        Assert.AreEqual(
            match.Groups["integrity"].Value,
            $"sha384-{Sha384OfBundle()}",
            "the vendored orb.min.js does not match the SRI hash orbGraph.js pins, so the "
                + "browser will block it and no graph will ever render. If the file was not "
                + "intentionally updated, the usual cause is git rewriting its line endings on "
                + "checkout -- check that .gitattributes still marks the vendor directory binary.");
    }

    [TestMethod]
    public void VendoredBundle_MatchesItsSha384Sidecar()
    {
        var sidecar = File.ReadAllText($"{BundlePath}.sha384").Trim();

        Assert.AreEqual(sidecar, $"sha384-{Sha384OfBundle()}");
    }

    private static string Sha384OfBundle()
    {
        using var stream = File.OpenRead(BundlePath);

        return Convert.ToBase64String(SHA384.HashData(stream));
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
