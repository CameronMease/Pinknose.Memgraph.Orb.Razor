namespace Pinknose.Memgraph.Orb.Razor;

/// <summary>
/// Orb's merge() upserts but never deletes, so removals have to be computed and sent
/// separately. Only ids that vanished matter — additions and updates ride along in merge.
/// </summary>
internal static class OrbGraphDiff
{
    public static string[] RemovedIds(
        IReadOnlyCollection<string> previous,
        IReadOnlyCollection<string> current)
    {
        if (previous.Count == 0)
        {
            return [];
        }

        var currentSet = new HashSet<string>(current, StringComparer.Ordinal);
        return previous.Where(id => !currentSet.Contains(id)).ToArray();
    }
}
