namespace Pinknose.Memgraph.Orb.Razor;

/// <summary>A ready-made <see cref="IOrbEdge"/>. Derive from it to carry domain data.</summary>
public class OrbEdge(string id, string start, string end) : IOrbEdge
{
    public string Id { get; } = id;
    public string Start { get; } = start;
    public string End { get; } = end;
    public string? Label { get; set; }
    public OrbEdgeStyle? Style { get; set; }
}
