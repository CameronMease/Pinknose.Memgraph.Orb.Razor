# Pinknose.Memgraph.Orb.Razor

A Blazor component library wrapping [`@memgraph/orb`](https://github.com/memgraph/orb) 1.0.2,
the force-directed graph visualization library from Memgraph. It gives you a typed `<OrbGraph>`
component driven by your own domain types instead of hand-written JS interop.

**Verification status:** exercised end-to-end (unit tests + a Playwright browser suite) under
**both Blazor Server and Blazor WebAssembly** — the sample host serves the same demo component
under each render mode, and every browser test runs against both. What is still unverified is
**publish trimming**: the suite runs a development build, which does not trim, so a trimmed
WASM publish has never been exercised.

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

## Styles are merged over Orb's defaults

An `OrbNodeStyle`/`OrbEdgeStyle` only has to carry what you want to change. Whatever you leave
unset falls back to Orb's own default rather than to nothing, so a node with a `Label` and no
`Style` still renders at Orb's default size, and setting only `Color` does not cost you the
size. Setting `Style` back to `null` returns the node or edge to Orb's defaults entirely.

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

## Node positions are preserved across updates

When `Nodes`/`Edges` change, the component pushes the update through Orb's `merge()`, not a
full re-`setup()`. `merge()` upserts in place, so nodes that already exist keep their current
simulated position instead of the graph re-laying out from scratch. Only genuinely new nodes
get a fresh position from Orb's layout.

## Sample host

`samples/Pinknose.Memgraph.Orb.Razor.SampleHost` serves two pages:

| Route | Render mode |
| --- | --- |
| `/orb-server` | `InteractiveServer` |
| `/orb-wasm` | `InteractiveWebAssembly` (component lives in the `.Client` project) |

Both render the same `OrbDemoView` component and are deliberately kept to nothing but
`<OrbDemoView />`, so the only difference between them is the render mode — which is what makes
"passes on one, fails on the other" a meaningful signal. A unit test enforces that. Add demo
features to `OrbDemoView`, never to a page. Append `?styled=false` to either route to render the
graph with no labels and no styles.

```bash
dotnet run --project samples/Pinknose.Memgraph.Orb.Razor.SampleHost
```

## Known gaps

- **`Settings` going non-null → null is currently a no-op.** Once you've supplied an
  `OrbSettings` object, setting the parameter back to `null` on a later render does not clear
  or reset anything already applied — the component only pushes a settings update when the new
  value is both non-null and different from what was last sent. If you need to change
  settings, mutate/replace the object; don't rely on nulling it out.
- **A trimmed WebAssembly publish has never been run.** The WASM coverage above comes from a
  development build, which does not trim. The library serializes only its own types, but
  whether a `dotnet publish` with trimming enabled keeps everything the reflection-based
  serializer and `[JSInvokable]` binding need is still unverified.
- **`OrbMapView` (geo layout) is not supported.** This library wraps Orb's canvas graph view
  (`OrbView`) only. Orb's map-based view is out of scope.
