namespace Pinknose.Memgraph.Orb.Razor;

/// <summary>A point in the graph's coordinate space.</summary>
public readonly record struct OrbPoint(double X, double Y);

public sealed class OrbNodeEventArgs<TNode>
{
    public required TNode Node { get; init; }
    public OrbPoint LocalPoint { get; init; }
    public OrbPoint GlobalPoint { get; init; }
}

public sealed class OrbEdgeEventArgs<TEdge>
{
    public required TEdge Edge { get; init; }
    public OrbPoint LocalPoint { get; init; }
    public OrbPoint GlobalPoint { get; init; }
}

public sealed class OrbBackgroundEventArgs
{
    public OrbPoint LocalPoint { get; init; }
    public OrbPoint GlobalPoint { get; init; }
}
