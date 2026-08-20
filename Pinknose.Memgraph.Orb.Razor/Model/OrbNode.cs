namespace Pinknose.Memgraph.Orb.Razor;

/// <summary>A ready-made <see cref="IOrbNode"/>. Derive from it to carry domain data.</summary>
/// <remarks>
/// Use this when you have no domain type to implement <see cref="IOrbNode"/> on, or for quick
/// experiments. For real applications, implementing the interface on the type you already have
/// is usually better: node events hand back the instance you supplied, so there is nothing to
/// map back to your model.
/// </remarks>
/// <param name="id">The node's unique, stable identifier.</param>
public class OrbNode(string id) : IOrbNode
{
    /// <inheritdoc />
    public string Id { get; } = id;

    /// <inheritdoc />
    public string? Label { get; set; }

    /// <inheritdoc />
    public OrbNodeStyle? Style { get; set; }
}
