namespace Pinknose.Memgraph.Orb.Razor;

/// <summary>A graph edge. <see cref="Start"/> and <see cref="End"/> are node ids.</summary>
/// <remarks>
/// As with <see cref="IOrbNode"/>, implement this on your own domain type: the instance you
/// supply is the instance handed back on <c>OnEdgeClick</c> and the other edge events.
/// </remarks>
public interface IOrbEdge
{
    /// <summary>Identifies the edge. Must be unique across the graph and stable across renders.</summary>
    string Id { get; }

    /// <summary>The <see cref="IOrbNode.Id"/> of the node this edge starts at.</summary>
    /// <remarks>
    /// Removing a node removes its edges with it — Orb cascades that itself, so edges pointing
    /// at a departed node do not need pruning from your collection first.
    /// </remarks>
    string Start { get; }

    /// <summary>The <see cref="IOrbNode.Id"/> of the node this edge ends at.</summary>
    string End { get; }

    /// <summary>Text drawn along the edge. <see langword="null"/> draws no label.</summary>
    /// <remarks>
    /// As with a node's label, this produces a partial style that is merged over Orb's
    /// defaults, so a labelled edge keeps its default width and colour.
    /// </remarks>
    string? Label => null;

    /// <summary>Appearance overrides. <see langword="null"/> uses Orb's defaults throughout.</summary>
    OrbEdgeStyle? Style => null;
}
