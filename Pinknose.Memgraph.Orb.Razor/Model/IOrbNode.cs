namespace Pinknose.Memgraph.Orb.Razor;

/// <summary>A graph node. Implement <see cref="Id"/>; the rest are optional.</summary>
/// <remarks>
/// Implement this on your own domain type rather than converting to a separate model: the
/// instance you supply is the instance handed back to you on <c>OnNodeClick</c> and the other
/// node events, so there is nothing to look up or map back.
/// </remarks>
public interface IOrbNode
{
    /// <summary>Identifies the node. Must be unique across the graph and stable across renders.</summary>
    /// <remarks>
    /// Stability is what lets a node keep its simulated position when the collection changes:
    /// updates are pushed through Orb's <c>merge()</c>, which matches on this value. Two nodes
    /// sharing an id is an error, and the component logs it and skips the update rather than
    /// rendering a graph that disagrees with your data.
    /// </remarks>
    string Id { get; }

    /// <summary>Text drawn next to the node. <see langword="null"/> draws no label.</summary>
    /// <remarks>
    /// Orb carries a label inside the node's style, so setting this produces a style even when
    /// <see cref="Style"/> is <see langword="null"/>. That partial style is merged over Orb's
    /// defaults, so a labelled node keeps its default size and colour.
    /// </remarks>
    string? Label => null;

    /// <summary>Appearance overrides. <see langword="null"/> uses Orb's defaults throughout.</summary>
    /// <remarks>
    /// Only the properties you set are applied; everything else falls back to Orb's default
    /// rather than to nothing. Returning <see langword="null"/> after having returned a style
    /// restores the defaults.
    /// </remarks>
    OrbNodeStyle? Style => null;
}
