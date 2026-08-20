# Memgraph Orb Graph Visualization Library for Blazor/.NET

[![ci](https://github.com/CameronMease/Pinknose.Memgraph.Orb.Razor/actions/workflows/ci.yml/badge.svg)](https://github.com/CameronMease/Pinknose.Memgraph.Orb.Razor/actions/workflows/ci.yml)
[![docs](https://github.com/CameronMease/Pinknose.Memgraph.Orb.Razor/actions/workflows/docs.yml/badge.svg)](https://github.com/CameronMease/Pinknose.Memgraph.Orb.Razor/actions/workflows/docs.yml)

▶️ **[Live demo](https://cameronmease.github.io/Pinknose.Memgraph.Orb.Razor/demo/)** ·
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

## Known gaps

- **`OrbMapView` (geo layout) is not supported.** This library wraps Orb's canvas graph view
  (`OrbView`) only. Orb's map-based view is out of scope.

## Contributing

Building the library, running the samples and tests, regenerating the API site, and cutting a
release are covered in [CONTRIBUTING.md](CONTRIBUTING.md).
