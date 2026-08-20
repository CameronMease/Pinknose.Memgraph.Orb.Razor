namespace Pinknose.Memgraph.Orb.Razor;

/// <summary>
/// Renders a force-directed graph of your own node and edge types, using Memgraph's Orb.
/// </summary>
/// <remarks>
/// <para>
/// Supply <c>Nodes</c> and <c>Edges</c> as collections of your domain types; the component
/// projects them into Orb's wire format and keeps the graph in step as they change. Events
/// hand your own instances back, so there is nothing to map in either direction.
/// </para>
/// <para>
/// Blazor infers <typeparamref name="TNode"/> and <typeparamref name="TEdge"/> from the
/// collections only while no event callback is wired. As soon as one is, inference breaks with
/// a <c>CS1503</c> pointing at the callback rather than at the missing type arguments, so
/// supply them explicitly.
/// </para>
/// <para>
/// Works under both Blazor Server and Blazor WebAssembly with no difference in usage.
/// </para>
/// </remarks>
/// <typeparam name="TNode">The node type, implementing <see cref="IOrbNode"/>.</typeparam>
/// <typeparam name="TEdge">The edge type, implementing <see cref="IOrbEdge"/>.</typeparam>
/// <example>
/// <code>
/// &lt;OrbGraph TNode="Person" TEdge="Relationship"
///           Nodes="@_people"
///           Edges="@_relationships"
///           Height="600px"
///           OnNodeClick="@(e =&gt; _selected = e.Node)" /&gt;
/// </code>
/// </example>
public partial class OrbGraph<TNode, TEdge>
    where TNode : IOrbNode
    where TEdge : IOrbEdge
{
}
