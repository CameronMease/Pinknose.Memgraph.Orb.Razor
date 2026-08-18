namespace Pinknose.Memgraph.Orb.Razor;

/// <summary>A graph node. Implement <see cref="Id"/>; the rest are optional.</summary>
public interface IOrbNode
{
    string Id { get; }
    string? Label => null;
    OrbNodeStyle? Style => null;
}
