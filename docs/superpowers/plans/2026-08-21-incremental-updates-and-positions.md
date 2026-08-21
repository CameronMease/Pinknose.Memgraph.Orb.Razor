# Incremental Updates and Node Positions Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Send only what changed on an update instead of the whole graph, and expose Orb's two node-positioning mechanisms for what they each actually do.

**Architecture:** Change detection moves from one whole-graph JSON comparison to a per-id comparison of individually serialized nodes and edges — same guarantee, smaller payload, no JavaScript change. Positioning adds a JS-side position map read by a `getPosition` closure (which seeds nodes into the simulator), plus a pass-through to `IGraph.setNodePositions` (which repositions rendered nodes).

**Tech Stack:** .NET 10, Blazor RCL, `@memgraph/orb` 1.0.2 vendored, MSTest 4.3.3, bUnit 2.9.0, Playwright browser tests.

## Global Constraints

- Branch: `feat/incremental-and-positions`, already created as a worktree at `../orb-incremental` off `master`. **Do not check out `master` in the main clone** — `feat/pages-demo` has uncommitted work there and `Demo.razor` does not exist on `master`.
- **`PublicAPI.Unshipped.txt` must be updated for every public member added.** The build fails otherwise. This is the release contract; treat a diff there as intentional and reviewable.
- Serialization is source-generated through `OrbJsonContext` because the library is published trimmable. **Any new type that gets serialized needs a `[JsonSerializable]` entry**, or it fails only under a trimmed publish — which the `TrimmedPublishTests` project exists to catch.
- Run unit tests: `dotnet test tests/Pinknose.Memgraph.Orb.Razor.Tests/Pinknose.Memgraph.Orb.Razor.Tests.csproj`
- Browser tests need the sample host; see `CONTRIBUTING.md` for how that suite is run.
- The design spec is `docs/superpowers/specs/2026-08-21-incremental-updates-and-positions-design.md`, Revision B. Section 2 lists facts read from Orb's `.d.ts` and `dist/*.js` — trust it over intuition about Orb's behaviour.

---

### Task 1: Confirm what `setNodePositions` does while physics runs

The spec's Revision B asserts that a position set during a running simulation is overwritten on the next tick. **That is an inference from source, not an observation**, and the documentation written in Task 8 depends on which way it goes. Settle it before building on it.

This task produces a *fact*, and possibly a test. It may produce no library change at all.

**Files:**
- Test: `tests/Pinknose.Memgraph.Orb.Razor.BrowserTests/NodePositionBehaviourTests.cs`

- [ ] **Step 1: Write a browser test that places a node and watches it**

Follow `OrbGraphSmokeTests` and `OrbPageDriver` for how the browser suite reaches the view — the sample page exposes `globalThis.__orbTestView` when the host carries `data-orb-test`.

```csharp
namespace Pinknose.Memgraph.Orb.Razor.BrowserTests;

/// <summary>
/// What Orb actually does with a position set while the force simulation is running.
///
/// <para>
/// The design reasons from source that <c>setNodePositions</c> writes the rendered position and is
/// overwritten on the next simulator report. That reasoning has not been observed, and the
/// documentation for the whole positioning feature depends on it, so it is measured here rather
/// than asserted from the shape of the code.
/// </para>
/// </summary>
[TestClass]
public sealed class NodePositionBehaviourTests
{
    [TestMethod]
    public async Task SetNodePositions_WithPhysicsRunning_DoesNotHold()
    {
        // Place a node far from where any layout would put it, let the simulation tick, and read
        // the position back. Assert whichever way it actually goes — and if it holds, the spec's
        // caveat is wrong and Task 8's documentation changes accordingly.
    }

    [TestMethod]
    public async Task SetNodePositions_WithPhysicsDisabled_Holds()
    {
        // OrbForceLayout { IsPhysicsEnabled = false }, place, read back.
    }
}
```

- [ ] **Step 2: Run it and record what happened**

Run the browser suite. Whatever the result, write it down — this is the deliverable.

- [ ] **Step 3: If the spec was wrong, amend it before continuing**

If positions *do* hold with physics running, add a Revision C to the spec correcting Revision B and simplifying Task 8's documentation. Do not carry a known-false statement forward into the README.

- [ ] **Step 4: Commit**

```bash
git add tests/Pinknose.Memgraph.Orb.Razor.BrowserTests/NodePositionBehaviourTests.cs
git commit -m "test: what a position set during simulation actually does"
```

---

### Task 2: Serialize one node and one edge at a time

Change detection needs per-id serialization. `OrbJson` only serializes a whole `OrbGraphPayload` today.

**Files:**
- Modify: `Pinknose.Memgraph.Orb.Razor/Serialization/OrbJsonContext.cs`
- Test: `tests/Pinknose.Memgraph.Orb.Razor.Tests/Serialization/OrbSerializationTests.cs`

**Interfaces:**
- Produces: `OrbJson.SerializeNode(OrbNodePayload)` and `OrbJson.SerializeEdge(OrbEdgePayload)`, both `internal static string`.

- [ ] **Step 1: Write the failing test**

Append to `OrbSerializationTests`:

```csharp
    [TestMethod]
    public void SerializeNode_ProducesTheSameShapeAsInsideAWholeGraph()
    {
        // The per-node serialization is what change detection compares, so it has to describe the
        // node exactly as the graph payload does. If the two ever diverge, a node could look
        // unchanged individually while its graph representation differs.
        var node = new OrbNodePayload { Id = "n1", Style = new OrbNodeStylePayload { Color = "#fff" } };

        var alone = OrbJson.SerializeNode(node);
        var inGraph = OrbJson.SerializeGraph(new OrbGraphPayload { Nodes = [node], Edges = [] });

        StringAssert.Contains(inGraph, alone);
    }

    [TestMethod]
    public void SerializeNode_TwoNodesDifferingOnlyByStyle_SerializeDifferently()
    {
        var a = new OrbNodePayload { Id = "n1", Style = new OrbNodeStylePayload { Color = "#fff" } };
        var b = new OrbNodePayload { Id = "n1", Style = new OrbNodeStylePayload { Color = "#000" } };

        Assert.AreNotEqual(OrbJson.SerializeNode(a), OrbJson.SerializeNode(b));
    }

    [TestMethod]
    public void SerializeEdge_IncludesEndpoints()
    {
        var edge = new OrbEdgePayload { Id = "e1", Start = "n1", End = "n2" };

        var json = OrbJson.SerializeEdge(edge);

        StringAssert.Contains(json, "\"start\":\"n1\"");
        StringAssert.Contains(json, "\"end\":\"n2\"");
    }
```

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test tests/Pinknose.Memgraph.Orb.Razor.Tests/Pinknose.Memgraph.Orb.Razor.Tests.csproj`
Expected: FAIL — `SerializeNode` does not exist.

- [ ] **Step 3: Register the payload types and add the methods**

In `OrbJsonContext.cs`, add two attributes beside the existing ones:

```csharp
[JsonSerializable(typeof(OrbNodePayload))]
[JsonSerializable(typeof(OrbEdgePayload))]
```

**This registration is not optional.** Without it these serialize reflectively, which produces an IL2026 trim warning and can fail outright in a trimmed WebAssembly publish — the failure mode `TrimmedPublishTests` guards.

In `OrbJson`, beside `SerializeGraph`:

```csharp
    // Serialized individually so an update can compare node against node and send only what
    // differs. The comparison must read exactly what gets sent, which is why this shares the same
    // options and the same generated metadata as SerializeGraph rather than being reimplemented.
    public static string SerializeNode(OrbNodePayload payload)
        => JsonSerializer.Serialize(payload, TypeInfo<OrbNodePayload>(Options));

    public static string SerializeEdge(OrbEdgePayload payload)
        => JsonSerializer.Serialize(payload, TypeInfo<OrbEdgePayload>(Options));
```

- [ ] **Step 4: Run to verify they pass**

Run: `dotnet test tests/Pinknose.Memgraph.Orb.Razor.Tests/Pinknose.Memgraph.Orb.Razor.Tests.csproj`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add Pinknose.Memgraph.Orb.Razor/Serialization/OrbJsonContext.cs tests/Pinknose.Memgraph.Orb.Razor.Tests/Serialization/OrbSerializationTests.cs
git commit -m "feat: serialize one node at a time, so a change can be found per node"
```

---

### Task 3: OrbGraphDiff learns what was added or changed

**Files:**
- Modify: `Pinknose.Memgraph.Orb.Razor/Internal/OrbGraphDiff.cs`
- Test: `tests/Pinknose.Memgraph.Orb.Razor.Tests/Internal/OrbGraphDiffTests.cs`

**Interfaces:**
- Produces: `internal static string[] ChangedIds(IReadOnlyDictionary<string, string> previous, IReadOnlyDictionary<string, string> current)` — ids whose serialization is new or different. `RemovedIds` is unchanged.

- [ ] **Step 1: Write the failing test**

Append to `OrbGraphDiffTests`:

```csharp
    [TestMethod]
    public void ChangedIds_ReturnsWhatIsNew()
    {
        var changed = OrbGraphDiff.ChangedIds(
            new Dictionary<string, string> { ["a"] = "1" },
            new Dictionary<string, string> { ["a"] = "1", ["b"] = "2" });

        CollectionAssert.AreEquivalent(new[] { "b" }, changed);
    }

    [TestMethod]
    public void ChangedIds_ReturnsWhatDiffers()
    {
        var changed = OrbGraphDiff.ChangedIds(
            new Dictionary<string, string> { ["a"] = "1" },
            new Dictionary<string, string> { ["a"] = "2" });

        CollectionAssert.AreEquivalent(new[] { "a" }, changed);
    }

    [TestMethod]
    public void ChangedIds_IgnoresWhatIsIdentical()
    {
        var changed = OrbGraphDiff.ChangedIds(
            new Dictionary<string, string> { ["a"] = "1", ["b"] = "2" },
            new Dictionary<string, string> { ["a"] = "1", ["b"] = "2" });

        Assert.AreEqual(0, changed.Length);
    }

    [TestMethod]
    public void ChangedIds_FirstUpdate_ReturnsEverything()
    {
        var changed = OrbGraphDiff.ChangedIds(
            new Dictionary<string, string>(),
            new Dictionary<string, string> { ["a"] = "1", ["b"] = "2" });

        CollectionAssert.AreEquivalent(new[] { "a", "b" }, changed);
    }

    [TestMethod]
    public void ChangedIds_IgnoresRemovals()
    {
        // Removal is RemovedIds' job. An id absent from current is not a change to send.
        var changed = OrbGraphDiff.ChangedIds(
            new Dictionary<string, string> { ["a"] = "1", ["b"] = "2" },
            new Dictionary<string, string> { ["a"] = "1" });

        Assert.AreEqual(0, changed.Length);
    }
```

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test tests/Pinknose.Memgraph.Orb.Razor.Tests/Pinknose.Memgraph.Orb.Razor.Tests.csproj`
Expected: FAIL — `ChangedIds` does not exist.

- [ ] **Step 3: Implement**

```csharp
    /// <summary>
    /// Ids whose serialization is new or differs from last time.
    ///
    /// <para>
    /// Compares serialized output rather than the consumer's own instances. That is deliberate and
    /// is the slower of the two options: a consumer's <c>Equals</c> can report two nodes identical
    /// while their projected styles differ, which would silently skip a real visual change. What is
    /// compared here is exactly what gets sent, so it cannot disagree with what Orb receives.
    /// </para>
    /// </summary>
    public static string[] ChangedIds(
        IReadOnlyDictionary<string, string> previous,
        IReadOnlyDictionary<string, string> current)
    {
        if (current.Count == 0)
        {
            return [];
        }

        var changed = new List<string>();

        foreach (var (id, json) in current)
        {
            if (!previous.TryGetValue(id, out var was) || !string.Equals(was, json, StringComparison.Ordinal))
            {
                changed.Add(id);
            }
        }

        return [.. changed];
    }
```

- [ ] **Step 4: Run to verify they pass**

Run: `dotnet test tests/Pinknose.Memgraph.Orb.Razor.Tests/Pinknose.Memgraph.Orb.Razor.Tests.csproj`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add Pinknose.Memgraph.Orb.Razor/Internal/OrbGraphDiff.cs tests/Pinknose.Memgraph.Orb.Razor.Tests/Internal/OrbGraphDiffTests.cs
git commit -m "feat: a diff that knows what changed, not only what left"
```

---

### Task 4: Send only what changed

**Files:**
- Modify: `Pinknose.Memgraph.Orb.Razor/OrbGraph.razor` (the update path, around lines 210–245, and the fields around line 29)
- Test: `tests/Pinknose.Memgraph.Orb.Razor.Tests/OrbGraphComponentTests.cs`

**Interfaces:**
- Consumes: `OrbJson.SerializeNode`/`SerializeEdge` (Task 2), `OrbGraphDiff.ChangedIds` (Task 3).
- Produces: no public change. `updateData`'s JS signature is unchanged — it receives a smaller payload.

- [ ] **Step 1: Write the failing test**

```csharp
    [TestMethod]
    public void SecondUpdate_SendsOnlyTheNewNodes()
    {
        // The regression guard that matters. The defect this prevents is payload size, so nothing
        // fails when it comes back -- without this test the fix is asserted rather than shown.
        var module = SetupModule();

        var cut = Render<OrbGraph<OrbNode, OrbEdge>>(p => p
            .Add(x => x.Nodes, new[] { new OrbNode("n1") })
            .Add(x => x.Edges, Array.Empty<OrbEdge>()));

        cut.SetParametersAndRender(p => p
            .Add(x => x.Nodes, new[] { new OrbNode("n1"), new OrbNode("n2") })
            .Add(x => x.Edges, Array.Empty<OrbEdge>()));

        var update = module.Invocations["updateData"].Single();
        var dataJson = (string?)update.Arguments[1];

        StringAssert.Contains(dataJson!, "\"id\":\"n2\"");
        Assert.IsFalse(
            dataJson!.Contains("\"id\":\"n1\"", StringComparison.Ordinal),
            "n1 was already on screen and unchanged, so re-sending it is the bug this prevents.");
    }

    [TestMethod]
    public void AnUpdateChangingOneNodesStyle_SendsThatNodeAndNoOther()
    {
        var module = SetupModule();

        var cut = Render<OrbGraph<OrbNode, OrbEdge>>(p => p
            .Add(x => x.Nodes, new[] { new OrbNode("n1"), new OrbNode("n2") })
            .Add(x => x.Edges, Array.Empty<OrbEdge>()));

        cut.SetParametersAndRender(p => p
            .Add(x => x.Nodes, new[]
            {
                new OrbNode("n1") { Style = new OrbNodeStyle { Color = "#f00" } },
                new OrbNode("n2"),
            })
            .Add(x => x.Edges, Array.Empty<OrbEdge>()));

        var dataJson = (string?)module.Invocations["updateData"].Single().Arguments[1];

        StringAssert.Contains(dataJson!, "\"id\":\"n1\"");
        Assert.IsFalse(dataJson!.Contains("\"id\":\"n2\"", StringComparison.Ordinal));
    }

    [TestMethod]
    public void AnUpdateChangingNothing_DoesNotCallIntoJavaScript()
    {
        var module = SetupModule();

        var cut = Render<OrbGraph<OrbNode, OrbEdge>>(p => p
            .Add(x => x.Nodes, TwoNodes)
            .Add(x => x.Edges, OneEdge));

        cut.SetParametersAndRender(p => p
            .Add(x => x.Nodes, new[] { new OrbNode("n1"), new OrbNode("n2") })
            .Add(x => x.Edges, new[] { new OrbEdge("e1", "n1", "n2") }));

        Assert.IsFalse(module.Invocations.ContainsKey("updateData"));
    }

    [TestMethod]
    public void ARemovalStillReachesTheView_EvenWithNothingAdded()
    {
        var module = SetupModule();

        var cut = Render<OrbGraph<OrbNode, OrbEdge>>(p => p
            .Add(x => x.Nodes, TwoNodes)
            .Add(x => x.Edges, Array.Empty<OrbEdge>()));

        cut.SetParametersAndRender(p => p
            .Add(x => x.Nodes, new[] { new OrbNode("n1") })
            .Add(x => x.Edges, Array.Empty<OrbEdge>()));

        var removedNodeIds = (string[]?)module.Invocations["updateData"].Single().Arguments[2];

        CollectionAssert.AreEquivalent(new[] { "n2" }, removedNodeIds);
    }
```

`OrbNode`'s exact shape — whether `Style` is settable via initializer — is in `Model/OrbNode.cs`. Read it and match; do not invent a constructor.

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test tests/Pinknose.Memgraph.Orb.Razor.Tests/Pinknose.Memgraph.Orb.Razor.Tests.csproj`
Expected: FAIL — the second update currently sends both nodes.

- [ ] **Step 3: Track serialized output per id**

Replace the `_lastDataJson` field with per-id maps, keeping `_sentNodes`/`_sentEdges` exactly as they are — those map ids back to consumer instances for event dispatch and are unrelated:

```csharp
    private Dictionary<string, string> _sentNodeJson = [];
    private Dictionary<string, string> _sentEdgeJson = [];
```

- [ ] **Step 4: Rewrite the update path**

In the update branch (currently around lines 216–243), replace the whole-graph serialize-and-compare with:

```csharp
            var nodeJson = projection.Payload.Nodes.ToDictionary(
                n => n.Id, OrbJson.SerializeNode, StringComparer.Ordinal);
            var edgeJson = projection.Payload.Edges.ToDictionary(
                e => e.Id, OrbJson.SerializeEdge, StringComparer.Ordinal);

            var changedNodeIds = OrbGraphDiff.ChangedIds(_sentNodeJson, nodeJson);
            var changedEdgeIds = OrbGraphDiff.ChangedIds(_sentEdgeJson, edgeJson);
            var removedNodeIds = OrbGraphDiff.RemovedIds(_sentNodeJson.Keys, nodeJson.Keys);
            var removedEdgeIds = OrbGraphDiff.RemovedIds(_sentEdgeJson.Keys, edgeJson.Keys);

            // The id->instance maps are refreshed whenever there is a fresh projection, even when
            // nothing is sent -- see the existing comment above them. That reasoning is unchanged.
            _sentNodes = projection.NodesById;
            _sentEdges = projection.EdgesById;
            _sentNodeJson = nodeJson;
            _sentEdgeJson = edgeJson;

            if (changedNodeIds.Length > 0 || changedEdgeIds.Length > 0
                || removedNodeIds.Length > 0 || removedEdgeIds.Length > 0)
            {
                // Only the changed subset goes over the wire. Orb's merge() upserts, so a node it
                // already holds and that did not change needs no mention.
                var changedNodes = new HashSet<string>(changedNodeIds, StringComparer.Ordinal);
                var changedEdges = new HashSet<string>(changedEdgeIds, StringComparer.Ordinal);

                var delta = new OrbGraphPayload
                {
                    Nodes = [.. projection.Payload.Nodes.Where(n => changedNodes.Contains(n.Id))],
                    Edges = [.. projection.Payload.Edges.Where(e => changedEdges.Contains(e.Id))],
                };

                await _module!.InvokeVoidAsync(
                    "updateData", _handle, OrbJson.SerializeGraph(delta), removedNodeIds, removedEdgeIds);
            }
```

- [ ] **Step 4b: Seed the maps on first render**

The first render sends everything through `initializeOrb` and `setup()`, which is correct and does not change. But it currently records `_lastDataJson = dataJson`, and that field is going away. Replace it so the *first update* has something to compare against — otherwise the first update re-sends the whole graph, which is the bug this task removes, one render later.

In the first-render success block (around line 170, beside `_sentNodes = projection.NodesById;`):

```csharp
            _sentNodeJson = projection.Payload.Nodes.ToDictionary(
                n => n.Id, OrbJson.SerializeNode, StringComparer.Ordinal);
            _sentEdgeJson = projection.Payload.Edges.ToDictionary(
                e => e.Id, OrbJson.SerializeEdge, StringComparer.Ordinal);
```

Delete `_lastDataJson` and every remaining use of it. `dataJson` on that path stays — `initializeOrb` still takes the whole graph.

A test for this specific seam, added alongside the others in Step 1:

```csharp
    [TestMethod]
    public void TheFirstUpdateAfterRender_DoesNotResendWhatSetupAlreadySent()
    {
        // Guards the seam between the two paths. If first render does not record what it sent,
        // everything looks changed on the first update and the whole graph goes again -- the
        // original bug, delayed by exactly one render.
        var module = SetupModule();

        var cut = Render<OrbGraph<OrbNode, OrbEdge>>(p => p
            .Add(x => x.Nodes, new[] { new OrbNode("n1") })
            .Add(x => x.Edges, Array.Empty<OrbEdge>()));

        cut.SetParametersAndRender(p => p
            .Add(x => x.Nodes, new[] { new OrbNode("n1"), new OrbNode("n2") })
            .Add(x => x.Edges, Array.Empty<OrbEdge>()));

        var dataJson = (string?)module.Invocations["updateData"].Single().Arguments[1];

        Assert.IsFalse(dataJson!.Contains("\"id\":\"n1\"", StringComparison.Ordinal));
    }
```

- [ ] **Step 5: Run the whole suite**

Run: `dotnet test tests/Pinknose.Memgraph.Orb.Razor.Tests/Pinknose.Memgraph.Orb.Razor.Tests.csproj`
Expected: PASS, including every existing test. A failure in the duplicate-id or dangling-edge tests means the projection path was disturbed — it should not have been.

- [ ] **Step 6: Commit**

```bash
git add Pinknose.Memgraph.Orb.Razor/OrbGraph.razor tests/Pinknose.Memgraph.Orb.Razor.Tests/OrbGraphComponentTests.cs
git commit -m "feat: an update carries what changed, not the whole graph again"
```

---

### Task 5: The JS position map and the getPosition hook

**Files:**
- Modify: `Pinknose.Memgraph.Orb.Razor/wwwroot/orbGraph.js`

**Interfaces:**
- Produces: `setSeedPositions(handle, positions)`, `clearSeedPositions(handle)` exports; `handle.positions` map; `getPosition` installed on the view.

- [ ] **Step 1: Create the map and install the hook**

`getPosition` is part of `IOrbViewSettings`, and Orb consults it in `_assignPositions` on both setup and merge — that is the only public route by which a position reaches the simulator.

In `initializeOrb`, create the map before the view and pass a closure over it at construction:

```js
    const settings = parseJson(settingsJson);
    const positions = new Map();
    const view = new OrbView(host, {
        // Read on every setup and merge, for the nodes being added. This is what decides where a
        // node ENTERS the simulation -- setNodePositions writes the rendered position after the
        // fact and the simulator overwrites it.
        getPosition: (node) => positions.get(String(node.getId())),
    });
    const handle = { view, host, dotNetRef, positions, defaultSettings: view.getSettings() };
```

- [ ] **Step 2: Guard the hook against a settings reset**

`resetSettings` re-applies `handle.defaultSettings`, and `applySettings` merges into current settings. Neither is guaranteed to preserve a function member across Orb's own copying, and `defaultSettings` is snapshotted at construction.

Add a helper and call it after **every** settings mutation, rather than reasoning about how Orb copies functions:

```js
// Re-asserts the position hook after any settings change. setSettings merges and resetSettings
// re-applies a snapshot; neither is guaranteed to carry a function member through Orb's own
// copying. Re-installing is cheap and removes the question entirely.
function installPositionHook(handle) {
    handle.view.setSettings({
        getPosition: (node) => handle.positions.get(String(node.getId())),
    });
}
```

Call it at the end of `initializeOrb`, `applySettings` and `resetSettings`.

- [ ] **Step 3: Add the two exports**

```js
// Merges rather than replaces, so a caller seeding newly arrived nodes need not restate the ones
// already placed -- which is the accumulating case this exists for.
export function setSeedPositions(handle, positions) {
    if (!handle || !positions) return;

    for (const p of positions) {
        handle.positions.set(String(p.id), { x: p.x, y: p.y });
    }
}

export function clearSeedPositions(handle) {
    handle?.positions.clear();
}

export function setNodePositions(handle, positions) {
    if (!handle || !positions?.length) return;

    handle.view.data.setNodePositions(positions);
    handle.view.render();
}

export function clearNodePositions(handle) {
    if (!handle) return;

    handle.view.data.clearPositions();
    handle.view.render();
}
```

- [ ] **Step 4: Clear the map on dispose**

In `disposeOrb`, beside the existing test-hook cleanup:

```js
    handle.positions?.clear();
```

- [ ] **Step 5: Build the sample host to confirm the module still parses**

Run: `dotnet build samples/Pinknose.Memgraph.Orb.Razor.SampleHost/Pinknose.Memgraph.Orb.Razor.SampleHost.csproj`
Expected: builds. A syntax error in the module would not surface until runtime otherwise.

- [ ] **Step 6: Commit**

```bash
git add Pinknose.Memgraph.Orb.Razor/wwwroot/orbGraph.js
git commit -m "feat: a position map the simulator reads when a node arrives"
```

---

### Task 6: The public positioning API

**Files:**
- Create: `Pinknose.Memgraph.Orb.Razor/Model/OrbNodePosition.cs`
- Modify: `Pinknose.Memgraph.Orb.Razor/OrbGraph.razor`
- Modify: `Pinknose.Memgraph.Orb.Razor/PublicAPI.Unshipped.txt`
- Test: `tests/Pinknose.Memgraph.Orb.Razor.Tests/OrbGraphComponentTests.cs`

**Interfaces:**
- Produces: `public readonly record struct OrbNodePosition(string Id, double X, double Y)`; on `OrbGraph<TNode, TEdge>`: `SetSeedPositionsAsync`, `ClearSeedPositionsAsync`, `SetNodePositionsAsync`, `ClearNodePositionsAsync`, all `ValueTask`.

- [ ] **Step 1: Write the failing test**

```csharp
    [TestMethod]
    public async Task SetSeedPositionsAsync_SendsTheCoordinatesToTheMap()
    {
        var module = SetupModule();

        var cut = Render<OrbGraph<OrbNode, OrbEdge>>(p => p
            .Add(x => x.Nodes, TwoNodes)
            .Add(x => x.Edges, OneEdge));

        await cut.Instance.SetSeedPositionsAsync([new OrbNodePosition("n1", 10, 20)]);

        Assert.AreEqual(1, module.Invocations["setSeedPositions"].Count);
    }

    [TestMethod]
    public async Task SetSeedPositionsAsync_AnEmptyBatch_DoesNotCallIntoJavaScript()
    {
        var module = SetupModule();

        var cut = Render<OrbGraph<OrbNode, OrbEdge>>(p => p
            .Add(x => x.Nodes, TwoNodes)
            .Add(x => x.Edges, OneEdge));

        await cut.Instance.SetSeedPositionsAsync([]);

        Assert.IsFalse(module.Invocations.ContainsKey("setSeedPositions"));
    }

    [TestMethod]
    public async Task SetNodePositionsAsync_ReachesTheGraph()
    {
        var module = SetupModule();

        var cut = Render<OrbGraph<OrbNode, OrbEdge>>(p => p
            .Add(x => x.Nodes, TwoNodes)
            .Add(x => x.Edges, OneEdge));

        await cut.Instance.SetNodePositionsAsync([new OrbNodePosition("n1", 10, 20)]);

        Assert.AreEqual(1, module.Invocations["setNodePositions"].Count);
    }
```

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test tests/Pinknose.Memgraph.Orb.Razor.Tests/Pinknose.Memgraph.Orb.Razor.Tests.csproj`
Expected: FAIL — the methods do not exist.

- [ ] **Step 3: Add the position type**

```csharp
namespace Pinknose.Memgraph.Orb.Razor;

/// <summary>
/// One node's coordinates, for <see cref="OrbGraph{TNode, TEdge}.SetSeedPositionsAsync"/> and
/// <see cref="OrbGraph{TNode, TEdge}.SetNodePositionsAsync"/>.
/// </summary>
public readonly record struct OrbNodePosition(string Id, double X, double Y);
```

- [ ] **Step 4: Add the four methods**

Follow the existing imperative methods (`RecenterAsync`, `SelectNodeAsync`) for how they reach the module and how they behave before the view exists — match that pattern rather than inventing a second one.

```csharp
    /// <summary>
    /// Sets where nodes <b>enter</b> the simulation. Merges into what was seeded before.
    ///
    /// <para>
    /// Orb reads these when a node is set up or merged, and hands them to the simulator, so a
    /// seeded node starts at the coordinate given. It does not stay there: physics moves it like
    /// any other node. To place a node and have it hold, disable physics and use
    /// <see cref="SetNodePositionsAsync"/>.
    /// </para>
    /// </summary>
    public ValueTask SetSeedPositionsAsync(IEnumerable<OrbNodePosition> positions);

    /// <summary>Forgets every seeded position. Nodes already placed are not moved.</summary>
    public ValueTask ClearSeedPositionsAsync();

    /// <summary>
    /// Moves nodes already in the graph.
    ///
    /// <para>
    /// This sets the rendered position, not a pinned one. While the force simulation is running the
    /// simulator overwrites it. Use it with physics disabled, or use
    /// <see cref="SetSeedPositionsAsync"/> to influence a running layout.
    /// </para>
    /// </summary>
    public ValueTask SetNodePositionsAsync(IEnumerable<OrbNodePosition> positions);

    /// <summary>Clears positions Orb is holding for existing nodes.</summary>
    public ValueTask ClearNodePositionsAsync();
```

Both `Set` methods materialize the sequence once and return without an interop call when it is empty.

Adjust the `SetNodePositionsAsync` doc comment to match whatever **Task 1 actually measured**. If positions turned out to hold with physics running, say that instead — do not ship the inference.

- [ ] **Step 5: Update the public API file**

Add the five new entries to `PublicAPI.Unshipped.txt`. The build fails until they are there, and the diff is the release contract.

- [ ] **Step 6: Run the suite**

Run: `dotnet test tests/Pinknose.Memgraph.Orb.Razor.Tests/Pinknose.Memgraph.Orb.Razor.Tests.csproj`
Expected: PASS, including `PackagingTests`.

- [ ] **Step 7: Commit**

```bash
git add Pinknose.Memgraph.Orb.Razor tests/Pinknose.Memgraph.Orb.Razor.Tests
git commit -m "feat: seed where a node arrives, or move one that is already there"
```

---

### Task 7: A merged node enters at its seed

The test that makes Task 5 and 6 worth having, and the one that distinguishes seeding from repositioning. It cannot be written in bUnit — it needs a real Orb.

**Files:**
- Modify: `tests/Pinknose.Memgraph.Orb.Razor.BrowserTests/NodePositionBehaviourTests.cs`

- [ ] **Step 1: Write the test**

```csharp
    [TestMethod]
    public async Task ANodeMergedAfterItsSeedWasSet_EntersAtThatCoordinate()
    {
        // Seed a coordinate for a node that does not exist yet, then add it. Orb consults
        // getPosition for newly merged nodes and hands the result to simulator.mergeData, so the
        // node should start there rather than wherever the layout would otherwise drop it.
        //
        // Read the position back immediately, before the simulation has had time to move it far --
        // the assertion is about where it entered, not about where it settles.
    }

    [TestMethod]
    public async Task SeedingIsMergedNotReplaced()
    {
        // Seed a, then seed b, then add both. Both should honour their seeds -- the second call
        // must not have forgotten the first.
    }
```

Follow `OrbPageDriver` for reaching `__orbTestView` and reading node positions.

- [ ] **Step 2: Run the browser suite**

Expected: PASS. A failure here means `getPosition` is not installed where it needs to be — check Task 5 Step 2, the settings-reset guard, before suspecting Orb.

- [ ] **Step 3: Commit**

```bash
git add tests/Pinknose.Memgraph.Orb.Razor.BrowserTests/NodePositionBehaviourTests.cs
git commit -m "test: a seeded node enters where it was told to"
```

---

### Task 8: Document both, and the gap between them

**Files:**
- Modify: `README.md`

- [ ] **Step 1: Add a positioning section**

Two mechanisms that sound alike and do different things is the most likely way this feature gets misused. Lead with the distinction, in a table like the spec's, and say plainly which one a caller wants for which job.

State what Task 1 measured, not what the design inferred.

- [ ] **Step 2: Add per-node pinning to "Known gaps"**

Beside the existing `OrbMapView` entry:

```markdown
- **Pinning individual nodes is not supported.** Orb's simulator can hold specific nodes still
  while physics arranges the rest around them — its own `IStickyNode` is documented for exactly
  that — but `OrbView` exposes only `fixNodes()`/`releaseNodes()` over the whole graph and keeps
  its simulator private. `SetSeedPositionsAsync` decides where a node *enters* the simulation,
  which is not the same thing. Closing this needs a change to Orb itself.
```

Naming the upstream reason matters: a consumer who hits this should learn it is Orb's limit, not an oversight here.

- [ ] **Step 3: Note the update behaviour**

Under the existing "Node positions are preserved across updates" section, add that an update now sends only nodes and edges whose serialized form changed — worth stating because it is the reason a large accumulating graph stays usable, and because it asks nothing of the consumer.

- [ ] **Step 4: Commit**

```bash
git add README.md
git commit -m "docs: two positioning mechanisms, and the one Orb does not offer"
```

---

## Self-Review

**Spec coverage.** Per-node serialized comparison (Tasks 2–4) · `EqualityComparer` rejection recorded in the code that replaces it (Task 3) · the regression guard that a second update sends only new nodes (Task 4) · JS position map behind `getPosition` (Task 5) · seed and reposition APIs with `PublicAPI` movement (Task 6) · the merged-node-enters-at-its-seed test (Task 7) · physics caveat measured before being documented (Tasks 1, 8) · per-node pinning recorded as a known gap with its upstream cause (Task 8).

**Deliberately not here:** the release tag. The spec says hold it until `Msei.RSGraph` has integrated and had a chance to surface more, because a published NuGet version can never be replaced.

**Known soft spots:**

- **Task 1 can invalidate Task 8 and part of Task 6.** That is why it is first. If positions turn out to hold under a running simulation, the spec needs a Revision C and the doc comments change. Do not skip it because the reasoning looks sound — it is reasoning, not observation.
- Tasks 1 and 7 leave browser-test bodies as intent rather than code, because the harness (`OrbPageDriver`, `SampleHostFixture`) is established in that project and should be followed rather than guessed at from here. Every other task carries complete code.
- Task 4 edits a file this plan does not reproduce in full. Read `OrbGraph.razor` around lines 150–260 before editing; the first-render path and the update path are separate and only the second changes.
- `OrbNode` is a class with a settable `Style`, verified, so Task 4's object-initializer test code compiles as written.
