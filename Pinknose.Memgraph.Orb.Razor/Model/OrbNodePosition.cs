namespace Pinknose.Memgraph.Orb.Razor;

/// <summary>
/// One node's coordinates, for <see cref="OrbGraph{TNode, TEdge}.SetSeedPositionsAsync"/> and
/// <see cref="OrbGraph{TNode, TEdge}.SetNodePositionsAsync"/>.
/// </summary>
/// <param name="Id">The <see cref="IOrbNode.Id"/> of the node this coordinate is for.</param>
/// <param name="X">The x coordinate.</param>
/// <param name="Y">The y coordinate.</param>
public readonly record struct OrbNodePosition(string Id, double X, double Y);
