# Pinknose.Memgraph.Orb.Razor

A Blazor component library wrapping [`@memgraph/orb`](https://github.com/memgraph/orb) 1.0.2,
the force-directed graph visualization library from Memgraph. It gives you a typed `<OrbGraph>`
component driven by your own domain types instead of hand-written JS interop.

**Verification status:** exercised end-to-end (unit tests + a Playwright browser suite) under
**Blazor Server**. **Blazor WebAssembly is untested.** Nothing here is known to break under
WASM, but nothing has been run under it either — treat it as unverified, not unsupported.

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

## Node positions are preserved across updates

When `Nodes`/`Edges` change, the component pushes the update through Orb's `merge()`, not a
full re-`setup()`. `merge()` upserts in place, so nodes that already exist keep their current
simulated position instead of the graph re-laying out from scratch. Only genuinely new nodes
get a fresh position from Orb's layout.

## Known gaps

- **`Settings` going non-null → null is currently a no-op.** Once you've supplied an
  `OrbSettings` object, setting the parameter back to `null` on a later render does not clear
  or reset anything already applied — the component only pushes a settings update when the new
  value is both non-null and different from what was last sent. If you need to change
  settings, mutate/replace the object; don't rely on nulling it out.
- **`OrbMapView` (geo layout) is not supported.** This library wraps Orb's canvas graph view
  (`OrbView`) only. Orb's map-based view is out of scope.
