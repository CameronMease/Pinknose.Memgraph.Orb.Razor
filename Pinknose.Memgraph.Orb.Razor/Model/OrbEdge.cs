namespace Pinknose.Memgraph.Orb.Razor;

/// <summary>A ready-made <see cref="IOrbEdge"/>. Derive from it to carry domain data.</summary>
/// <param name="id">The edge's unique, stable identifier.</param>
/// <param name="start">The <see cref="IOrbNode.Id"/> this edge starts at.</param>
/// <param name="end">The <see cref="IOrbNode.Id"/> this edge ends at.</param>
public class OrbEdge(string id, string start, string end) : IOrbEdge
{
    /// <inheritdoc />
    public string Id { get; } = id;

    /// <inheritdoc />
    public string Start { get; } = start;

    /// <inheritdoc />
    public string End { get; } = end;

    /// <inheritdoc />
    public string? Label { get; set; }

    /// <inheritdoc />
    public OrbEdgeStyle? Style { get; set; }
}
