# Pinknose.Memgraph.Orb.Razor — Incremental Updates and Node Positions

**Date:** 2026-08-21
**Status:** Approved design, ready for implementation planning
**Scope:** Two changes to the wrapper — sending only what changed on an update, and exposing Orb's
node positioning. One is internal; one moves the public API.

---

## 1. Context

Both changes are driven by a real consumer rather than by speculation. `Msei.RSGraph` is replacing
two hand-written cytoscape renderers with this package across the two surfaces of its dependency
trace viewer. Its design is at
`Msei.RSGraph/docs/superpowers/specs/2026-08-21-orb-graph-replacement-design.md`.

One of those surfaces **accumulates**: the reader expands a dependency one step at a time, and each
expansion adds nodes to what is already on screen. That is the shape the current update path handles
worst, and finding it is what prompted this work.

---

## 2. Verified facts about Orb 1.0.2

Read out of the shipped `.d.ts` and `dist/*.js` in `memgraph-orb-1.0.2.tgz`, not inferred. Several
of these contradict what a reasonable reading of the minified bundle suggests, which is why they are
written out.

**Node data carries no position.**

```ts
export interface IGraphData<N extends INodeBase, E extends IEdgeBase> { nodes: N[]; edges: E[]; }
export interface INodeBase { id: any; }
```

`setup()` and `merge()` therefore **cannot** seed `x`/`y` through node data. A declarative
`IOrbNode.Position` is not implementable.

**Positions are set in batch, through the graph.**

```ts
setNodePositions(positions: INodePosition[]): void;   // INodePosition = { id: any; x?: number; y?: number }
clearPositions(): void;
```

Both are on `IGraph`, reachable from `OrbView.data`.

**There is a per-node position callback in view settings.**

```ts
export interface IOrbViewSettings<N, E> {
    getPosition?(node: INode<N, E>): IPosition | undefined;
    ...
}
```

`OrbView._assignPositions` consults it for every node whenever positions are assigned.

**Neither route sets sticky coordinates.** `Graph.setNodePositions` and `OrbView._assignPositions`
both call `node.setPosition(...)`, which writes the node's rendered `_position`:

```js
setNodePositions(positions) {
    for (let i = 0; i < positions.length; i++) {
        const node = this._nodes.getOne(positions[i].id);
        if (node) { node.setPosition(positions[i], { isNotifySkipped: true }); }
    }
}
```

**Per-node pinning is not reachable.** `ISimulator` accepts a subset:

```ts
fixNodes(nodes?: ISimulationNode[]): void;
releaseNodes(nodes?: ISimulationNode[]): void;
```

but `OrbView` does not, and its `_simulator` is a private field with no accessor:

```ts
fixNodes(): void;
releaseNodes(): void;
```

So fix and release are all-or-nothing for any consumer of `OrbView`. This is despite Orb defining
`IStickyNode { sx?, sy? }` and documenting it for precisely the mixed case — *"This enables a
combination of sticky and free nodes where the free nodes are positioned by the simulator engine to
adjust to the immobilized sticky nodes."* The capability exists in the simulator and is not exposed
by the view.

**Current wrapper behaviour.** `OrbGraph.razor` projects the whole collection, serializes it whole,
compares the result against `_lastDataJson`, and skips the interop call when it matches and nothing
was removed. `OrbGraphDiff` computes removed ids and nothing else. The wrapper never calls `Equals`
on `TNode` or `TEdge` — there is no `EqualityComparer` or `IEquatable` use anywhere in it.

---

## 3. Change 1 — send only what changed

### The problem

For a consumer whose graph accumulates, the serialized JSON differs on every update, so the
short-circuit never fires and the whole graph goes over the wire each time. Expansion *k* re-sends
everything expansions 1 to *k* already placed. Under Blazor Server that is quadratic bytes on the
circuit for a linear amount of user activity.

### Design

**Per-node serialized comparison.** Serialize each node and edge individually, keep the previous
serialization per id, and send only those whose serialization is new or different, alongside the
removed ids that are already computed.

No JavaScript change is needed. `updateData` already routes through Orb's `merge()`, which upserts —
it simply receives a smaller payload.

### Why not compare `TNode` instances

Comparing the consumer's instances with `EqualityComparer<TNode>.Default` is cheaper and was the
first design considered. It was rejected because it is **weaker**, not merely faster.

The current whole-graph comparison reads exactly the values that get serialized, so it cannot
disagree with what Orb receives. Instance comparison can: a `TNode` whose `Equals` reports two nodes
identical while their projected `INodeStyle` differs would make the wrapper skip a real visual
change, silently and with nothing in the type system to warn about it. A consumer implementing a
record has no reason to suspect that its equality is load-bearing for rendering.

The guarantee is worth more than the cycles. Deciding "changed" from the serialized output preserves
it and still removes the quadratic re-send, which was the actual problem.

A useful consequence: **this change asks nothing of consumers.** No equality contract, no
documentation caveat, no silent degradation for anyone using mutable classes.

### Public API impact

None. `OrbGraphDiff` is internal and `PublicAPI.Unshipped.txt` does not move.

### Tests

- `OrbGraphDiffTests` — added, changed and removed, including the empty-previous case.
- A second update sends only the newly added nodes. **This is the regression guard that matters**:
  the defect is payload size, so nothing fails when it returns. Without this test the fix is
  asserted rather than demonstrated.
- Changing one node's style sends that node and no others.
- A node whose serialization is unchanged is not sent, even when the instance is different.
- Removal continues to work, and a removal with no additions still reaches the view.

---

## 4. Change 2 — node positions

### What is exposed

```csharp
public readonly record struct OrbNodePosition(string Id, double? X, double? Y);

// on OrbGraph<TNode, TEdge>
public ValueTask SetNodePositionsAsync(IEnumerable<OrbNodePosition> positions);
public ValueTask ClearNodePositionsAsync();
```

Batch rather than single, matching `IGraph.setNodePositions` and costing one interop call for any
number of nodes rather than one per node.

### The physics caveat

**This must be documented prominently or it will be reported as a bug.**

`setNodePositions` sets the *rendered* position, not sticky coordinates. A position set while the
force simulation is running is therefore expected to be overwritten as soon as the simulator next
writes positions back.

*(Inference, not yet measured: the mechanism above is read from source, but the resulting behaviour
has not been observed. The implementation plan must begin by confirming it, because the whole
shape of the documentation depends on which way it goes.)*

Positions are expected to be durable when the simulation is not running — `OrbForceLayout` with
`IsPhysicsEnabled = false`, or one of the static layouts. That combination is the supported use:
**a caller who wants to place nodes deliberately turns physics off and places them.**

### What is deliberately not exposed

**Per-node fix and release.** Not reachable through `OrbView` (section 2). Exposing it would mean an
upstream change to Orb — either widening `OrbView.fixNodes`/`releaseNodes` to take ids, or surfacing
the simulator.

This is worth recording as a **known gap** rather than passing over, because it is the capability a
consumer is most likely to want: pinning a few meaningful nodes and letting physics arrange the rest
around them is exactly what `IStickyNode` is documented to enable, and the wrapper cannot offer it.
The honest answer to a consumer asking for it is "Orb does not expose it, and here is the upstream
issue", not silence.

### The `getPosition` hook — deferred

`IOrbViewSettings.getPosition` would let supplied positions survive a `merge()` that adds nodes,
since it is consulted whenever positions are assigned. It is a JavaScript callback, so exposing it
means holding a position map on the JS side rather than passing a C# delegate.

Deferred, not rejected. Nothing in this change forecloses it, and it is the right mechanism if
"positions that persist across updates" is ever asked for. Adding it now would be building for a
consumer that has not asked.

### Public API impact

`OrbNodePosition`, `SetNodePositionsAsync` and `ClearNodePositionsAsync` are added to
`PublicAPI.Unshipped.txt`. The package is beta and its README states the API is likely to change, so
this needs release notes rather than a compatibility strategy.

### Tests

- A position batch reaches `setNodePositions` with the ids and coordinates given.
- An empty batch does not call into JavaScript.
- `ClearNodePositionsAsync` reaches `clearPositions`.
- Both throw or no-op predictably when called before the view exists, matching how the other
  imperative methods on `OrbGraph` already behave — follow the existing pattern rather than
  inventing a second one.
- A browser test placing nodes with physics disabled and asserting they stay put. This is the test
  that turns the caveat above from an inference into a documented behaviour.

---

## 5. Release

Both changes go on `feat/incremental-and-positions`, branched from `master` in a git worktree —
`feat/pages-demo` holds unrelated work in progress and `Demo.razor` does not exist on `master`.

**Hold the tag.** `Msei.RSGraph` is integrating against published `0.1.1-beta.1` and is the first
real consumer; building it out is likely to surface more than these two items. A published NuGet
version can never be replaced, and one prerelease carrying several fixes is worth more than several
carrying one each.

---

## 6. Risks

- **MAUI Blazor Hybrid is untested.** The matrix is Blazor Server, WebAssembly and trimmed
  WebAssembly. `Msei.RSGraph` ships on Hybrid and will exercise it against a real `.nupkg`. Expected
  to work — an ordinary Blazor host with RCL assets under `_content/` — but expectation is not
  evidence, and the packaging is the layer most likely to decide it.
- **The physics caveat is the most likely source of a "positions do not work" report.** Mitigated by
  documentation and by the browser test above, not by API design, because the limitation is Orb's.
- **Per-node serialized comparison costs a full serialize per update**, the same as today. It reduces
  bytes on the wire, not CPU in the render loop. If serialization cost ever becomes the constraint,
  that is a different change with a different design.
