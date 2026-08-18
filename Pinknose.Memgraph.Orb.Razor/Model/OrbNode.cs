namespace Pinknose.Memgraph.Orb.Razor;

/// <summary>A ready-made <see cref="IOrbNode"/>. Derive from it to carry domain data.</summary>
public class OrbNode(string id) : IOrbNode
{
    public string Id { get; } = id;
    public string? Label { get; set; }
    public OrbNodeStyle? Style { get; set; }
}
