# Memgraph Orb Graph Visualization Library for Blazor/.NET

[![ci](https://github.com/CameronMease/Pinknose.Memgraph.Orb.Razor/actions/workflows/ci.yml/badge.svg)](https://github.com/CameronMease/Pinknose.Memgraph.Orb.Razor/actions/workflows/ci.yml)
[![docs](https://github.com/CameronMease/Pinknose.Memgraph.Orb.Razor/actions/workflows/docs.yml/badge.svg)](https://github.com/CameronMease/Pinknose.Memgraph.Orb.Razor/actions/workflows/docs.yml)

📖 **[API documentation](https://cameronmease.github.io/Pinknose.Memgraph.Orb.Razor/)**

**`Pinknose.Memgraph.Orb.Razor`** — an independent Blazor wrapper for
[`@memgraph/orb`](https://github.com/memgraph/orb) 1.0.2, not affiliated with Memgraph.

Drive a force-directed graph from your own domain types through a typed `<OrbGraph>` component,
with no hand-written JS interop: implement two small interfaces on the types you already have,
and node and edge events hand those same instances back to you.

Runs under **Blazor Server** and **Blazor WebAssembly**, including a trimmed WebAssembly
publish — all three are exercised by the test suite on every change. The library is marked
trimmable and serializes through a source-generated context, so a WebAssembly publish trims it
down rather than shipping it whole, and reports no trim warnings against it.

> **Beta.** This is an early release. The API is likely to change, including in ways that break
> existing code, and under `0.x` those changes arrive in minor versions rather than major ones.
> Pin an exact version if you depend on it.
>
> Changes to the public surface are deliberate and recorded — the build fails if a public member
> is added, removed or changed without updating `PublicAPI.*.txt` — so breaks show up in the diff
> and in release notes, not silently.

## Installing

```bash
dotnet add package Pinknose.Memgraph.Orb.Razor
```

Targets .NET 10, and needs a Blazor app with an interactive render mode — Server or WebAssembly,
both supported. No script tag or JS setup: the component brings its own assets.

## Minimal example

Implement `IOrbNode`/`IOrbEdge` on your own domain types (only `Id`, or `Id`/`Start`/`End`,
are required — everything else has a default):

```csharp
public sealed record Person(string EmployeeId, string FullName) : IOrbNode
{
    public string Id => EmployeeId;
    public string? Label => FullName;
}

public sealed record Relationship(string Id, string Start, string End) : IOrbEdge;
```

```razor
<OrbGraph TNode="Person" TEdge="Relationship"
          Nodes="@_people"
          Edges="@_relationships"
          Height="600px" />

@code {
    private List<Person> _people = [new("n1", "Alice"), new("n2", "Bob")];
    private List<Relationship> _relationships = [new("e1", "n1", "n2")];
}
```

## The type-argument trap (read this first)

Blazor can infer `TNode`/`TEdge` from the `Nodes`/`Edges` collections **only while the
component has no `EventCallback` parameters wired up**. The moment you add any event
handler — `OnNodeClick`, `OnEdgeHoverEnter`, any of them — type inference breaks and the
compiler fails with `CS1503` pointing at the callback, not at the missing type arguments.

```razor
@* Fails to compile with CS1503 once OnNodeClick is added, unless TNode/TEdge are explicit *@
<OrbGraph TNode="Person" TEdge="Relationship"
          Nodes="@_people"
          Edges="@_relationships"
          OnNodeClick="@(e => _selected = e.Node)" />
```

This is almost always the first thing a new consumer hits. As soon as you add any event
callback, add `TNode="..."` and `TEdge="..."` explicitly — don't wait for the error to tell
you where to look.

## Styles are merged over Orb's defaults

An `OrbNodeStyle`/`OrbEdgeStyle` only has to carry what you want to change. Whatever you leave
unset falls back to Orb's own default rather than to nothing, so a node with a `Label` and no
`Style` still renders at Orb's default size, and setting only `Color` does not cost you the
size. Setting `Style` back to `null` returns the node or edge to Orb's defaults entirely.

`Settings` behaves the same way: supply only what you care about, and set the parameter back to
`null` to return the whole view to Orb's defaults. (Orb's own `setSettings` merges and has no
"unset", so the component snapshots the view's defaults before applying anything and re-applies
that snapshot when you clear.)

## Node positions are preserved across updates

When `Nodes`/`Edges` change, the component pushes the update through Orb's `merge()`, not a
full re-`setup()`. `merge()` upserts in place, so nodes that already exist keep their current
simulated position instead of the graph re-laying out from scratch. Only genuinely new nodes
get a fresh position from Orb's layout.

An update also sends only the nodes and edges whose serialized form actually changed since the
last push — the component compares what it last sent to what it would send now, one node and
one edge at a time, and only the differing subset goes over the wire. This is why a large,
continually-growing graph (a trace view that accumulates nodes as the user expands it, say)
stays responsive: each update's cost tracks what changed, not the size of everything on screen.
It also asks nothing of your domain types — there's no equality contract to implement, nothing
to get subtly wrong by forgetting a field in `Equals`, and no silent degradation if you do. The
comparison is made from the same JSON the component sends to Orb, so it can't disagree with
what Orb actually receives.

## Positioning nodes: seeding vs. setting

Two mechanisms both move nodes, sound similar, and do different things. Reaching for the wrong
one is the most likely way this feature gets misused, so the distinction matters more than
either method on its own.

| | `SetSeedPositionsAsync` | `SetNodePositionsAsync` |
|---|---|---|
| Writes | Where a node **enters** the simulation | The node's **rendered** position |
| Read by the simulator? | Yes — Orb hands the seeded coordinate to the simulator when the node is set up or merged | No — the simulator never sees this write |
| What happens next | Physics takes over immediately; the node moves like any other from that point on | The position holds — see below |
| Clears with | `ClearSeedPositionsAsync` | `ClearNodePositionsAsync` |
| Reach for it to... | Influence where a newly-arriving node appears in a running force layout | Move an existing node and have it stay put |

**Seeding is not pinning.** A seeded node is only placed at the given coordinate for the instant
it enters the simulation; physics is free to carry it anywhere from there. If you want new nodes
to appear near where a caller expects them in a running layout, seed their positions — that's
the intended use.

**Setting a position holds regardless of physics — this was measured, not assumed from
reading Orb's source.** An earlier draft of this library's design reasoned from Orb's source that
a `SetNodePositionsAsync` write would be overwritten the next time a running simulation reported,
and advised disabling physics before calling it. That reasoning was wrong. Measured directly: a
node placed 5000 units outside the graph, under a force simulation kept permanently hot so it
could never settle, held bit-exact for 2 full seconds across 3 runs, while a control confirmed
physics was genuinely live throughout. The simulator simply never observes a position written
this way, so there's nothing for it to overwrite — the position is durable **unconditionally**,
whether or not physics is running. No need to disable physics first.

## Known gaps

- **`OrbMapView` (geo layout) is not supported.** This library wraps Orb's canvas graph view
  (`OrbView`) only. Orb's map-based view is out of scope.
- **Pinning individual nodes is not supported.** Orb's simulator can hold specific nodes still
  while physics arranges the rest around them — its own `IStickyNode` is documented for exactly
  that — but `OrbView` exposes only `fixNodes()`/`releaseNodes()` over the whole graph and keeps
  its simulator private. `SetSeedPositionsAsync` decides where a node *enters* the simulation,
  which is not the same thing. Closing this needs a change to Orb itself.

## Contributing

Building the library, running the samples and tests, regenerating the API site, and cutting a
release are covered in [CONTRIBUTING.md](CONTRIBUTING.md).
