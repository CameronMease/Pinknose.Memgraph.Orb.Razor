namespace Pinknose.Memgraph.Orb.Razor;

/// <summary>A point in the graph's coordinate space.</summary>
/// <param name="X">The horizontal coordinate.</param>
/// <param name="Y">The vertical coordinate.</param>
public readonly record struct OrbPoint(double X, double Y);

/// <summary>Details of a node event.</summary>
/// <typeparam name="TNode">The node type supplied to the component.</typeparam>
public sealed class OrbNodeEventArgs<TNode>
{
    /// <summary>The node the event concerns.</summary>
    /// <remarks>
    /// This is the instance from your own <c>Nodes</c> collection, not a copy or a projection,
    /// so it can be compared by reference and used to reach the rest of your domain object.
    /// </remarks>
    public required TNode Node { get; init; }

    /// <summary>Where the pointer was, in the graph's simulation coordinates.</summary>
    /// <remarks>
    /// The same space node positions live in, so this is what to compare against a node's
    /// coordinates. It is unaffected by panning and zooming.
    /// </remarks>
    public OrbPoint LocalPoint { get; init; }

    /// <summary>Where the pointer was, in canvas pixels.</summary>
    /// <remarks>
    /// Measured from the canvas's top-left corner, so this is what to use for positioning HTML
    /// over the graph, such as a context menu or tooltip. It moves with panning and zooming.
    /// </remarks>
    public OrbPoint GlobalPoint { get; init; }
}

/// <summary>Details of an edge event.</summary>
/// <typeparam name="TEdge">The edge type supplied to the component.</typeparam>
public sealed class OrbEdgeEventArgs<TEdge>
{
    /// <summary>The edge the event concerns, as the instance from your own collection.</summary>
    public required TEdge Edge { get; init; }

    /// <summary>Where the pointer was, in the graph's simulation coordinates.</summary>
    public OrbPoint LocalPoint { get; init; }

    /// <summary>Where the pointer was, in canvas pixels from the canvas's top-left corner.</summary>
    public OrbPoint GlobalPoint { get; init; }
}

/// <summary>Details of a click on empty canvas, away from any node or edge.</summary>
public sealed class OrbBackgroundEventArgs
{
    /// <summary>Where the pointer was, in the graph's simulation coordinates.</summary>
    public OrbPoint LocalPoint { get; init; }

    /// <summary>Where the pointer was, in canvas pixels from the canvas's top-left corner.</summary>
    public OrbPoint GlobalPoint { get; init; }
}
