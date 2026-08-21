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

    /// <summary>
    /// Ids whose serialization is new or differs from last time.
    ///
    /// <para>
    /// Compares serialized output rather than the consumer's own instances. That is deliberate and
    /// is the slower of the two options: a consumer's <c>Equals</c> can report two nodes identical
    /// while their projected styles differ, which would silently skip a real visual change. What is
    /// compared here is exactly what gets sent, so it cannot disagree with what Orb receives.
    /// </para>
    /// </summary>
    public static string[] ChangedIds(
        IReadOnlyDictionary<string, string> previous,
        IReadOnlyDictionary<string, string> current)
    {
        if (current.Count == 0)
        {
            return [];
        }

        var changed = new List<string>();

        foreach (var (id, json) in current)
        {
            if (!previous.TryGetValue(id, out var was) || !string.Equals(was, json, StringComparison.Ordinal))
            {
                changed.Add(id);
            }
        }

        return [.. changed];
    }
}
