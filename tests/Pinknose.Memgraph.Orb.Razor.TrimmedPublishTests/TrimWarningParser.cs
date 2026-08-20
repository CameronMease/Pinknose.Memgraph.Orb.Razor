using System.Text.RegularExpressions;

namespace Pinknose.Memgraph.Orb.Razor.TrimmedPublishTests;

/// <summary>Extracts ILLink's trim warnings against one project from publish output.</summary>
internal static class TrimWarningParser
{
    // Deliberately not anchored to a drive letter: the same publish on a Linux runner emits
    // /home/runner/... paths, and a pattern that quietly matched none of them would leave the
    // "no warnings in the library" assertion passing for the wrong reason. The path is lazy so
    // it stops at the first ".cs(" rather than swallowing anything later in the message.
    private const string WarningPattern =
        @"(?<path>[^\s(][^(\r\n]*?\.cs)\((?<line>[0-9]+),[0-9]+\): Trim analysis warning (?<code>IL[0-9]+)";

    /// <summary>Counts distinct warning sites per source file inside <paramref name="libraryDirectory"/>.</summary>
    // Line numbers are deliberately not part of the result: they move whenever the file is
    // edited, which would report a change that has nothing to do with trimming.
    public static Dictionary<string, int> WarningsByFile(string publishOutput, string libraryDirectory)
    {
        var sites = new HashSet<string>();
        var counts = new Dictionary<string, int>();
        var library = Normalize(libraryDirectory);

        foreach (Match match in Regex.Matches(publishOutput, WarningPattern))
        {
            var path = match.Groups["path"].Value;
            if (!Normalize(path).StartsWith(library, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            // MSBuild repeats a warning once per referencing project; count each site once.
            if (!sites.Add($"{path}:{match.Groups["line"].Value}:{match.Groups["code"].Value}"))
            {
                continue;
            }

            var file = FileName(path);
            counts[file] = counts.GetValueOrDefault(file) + 1;
        }

        return counts;
    }

    // Not Path.GetFileName: on Linux it does not treat '\' as a separator, so a Windows-shaped
    // path would come back whole. Publish output is whatever the machine that produced it
    // wrote, and the tests feed both shapes through on one platform.
    private static string FileName(string path)
        => Normalize(path) is var normalized && normalized.LastIndexOf('/') is var slash && slash >= 0
            ? normalized[(slash + 1)..]
            : normalized;

    private static string Normalize(string path) => path.Replace('\\', '/');
}
