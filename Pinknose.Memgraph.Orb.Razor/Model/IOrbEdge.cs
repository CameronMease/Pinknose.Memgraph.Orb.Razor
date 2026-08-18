namespace Pinknose.Memgraph.Orb.Razor;

/// <summary>A graph edge. <see cref="Start"/> and <see cref="End"/> are node ids.</summary>
public interface IOrbEdge
{
    string Id { get; }
    string Start { get; }
    string End { get; }
    string? Label => null;
    OrbEdgeStyle? Style => null;
}
