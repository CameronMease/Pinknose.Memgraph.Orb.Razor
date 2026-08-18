# Pinknose.Memgraph.Orb.Razor — C# API Design

**Date:** 2026-08-18
**Status:** Approved, ready for implementation planning
**Scope:** The public .NET surface of the Razor wrapper around `@memgraph/orb` 1.0.2

---

## 1. Context

The library wraps Memgraph's Orb graph-visualization library for Blazor. A prior pass
(GitHub Copilot) produced a working vendoring and script-loading setup but targeted Orb's
**0.x** API, which does not exist in 1.0.2. That has been corrected; this document covers
the design of the real C# surface that replaces the current raw-JSON parameters.

### Verified facts about Orb 1.0.2

These were read out of the shipped `.d.ts` and `dist/*.js`, not inferred. Several contradict
Orb's public documentation and drive design decisions below.

| Fact | Source | Consequence |
|---|---|---|
| There is no `Orb` class. The browser global is a namespace exporting `OrbView`, `OrbMapView`, `Color`, `OrbEventType`, `RendererType`, `isNode`, `isEdge`, … | UMD exports | Interop constructs `new Orb.OrbView(el, settings)` |
| Data is not a constructor option — it goes through `view.data.setup({nodes, edges})` | `orb-view.d.ts` | Two-step init |
| Edges are `{ id, start, end }`, not `from`/`to` | `IEdgeBase` | `IOrbEdge.Start` / `.End` |
| `setup()` calls `removeAll()` and rebuilds every node, destroying all positions | `graph.js` | Full re-setup causes a visible re-layout; avoided on updates |
| `merge()` upserts; `setPosition` is guarded by `if ('x' in position && 'y' in position)` | `graph.js`, `node.js` | Payloads omitting x/y **preserve** existing positions |
| `merge()` never removes anything; deletion is only `remove({nodeIds, edgeIds})` | `graph.js` | Wrapper must diff to compute removals |
| `_applyStyle()` contains `if (node.hasStyle()) continue;` | `graph.js` | A default-style callback applies **once per node**; changed styles would never repaint |
| `_insertEdges` silently drops edges whose endpoints are absent, and never retries | `graph.js` | Orphaned edges vanish with no error |
| `node.getLabel()` returns `this._style.label` | `node.js`, `edge.js` | Label is a *style* property in Orb, not data |
| `Node`'s constructor does `this.id = data.data.id` | `node.js` | `INode.id` is our own string id; the `.d.ts` declaring it `number` is wrong |

---

## 2. Decisions

| # | Decision | Rationale |
|---|---|---|
| D1 | Graph-generic; no Memgraph-specific coupling | Consumers bring arbitrary domain objects |
| D2 | `OrbGraph<TNode, TEdge>` constrained to `IOrbNode` / `IOrbEdge` | Markup is just `Nodes`/`Edges`; no selector delegates, no forced wrapper objects |
| D3 | Default interface members for `Label` / `Style` | Implementing `IOrbNode` on an existing type costs one property (`Id`) |
| D4 | Ids are `string` | Keeps diff sets, event round-trip, and JSON unambiguous |
| D5 | Never serialize `TNode`/`TEdge`; project to internal DTOs | WASM trim safety; domain fields never reach the browser |
| D6 | Updates via `merge` + `remove`-by-id-diff | Preserves node positions; no visible re-layout on change |
| D7 | Styles evaluated in C#, pushed per-node from JS | Orb's style hook is a synchronous callback; marshalling it per node is impossible on Server and wrong everywhere. Also the only way around the `hasStyle()` skip |
| D8 | Fully typed `OrbSettings`; **no** `SettingsJson` escape hatch | The settings surface is finite and known; a string parameter is the hole this redesign exists to close |
| D9 | All settings/style properties nullable, `WhenWritingNull` | Orb's own defaults stand; wrapper cannot drift from the wrapped library |
| D10 | No `ScriptUrl` / `ScriptIntegrity` / `StyleUrl` parameters | The bundle is vendored; exposing the URL invites pointing at an incompatible build |
| D11 | `AdditionalAttributes` splatting instead of a `Class` parameter | Covers `id`, `data-*`, `aria-*`, etc. with one idiomatic parameter |
| D12 | Separate `Nodes` and `Edges` parameters | Matches how consumers hold data; self-healing resend removes the main risk (§6) |
| D13 | Both Blazor Server and WASM supported | Interop kept chatty-safe for Server; identical API on both |
| D14 | Hover exposed as `HoverEnter` / `HoverLeave`, synthesized in JS | Orb's raw hover fires per mousemove — unusable on Server, and worse as an API |
| D15 | `OrbMapView` out of scope for v1 | Separate view class with its own settings and tile config |

---

## 3. Public surface

### 3.1 Component

```csharp
public partial class OrbGraph<TNode, TEdge> : ComponentBase, IAsyncDisposable
    where TNode : IOrbNode
    where TEdge : IOrbEdge
{
    [Parameter, EditorRequired] public IEnumerable<TNode> Nodes { get; set; } = [];
    [Parameter]                 public IEnumerable<TEdge> Edges { get; set; } = [];

    [Parameter] public OrbSettings? Settings { get; set; }
    [Parameter] public string Width  { get; set; } = "100%";
    [Parameter] public string Height { get; set; } = "500px";

    [Parameter(CaptureUnmatchedValues = true)]
    public IReadOnlyDictionary<string, object>? AdditionalAttributes { get; set; }

    [Parameter] public EventCallback<OrbNodeEventArgs<TNode>> OnNodeClick       { get; set; }
    [Parameter] public EventCallback<OrbNodeEventArgs<TNode>> OnNodeDoubleClick { get; set; }
    [Parameter] public EventCallback<OrbNodeEventArgs<TNode>> OnNodeRightClick  { get; set; }
    [Parameter] public EventCallback<OrbNodeEventArgs<TNode>> OnNodeHoverEnter  { get; set; }
    [Parameter] public EventCallback<OrbNodeEventArgs<TNode>> OnNodeHoverLeave  { get; set; }
    [Parameter] public EventCallback<OrbEdgeEventArgs<TEdge>> OnEdgeClick       { get; set; }
    [Parameter] public EventCallback<OrbEdgeEventArgs<TEdge>> OnEdgeDoubleClick { get; set; }
    [Parameter] public EventCallback<OrbEdgeEventArgs<TEdge>> OnEdgeRightClick  { get; set; }
    [Parameter] public EventCallback<OrbEdgeEventArgs<TEdge>> OnEdgeHoverEnter  { get; set; }
    [Parameter] public EventCallback<OrbEdgeEventArgs<TEdge>> OnEdgeHoverLeave  { get; set; }
    [Parameter] public EventCallback<OrbBackgroundEventArgs>  OnBackgroundClick { get; set; }

    // imperative, via @ref
    public ValueTask RecenterAsync();
    public ValueTask ZoomInAsync();
    public ValueTask ZoomOutAsync();
    public ValueTask FixNodesAsync();
    public ValueTask ReleaseNodesAsync();
    public ValueTask SelectNodeAsync(string id);
    public ValueTask SelectEdgeAsync(string id);
    public ValueTask UnselectAllAsync();
    public ValueTask<string> GetSvgAsync();
}
```

### 3.2 Node and edge contracts

```csharp
public interface IOrbNode
{
    string Id { get; }
    string? Label => null;
    OrbNodeStyle? Style => null;
}

public interface IOrbEdge
{
    string Id    { get; }
    string Start { get; }   // node id
    string End   { get; }   // node id
    string? Label => null;
    OrbEdgeStyle? Style => null;
}

public class OrbNode(string id) : IOrbNode
{
    public string Id { get; } = id;
    public string? Label { get; set; }
    public OrbNodeStyle? Style { get; set; }
}

public class OrbEdge(string id, string start, string end) : IOrbEdge
{
    public string Id { get; } = id;
    public string Start { get; } = start;
    public string End { get; } = end;
    public string? Label { get; set; }
    public OrbEdgeStyle? Style { get; set; }
}
```

Three usage tiers, no API change between them:

1. **Built-in types** — `List<OrbNode>`, nothing to define.
2. **Subclass** — derive from `OrbNode` to carry domain data with typed events.
3. **Implement the interface on your own type** — existing collections go straight in,
   nothing allocated to adapt them.

### 3.3 Event arguments

```csharp
public sealed class OrbNodeEventArgs<TNode>
{
    public required TNode Node { get; init; }        // the original instance
    public OrbPoint LocalPoint  { get; init; }       // graph coordinates
    public OrbPoint GlobalPoint { get; init; }       // canvas coordinates
}

public sealed class OrbEdgeEventArgs<TEdge>
{
    public required TEdge Edge { get; init; }
    public OrbPoint LocalPoint  { get; init; }
    public OrbPoint GlobalPoint { get; init; }
}

public sealed class OrbBackgroundEventArgs
{
    public OrbPoint LocalPoint  { get; init; }
    public OrbPoint GlobalPoint { get; init; }
}

public readonly record struct OrbPoint(double X, double Y);
```

### 3.4 Styles

Mirror `INodeStyle` / `IEdgeStyle` field for field, all nullable.

```csharp
public sealed class OrbNodeStyle           // 22 properties
{
    public string? Color { get; set; }
    public string? ColorHover { get; set; }
    public string? ColorSelected { get; set; }
    public string? BorderColor { get; set; }
    public string? BorderColorHover { get; set; }
    public string? BorderColorSelected { get; set; }
    public double? BorderWidth { get; set; }
    public double? BorderWidthSelected { get; set; }
    public string? FontBackgroundColor { get; set; }
    public string? FontColor { get; set; }
    public string? FontFamily { get; set; }
    public double? FontSize { get; set; }
    public string? ImageUrl { get; set; }
    public string? ImageUrlSelected { get; set; }
    public string? ShadowColor { get; set; }
    public double? ShadowSize { get; set; }
    public double? ShadowOffsetX { get; set; }
    public double? ShadowOffsetY { get; set; }
    public OrbNodeShape? Shape { get; set; }
    public double? Size { get; set; }
    public double? Mass { get; set; }
    public double? ZIndex { get; set; }
}

public sealed class OrbEdgeStyle           // 17 properties
{
    public string? Color { get; set; }
    public string? ColorHover { get; set; }
    public string? ColorSelected { get; set; }
    public double? Width { get; set; }
    public double? WidthHover { get; set; }
    public double? WidthSelected { get; set; }
    public double? ArrowSize { get; set; }
    public string? FontBackgroundColor { get; set; }
    public string? FontColor { get; set; }
    public string? FontFamily { get; set; }
    public double? FontSize { get; set; }
    public string? ShadowColor { get; set; }
    public double? ShadowSize { get; set; }
    public double? ShadowOffsetX { get; set; }
    public double? ShadowOffsetY { get; set; }
    public double? ZIndex { get; set; }
    public OrbEdgeLineStyle? LineStyle { get; set; }
}
```

**Deliberate deviations from Orb's shape:**

- `Label` is omitted from both style classes. Orb keeps it inside the style object, but
  exposing it in two places gives two competing ways to set one thing. `Label` lives on
  `IOrbNode`/`IOrbEdge`; the serializer folds it into the style payload.
- `EdgeType` (`straight`/`loopback`/`curved`) is not exposed. Orb computes it from graph
  topology; it is not a style input.

### 3.5 Settings

```csharp
public sealed class OrbSettings
{
    public OrbRenderSettings?      Render      { get; set; }
    public OrbInteractionSettings? Interaction { get; set; }
    public OrbSelectionSettings?   Selection   { get; set; }   // Orb's "strategy"
    public OrbLayout?              Layout      { get; set; }
    public int?  ZoomFitTransitionMs      { get; set; }
    public bool? IsOutOfBoundsDragEnabled { get; set; }
    public bool? AreCoordinatesRounded    { get; set; }
}

public sealed class OrbRenderSettings
{
    public OrbRendererType? Type { get; set; }
    public double? Fps { get; set; }
    public double? MinZoom { get; set; }
    public double? MaxZoom { get; set; }
    public double? FitZoomMargin { get; set; }
    public bool?   LabelsIsEnabled { get; set; }
    public bool?   LabelsOnEventIsEnabled { get; set; }
    public bool?   ShadowIsEnabled { get; set; }
    public bool?   ShadowOnEventIsEnabled { get; set; }
    public double? ContextAlphaOnEvent { get; set; }
    public bool?   ContextAlphaOnEventIsEnabled { get; set; }
    public string? BackgroundColor { get; set; }
    public double? DevicePixelRatio { get; set; }
    public bool?   AreCollapsedContainerDimensionsAllowed { get; set; }
}

public sealed class OrbInteractionSettings
{
    public bool? IsDragEnabled { get; set; }
    public bool? IsZoomEnabled { get; set; }
}

public sealed class OrbSelectionSettings
{
    public bool? IsDefaultSelectEnabled { get; set; }
    public bool? IsDefaultHoverEnabled { get; set; }
    public bool? IsDefaultMultiSelectEnabled { get; set; }
    public bool? IsDefaultSelectCascadeEnabled { get; set; }
}
```

Layout is a discriminated union in Orb (`options` keyed on `type`), expressed as a class
hierarchy so invalid combinations are unrepresentable:

```csharp
public abstract class OrbLayout            // serializes to { type, options }
{
    public OrbAnchor? AnchorX { get; set; }    // Orb's ILayoutOptionsBase
    public OrbAnchor? AnchorY { get; set; }
}

public sealed class OrbForceLayout : OrbLayout
{
    public bool? IsPhysicsEnabled { get; set; }
    public bool? IsSimulatingOnDataUpdate { get; set; }
    public bool? IsSimulatingOnSettingsUpdate { get; set; }
    public bool? IsSimulatingOnUnstick { get; set; }
    public bool? UseGpu { get; set; }
    public OrbForceLinks?     Links     { get; set; }
    public OrbForceManyBody?  ManyBody  { get; set; }
    public OrbForceCollision? Collision { get; set; }
    public OrbForceAlpha?     Alpha     { get; set; }
    public OrbForceCentering? Centering { get; set; }
}
```

The five force sub-objects mirror Orb's `IForceLayoutLinks`, `IForceLayoutManyBody`,
`IForceLayoutCollision`, `IForceLayoutAlpha`, and `IForceLayoutCentering` field for field,
all properties nullable — e.g. `OrbForceLinks { Distance, Strength, Iterations }`.

```csharp

public sealed class OrbGridLayout : OrbLayout
{
    public double? RowGap { get; set; }
    public double? ColGap { get; set; }
}

public sealed class OrbCircularLayout : OrbLayout
{
    public double? Radius { get; set; }
    public double? CenterX { get; set; }
    public double? CenterY { get; set; }
}

public sealed class OrbHierarchicalLayout : OrbLayout
{
    public double? NodeGap { get; set; }
    public double? LevelGap { get; set; }
    public double? TreeGap { get; set; }
    public OrbLayoutOrientation? Orientation { get; set; }
    public bool? Reversed { get; set; }
}
```

### 3.6 Enums

```csharp
public enum OrbRendererType      { Canvas, WebGl }
public enum OrbNodeShape         { Circle, Dot, Square, Diamond, Triangle,
                                   TriangleDown, Star, Hexagon }
public enum OrbLayoutOrientation { Horizontal, Vertical }
public enum OrbAnchor            { Start, Center, End }

public abstract class OrbEdgeLineStyle
{
    public static OrbEdgeLineStyle Solid  { get; }
    public static OrbEdgeLineStyle Dashed { get; }
    public static OrbEdgeLineStyle Dotted { get; }
    public static OrbEdgeLineStyle Custom(params double[] pattern);
}
```

Serialized as camelCase strings to match Orb (`TriangleDown` → `"triangleDown"`).

### 3.7 Usage

```razor
<OrbGraph Nodes="@people" Edges="@friendships"
          Height="600px"
          class="border rounded"
          Settings="@(new OrbSettings { Layout = new OrbForceLayout() })"
          OnNodeClick="@(e => Show(e.Node))" />

@code {
    public record Person(string EmployeeId, string FullName, decimal Salary) : IOrbNode
    {
        public string Id => EmployeeId;
        public string? Label => FullName;
    }

    private List<Person> people = [];
    private void Show(Person p) { /* typed; Salary never left .NET */ }
}
```

---

## 4. Architecture

```
Pinknose.Memgraph.Orb.Razor/
  OrbGraph.razor / .razor.css     component: lifecycle, host div, interop plumbing
  OrbEventRelay.cs                sealed, non-generic [JSInvokable] dispatch target
  Model/                          IOrbNode, IOrbEdge, OrbNode, OrbEdge,
                                  OrbNodeStyle, OrbEdgeStyle, OrbSettings tree, enums
  Events/                         OrbNodeEventArgs<T>, OrbEdgeEventArgs<T>,
                                  OrbBackgroundEventArgs, OrbPoint
  Internal/OrbProjector.cs        Nodes/Edges → payload DTOs + endpoint validation
  Internal/OrbGraphDiff.cs        id sets → removed node/edge ids
  Serialization/OrbJsonContext.cs source-generated JsonSerializerContext
  wwwroot/orbGraph.js             interop module
  wwwroot/vendor/memgraph/orb/1.0.2/   pinned bundle + .sha384
```

Projection, validation, diffing, and serialization live in plain classes so they are
testable without a renderer or a browser. The `.razor` file keeps only lifecycle and
interop.

Because D10 removes the script-location parameters, the vendored bundle path and its
SHA-384 integrity hash become private constants in the component, and the browser enforces
the hash natively via the `integrity` attribute on the injected `<script>` tag (verified:
a corrupted hash on that URL is blocked, not executed). The stylesheet loader is removed
entirely — Orb's canvas renderer needs no CSS, and the only consumer of it would have been
`OrbMapView`, which is out of scope.

### JS module surface

```js
initializeOrb(host, dotNetRef, settingsJson, dataJson, subscribedEvents) → handle
updateData(handle, dataJson, removedNodeIds, removedEdgeIds)
applySettings(handle, settingsJson)
recenter | zoomIn | zoomOut | fixNodes | releaseNodes | unselectAll (handle)
selectNode(handle, id) | selectEdge(handle, id) | getSvg(handle)
disposeOrb(handle)
```

---

## 5. Data flow

### 5.1 Initialization

`OnAfterRenderAsync(firstRender)` imports the module, creates a `DotNetObjectReference`
over `OrbEventRelay`, and passes settings plus initial data in a single call. JS constructs
the `OrbView`, calls `data.setup(...)`, pushes styles, subscribes the requested events, then
`render(() => recenter())`.

`subscribedEvents` is derived from `EventCallback.HasDelegate`, so JS installs listeners
only for callbacks the consumer actually wired up. A graph with no hover handler never
installs a `mouse-move` listener.

`[JSInvokable]` lives on the non-generic relay rather than the open generic component,
avoiding generic-interop sharp edges:

```csharp
internal sealed class OrbEventRelay
{
    private readonly Func<string, OrbEventPayload, Task> _dispatch;
    [JSInvokable] public Task HandleOrbEvent(string type, OrbEventPayload payload)
        => _dispatch(type, payload);
}

internal sealed class OrbEventPayload
{
    public string? Id { get; set; }              // node or edge id; null for background
    public double LocalX { get; set; }
    public double LocalY { get; set; }
    public double GlobalX { get; set; }
    public double GlobalY { get; set; }
}
```

`type` is one of `node-click`, `node-double-click`, `node-right-click`, `node-hover-enter`,
`node-hover-leave`, and the `edge-*` equivalents, plus `background-click`. The relay routes
on `type`, resolves `Id` against the tracked dictionary, and invokes the matching callback.

### 5.2 Updates

`OnParametersSetAsync`, after first render:

```
project Nodes/Edges → payload DTOs
validate edge Start/End against node id set    → log warnings, send anyway (§6)
removedNodeIds = _sentNodes.Keys - current node ids
removedEdgeIds = _sentEdges.Keys - current edge ids
if payload unchanged && nothing removed → return          ← no interop at all
updateData(handle, payloadJson, removedNodeIds, removedEdgeIds)
_sentNodes / _sentEdges = current
```

The early-out is load-bearing: `OnParametersSet` fires on *every* parent re-render, so
without it an unrelated state change elsewhere on the page would push the entire graph
across the wire.

A changed `Settings` object is handled on the same pass and gated the same way: if the
serialized settings differ from what was last sent, `applySettings` calls Orb's
`view.setSettings(...)`. Settings and data changes in one render produce at most two
interop calls.

`Dictionary<string, TNode>` does double duty — its key set is the diff set, and it resolves
event ids back to original instances.

JS order: `data.merge(payload)` → `data.remove({nodeIds, edgeIds})` → push styles →
`view.render()`.

### 5.3 Style push

`setDefaultStyle` is never used, because `_applyStyle()` skips nodes where `hasStyle()` is
true — a style that changed because a domain flag flipped would never repaint. Instead JS
walks the payload after merge:

```js
for (const n of payload.nodes) {
    if (!n.style) continue;
    graph.getNodeById(n.id)?.setStyle(n.style, { isNotifySkipped: true });
}
```

### 5.4 Events

Hover is synthesized in JS from `mouse-move`, whose payload carries `subject?` (node, edge,
or nothing). Only transitions are marshalled:

```js
view.events.on('mouse-move', (e) => {
    const id = e.subject ? String(e.subject.id) : null;
    if (id === hoverId) return;                    // the mousemove flood dies here
    if (hoverId) send('hover-leave', hoverId, e);
    if (id)      send('hover-enter', id, e);
    hoverId = id;
});
```

A user sweeping across a node produces exactly two messages. `Orb.isNode` / `Orb.isEdge`
classify the subject. Clicks pass straight through, one message per action.

On the C# side the relay resolves the id and invokes the callback; `EventCallback.InvokeAsync`
re-renders the consumer automatically.

### 5.5 Serialization

Internal DTOs only — `{ id, style }` and `{ id, start, end, style }`. One source-generated
`JsonSerializerContext` covering the DTOs and settings tree; camelCase, `WhenWritingNull`,
string enums. Trim-safe under WASM publish, and the reason domain fields cannot leak.

### 5.6 Host element attributes

`class` and `style` are extracted from `AdditionalAttributes` (case-insensitively) and merged
rather than splatted, since `@attributes` overwrites rather than merges. The consumer's
`style` is appended after the generated `width`/`height` so it wins. Everything else passes
through untouched.

---

## 6. Error handling

**Initialization failure must not kill the page.** A `JSException` during init is caught and
logged; the component leaves an empty host div. (Current behavior terminates the whole
Blazor circuit.)

**Dangling edges are logged, not thrown.** Throwing from `OnParametersSetAsync` tears down
the circuit — disproportionate for a data inconsistency. The wrapper warns via `ILogger`
naming the offending edge ids and sends them anyway.

This yields a property better than raw Orb: Orb drops an orphaned edge permanently and never
retries, but the wrapper resends the full node and edge set on every update, so once the
missing node appears, `merge` creates the previously-dropped edge. **The graph self-heals.**
Validation exists for visibility; correctness comes free from resending.

**Readiness gating.** A `TaskCompletionSource` gate makes imperative methods await
initialization rather than null-checking a handle, so `RecenterAsync()` called from a
consumer's `OnAfterRenderAsync` cannot race.

**Disposal** swallows `JSDisconnectedException`; post-dispose calls no-op rather than throw.

---

## 7. Testing

Test framework is **MSTest** throughout.

> bUnit 2.x renames its base type to `Bunit.BunitContext`, keeping `Bunit.TestContext` only
> as a compatibility shim. Component tests derive from `BunitContext`, which sidesteps the
> collision with MSTest's own `TestContext` property that affected bUnit 1.x.

**Unit (MSTest)** — over the plain classes: projection correctness, `Label` folding into the
style payload, dangling-edge detection, diff produces correct removals, settings serialize
sparsely with camelCase, enum mapping (`TriangleDown` → `"triangleDown"`), line-style union
serialization.

**Component (MSTest + bUnit)** — parameters produce the expected interop calls, the
unchanged-payload early-out actually fires, `HasDelegate` limits subscriptions, relay events
reach consumer callbacks carrying the original instance, disposal calls through in order.
bUnit's `JSInterop` mock asserts invocations without a browser.

**Browser (MSTest + Microsoft.Playwright.MSTest)** — against the sample host. This is the
layer that matters most: every bug found so far (wrong constructor, `from`/`to`, the
`hasStyle()` skip) compiled cleanly and would pass a mocked `IJSRuntime`.

Smoke suite:
1. Graph renders a non-blank canvas (painted-pixel count > 0)
2. Clicking a node fires `OnNodeClick` with the right instance
3. Hover enter/leave fire exactly once each per crossing
4. Updating `Nodes` preserves existing node positions
5. Removing from `Nodes` deletes the node and its edges
6. No server-side exception on navigate-away (disposal)

---

## 8. Out of scope for v1

- `OrbMapView` (geo/Leaflet layout)
- Programmatic hover (`hoverNodeById`)
- Custom node positioning via `getPosition`
- Viewport-dependent styling (would need the callback route, D7)
- NuGet packaging metadata, README, repo hygiene — tracked separately

---

## 9. Open risks

| Risk | Mitigation |
|---|---|
| **Settled (Task 1 spike):** `mouse-move` never reports edge subjects, and neither does Orb's built-in `edge-hover` event. Confirmed by an empirical sweep (0/151 edge hits swept precisely along the node-to-node line, 0 `edge-hover` firings) and by source inspection: the default interaction strategy's `onMouseMove` calls only `getNearestNode`, never `getNearestEdge` — no code path can ever produce an edge `changedSubject`. (`onMouseClick`, by contrast, does fall back to `getNearestEdge`.) | **Chosen approach:** the JS interop module synthesizes edge hover itself. On every `mouse-move`, call `view.data.getNearestNode(localPoint)`, and if null, `view.data.getNearestEdge(localPoint)` — the same public-facade lookup (`view.data` exposes the same methods as the private `_graph`) Orb's own click strategy already performs internally — then run the existing id-transition dedupe against whichever subject that resolves to. Public API and D14/§5.4 design are unaffected; only the JS-side subject resolution changes from trusting `e.subject` to a manual two-step lookup. |
| **Settled (Task 1 spike):** complex types as `[JSInvokable]` **parameters** bind cleanly. Verified against the running sample host: a `Probe { Id, LocalX }` parameter on `[JSInvokable] Accept(string type, Probe payload)`, invoked from JS with camelCase `{ id: 'n1', localX: 12.5 }`, produced `payload.Id == "n1"` and `payload.LocalX == 12.5` with no errors — Blazor's interop deserializer matches camelCase JS properties to PascalCase C# properties automatically. | **Chosen approach:** `OrbEventRelay.HandleOrbEvent(string type, OrbEventPayload payload)` proceeds as designed in §5.1, with a typed `OrbEventPayload` parameter. The JSON-string-plus-`OrbJsonContext` fallback is not needed. |
| Force-layout option tree is deep and only partially exercised by the sample | Model it, but treat unexercised branches as unverified until a test covers them |
