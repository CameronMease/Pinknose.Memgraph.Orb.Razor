# Orb Razor C# API Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the wrapper's raw-JSON parameters with a strongly-typed Blazor API over `@memgraph/orb` 1.0.2, covering typed nodes/edges, styles, settings, events, and imperative view control.

**Architecture:** `OrbGraph<TNode, TEdge>` constrained to `IOrbNode`/`IOrbEdge`. Projection, validation, diffing, and serialization live in plain testable classes; the `.razor` file holds only lifecycle and interop. Consumer objects never cross to JS — only internal DTOs do. Updates go through Orb's `merge` + `remove` so node positions survive.

**Tech Stack:** .NET 10, Blazor (Server + WASM), System.Text.Json source generation, MSTest 4.3.3, bUnit 2.9.0, Microsoft.Playwright.MSTest 1.62.0.

**Spec:** `docs/superpowers/specs/2026-08-18-orb-razor-api-design.md`

## Global Constraints

- Target framework `net10.0`; `Nullable` and `ImplicitUsings` enabled on every project.
- Ids are `string` throughout. No `object` or generic id types.
- Consumer types (`TNode`/`TEdge`) are **never** serialized. Only internal DTOs reach JSON.
- All style and settings properties are nullable and serialized with `JsonIgnoreCondition.WhenWritingNull`, so Orb's own defaults stand.
- JSON is camelCase; enums serialize as camelCase strings (`TriangleDown` → `"triangleDown"`).
- No `SettingsJson`, `DataJson`, `ScriptUrl`, `ScriptIntegrity`, `StyleUrl`, or `Class` parameters. These were deliberately removed.
- The vendored bundle path and its SHA-384 hash are private constants: `./_content/Pinknose.Memgraph.Orb.Razor/vendor/memgraph/orb/1.0.2/orb.min.js` and `sha384-/bdC+Sgoda/KpkiTPljaZXPEpNJg712oGud22zh7zsoVZch3PRsvcjfqNLRCaIoT`.
- Library project must build with **0 warnings**.
- Component tests derive from `Bunit.BunitContext` (not `TestContext` — that collides with MSTest).
- Never call `setDefaultStyle`; styles are pushed per node (Orb's `_applyStyle` skips nodes where `hasStyle()` is true).
- Node payloads must never include `x`/`y` keys — their absence is what makes `merge` preserve positions.

---

## File Structure

**Library** — `Pinknose.Memgraph.Orb.Razor/`

| Path | Responsibility |
|---|---|
| `Model/IOrbNode.cs`, `Model/IOrbEdge.cs` | Consumer contracts, default interface members |
| `Model/OrbNode.cs`, `Model/OrbEdge.cs` | Ready-made implementations |
| `Model/OrbNodeStyle.cs`, `Model/OrbEdgeStyle.cs` | Style property bags |
| `Model/OrbEdgeLineStyle.cs` | Line-style discriminated union |
| `Model/Enums.cs` | `OrbNodeShape`, `OrbRendererType`, `OrbLayoutOrientation`, `OrbAnchor` |
| `Model/OrbSettings.cs` | Settings root + render/interaction/selection |
| `Model/OrbLayout.cs` | Layout hierarchy + force sub-objects |
| `Events/OrbEventArgs.cs` | `OrbNodeEventArgs<T>`, `OrbEdgeEventArgs<T>`, `OrbBackgroundEventArgs`, `OrbPoint` |
| `Internal/OrbPayload.cs` | Internal DTOs |
| `Internal/OrbProjector.cs` | Nodes/Edges → DTOs + endpoint validation |
| `Internal/OrbGraphDiff.cs` | id sets → removals |
| `Internal/OrbEventPayload.cs`, `Internal/OrbEventRelay.cs` | `[JSInvokable]` dispatch |
| `Serialization/OrbJsonContext.cs` | Source-generated context + converters |
| `OrbGraph.razor` / `.razor.css` | Component: lifecycle, host div, interop |
| `wwwroot/orbGraph.js` | Interop module |

**Tests**

| Path | Responsibility |
|---|---|
| `tests/Pinknose.Memgraph.Orb.Razor.Tests/` | MSTest + bUnit unit and component tests |
| `tests/Pinknose.Memgraph.Orb.Razor.BrowserTests/` | MSTest + Playwright smoke suite |

---

## Task 1: De-risk the two interop assumptions

The spec's §9 lists two unverified assumptions that would force design changes. Both are cheap to test against the running sample and must be settled before code depends on them.

**Files:**
- Modify: `docs/superpowers/specs/2026-08-18-orb-razor-api-design.md` (record findings in §9)

**Interfaces:**
- Consumes: nothing
- Produces: a recorded decision on (a) whether hover uses `mouse-move` or `node-hover`/`edge-hover`, and (b) whether `[JSInvokable]` takes a complex type or a JSON string.

- [ ] **Step 1: Start the sample host**

```bash
dotnet run --project samples/Pinknose.Memgraph.Orb.Razor.SampleHost/Pinknose.Memgraph.Orb.Razor.SampleHost.csproj --launch-profile http
```

Browse to `http://localhost:5053/orb-demo`.

- [ ] **Step 2: Probe whether `mouse-move` reports edge subjects**

In the browser console:

```js
const view = new Orb.OrbView(document.querySelector('.orb-graph'));
view.data.setup({
  nodes: [{ id: 'n1' }, { id: 'n2' }],
  edges: [{ id: 'e1', start: 'n1', end: 'n2' }]
});
view.render();
window.__seen = { node: 0, edge: 0, none: 0 };
view.events.on('mouse-move', (e) => {
  if (!e.subject) window.__seen.none++;
  else if (Orb.isNode(e.subject)) window.__seen.node++;
  else if (Orb.isEdge(e.subject)) window.__seen.edge++;
});
```

Sweep the mouse across both a node and the line between the nodes, then read `window.__seen`.

**Decision:** if `edge` stays 0 while `node` increments, `mouse-move` does not surface edge subjects. Record that, and switch the hover design to subscribe `node-hover` and `edge-hover` directly, keeping the same id-transition dedupe (identical public API, different JS source events).

- [ ] **Step 3: Probe complex-type `[JSInvokable]` parameters**

Add to `samples/.../Components/Pages/OrbDemo.razor`, temporarily:

```razor
@implements IDisposable
@inject IJSRuntime Js

@code {
    private DotNetObjectReference<OrbDemo>? _ref;
    public sealed class Probe { public string? Id { get; set; } public double LocalX { get; set; } }

    [JSInvokable]
    public Task Accept(string type, Probe payload)
    {
        Console.WriteLine($"probe ok: {type} {payload.Id} {payload.LocalX}");
        return Task.CompletedTask;
    }

    protected override async Task OnAfterRenderAsync(bool first)
    {
        if (!first) return;
        _ref = DotNetObjectReference.Create(this);
        await Js.InvokeVoidAsync("eval",
            "window.__probe = (r) => r.invokeMethodAsync('Accept', 'node-click', { id: 'n1', localX: 12.5 })");
        await Js.InvokeVoidAsync("__probe", _ref);
    }

    public void Dispose() => _ref?.Dispose();
}
```

Expected: `probe ok: node-click n1 12.5` in the server console.

**Decision:** if this fails to bind, change `OrbEventRelay.HandleOrbEvent` to `(string type, string payloadJson)` and deserialize with `OrbJsonContext` in Task 7. The public API is unaffected either way.

- [ ] **Step 4: Revert the probe**

```bash
git checkout samples/Pinknose.Memgraph.Orb.Razor.SampleHost/Components/Pages/OrbDemo.razor
```

- [ ] **Step 5: Record findings in the spec**

Replace the first two rows of the spec's §9 risk table with what was observed, stating the chosen approach as settled.

- [ ] **Step 6: Commit**

```bash
git add docs/superpowers/specs/2026-08-18-orb-razor-api-design.md
git commit -m "docs: settle hover source and JSInvokable payload shape"
```

---

## Task 2: Node and edge model + test project

**Files:**
- Create: `Pinknose.Memgraph.Orb.Razor/Model/IOrbNode.cs`, `Model/IOrbEdge.cs`, `Model/OrbNode.cs`, `Model/OrbEdge.cs`
- Create: `tests/Pinknose.Memgraph.Orb.Razor.Tests/Pinknose.Memgraph.Orb.Razor.Tests.csproj`
- Create: `tests/Pinknose.Memgraph.Orb.Razor.Tests/Model/OrbNodeTests.cs`
- Modify: `Pinknose.Memgraph.Orb.Razor.slnx`

**Interfaces:**
- Consumes: nothing
- Produces: `IOrbNode { string Id; string? Label; OrbNodeStyle? Style }`, `IOrbEdge { string Id; string Start; string End; string? Label; OrbEdgeStyle? Style }`, and concrete `OrbNode(string id)` / `OrbEdge(string id, string start, string end)`. `OrbNodeStyle`/`OrbEdgeStyle` arrive in Task 3 — declare them as empty sealed classes here and fill them in Task 3.

- [ ] **Step 1: Create the test project**

```bash
dotnet new mstest -f net10.0 -o tests/Pinknose.Memgraph.Orb.Razor.Tests
rm tests/Pinknose.Memgraph.Orb.Razor.Tests/UnitTest1.cs
dotnet add tests/Pinknose.Memgraph.Orb.Razor.Tests package MSTest --version 4.3.3
dotnet add tests/Pinknose.Memgraph.Orb.Razor.Tests reference Pinknose.Memgraph.Orb.Razor/Pinknose.Memgraph.Orb.Razor.csproj
dotnet sln Pinknose.Memgraph.Orb.Razor.slnx add tests/Pinknose.Memgraph.Orb.Razor.Tests
```

- [ ] **Step 2: Write the failing test**

`tests/Pinknose.Memgraph.Orb.Razor.Tests/Model/OrbNodeTests.cs`:

```csharp
namespace Pinknose.Memgraph.Orb.Razor.Tests.Model;

[TestClass]
public class OrbNodeTests
{
    private sealed record Person(string EmployeeId, string FullName) : IOrbNode
    {
        public string Id => EmployeeId;
        public string? Label => FullName;
    }

    [TestMethod]
    public void OrbNode_ExposesIdAndOptionalMembers()
    {
        var node = new OrbNode("n1") { Label = "Alice" };

        Assert.AreEqual("n1", node.Id);
        Assert.AreEqual("Alice", node.Label);
        Assert.IsNull(node.Style);
    }

    [TestMethod]
    public void OrbEdge_ExposesEndpoints()
    {
        var edge = new OrbEdge("e1", "n1", "n2") { Label = "KNOWS" };

        Assert.AreEqual("e1", edge.Id);
        Assert.AreEqual("n1", edge.Start);
        Assert.AreEqual("n2", edge.End);
        Assert.AreEqual("KNOWS", edge.Label);
    }

    [TestMethod]
    public void CustomType_ImplementingIOrbNode_NeedsOnlyId()
    {
        IOrbNode person = new Person("E-1", "Ada");

        Assert.AreEqual("E-1", person.Id);
        Assert.AreEqual("Ada", person.Label);
        Assert.IsNull(person.Style);          // default interface member
    }
}
```

- [ ] **Step 3: Run the test to verify it fails**

Run: `dotnet test tests/Pinknose.Memgraph.Orb.Razor.Tests --filter "FullyQualifiedName~OrbNodeTests"`
Expected: FAIL — `IOrbNode` / `OrbNode` do not exist.

- [ ] **Step 4: Write the implementation**

`Model/IOrbNode.cs`:

```csharp
namespace Pinknose.Memgraph.Orb.Razor;

/// <summary>A graph node. Implement <see cref="Id"/>; the rest are optional.</summary>
public interface IOrbNode
{
    string Id { get; }
    string? Label => null;
    OrbNodeStyle? Style => null;
}
```

`Model/IOrbEdge.cs`:

```csharp
namespace Pinknose.Memgraph.Orb.Razor;

/// <summary>A graph edge. <see cref="Start"/> and <see cref="End"/> are node ids.</summary>
public interface IOrbEdge
{
    string Id { get; }
    string Start { get; }
    string End { get; }
    string? Label => null;
    OrbEdgeStyle? Style => null;
}
```

`Model/OrbNode.cs`:

```csharp
namespace Pinknose.Memgraph.Orb.Razor;

/// <summary>A ready-made <see cref="IOrbNode"/>. Derive from it to carry domain data.</summary>
public class OrbNode(string id) : IOrbNode
{
    public string Id { get; } = id;
    public string? Label { get; set; }
    public OrbNodeStyle? Style { get; set; }
}
```

`Model/OrbEdge.cs`:

```csharp
namespace Pinknose.Memgraph.Orb.Razor;

/// <summary>A ready-made <see cref="IOrbEdge"/>. Derive from it to carry domain data.</summary>
public class OrbEdge(string id, string start, string end) : IOrbEdge
{
    public string Id { get; } = id;
    public string Start { get; } = start;
    public string End { get; } = end;
    public string? Label { get; set; }
    public OrbEdgeStyle? Style { get; set; }
}
```

Also create placeholder style classes so this compiles — Task 3 fills them:

`Model/OrbNodeStyle.cs`: `namespace Pinknose.Memgraph.Orb.Razor; public sealed class OrbNodeStyle { }`
`Model/OrbEdgeStyle.cs`: `namespace Pinknose.Memgraph.Orb.Razor; public sealed class OrbEdgeStyle { }`

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test tests/Pinknose.Memgraph.Orb.Razor.Tests --filter "FullyQualifiedName~OrbNodeTests"`
Expected: PASS, 3 tests.

- [ ] **Step 6: Commit**

```bash
git add Pinknose.Memgraph.Orb.Razor/Model tests/ Pinknose.Memgraph.Orb.Razor.slnx
git commit -m "feat: add IOrbNode/IOrbEdge contracts and default implementations"
```

---

## Task 3: Style model

**Files:**
- Modify: `Pinknose.Memgraph.Orb.Razor/Model/OrbNodeStyle.cs`, `Model/OrbEdgeStyle.cs`
- Create: `Pinknose.Memgraph.Orb.Razor/Model/OrbEdgeLineStyle.cs`, `Model/Enums.cs`
- Create: `tests/Pinknose.Memgraph.Orb.Razor.Tests/Model/OrbStyleTests.cs`

**Interfaces:**
- Consumes: nothing
- Produces: `OrbNodeStyle` (22 nullable properties), `OrbEdgeStyle` (17), `OrbEdgeLineStyle` with `Solid`/`Dashed`/`Dotted`/`Custom(double[])`, and enums `OrbNodeShape`, `OrbRendererType`, `OrbLayoutOrientation`, `OrbAnchor`. Note: neither style class has a `Label` property — label lives on `IOrbNode`/`IOrbEdge` and is folded into the style payload during projection (Task 6).

- [ ] **Step 1: Write the failing test**

```csharp
namespace Pinknose.Memgraph.Orb.Razor.Tests.Model;

[TestClass]
public class OrbStyleTests
{
    [TestMethod]
    public void NodeStyle_DefaultsToAllNull()
    {
        var style = new OrbNodeStyle();

        Assert.IsNull(style.Color);
        Assert.IsNull(style.Size);
        Assert.IsNull(style.Shape);
    }

    [TestMethod]
    public void NodeStyle_HasNoLabelProperty()
    {
        // Label lives on IOrbNode, not the style bag — two ways to set one thing is a bug.
        Assert.IsNull(typeof(OrbNodeStyle).GetProperty("Label"));
        Assert.IsNull(typeof(OrbEdgeStyle).GetProperty("Label"));
    }

    [TestMethod]
    public void LineStyle_SharedInstancesCarryTheirKind()
    {
        Assert.AreEqual("solid", OrbEdgeLineStyle.Solid.Kind);
        Assert.AreEqual("dashed", OrbEdgeLineStyle.Dashed.Kind);
        Assert.AreEqual("dotted", OrbEdgeLineStyle.Dotted.Kind);
    }

    [TestMethod]
    public void LineStyle_CustomCarriesPattern()
    {
        var custom = OrbEdgeLineStyle.Custom(4, 2, 1);

        Assert.AreEqual("custom", custom.Kind);
        CollectionAssert.AreEqual(new double[] { 4, 2, 1 }, custom.Pattern);
    }

    [TestMethod]
    public void LineStyle_CustomRejectsEmptyPattern()
    {
        Assert.ThrowsExactly<ArgumentException>(() => OrbEdgeLineStyle.Custom());
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test tests/Pinknose.Memgraph.Orb.Razor.Tests --filter "FullyQualifiedName~OrbStyleTests"`
Expected: FAIL — `OrbEdgeLineStyle` does not exist, style properties missing.

- [ ] **Step 3: Write the enums**

`Model/Enums.cs`:

```csharp
namespace Pinknose.Memgraph.Orb.Razor;

public enum OrbNodeShape { Circle, Dot, Square, Diamond, Triangle, TriangleDown, Star, Hexagon }

public enum OrbRendererType { Canvas, WebGl }

public enum OrbLayoutOrientation { Horizontal, Vertical }

public enum OrbAnchor { Start, Center, End }
```

- [ ] **Step 4: Write the line-style union**

`Model/OrbEdgeLineStyle.cs`:

```csharp
namespace Pinknose.Memgraph.Orb.Razor;

/// <summary>Mirrors Orb's <c>IEdgeLineStyle</c> discriminated union.</summary>
public sealed class OrbEdgeLineStyle
{
    private OrbEdgeLineStyle(string kind, double[]? pattern = null)
    {
        Kind = kind;
        Pattern = pattern;
    }

    public string Kind { get; }
    public double[]? Pattern { get; }

    public static OrbEdgeLineStyle Solid { get; } = new("solid");
    public static OrbEdgeLineStyle Dashed { get; } = new("dashed");
    public static OrbEdgeLineStyle Dotted { get; } = new("dotted");

    public static OrbEdgeLineStyle Custom(params double[] pattern)
    {
        if (pattern is null || pattern.Length == 0)
        {
            throw new ArgumentException("A custom line style needs at least one dash length.", nameof(pattern));
        }

        return new OrbEdgeLineStyle("custom", pattern);
    }
}
```

- [ ] **Step 5: Write the style classes**

`Model/OrbNodeStyle.cs` — 22 nullable properties, no `Label`:

```csharp
namespace Pinknose.Memgraph.Orb.Razor;

/// <summary>Mirrors Orb's <c>INodeStyle</c>. Null properties fall back to Orb's defaults.</summary>
public sealed class OrbNodeStyle
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
```

`Model/OrbEdgeStyle.cs` — 17 nullable properties, no `Label`:

```csharp
namespace Pinknose.Memgraph.Orb.Razor;

/// <summary>Mirrors Orb's <c>IEdgeStyle</c>. Null properties fall back to Orb's defaults.</summary>
public sealed class OrbEdgeStyle
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

- [ ] **Step 6: Run the tests to verify they pass**

Run: `dotnet test tests/Pinknose.Memgraph.Orb.Razor.Tests --filter "FullyQualifiedName~OrbStyleTests"`
Expected: PASS, 5 tests.

- [ ] **Step 7: Commit**

```bash
git add Pinknose.Memgraph.Orb.Razor/Model tests/
git commit -m "feat: add node and edge style models"
```

---

## Task 4: Settings and layout model

**Files:**
- Create: `Pinknose.Memgraph.Orb.Razor/Model/OrbSettings.cs`, `Model/OrbLayout.cs`
- Create: `tests/Pinknose.Memgraph.Orb.Razor.Tests/Model/OrbSettingsTests.cs`

**Interfaces:**
- Consumes: `OrbRendererType`, `OrbLayoutOrientation`, `OrbAnchor` (Task 3)
- Produces: `OrbSettings` with `Render`/`Interaction`/`Selection`/`Layout`/`ZoomFitTransitionMs`/`IsOutOfBoundsDragEnabled`/`AreCoordinatesRounded`; `OrbLayout` abstract base exposing `LayoutType` (used by the serializer in Task 5) plus `AnchorX`/`AnchorY`; subclasses `OrbForceLayout`, `OrbGridLayout`, `OrbCircularLayout`, `OrbHierarchicalLayout`; force sub-objects `OrbForceLinks`, `OrbForceManyBody`, `OrbForceCollision`, `OrbForceAlpha`, `OrbForceCentering`.

- [ ] **Step 1: Write the failing test**

```csharp
namespace Pinknose.Memgraph.Orb.Razor.Tests.Model;

[TestClass]
public class OrbSettingsTests
{
    [TestMethod]
    public void Settings_DefaultToAllNull()
    {
        var settings = new OrbSettings();

        Assert.IsNull(settings.Render);
        Assert.IsNull(settings.Layout);
        Assert.IsNull(settings.ZoomFitTransitionMs);
    }

    [TestMethod]
    public void Layouts_ReportTheirOrbTypeDiscriminator()
    {
        Assert.AreEqual("force", new OrbForceLayout().LayoutType);
        Assert.AreEqual("grid", new OrbGridLayout().LayoutType);
        Assert.AreEqual("circular", new OrbCircularLayout().LayoutType);
        Assert.AreEqual("hierarchical", new OrbHierarchicalLayout().LayoutType);
    }

    [TestMethod]
    public void Layout_CarriesAnchorsFromTheBase()
    {
        var layout = new OrbGridLayout { RowGap = 40, AnchorX = OrbAnchor.Center };

        Assert.AreEqual(40d, layout.RowGap);
        Assert.AreEqual(OrbAnchor.Center, layout.AnchorX);
        Assert.IsNull(layout.AnchorY);
    }

    [TestMethod]
    public void ForceLayout_ExposesNestedOptionObjects()
    {
        var layout = new OrbForceLayout
        {
            IsPhysicsEnabled = true,
            Links = new OrbForceLinks { Distance = 120 },
            ManyBody = new OrbForceManyBody { Strength = -50 }
        };

        Assert.IsTrue(layout.IsPhysicsEnabled);
        Assert.AreEqual(120d, layout.Links!.Distance);
        Assert.AreEqual(-50d, layout.ManyBody!.Strength);
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test tests/Pinknose.Memgraph.Orb.Razor.Tests --filter "FullyQualifiedName~OrbSettingsTests"`
Expected: FAIL — `OrbSettings` does not exist.

- [ ] **Step 3: Write the settings root**

`Model/OrbSettings.cs`:

```csharp
namespace Pinknose.Memgraph.Orb.Razor;

/// <summary>Mirrors Orb's <c>IOrbViewSettingsInit</c>. Null members fall back to Orb's defaults.</summary>
public sealed class OrbSettings
{
    public OrbRenderSettings? Render { get; set; }
    public OrbInteractionSettings? Interaction { get; set; }
    public OrbSelectionSettings? Selection { get; set; }
    public OrbLayout? Layout { get; set; }
    public int? ZoomFitTransitionMs { get; set; }
    public bool? IsOutOfBoundsDragEnabled { get; set; }
    public bool? AreCoordinatesRounded { get; set; }
}

public sealed class OrbRenderSettings
{
    public OrbRendererType? Type { get; set; }
    public double? Fps { get; set; }
    public double? MinZoom { get; set; }
    public double? MaxZoom { get; set; }
    public double? FitZoomMargin { get; set; }
    public bool? LabelsIsEnabled { get; set; }
    public bool? LabelsOnEventIsEnabled { get; set; }
    public bool? ShadowIsEnabled { get; set; }
    public bool? ShadowOnEventIsEnabled { get; set; }
    public double? ContextAlphaOnEvent { get; set; }
    public bool? ContextAlphaOnEventIsEnabled { get; set; }
    public string? BackgroundColor { get; set; }
    public double? DevicePixelRatio { get; set; }
    public bool? AreCollapsedContainerDimensionsAllowed { get; set; }
}

public sealed class OrbInteractionSettings
{
    public bool? IsDragEnabled { get; set; }
    public bool? IsZoomEnabled { get; set; }
}

/// <summary>Orb calls this "strategy"; it controls built-in select and hover behaviour.</summary>
public sealed class OrbSelectionSettings
{
    public bool? IsDefaultSelectEnabled { get; set; }
    public bool? IsDefaultHoverEnabled { get; set; }
    public bool? IsDefaultMultiSelectEnabled { get; set; }
    public bool? IsDefaultSelectCascadeEnabled { get; set; }
}
```

- [ ] **Step 4: Write the layout hierarchy**

`Model/OrbLayout.cs`:

```csharp
namespace Pinknose.Memgraph.Orb.Razor;

/// <summary>Base for Orb's layout union. Serializes to <c>{ type, options }</c>.</summary>
public abstract class OrbLayout
{
    /// <summary>The discriminator Orb expects in <c>layout.type</c>.</summary>
    public abstract string LayoutType { get; }

    public OrbAnchor? AnchorX { get; set; }
    public OrbAnchor? AnchorY { get; set; }
}

public sealed class OrbForceLayout : OrbLayout
{
    public override string LayoutType => "force";

    public bool? IsPhysicsEnabled { get; set; }
    public bool? IsSimulatingOnDataUpdate { get; set; }
    public bool? IsSimulatingOnSettingsUpdate { get; set; }
    public bool? IsSimulatingOnUnstick { get; set; }
    public bool? UseGpu { get; set; }
    public OrbForceLinks? Links { get; set; }
    public OrbForceManyBody? ManyBody { get; set; }
    public OrbForceCollision? Collision { get; set; }
    public OrbForceAlpha? Alpha { get; set; }
    public OrbForceCentering? Centering { get; set; }
}

public sealed class OrbGridLayout : OrbLayout
{
    public override string LayoutType => "grid";

    public double? RowGap { get; set; }
    public double? ColGap { get; set; }
}

public sealed class OrbCircularLayout : OrbLayout
{
    public override string LayoutType => "circular";

    public double? Radius { get; set; }
    public double? CenterX { get; set; }
    public double? CenterY { get; set; }
}

public sealed class OrbHierarchicalLayout : OrbLayout
{
    public override string LayoutType => "hierarchical";

    public double? NodeGap { get; set; }
    public double? LevelGap { get; set; }
    public double? TreeGap { get; set; }
    public OrbLayoutOrientation? Orientation { get; set; }
    public bool? Reversed { get; set; }
}

public sealed class OrbForceLinks
{
    public double? Distance { get; set; }
    public double? Strength { get; set; }
    public double? Iterations { get; set; }
}

public sealed class OrbForceManyBody
{
    public double? Strength { get; set; }
    public double? Theta { get; set; }
    public double? DistanceMin { get; set; }
    public double? DistanceMax { get; set; }
}

public sealed class OrbForceCollision
{
    public double? Radius { get; set; }
    public double? Strength { get; set; }
    public double? Iterations { get; set; }
}

public sealed class OrbForceAlpha
{
    public double? Alpha { get; set; }
    public double? AlphaMin { get; set; }
    public double? AlphaDecay { get; set; }
    public double? AlphaTarget { get; set; }
}

public sealed class OrbForceCentering
{
    public double? X { get; set; }
    public double? Y { get; set; }
    public double? Strength { get; set; }
}
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test tests/Pinknose.Memgraph.Orb.Razor.Tests --filter "FullyQualifiedName~OrbSettingsTests"`
Expected: PASS, 4 tests.

- [ ] **Step 6: Commit**

```bash
git add Pinknose.Memgraph.Orb.Razor/Model tests/
git commit -m "feat: add typed settings and layout model"
```

---

## Task 5: Payload DTOs and serialization

**Files:**
- Create: `Pinknose.Memgraph.Orb.Razor/Internal/OrbPayload.cs`
- Create: `Pinknose.Memgraph.Orb.Razor/Serialization/OrbJsonContext.cs`, `Serialization/OrbLayoutConverter.cs`, `Serialization/OrbEdgeLineStyleConverter.cs`
- Create: `tests/Pinknose.Memgraph.Orb.Razor.Tests/Serialization/OrbSerializationTests.cs`

**Interfaces:**
- Consumes: `OrbSettings`, `OrbLayout` (Task 4), `OrbNodeStyle`, `OrbEdgeStyle`, `OrbEdgeLineStyle` (Task 3)
- Produces: `OrbNodePayload { string Id; OrbNodeStylePayload? Style }`, `OrbEdgePayload { string Id; string Start; string End; OrbEdgeStylePayload? Style }`, `OrbGraphPayload { List<OrbNodePayload> Nodes; List<OrbEdgePayload> Edges }`, the two style payload DTOs (public `Label` plus every public style property), and `OrbJson.SerializeGraph`/`SerializeSettings` used by Tasks 6, 9 and 10.

> **Why separate style payload types?** Orb reads labels from `style.label`, but the public
> `OrbNodeStyle` deliberately has no `Label` (spec §3.4). Adding an `internal` one would not
> work — System.Text.Json ignores non-public properties, so every label would silently
> vanish. The payload DTO carries `Label` publicly and keeps the wire format out of the
> public styling surface, which is what the spec's "only internal DTOs cross" rule requires.

- [ ] **Step 1: Write the failing test**

```csharp
using System.Text.Json;

namespace Pinknose.Memgraph.Orb.Razor.Tests.Serialization;

[TestClass]
public class OrbSerializationTests
{
    [TestMethod]
    public void Payload_UsesCamelCaseAndOmitsNulls()
    {
        var payload = new OrbGraphPayload
        {
            Nodes = [new OrbNodePayload { Id = "n1", Style = new OrbNodeStylePayload { Color = "#c33" } }],
            Edges = []
        };

        var json = OrbJson.SerializeGraph(payload);

        Assert.AreEqual("""{"nodes":[{"id":"n1","style":{"color":"#c33"}}],"edges":[]}""", json);
    }

    [TestMethod]
    public void StylePayload_EmitsLabelWhereOrbExpectsIt()
    {
        var json = OrbJson.SerializeGraph(new OrbGraphPayload
        {
            Nodes = [new OrbNodePayload { Id = "n1", Style = new OrbNodeStylePayload { Label = "Alice" } }],
            Edges = []
        });

        StringAssert.Contains(json, "\"style\":{\"label\":\"Alice\"}");
    }

    [TestMethod]
    public void NodePayload_NeverEmitsPositionKeys()
    {
        // Absence of x/y is what makes Orb's merge() preserve existing positions.
        var json = OrbJson.SerializeGraph(new OrbGraphPayload
        {
            Nodes = [new OrbNodePayload { Id = "n1" }],
            Edges = []
        });

        Assert.IsFalse(json.Contains("\"x\""), json);
        Assert.IsFalse(json.Contains("\"y\""), json);
    }

    [TestMethod]
    public void Enums_SerializeAsOrbCamelCaseStrings()
    {
        var json = OrbJson.SerializeGraph(new OrbGraphPayload
        {
            Nodes = [new OrbNodePayload { Id = "n1", Style = new OrbNodeStylePayload { Shape = OrbNodeShape.TriangleDown } }],
            Edges = []
        });

        StringAssert.Contains(json, "\"shape\":\"triangleDown\"");
    }

    [TestMethod]
    public void Settings_SerializeSparsely()
    {
        var json = OrbJson.SerializeSettings(new OrbSettings
        {
            Interaction = new OrbInteractionSettings { IsZoomEnabled = false }
        });

        Assert.AreEqual("""{"interaction":{"isZoomEnabled":false}}""", json);
    }

    [TestMethod]
    public void Layout_SerializesAsTypeAndOptions()
    {
        var json = OrbJson.SerializeSettings(new OrbSettings
        {
            Layout = new OrbGridLayout { RowGap = 40 }
        });

        Assert.AreEqual("""{"layout":{"type":"grid","options":{"rowGap":40}}}""", json);
    }

    [TestMethod]
    public void LineStyle_SerializesSharedKindWithoutPattern()
    {
        var json = OrbJson.SerializeGraph(new OrbGraphPayload
        {
            Nodes = [],
            Edges = [new OrbEdgePayload
            {
                Id = "e1", Start = "n1", End = "n2",
                Style = new OrbEdgeStylePayload { LineStyle = OrbEdgeLineStyle.Dashed }
            }]
        });

        StringAssert.Contains(json, "\"lineStyle\":{\"type\":\"dashed\"}");
    }

    [TestMethod]
    public void LineStyle_SerializesCustomPattern()
    {
        var json = OrbJson.SerializeGraph(new OrbGraphPayload
        {
            Nodes = [],
            Edges = [new OrbEdgePayload
            {
                Id = "e1", Start = "n1", End = "n2",
                Style = new OrbEdgeStylePayload { LineStyle = OrbEdgeLineStyle.Custom(4, 2) }
            }]
        });

        StringAssert.Contains(json, "\"lineStyle\":{\"type\":\"custom\",\"pattern\":[4,2]}");
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test tests/Pinknose.Memgraph.Orb.Razor.Tests --filter "FullyQualifiedName~OrbSerializationTests"`
Expected: FAIL — `OrbGraphPayload` and `OrbJson` do not exist.

- [ ] **Step 3: Write the payload DTOs**

`Internal/OrbPayload.cs`:

```csharp
namespace Pinknose.Memgraph.Orb.Razor;

/// <summary>What actually crosses to JavaScript. Consumer types never do.</summary>
public sealed class OrbGraphPayload
{
    public List<OrbNodePayload> Nodes { get; set; } = [];
    public List<OrbEdgePayload> Edges { get; set; } = [];
}

public sealed class OrbNodePayload
{
    public required string Id { get; set; }
    public OrbNodeStylePayload? Style { get; set; }
}

public sealed class OrbEdgePayload
{
    public required string Id { get; set; }
    public required string Start { get; set; }
    public required string End { get; set; }
    public OrbEdgeStylePayload? Style { get; set; }
}

/// <summary>
/// Wire shape for node styling. Mirrors <see cref="OrbNodeStyle"/> and adds <c>Label</c>,
/// which Orb reads from the style object but which the public styling surface omits.
/// </summary>
public sealed class OrbNodeStylePayload
{
    public string? Label { get; set; }
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

/// <summary>Wire shape for edge styling. Mirrors <see cref="OrbEdgeStyle"/> plus <c>Label</c>.</summary>
public sealed class OrbEdgeStylePayload
{
    public string? Label { get; set; }
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

> Property order matters: `Label` is declared first so the asserted JSON strings in the
> tests match. If you reorder, update the expected strings.

Also add to `Pinknose.Memgraph.Orb.Razor.csproj` so the tests can reach `OrbJson`,
`OrbProjector`, and `OrbGraphDiff`:

```xml
  <ItemGroup>
    <InternalsVisibleTo Include="Pinknose.Memgraph.Orb.Razor.Tests" />
  </ItemGroup>
```

- [ ] **Step 4: Write the converters**

`Serialization/OrbLayoutConverter.cs`:

```csharp
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Pinknose.Memgraph.Orb.Razor;

/// <summary>Writes Orb's <c>{ type, options }</c> layout shape; anchors live inside options.</summary>
internal sealed class OrbLayoutConverter : JsonConverter<OrbLayout>
{
    public override OrbLayout? Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options)
        => throw new NotSupportedException("Layout settings are write-only.");

    public override void Write(Utf8JsonWriter writer, OrbLayout value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteString("type", value.LayoutType);

        writer.WritePropertyName("options");
        JsonSerializer.Serialize(writer, value, value.GetType(), OrbJson.OptionsWithoutLayoutConverter);

        writer.WriteEndObject();
    }
}
```

`Serialization/OrbEdgeLineStyleConverter.cs`:

```csharp
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Pinknose.Memgraph.Orb.Razor;

internal sealed class OrbEdgeLineStyleConverter : JsonConverter<OrbEdgeLineStyle>
{
    public override OrbEdgeLineStyle? Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options)
        => throw new NotSupportedException("Line styles are write-only.");

    public override void Write(Utf8JsonWriter writer, OrbEdgeLineStyle value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteString("type", value.Kind);

        if (value.Pattern is { Length: > 0 })
        {
            writer.WritePropertyName("pattern");
            writer.WriteStartArray();
            foreach (var dash in value.Pattern)
            {
                writer.WriteNumberValue(dash);
            }

            writer.WriteEndArray();
        }

        writer.WriteEndObject();
    }
}
```

- [ ] **Step 5: Write the serializer entry point**

`Serialization/OrbJsonContext.cs`:

```csharp
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Pinknose.Memgraph.Orb.Razor;

/// <summary>
/// Serialization for everything crossing to JavaScript. Only library-owned types appear
/// here, which is what keeps WASM publish-trimming safe.
/// </summary>
internal static class OrbJson
{
    internal static readonly JsonSerializerOptions Options = Build(includeLayoutConverter: true);

    /// <summary>Used by <see cref="OrbLayoutConverter"/> to write the inner options object
    /// without recursing back into itself.</summary>
    internal static readonly JsonSerializerOptions OptionsWithoutLayoutConverter =
        Build(includeLayoutConverter: false);

    public static string SerializeGraph(OrbGraphPayload payload)
        => JsonSerializer.Serialize(payload, Options);

    public static string SerializeSettings(OrbSettings settings)
        => JsonSerializer.Serialize(settings, Options);

    private static JsonSerializerOptions Build(bool includeLayoutConverter)
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        options.Converters.Add(new OrbEdgeLineStyleConverter());

        if (includeLayoutConverter)
        {
            options.Converters.Add(new OrbLayoutConverter());
        }

        options.MakeReadOnly();
        return options;
    }
}
```

> **Known deviation from the spec.** Spec D5 and §5.5 call for a source-generated
> `JsonSerializerContext` so WASM publish-trimming stays safe. This task ships
> **reflection-based** options instead, because combining a generated context with the two
> custom converters (and the runtime-typed layout polymorphism inside `OrbLayoutConverter`)
> is fiddly enough that specifying it unverified would be worse than deferring it.
>
> Nothing in this plan verifies trimming — there is no WASM sample to publish, so no task
> would catch the problem. It is listed in **Deferred** and must be done before the library
> is advertised as WASM-safe. The exact-JSON assertions in this task are what will catch
> behavioural drift when the switch happens.

- [ ] **Step 6: Run the tests to verify they pass**

Run: `dotnet test tests/Pinknose.Memgraph.Orb.Razor.Tests --filter "FullyQualifiedName~OrbSerializationTests"`
Expected: PASS, 8 tests.

- [ ] **Step 7: Commit**

```bash
git add Pinknose.Memgraph.Orb.Razor/Internal Pinknose.Memgraph.Orb.Razor/Serialization tests/
git commit -m "feat: add payload DTOs and Orb-shaped JSON serialization"
```

---

## Task 6: Projector — domain objects to payload

**Files:**
- Create: `Pinknose.Memgraph.Orb.Razor/Internal/OrbProjector.cs`
- Create: `tests/Pinknose.Memgraph.Orb.Razor.Tests/Internal/OrbProjectorTests.cs`

**Interfaces:**
- Consumes: `IOrbNode`, `IOrbEdge` (Task 2), `OrbGraphPayload` (Task 5)
- Produces: `OrbProjector.Project<TNode, TEdge>(IEnumerable<TNode>, IEnumerable<TEdge>)` returning `OrbProjectionResult<TNode, TEdge>` with members `Payload` (`OrbGraphPayload`), `NodesById` (`Dictionary<string, TNode>`), `EdgesById` (`Dictionary<string, TEdge>`), and `DanglingEdgeIds` (`IReadOnlyList<string>`). Task 9 consumes all four.

- [ ] **Step 1: Write the failing test**

```csharp
namespace Pinknose.Memgraph.Orb.Razor.Tests.Internal;

[TestClass]
public class OrbProjectorTests
{
    private static OrbProjectionResult<OrbNode, OrbEdge> Project(
        IEnumerable<OrbNode> nodes, IEnumerable<OrbEdge> edges)
        => OrbProjector.Project(nodes, edges);

    [TestMethod]
    public void Project_MapsIdsAndEndpoints()
    {
        var result = Project(
            [new OrbNode("n1"), new OrbNode("n2")],
            [new OrbEdge("e1", "n1", "n2")]);

        Assert.AreEqual(2, result.Payload.Nodes.Count);
        Assert.AreEqual("e1", result.Payload.Edges[0].Id);
        Assert.AreEqual("n1", result.Payload.Edges[0].Start);
        Assert.AreEqual("n2", result.Payload.Edges[0].End);
    }

    [TestMethod]
    public void Project_FoldsLabelIntoStyle()
    {
        // Orb reads labels from style.label, so Label must land there.
        var result = Project([new OrbNode("n1") { Label = "Alice" }], []);

        Assert.AreEqual("Alice", result.Payload.Nodes[0].Style!.Label);
    }

    [TestMethod]
    public void Project_LabelDoesNotOverwriteOtherStyleProperties()
    {
        var result = Project(
            [new OrbNode("n1") { Label = "Alice", Style = new OrbNodeStyle { Color = "#c33" } }],
            []);

        var style = result.Payload.Nodes[0].Style!;
        Assert.AreEqual("Alice", style.Label);
        Assert.AreEqual("#c33", style.Color);
    }

    [TestMethod]
    public void Project_CopiesEveryStyleProperty()
    {
        var result = Project(
            [new OrbNode("n1") { Style = new OrbNodeStyle
            {
                Color = "#c33", Shape = OrbNodeShape.Hexagon, Size = 14, ZIndex = 3
            } }],
            []);

        var style = result.Payload.Nodes[0].Style!;
        Assert.AreEqual("#c33", style.Color);
        Assert.AreEqual(OrbNodeShape.Hexagon, style.Shape);
        Assert.AreEqual(14d, style.Size);
        Assert.AreEqual(3d, style.ZIndex);
    }

    [TestMethod]
    public void Project_CopiesEveryEdgeStyleProperty()
    {
        var result = Project(
            [new OrbNode("n1"), new OrbNode("n2")],
            [new OrbEdge("e1", "n1", "n2") { Style = new OrbEdgeStyle
            {
                Color = "#333", Width = 2, LineStyle = OrbEdgeLineStyle.Dotted
            } }]);

        var style = result.Payload.Edges[0].Style!;
        Assert.AreEqual("#333", style.Color);
        Assert.AreEqual(2d, style.Width);
        Assert.AreSame(OrbEdgeLineStyle.Dotted, style.LineStyle);
    }

    [TestMethod]
    public void Project_LeavesStyleNullWhenNothingToStyle()
    {
        var result = Project([new OrbNode("n1")], []);

        Assert.IsNull(result.Payload.Nodes[0].Style);
    }

    [TestMethod]
    public void Project_IndexesOriginalInstancesById()
    {
        var alice = new OrbNode("n1");
        var result = Project([alice], []);

        Assert.AreSame(alice, result.NodesById["n1"]);
    }

    [TestMethod]
    public void Project_ReportsDanglingEdgesButStillSendsThem()
    {
        var result = Project([new OrbNode("n1")], [new OrbEdge("e1", "n1", "missing")]);

        CollectionAssert.AreEqual(new[] { "e1" }, result.DanglingEdgeIds.ToArray());
        Assert.AreEqual(1, result.Payload.Edges.Count,
            "dangling edges are still sent so the graph self-heals when the node arrives");
    }

    [TestMethod]
    public void Project_ThrowsOnDuplicateNodeId()
    {
        Assert.ThrowsExactly<InvalidOperationException>(
            () => Project([new OrbNode("n1"), new OrbNode("n1")], []));
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test tests/Pinknose.Memgraph.Orb.Razor.Tests --filter "FullyQualifiedName~OrbProjectorTests"`
Expected: FAIL — `OrbProjector` does not exist.

- [ ] **Step 3: Write the projector**

`Internal/OrbProjector.cs`:

```csharp
namespace Pinknose.Memgraph.Orb.Razor;

internal sealed class OrbProjectionResult<TNode, TEdge>
{
    public required OrbGraphPayload Payload { get; init; }
    public required Dictionary<string, TNode> NodesById { get; init; }
    public required Dictionary<string, TEdge> EdgesById { get; init; }
    public required IReadOnlyList<string> DanglingEdgeIds { get; init; }
}

/// <summary>Projects consumer objects down to the handful of fields Orb needs.</summary>
internal static class OrbProjector
{
    public static OrbProjectionResult<TNode, TEdge> Project<TNode, TEdge>(
        IEnumerable<TNode> nodes,
        IEnumerable<TEdge> edges)
        where TNode : IOrbNode
        where TEdge : IOrbEdge
    {
        var nodesById = new Dictionary<string, TNode>(StringComparer.Ordinal);
        var nodePayloads = new List<OrbNodePayload>();

        foreach (var node in nodes)
        {
            if (!nodesById.TryAdd(node.Id, node))
            {
                throw new InvalidOperationException($"Duplicate node id '{node.Id}'.");
            }

            nodePayloads.Add(new OrbNodePayload
            {
                Id = node.Id,
                Style = MergeLabel(node.Style, node.Label)
            });
        }

        var edgesById = new Dictionary<string, TEdge>(StringComparer.Ordinal);
        var edgePayloads = new List<OrbEdgePayload>();
        var dangling = new List<string>();

        foreach (var edge in edges)
        {
            if (!edgesById.TryAdd(edge.Id, edge))
            {
                throw new InvalidOperationException($"Duplicate edge id '{edge.Id}'.");
            }

            if (!nodesById.ContainsKey(edge.Start) || !nodesById.ContainsKey(edge.End))
            {
                dangling.Add(edge.Id);
            }

            edgePayloads.Add(new OrbEdgePayload
            {
                Id = edge.Id,
                Start = edge.Start,
                End = edge.End,
                Style = MergeLabel(edge.Style, edge.Label)
            });
        }

        return new OrbProjectionResult<TNode, TEdge>
        {
            Payload = new OrbGraphPayload { Nodes = nodePayloads, Edges = edgePayloads },
            NodesById = nodesById,
            EdgesById = edgesById,
            DanglingEdgeIds = dangling
        };
    }

    // Copies into the wire DTO — the consumer's style instance is theirs, not ours.
    private static OrbNodeStylePayload? MergeLabel(OrbNodeStyle? style, string? label)
    {
        if (style is null && label is null)
        {
            return null;
        }

        return new OrbNodeStylePayload
        {
            Color = style?.Color,
            ColorHover = style?.ColorHover,
            ColorSelected = style?.ColorSelected,
            BorderColor = style?.BorderColor,
            BorderColorHover = style?.BorderColorHover,
            BorderColorSelected = style?.BorderColorSelected,
            BorderWidth = style?.BorderWidth,
            BorderWidthSelected = style?.BorderWidthSelected,
            FontBackgroundColor = style?.FontBackgroundColor,
            FontColor = style?.FontColor,
            FontFamily = style?.FontFamily,
            FontSize = style?.FontSize,
            ImageUrl = style?.ImageUrl,
            ImageUrlSelected = style?.ImageUrlSelected,
            ShadowColor = style?.ShadowColor,
            ShadowSize = style?.ShadowSize,
            ShadowOffsetX = style?.ShadowOffsetX,
            ShadowOffsetY = style?.ShadowOffsetY,
            Shape = style?.Shape,
            Size = style?.Size,
            Mass = style?.Mass,
            ZIndex = style?.ZIndex,
            Label = label
        };
    }

    private static OrbEdgeStylePayload? MergeLabel(OrbEdgeStyle? style, string? label)
    {
        if (style is null && label is null)
        {
            return null;
        }

        return new OrbEdgeStylePayload
        {
            Color = style?.Color,
            ColorHover = style?.ColorHover,
            ColorSelected = style?.ColorSelected,
            Width = style?.Width,
            WidthHover = style?.WidthHover,
            WidthSelected = style?.WidthSelected,
            ArrowSize = style?.ArrowSize,
            FontBackgroundColor = style?.FontBackgroundColor,
            FontColor = style?.FontColor,
            FontFamily = style?.FontFamily,
            FontSize = style?.FontSize,
            ShadowColor = style?.ShadowColor,
            ShadowSize = style?.ShadowSize,
            ShadowOffsetX = style?.ShadowOffsetX,
            ShadowOffsetY = style?.ShadowOffsetY,
            ZIndex = style?.ZIndex,
            LineStyle = style?.LineStyle,
            Label = label
        };
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test tests/Pinknose.Memgraph.Orb.Razor.Tests --filter "FullyQualifiedName~OrbProjectorTests"`
Expected: PASS, 9 tests.

- [ ] **Step 5: Run the whole suite for regressions**

Run: `dotnet test tests/Pinknose.Memgraph.Orb.Razor.Tests`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add Pinknose.Memgraph.Orb.Razor tests/
git commit -m "feat: project domain objects into Orb payloads"
```

---

## Task 7: Diff

**Files:**
- Create: `Pinknose.Memgraph.Orb.Razor/Internal/OrbGraphDiff.cs`
- Create: `tests/Pinknose.Memgraph.Orb.Razor.Tests/Internal/OrbGraphDiffTests.cs`

**Interfaces:**
- Consumes: nothing
- Produces: `OrbGraphDiff.RemovedIds(IReadOnlyCollection<string> previous, IReadOnlyCollection<string> current)` returning `string[]`. Task 9 calls it once for nodes and once for edges.

- [ ] **Step 1: Write the failing test**

```csharp
namespace Pinknose.Memgraph.Orb.Razor.Tests.Internal;

[TestClass]
public class OrbGraphDiffTests
{
    [TestMethod]
    public void RemovedIds_ReturnsWhatDisappeared()
    {
        var removed = OrbGraphDiff.RemovedIds(["a", "b", "c"], ["a", "c"]);

        CollectionAssert.AreEquivalent(new[] { "b" }, removed);
    }

    [TestMethod]
    public void RemovedIds_IgnoresAdditions()
    {
        var removed = OrbGraphDiff.RemovedIds(["a"], ["a", "b"]);

        Assert.AreEqual(0, removed.Length);
    }

    [TestMethod]
    public void RemovedIds_HandlesEmptyPrevious()
    {
        var removed = OrbGraphDiff.RemovedIds([], ["a"]);

        Assert.AreEqual(0, removed.Length);
    }

    [TestMethod]
    public void RemovedIds_ReturnsAllWhenCurrentIsEmpty()
    {
        var removed = OrbGraphDiff.RemovedIds(["a", "b"], []);

        CollectionAssert.AreEquivalent(new[] { "a", "b" }, removed);
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test tests/Pinknose.Memgraph.Orb.Razor.Tests --filter "FullyQualifiedName~OrbGraphDiffTests"`
Expected: FAIL — `OrbGraphDiff` does not exist.

- [ ] **Step 3: Write the implementation**

`Internal/OrbGraphDiff.cs`:

```csharp
namespace Pinknose.Memgraph.Orb.Razor;

/// <summary>
/// Orb's merge() upserts but never deletes, so removals have to be computed and sent
/// separately. Only ids that vanished matter — additions and updates ride along in merge.
/// </summary>
internal static class OrbGraphDiff
{
    public static string[] RemovedIds(
        IReadOnlyCollection<string> previous,
        IReadOnlyCollection<string> current)
    {
        if (previous.Count == 0)
        {
            return [];
        }

        var currentSet = new HashSet<string>(current, StringComparer.Ordinal);
        return previous.Where(id => !currentSet.Contains(id)).ToArray();
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test tests/Pinknose.Memgraph.Orb.Razor.Tests --filter "FullyQualifiedName~OrbGraphDiffTests"`
Expected: PASS, 4 tests.

- [ ] **Step 5: Commit**

```bash
git add Pinknose.Memgraph.Orb.Razor/Internal tests/
git commit -m "feat: compute removed node and edge ids"
```

---

## Task 8: Event args and relay

**Files:**
- Create: `Pinknose.Memgraph.Orb.Razor/Events/OrbEventArgs.cs`
- Create: `Pinknose.Memgraph.Orb.Razor/Internal/OrbEventPayload.cs`, `Internal/OrbEventRelay.cs`
- Create: `tests/Pinknose.Memgraph.Orb.Razor.Tests/Internal/OrbEventRelayTests.cs`

**Interfaces:**
- Consumes: nothing
- Produces: `OrbPoint(double X, double Y)`; `OrbNodeEventArgs<TNode> { TNode Node; OrbPoint LocalPoint; OrbPoint GlobalPoint }`; `OrbEdgeEventArgs<TEdge>` and `OrbBackgroundEventArgs` likewise; `OrbEventPayload { string? Id; double LocalX; double LocalY; double GlobalX; double GlobalY }`; `OrbEventRelay` with `[JSInvokable] Task HandleOrbEvent(string type, OrbEventPayload payload)`. Task 9 constructs the relay with a dispatch delegate.
- **If Task 1 Step 3 failed**, change `HandleOrbEvent` to `(string type, string payloadJson)` and deserialize inside the relay; the tests below change shape accordingly but the assertions stay the same.

- [ ] **Step 1: Write the failing test**

```csharp
namespace Pinknose.Memgraph.Orb.Razor.Tests.Internal;

[TestClass]
public class OrbEventRelayTests
{
    [TestMethod]
    public async Task HandleOrbEvent_ForwardsTypeAndPayload()
    {
        string? seenType = null;
        OrbEventPayload? seenPayload = null;

        var relay = new OrbEventRelay((type, payload) =>
        {
            seenType = type;
            seenPayload = payload;
            return Task.CompletedTask;
        });

        await relay.HandleOrbEvent("node-click", new OrbEventPayload
        {
            Id = "n1", LocalX = 1, LocalY = 2, GlobalX = 3, GlobalY = 4
        });

        Assert.AreEqual("node-click", seenType);
        Assert.AreEqual("n1", seenPayload!.Id);
        Assert.AreEqual(1d, seenPayload.LocalX);
        Assert.AreEqual(4d, seenPayload.GlobalY);
    }

    [TestMethod]
    public void EventArgs_CarryTheOriginalInstance()
    {
        var alice = new OrbNode("n1");
        var args = new OrbNodeEventArgs<OrbNode>
        {
            Node = alice,
            LocalPoint = new OrbPoint(1, 2),
            GlobalPoint = new OrbPoint(3, 4)
        };

        Assert.AreSame(alice, args.Node);
        Assert.AreEqual(1d, args.LocalPoint.X);
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test tests/Pinknose.Memgraph.Orb.Razor.Tests --filter "FullyQualifiedName~OrbEventRelayTests"`
Expected: FAIL — `OrbEventRelay` does not exist.

- [ ] **Step 3: Write the event args**

`Events/OrbEventArgs.cs`:

```csharp
namespace Pinknose.Memgraph.Orb.Razor;

/// <summary>A point in the graph's coordinate space.</summary>
public readonly record struct OrbPoint(double X, double Y);

public sealed class OrbNodeEventArgs<TNode>
{
    public required TNode Node { get; init; }
    public OrbPoint LocalPoint { get; init; }
    public OrbPoint GlobalPoint { get; init; }
}

public sealed class OrbEdgeEventArgs<TEdge>
{
    public required TEdge Edge { get; init; }
    public OrbPoint LocalPoint { get; init; }
    public OrbPoint GlobalPoint { get; init; }
}

public sealed class OrbBackgroundEventArgs
{
    public OrbPoint LocalPoint { get; init; }
    public OrbPoint GlobalPoint { get; init; }
}
```

- [ ] **Step 4: Write the payload and relay**

`Internal/OrbEventPayload.cs`:

```csharp
namespace Pinknose.Memgraph.Orb.Razor;

/// <summary>Wire shape for events coming back from JavaScript.</summary>
public sealed class OrbEventPayload
{
    public string? Id { get; set; }
    public double LocalX { get; set; }
    public double LocalY { get; set; }
    public double GlobalX { get; set; }
    public double GlobalY { get; set; }
}
```

`Internal/OrbEventRelay.cs`:

```csharp
using Microsoft.JSInterop;

namespace Pinknose.Memgraph.Orb.Razor;

/// <summary>
/// Non-generic [JSInvokable] target. Keeping this off the open generic component avoids
/// generic-interop sharp edges and gives the dispatch path one job.
/// </summary>
public sealed class OrbEventRelay(Func<string, OrbEventPayload, Task> dispatch)
{
    private readonly Func<string, OrbEventPayload, Task> _dispatch = dispatch;

    [JSInvokable]
    public Task HandleOrbEvent(string type, OrbEventPayload payload) => _dispatch(type, payload);
}
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test tests/Pinknose.Memgraph.Orb.Razor.Tests --filter "FullyQualifiedName~OrbEventRelayTests"`
Expected: PASS, 2 tests.

- [ ] **Step 6: Commit**

```bash
git add Pinknose.Memgraph.Orb.Razor/Events Pinknose.Memgraph.Orb.Razor/Internal tests/
git commit -m "feat: add event args and JSInvokable relay"
```

---

## Task 9: JavaScript interop module

**Files:**
- Modify: `Pinknose.Memgraph.Orb.Razor/wwwroot/orbGraph.js` (full rewrite)

**Interfaces:**
- Consumes: nothing from C# at build time
- Produces: the module surface Task 10 calls — `initializeOrb(host, dotNetRef, settingsJson, dataJson, subscribedEvents)` returning a handle, `updateData(handle, dataJson, removedNodeIds, removedEdgeIds)`, `applySettings(handle, settingsJson)`, `recenter|zoomIn|zoomOut|fixNodes|releaseNodes|unselectAll(handle)`, `selectNode(handle, id)`, `selectEdge(handle, id)`, `getSvg(handle)`, `disposeOrb(handle)`.

Event type strings sent to C#: `node-click`, `node-double-click`, `node-right-click`, `node-hover-enter`, `node-hover-leave`, `edge-click`, `edge-double-click`, `edge-right-click`, `edge-hover-enter`, `edge-hover-leave`, `background-click`.

- [ ] **Step 1: Rewrite the module**

Replace `wwwroot/orbGraph.js` entirely:

```js
const SCRIPT_URL = "./_content/Pinknose.Memgraph.Orb.Razor/vendor/memgraph/orb/1.0.2/orb.min.js";
const SCRIPT_INTEGRITY = "sha384-/bdC+Sgoda/KpkiTPljaZXPEpNJg712oGud22zh7zsoVZch3PRsvcjfqNLRCaIoT";

let scriptLoad = null;

function ensureScript() {
    if (scriptLoad) {
        return scriptLoad;
    }

    scriptLoad = new Promise((resolve, reject) => {
        const existing = document.querySelector(`script[src="${SCRIPT_URL}"]`);
        if (existing) {
            resolve();
            return;
        }

        const script = document.createElement("script");
        script.src = SCRIPT_URL;
        script.async = true;
        // The browser verifies this itself and refuses to execute a mismatched bundle.
        script.integrity = SCRIPT_INTEGRITY;
        script.crossOrigin = "anonymous";
        script.onload = () => resolve();
        script.onerror = () => reject(new Error(`Failed to load Orb from '${SCRIPT_URL}'.`));
        document.head.appendChild(script);
    });

    return scriptLoad;
}

// A resolved script tag is not the same as the UMD bundle having run: a tag added by an
// earlier component may still be in flight when we adopt it above.
async function resolveOrbView(maxRetries = 50, delayMs = 20) {
    for (let i = 0; i < maxRetries; i++) {
        if (typeof globalThis.Orb?.OrbView === "function") {
            return globalThis.Orb.OrbView;
        }

        if (i < maxRetries - 1) {
            await new Promise((resolve) => setTimeout(resolve, delayMs));
        }
    }

    throw new Error("The 'OrbView' export was not found on the global Orb namespace.");
}

function parseJson(value) {
    return value && value.trim() ? JSON.parse(value) : null;
}

// Orb's _applyStyle() skips any node that already has a style, so setDefaultStyle would
// apply exactly once per node and never repaint a changed style. Push them explicitly.
function pushStyles(view, payload) {
    const graph = view.data;

    for (const node of payload.nodes) {
        if (node.style) {
            graph.getNodeById(node.id)?.setStyle(node.style, { isNotifySkipped: true });
        }
    }

    for (const edge of payload.edges) {
        if (edge.style) {
            graph.getEdgeById(edge.id)?.setStyle(edge.style, { isNotifySkipped: true });
        }
    }
}

function pointsOf(event) {
    return {
        localX: event.localPoint?.x ?? 0,
        localY: event.localPoint?.y ?? 0,
        globalX: event.globalPoint?.x ?? 0,
        globalY: event.globalPoint?.y ?? 0
    };
}

function subscribe(handle, subscribedEvents) {
    const wanted = new Set(subscribedEvents ?? []);
    const { view, dotNetRef } = handle;

    const send = (type, id, event) =>
        dotNetRef.invokeMethodAsync("HandleOrbEvent", type, { id, ...pointsOf(event) });

    const classify = (subject) => {
        if (!subject) return null;
        if (globalThis.Orb.isNode(subject)) return "node";
        if (globalThis.Orb.isEdge(subject)) return "edge";
        return null;
    };

    const forwardSubject = (orbEvent, nodeType, edgeType) => {
        view.events.on(orbEvent, (e) => {
            const kind = classify(e.subject ?? e.node ?? e.edge);
            const subject = e.subject ?? e.node ?? e.edge;
            if (kind === "node" && wanted.has(nodeType)) send(nodeType, String(subject.id), e);
            else if (kind === "edge" && wanted.has(edgeType)) send(edgeType, String(subject.id), e);
            else if (!kind && wanted.has("background-click") && orbEvent === "mouse-click") {
                send("background-click", null, e);
            }
        });
    };

    forwardSubject("mouse-click", "node-click", "edge-click");
    forwardSubject("mouse-double-click", "node-double-click", "edge-double-click");
    forwardSubject("mouse-right-click", "node-right-click", "edge-right-click");

    const hoverWanted = ["node-hover-enter", "node-hover-leave",
                         "edge-hover-enter", "edge-hover-leave"].some((t) => wanted.has(t));

    if (hoverWanted) {
        // Orb fires hover per mousemove. Only transitions are marshalled, so sweeping across
        // a node costs two messages instead of one per frame.
        let hoveredId = null;
        let hoveredKind = null;

        view.events.on("mouse-move", (e) => {
            const subject = e.subject;
            const kind = classify(subject);
            const id = subject ? String(subject.id) : null;

            if (id === hoveredId) {
                return;
            }

            if (hoveredId) {
                const leave = `${hoveredKind}-hover-leave`;
                if (wanted.has(leave)) send(leave, hoveredId, e);
            }

            if (id) {
                const enter = `${kind}-hover-enter`;
                if (wanted.has(enter)) send(enter, id, e);
            }

            hoveredId = id;
            hoveredKind = kind;
        });
    }
}

export async function initializeOrb(host, dotNetRef, settingsJson, dataJson, subscribedEvents) {
    if (!host) {
        throw new Error("Host element is required.");
    }

    await ensureScript();
    const OrbView = await resolveOrbView();

    const settings = parseJson(settingsJson) ?? undefined;
    const view = new OrbView(host, settings);
    const handle = { view, host, dotNetRef };

    const payload = parseJson(dataJson);
    if (payload) {
        view.data.setup(payload);
        pushStyles(view, payload);
    }

    subscribe(handle, subscribedEvents);
    view.render(() => view.recenter());

    return handle;
}

export function updateData(handle, dataJson, removedNodeIds, removedEdgeIds) {
    if (!handle) return;

    const payload = parseJson(dataJson);
    if (payload) {
        handle.view.data.merge(payload);
    }

    if (removedNodeIds?.length || removedEdgeIds?.length) {
        handle.view.data.remove({
            nodeIds: removedNodeIds ?? [],
            edgeIds: removedEdgeIds ?? []
        });
    }

    if (payload) {
        pushStyles(handle.view, payload);
    }

    handle.view.render();
}

export function applySettings(handle, settingsJson) {
    const settings = parseJson(settingsJson);
    if (handle && settings) {
        handle.view.setSettings(settings);
    }
}

export function recenter(handle)      { handle?.view.recenter(); }
export function zoomIn(handle)        { handle?.view.zoomIn(); }
export function zoomOut(handle)       { handle?.view.zoomOut(); }
export function fixNodes(handle)      { handle?.view.fixNodes(); }
export function releaseNodes(handle)  { handle?.view.releaseNodes(); }
export function unselectAll(handle)   { handle?.view.interaction.unselectAll(); handle?.view.render(); }
export function selectNode(handle, id) { handle?.view.interaction.selectNodeById(id); handle?.view.render(); }
export function selectEdge(handle, id) { handle?.view.interaction.selectEdgeById(id); handle?.view.render(); }
export function getSvg(handle)        { return handle ? handle.view.getSVG() : ""; }

export function disposeOrb(handle) {
    if (!handle) return;

    handle.view?.destroy();

    if (handle.host) {
        handle.host.innerHTML = "";
    }
}
```

- [ ] **Step 2: Verify it builds and the old demo still loads**

```bash
dotnet build Pinknose.Memgraph.Orb.Razor.slnx
```

Expected: build succeeds. The sample still uses the old parameters and will be updated in
Task 11 — its page may error until then, which is expected.

- [ ] **Step 3: Commit**

```bash
git add Pinknose.Memgraph.Orb.Razor/wwwroot/orbGraph.js
git commit -m "feat: rewrite interop module for typed payloads, styles and events"
```

---

## Task 10: The component

**Files:**
- Modify: `Pinknose.Memgraph.Orb.Razor/OrbGraph.razor` (full rewrite)
- Create: `tests/Pinknose.Memgraph.Orb.Razor.Tests/OrbGraphComponentTests.cs`
- Modify: `tests/Pinknose.Memgraph.Orb.Razor.Tests/Pinknose.Memgraph.Orb.Razor.Tests.csproj`

**Interfaces:**
- Consumes: `OrbProjector` (Task 6), `OrbGraphDiff` (Task 7), `OrbEventRelay`/`OrbEventPayload`/event args (Task 8), `OrbJson` (Task 5), the JS module (Task 9)
- Produces: `OrbGraph<TNode, TEdge>` — the full public surface from the spec §3.1.

- [ ] **Step 1: Add bUnit to the test project**

```bash
dotnet add tests/Pinknose.Memgraph.Orb.Razor.Tests package bunit --version 2.9.0
```

- [ ] **Step 2: Write the failing test**

```csharp
using Bunit;
using Microsoft.Extensions.DependencyInjection;

namespace Pinknose.Memgraph.Orb.Razor.Tests;

[TestClass]
public class OrbGraphComponentTests : BunitContext
{
    private static readonly OrbNode[] TwoNodes = [new OrbNode("n1"), new OrbNode("n2")];
    private static readonly OrbEdge[] OneEdge = [new OrbEdge("e1", "n1", "n2")];

    private BunitJSModuleInterop SetupModule()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        return JSInterop.SetupModule("./_content/Pinknose.Memgraph.Orb.Razor/orbGraph.js");
    }

    [TestMethod]
    public void Render_InitializesOrbWithProjectedData()
    {
        var module = SetupModule();

        Render<OrbGraph<OrbNode, OrbEdge>>(p => p
            .Add(x => x.Nodes, TwoNodes)
            .Add(x => x.Edges, OneEdge));

        var init = module.Invocations["initializeOrb"].Single();
        var dataJson = (string?)init.Arguments[3];

        StringAssert.Contains(dataJson!, "\"id\":\"n1\"");
        StringAssert.Contains(dataJson!, "\"start\":\"n1\"");
    }

    [TestMethod]
    public void Render_SubscribesOnlyToWiredEvents()
    {
        var module = SetupModule();

        Render<OrbGraph<OrbNode, OrbEdge>>(p => p
            .Add(x => x.Nodes, TwoNodes)
            .Add(x => x.OnNodeClick, _ => { }));

        var subscribed = (string[]?)module.Invocations["initializeOrb"].Single().Arguments[4];

        CollectionAssert.Contains(subscribed, "node-click");
        CollectionAssert.DoesNotContain(subscribed, "node-hover-enter");
    }

    [TestMethod]
    public void HostElement_MergesConsumerClassWithBaseClass()
    {
        SetupModule();

        var cut = Render<OrbGraph<OrbNode, OrbEdge>>(p => p
            .Add(x => x.Nodes, TwoNodes)
            .AddUnmatched("class", "border rounded")
            .AddUnmatched("id", "org-chart"));

        var div = cut.Find("div");

        StringAssert.Contains(div.GetAttribute("class")!, "orb-graph");
        StringAssert.Contains(div.GetAttribute("class")!, "border rounded");
        Assert.AreEqual("org-chart", div.GetAttribute("id"));
    }

    [TestMethod]
    public void HostElement_AppliesWidthAndHeight()
    {
        SetupModule();

        var cut = Render<OrbGraph<OrbNode, OrbEdge>>(p => p
            .Add(x => x.Nodes, TwoNodes)
            .Add(x => x.Height, "600px"));

        StringAssert.Contains(cut.Find("div").GetAttribute("style")!, "height:600px");
    }

    [TestMethod]
    public void UnchangedParameters_DoNotCallUpdateData()
    {
        var module = SetupModule();

        var cut = Render<OrbGraph<OrbNode, OrbEdge>>(p => p
            .Add(x => x.Nodes, TwoNodes)
            .Add(x => x.Edges, OneEdge));

        cut.Render();   // re-render with identical parameters

        Assert.AreEqual(0, module.Invocations["updateData"].Count,
            "an unchanged payload must not cross the interop boundary");
    }

    [TestMethod]
    public void RemovingANode_SendsItsIdForRemoval()
    {
        var module = SetupModule();

        var cut = Render<OrbGraph<OrbNode, OrbEdge>>(p => p
            .Add(x => x.Nodes, TwoNodes)
            .Add(x => x.Edges, OneEdge));

        cut.SetParametersAndRender(p => p
            .Add(x => x.Nodes, [TwoNodes[0]])
            .Add(x => x.Edges, System.Array.Empty<OrbEdge>()));

        var update = module.Invocations["updateData"].Single();
        var removedNodeIds = (string[]?)update.Arguments[2];

        CollectionAssert.Contains(removedNodeIds, "n2");
    }

    [TestMethod]
    public async Task NodeClickFromJs_InvokesCallbackWithOriginalInstance()
    {
        SetupModule();

        OrbNode? clicked = null;
        var cut = Render<OrbGraph<OrbNode, OrbEdge>>(p => p
            .Add(x => x.Nodes, TwoNodes)
            .Add(x => x.OnNodeClick, e => clicked = e.Node));

        await cut.Instance.HandleEventForTestsAsync("node-click", new OrbEventPayload { Id = "n1" });

        Assert.AreSame(TwoNodes[0], clicked);
    }
}
```

- [ ] **Step 3: Run the test to verify it fails**

Run: `dotnet test tests/Pinknose.Memgraph.Orb.Razor.Tests --filter "FullyQualifiedName~OrbGraphComponentTests"`
Expected: FAIL — the component has no `Nodes` parameter.

- [ ] **Step 4: Write the component**

Replace `OrbGraph.razor` entirely:

```razor
@typeparam TNode where TNode : IOrbNode
@typeparam TEdge where TEdge : IOrbEdge
@using Microsoft.Extensions.Logging
@using Microsoft.JSInterop
@implements IAsyncDisposable
@inject IJSRuntime JsRuntime
@inject ILoggerFactory LoggerFactory

<div @ref="_hostElement" class="@_cssClass" style="@_hostStyle" @attributes="_passThrough"></div>

@code {
    private const string ModuleUrl = "./_content/Pinknose.Memgraph.Orb.Razor/orbGraph.js";
    private const string BaseClass = "orb-graph";

    private IJSObjectReference? _module;
    private IJSObjectReference? _handle;
    private DotNetObjectReference<OrbEventRelay>? _relayRef;
    private ElementReference _hostElement;
    private ILogger? _logger;

    private readonly TaskCompletionSource<bool> _ready =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private Dictionary<string, TNode> _sentNodes = [];
    private Dictionary<string, TEdge> _sentEdges = [];
    private string? _lastDataJson;
    private string? _lastSettingsJson;
    private bool _disposed;

    private string _cssClass = BaseClass;
    private string _hostStyle = "";
    private Dictionary<string, object>? _passThrough;

    [Parameter, EditorRequired] public IEnumerable<TNode> Nodes { get; set; } = [];
    [Parameter] public IEnumerable<TEdge> Edges { get; set; } = [];
    [Parameter] public OrbSettings? Settings { get; set; }
    [Parameter] public string Width { get; set; } = "100%";
    [Parameter] public string Height { get; set; } = "500px";

    [Parameter(CaptureUnmatchedValues = true)]
    public IReadOnlyDictionary<string, object>? AdditionalAttributes { get; set; }

    [Parameter] public EventCallback<OrbNodeEventArgs<TNode>> OnNodeClick { get; set; }
    [Parameter] public EventCallback<OrbNodeEventArgs<TNode>> OnNodeDoubleClick { get; set; }
    [Parameter] public EventCallback<OrbNodeEventArgs<TNode>> OnNodeRightClick { get; set; }
    [Parameter] public EventCallback<OrbNodeEventArgs<TNode>> OnNodeHoverEnter { get; set; }
    [Parameter] public EventCallback<OrbNodeEventArgs<TNode>> OnNodeHoverLeave { get; set; }
    [Parameter] public EventCallback<OrbEdgeEventArgs<TEdge>> OnEdgeClick { get; set; }
    [Parameter] public EventCallback<OrbEdgeEventArgs<TEdge>> OnEdgeDoubleClick { get; set; }
    [Parameter] public EventCallback<OrbEdgeEventArgs<TEdge>> OnEdgeRightClick { get; set; }
    [Parameter] public EventCallback<OrbEdgeEventArgs<TEdge>> OnEdgeHoverEnter { get; set; }
    [Parameter] public EventCallback<OrbEdgeEventArgs<TEdge>> OnEdgeHoverLeave { get; set; }
    [Parameter] public EventCallback<OrbBackgroundEventArgs> OnBackgroundClick { get; set; }

    protected override void OnParametersSet()
    {
        _logger ??= LoggerFactory.CreateLogger("Pinknose.Memgraph.Orb.Razor.OrbGraph");

        string? extraClass = null, extraStyle = null;
        Dictionary<string, object>? passThrough = null;

        if (AdditionalAttributes is not null)
        {
            passThrough = new Dictionary<string, object>(
                AdditionalAttributes.Count, StringComparer.OrdinalIgnoreCase);

            foreach (var (key, value) in AdditionalAttributes)
            {
                if (string.Equals(key, "class", StringComparison.OrdinalIgnoreCase))
                {
                    extraClass = value?.ToString();
                }
                else if (string.Equals(key, "style", StringComparison.OrdinalIgnoreCase))
                {
                    extraStyle = value?.ToString();
                }
                else
                {
                    passThrough[key] = value;
                }
            }
        }

        _cssClass = string.IsNullOrWhiteSpace(extraClass) ? BaseClass : $"{BaseClass} {extraClass}";
        _hostStyle = $"width:{Width};height:{Height};{extraStyle}";
        _passThrough = passThrough;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            await InitializeAsync();
            return;
        }

        await PushChangesAsync();
    }

    private async Task InitializeAsync()
    {
        var projection = ProjectAndWarn();
        var dataJson = OrbJson.SerializeGraph(projection.Payload);
        var settingsJson = Settings is null ? null : OrbJson.SerializeSettings(Settings);

        var relay = new OrbEventRelay(DispatchEventAsync);
        _relayRef = DotNetObjectReference.Create(relay);

        try
        {
            _module = await JsRuntime.InvokeAsync<IJSObjectReference>("import", ModuleUrl);
            _handle = await _module.InvokeAsync<IJSObjectReference>(
                "initializeOrb", _hostElement, _relayRef, settingsJson, dataJson, SubscribedEvents());

            _sentNodes = projection.NodesById;
            _sentEdges = projection.EdgesById;
            _lastDataJson = dataJson;
            _lastSettingsJson = settingsJson;
            _ready.TrySetResult(true);
        }
        catch (JSException ex)
        {
            // A graph that fails to load must not take the circuit down with it.
            _logger?.LogError(ex, "Orb failed to initialize; the graph will not render.");
            _ready.TrySetResult(false);
        }
        catch (JSDisconnectedException)
        {
            _ready.TrySetResult(false);
        }
    }

    private async Task PushChangesAsync()
    {
        if (_disposed || !await IsReadyAsync())
        {
            return;
        }

        var projection = ProjectAndWarn();
        var dataJson = OrbJson.SerializeGraph(projection.Payload);

        var removedNodeIds = OrbGraphDiff.RemovedIds(_sentNodes.Keys, projection.NodesById.Keys);
        var removedEdgeIds = OrbGraphDiff.RemovedIds(_sentEdges.Keys, projection.EdgesById.Keys);

        if (dataJson != _lastDataJson || removedNodeIds.Length > 0 || removedEdgeIds.Length > 0)
        {
            await _module!.InvokeVoidAsync("updateData", _handle, dataJson, removedNodeIds, removedEdgeIds);
            _sentNodes = projection.NodesById;
            _sentEdges = projection.EdgesById;
            _lastDataJson = dataJson;
        }

        var settingsJson = Settings is null ? null : OrbJson.SerializeSettings(Settings);
        if (settingsJson is not null && settingsJson != _lastSettingsJson)
        {
            await _module!.InvokeVoidAsync("applySettings", _handle, settingsJson);
            _lastSettingsJson = settingsJson;
        }
    }

    private OrbProjectionResult<TNode, TEdge> ProjectAndWarn()
    {
        var projection = OrbProjector.Project(Nodes, Edges);

        if (projection.DanglingEdgeIds.Count > 0)
        {
            // Orb drops these silently. We resend everything each update, so they appear
            // as soon as their endpoints do — this warning is for visibility, not recovery.
            _logger?.LogWarning(
                "{Count} edge(s) reference missing nodes and will not render yet: {EdgeIds}",
                projection.DanglingEdgeIds.Count,
                string.Join(", ", projection.DanglingEdgeIds));
        }

        return projection;
    }

    private string[] SubscribedEvents()
    {
        var events = new List<string>(11);

        if (OnNodeClick.HasDelegate) events.Add("node-click");
        if (OnNodeDoubleClick.HasDelegate) events.Add("node-double-click");
        if (OnNodeRightClick.HasDelegate) events.Add("node-right-click");
        if (OnNodeHoverEnter.HasDelegate) events.Add("node-hover-enter");
        if (OnNodeHoverLeave.HasDelegate) events.Add("node-hover-leave");
        if (OnEdgeClick.HasDelegate) events.Add("edge-click");
        if (OnEdgeDoubleClick.HasDelegate) events.Add("edge-double-click");
        if (OnEdgeRightClick.HasDelegate) events.Add("edge-right-click");
        if (OnEdgeHoverEnter.HasDelegate) events.Add("edge-hover-enter");
        if (OnEdgeHoverLeave.HasDelegate) events.Add("edge-hover-leave");
        if (OnBackgroundClick.HasDelegate) events.Add("background-click");

        return [.. events];
    }

    /// <summary>Test seam for the relay dispatch path.</summary>
    internal Task HandleEventForTestsAsync(string type, OrbEventPayload payload)
        => DispatchEventAsync(type, payload);

    private Task DispatchEventAsync(string type, OrbEventPayload payload)
    {
        var local = new OrbPoint(payload.LocalX, payload.LocalY);
        var global = new OrbPoint(payload.GlobalX, payload.GlobalY);

        if (type == "background-click")
        {
            return OnBackgroundClick.InvokeAsync(
                new OrbBackgroundEventArgs { LocalPoint = local, GlobalPoint = global });
        }

        if (payload.Id is null)
        {
            return Task.CompletedTask;
        }

        if (type.StartsWith("node-", StringComparison.Ordinal))
        {
            if (!_sentNodes.TryGetValue(payload.Id, out var node))
            {
                return Task.CompletedTask;
            }

            var args = new OrbNodeEventArgs<TNode>
            {
                Node = node, LocalPoint = local, GlobalPoint = global
            };

            return type switch
            {
                "node-click" => OnNodeClick.InvokeAsync(args),
                "node-double-click" => OnNodeDoubleClick.InvokeAsync(args),
                "node-right-click" => OnNodeRightClick.InvokeAsync(args),
                "node-hover-enter" => OnNodeHoverEnter.InvokeAsync(args),
                "node-hover-leave" => OnNodeHoverLeave.InvokeAsync(args),
                _ => Task.CompletedTask
            };
        }

        if (!_sentEdges.TryGetValue(payload.Id, out var edge))
        {
            return Task.CompletedTask;
        }

        var edgeArgs = new OrbEdgeEventArgs<TEdge>
        {
            Edge = edge, LocalPoint = local, GlobalPoint = global
        };

        return type switch
        {
            "edge-click" => OnEdgeClick.InvokeAsync(edgeArgs),
            "edge-double-click" => OnEdgeDoubleClick.InvokeAsync(edgeArgs),
            "edge-right-click" => OnEdgeRightClick.InvokeAsync(edgeArgs),
            "edge-hover-enter" => OnEdgeHoverEnter.InvokeAsync(edgeArgs),
            "edge-hover-leave" => OnEdgeHoverLeave.InvokeAsync(edgeArgs),
            _ => Task.CompletedTask
        };
    }

    private async Task<bool> IsReadyAsync()
        => !_disposed && _handle is not null && await _ready.Task;

    public async ValueTask RecenterAsync() => await InvokeViewAsync("recenter");
    public async ValueTask ZoomInAsync() => await InvokeViewAsync("zoomIn");
    public async ValueTask ZoomOutAsync() => await InvokeViewAsync("zoomOut");
    public async ValueTask FixNodesAsync() => await InvokeViewAsync("fixNodes");
    public async ValueTask ReleaseNodesAsync() => await InvokeViewAsync("releaseNodes");
    public async ValueTask UnselectAllAsync() => await InvokeViewAsync("unselectAll");
    public async ValueTask SelectNodeAsync(string id) => await InvokeViewAsync("selectNode", id);
    public async ValueTask SelectEdgeAsync(string id) => await InvokeViewAsync("selectEdge", id);

    public async ValueTask<string> GetSvgAsync()
    {
        if (!await IsReadyAsync())
        {
            return string.Empty;
        }

        try
        {
            return await _module!.InvokeAsync<string>("getSvg", _handle);
        }
        catch (JSDisconnectedException)
        {
            return string.Empty;
        }
    }

    private async ValueTask InvokeViewAsync(string name, params object?[] args)
    {
        if (!await IsReadyAsync())
        {
            return;
        }

        try
        {
            await _module!.InvokeVoidAsync(name, [_handle, .. args]);
        }
        catch (JSDisconnectedException)
        {
        }
    }

    public async ValueTask DisposeAsync()
    {
        _disposed = true;
        _ready.TrySetResult(false);

        try
        {
            if (_module is not null && _handle is not null)
            {
                await _module.InvokeVoidAsync("disposeOrb", _handle);
            }

            if (_handle is not null)
            {
                await _handle.DisposeAsync();
            }

            if (_module is not null)
            {
                await _module.DisposeAsync();
            }
        }
        catch (JSDisconnectedException)
        {
        }
        finally
        {
            _relayRef?.Dispose();
        }
    }
}
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test tests/Pinknose.Memgraph.Orb.Razor.Tests --filter "FullyQualifiedName~OrbGraphComponentTests"`
Expected: PASS, 7 tests.

- [ ] **Step 6: Confirm the library builds clean**

Run: `dotnet build Pinknose.Memgraph.Orb.Razor/Pinknose.Memgraph.Orb.Razor.csproj`
Expected: 0 warnings, 0 errors.

- [ ] **Step 7: Commit**

```bash
git add Pinknose.Memgraph.Orb.Razor/OrbGraph.razor tests/
git commit -m "feat: typed OrbGraph component with events and imperative control"
```

---

## Task 11: Update the sample

**Files:**
- Modify: `samples/Pinknose.Memgraph.Orb.Razor.SampleHost/Components/Pages/OrbDemo.razor`

**Interfaces:**
- Consumes: the full component surface from Task 10
- Produces: a demo page exercising typed nodes via a custom `IOrbNode` implementation, styles, settings, click and hover events, and an imperative button — the fixtures the Playwright suite in Task 12 drives. Element ids used by those tests: `#recenter-btn`, `#selection-label`, `#hover-label`, `#remove-btn`.

- [ ] **Step 1: Rewrite the demo page**

```razor
@page "/orb-demo"
@rendermode InteractiveServer

<PageTitle>Orb Demo</PageTitle>

<h1>Orb Demo</h1>

<p>
    Selected: <strong id="selection-label">@(_selected?.FullName ?? "none")</strong> |
    Hovered: <strong id="hover-label">@(_hovered?.FullName ?? "none")</strong>
</p>

<button id="recenter-btn" class="btn btn-secondary" @onclick="RecenterAsync">Recenter</button>
<button id="remove-btn" class="btn btn-secondary" @onclick="RemoveCarol">Remove Carol</button>

<OrbGraph @ref="_graph"
          Nodes="@_people"
          Edges="@_relationships"
          Height="600px"
          class="border rounded"
          Settings="@_settings"
          OnNodeClick="@(e => _selected = e.Node)"
          OnNodeHoverEnter="@(e => _hovered = e.Node)"
          OnNodeHoverLeave="@(_ => _hovered = null)" />

@code {
    private sealed record Person(string EmployeeId, string FullName, bool IsManager) : IOrbNode
    {
        public string Id => EmployeeId;
        public string? Label => FullName;
        public OrbNodeStyle? Style => new()
        {
            Color = IsManager ? "#cc3333" : "#3399cc",
            Size = IsManager ? 12 : 8
        };
    }

    private sealed record Relationship(string Id, string Start, string End, string Kind) : IOrbEdge
    {
        public string? Label => Kind;
    }

    private OrbGraph<Person, Relationship>? _graph;
    private Person? _selected;
    private Person? _hovered;

    private List<Person> _people =
    [
        new("n1", "Alice", true),
        new("n2", "Bob", false),
        new("n3", "Carol", false)
    ];

    private List<Relationship> _relationships =
    [
        new("e1", "n1", "n2", "KNOWS"),
        new("e2", "n2", "n3", "WORKS_WITH")
    ];

    private readonly OrbSettings _settings = new()
    {
        Render = new OrbRenderSettings { LabelsIsEnabled = true, ShadowIsEnabled = true },
        Interaction = new OrbInteractionSettings { IsDragEnabled = true, IsZoomEnabled = true },
        Layout = new OrbForceLayout()
    };

    private async Task RecenterAsync()
    {
        if (_graph is not null)
        {
            await _graph.RecenterAsync();
        }
    }

    private void RemoveCarol()
    {
        _people = [.. _people.Where(p => p.Id != "n3")];
        _relationships = [.. _relationships.Where(r => r.Start != "n3" && r.End != "n3")];
    }
}
```

- [ ] **Step 2: Build and run the sample**

```bash
dotnet build Pinknose.Memgraph.Orb.Razor.slnx
dotnet run --project samples/Pinknose.Memgraph.Orb.Razor.SampleHost/Pinknose.Memgraph.Orb.Razor.SampleHost.csproj --launch-profile http
```

- [ ] **Step 3: Verify by hand in the browser**

At `http://localhost:5053/orb-demo`, confirm: three nodes render with Alice larger and red;
clicking a node updates "Selected"; hovering updates "Hovered" and clears on leave;
"Remove Carol" removes one node and its edge without the remaining nodes jumping position;
"Recenter" refits the view. Check the server console for no exceptions.

- [ ] **Step 4: Commit**

```bash
git add samples/
git commit -m "feat: rebuild sample demo on the typed API"
```

---

## Task 12: Playwright smoke suite

**Files:**
- Create: `tests/Pinknose.Memgraph.Orb.Razor.BrowserTests/Pinknose.Memgraph.Orb.Razor.BrowserTests.csproj`
- Create: `tests/Pinknose.Memgraph.Orb.Razor.BrowserTests/SampleHostFixture.cs`, `OrbGraphSmokeTests.cs`
- Modify: `Pinknose.Memgraph.Orb.Razor.slnx`

**Interfaces:**
- Consumes: the sample page from Task 11 and its element ids
- Produces: the six smoke tests from spec §7

- [ ] **Step 1: Create the browser test project**

```bash
dotnet new mstest -f net10.0 -o tests/Pinknose.Memgraph.Orb.Razor.BrowserTests
rm tests/Pinknose.Memgraph.Orb.Razor.BrowserTests/UnitTest1.cs
dotnet add tests/Pinknose.Memgraph.Orb.Razor.BrowserTests package Microsoft.Playwright.MSTest --version 1.62.0
dotnet sln Pinknose.Memgraph.Orb.Razor.slnx add tests/Pinknose.Memgraph.Orb.Razor.BrowserTests
dotnet build tests/Pinknose.Memgraph.Orb.Razor.BrowserTests
pwsh tests/Pinknose.Memgraph.Orb.Razor.BrowserTests/bin/Debug/net10.0/playwright.ps1 install chromium
```

- [ ] **Step 2: Write the sample host fixture**

`SampleHostFixture.cs`:

```csharp
using System.Diagnostics;
using System.Net.Http;

namespace Pinknose.Memgraph.Orb.Razor.BrowserTests;

/// <summary>Starts the sample host once for the whole assembly.</summary>
public static class SampleHostFixture
{
    public const string BaseUrl = "http://localhost:5099";

    private static Process? _host;

    [AssemblyInitialize]
    public static async Task StartAsync(TestContext _)
    {
        _host = Process.Start(new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = "run --project ../../../../../samples/Pinknose.Memgraph.Orb.Razor.SampleHost/"
                      + "Pinknose.Memgraph.Orb.Razor.SampleHost.csproj "
                      + $"--urls {BaseUrl}",
            UseShellExecute = false
        });

        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };

        for (var attempt = 0; attempt < 60; attempt++)
        {
            try
            {
                var response = await client.GetAsync($"{BaseUrl}/orb-demo");
                if (response.IsSuccessStatusCode)
                {
                    return;
                }
            }
            catch (HttpRequestException)
            {
            }
            catch (TaskCanceledException)
            {
            }

            await Task.Delay(1000);
        }

        throw new InvalidOperationException("Sample host did not start within 60 seconds.");
    }

    [AssemblyCleanup]
    public static void Stop()
    {
        if (_host is { HasExited: false })
        {
            _host.Kill(entireProcessTree: true);
        }

        _host?.Dispose();
    }
}
```

- [ ] **Step 3: Write the smoke tests**

`OrbGraphSmokeTests.cs`:

```csharp
using Microsoft.Playwright;
using Microsoft.Playwright.MSTest;

namespace Pinknose.Memgraph.Orb.Razor.BrowserTests;

[TestClass]
public class OrbGraphSmokeTests : PageTest
{
    private const string CountPaintedPixels = """
        () => {
            const c = document.querySelector('.orb-graph canvas');
            if (!c) return -1;
            const d = c.getContext('2d').getImageData(0, 0, c.width, c.height).data;
            let painted = 0;
            for (let i = 3; i < d.length; i += 4) { if (d[i] !== 0) painted++; }
            return painted;
        }
        """;

    private const string ReadNodePositions = """
        () => {
            const v = window.__orbTestView;
            return v ? JSON.stringify(v.data.getNodePositions()) : null;
        }
        """;

    [TestInitialize]
    public async Task GoToDemoAsync()
    {
        await Page.GotoAsync($"{SampleHostFixture.BaseUrl}/orb-demo");
        await Page.WaitForFunctionAsync("() => !!document.querySelector('.orb-graph canvas')");
        // Let the force simulation settle so pixel and position reads are stable.
        await Page.WaitForTimeoutAsync(2000);
    }

    [TestMethod]
    public async Task Graph_RendersANonBlankCanvas()
    {
        var painted = await Page.EvaluateAsync<int>(CountPaintedPixels);

        Assert.IsGreaterThan(0, painted, "the canvas rendered nothing");
    }

    [TestMethod]
    public async Task ClickingANode_RaisesOnNodeClickWithTheOriginalInstance()
    {
        await ClickFirstNodeAsync();

        await Expect(Page.Locator("#selection-label")).Not.ToHaveTextAsync("none");
    }

    [TestMethod]
    public async Task HoveringANode_RaisesEnterThenLeave()
    {
        var box = await Page.Locator(".orb-graph canvas").BoundingBoxAsync();
        var position = await FindNodePointAsync();

        await Page.Mouse.MoveAsync(box!.X + position.X, box.Y + position.Y);
        await Expect(Page.Locator("#hover-label")).Not.ToHaveTextAsync("none");

        await Page.Mouse.MoveAsync(box.X + 5, box.Y + 5);
        await Expect(Page.Locator("#hover-label")).ToHaveTextAsync("none");
    }

    [TestMethod]
    public async Task RemovingANode_DropsItAndItsEdge()
    {
        var before = await Page.EvaluateAsync<int>(
            "() => window.__orbTestView.data.getNodeCount()");

        await Page.ClickAsync("#remove-btn");
        await Page.WaitForFunctionAsync(
            $"() => window.__orbTestView.data.getNodeCount() === {before - 1}");

        var edges = await Page.EvaluateAsync<int>(
            "() => window.__orbTestView.data.getEdgeCount()");

        Assert.AreEqual(1, edges, "removing a node must take its edges with it");
    }

    [TestMethod]
    public async Task UpdatingNodes_PreservesExistingPositions()
    {
        var before = await Page.EvaluateAsync<string>(ReadNodePositions);

        await Page.ClickAsync("#remove-btn");
        await Page.WaitForTimeoutAsync(500);

        var after = await Page.EvaluateAsync<string>(ReadNodePositions);

        // Alice and Bob keep their coordinates; only Carol disappears.
        var aliceBefore = ExtractPosition(before, "n1");
        var aliceAfter = ExtractPosition(after, "n1");

        Assert.AreEqual(aliceBefore, aliceAfter, "merge must not reset simulated positions");
    }

    [TestMethod]
    public async Task NavigatingAway_DisposesWithoutServerError()
    {
        var errors = new List<string>();
        Page.Console += (_, msg) => { if (msg.Type == "error") errors.Add(msg.Text); };

        await Page.ClickAsync("a[href='counter']");
        await Expect(Page.Locator("h1")).ToHaveTextAsync("Counter");

        Assert.AreEqual(0, errors.Count, string.Join("\n", errors));
    }

    private async Task ClickFirstNodeAsync()
    {
        var box = await Page.Locator(".orb-graph canvas").BoundingBoxAsync();
        var position = await FindNodePointAsync();

        await Page.Mouse.ClickAsync(box!.X + position.X, box.Y + position.Y);
    }

    private async Task<(float X, float Y)> FindNodePointAsync()
    {
        var json = await Page.EvaluateAsync<string>("""
            () => {
                const v = window.__orbTestView;
                const n = v.data.getNodes()[0];
                const c = n.getCenter();
                const t = v.getSettings().render;
                return JSON.stringify({ x: c.x, y: c.y });
            }
            """);

        var point = System.Text.Json.JsonDocument.Parse(json).RootElement;
        return ((float)point.GetProperty("x").GetDouble(),
                (float)point.GetProperty("y").GetDouble());
    }

    private static string ExtractPosition(string positionsJson, string id)
    {
        using var doc = System.Text.Json.JsonDocument.Parse(positionsJson);

        foreach (var element in doc.RootElement.EnumerateArray())
        {
            if (element.GetProperty("id").GetString() == id)
            {
                return element.ToString();
            }
        }

        return "";
    }
}
```

- [ ] **Step 4: Expose the view for testing**

The tests above read `window.__orbTestView`. Add to the end of `initializeOrb` in
`wwwroot/orbGraph.js`, immediately before `return handle;`:

```js
    // Test seam: the browser suite needs to inspect graph state and node positions.
    globalThis.__orbTestView = view;
```

- [ ] **Step 5: Run the browser suite**

Run: `dotnet test tests/Pinknose.Memgraph.Orb.Razor.BrowserTests`
Expected: PASS, 6 tests.

If node hit-testing proves flaky because canvas coordinates differ from page coordinates,
convert through the view's transform inside `FindNodePointAsync` rather than adding waits.

- [ ] **Step 6: Run everything**

```bash
dotnet build Pinknose.Memgraph.Orb.Razor.slnx
dotnet test tests/Pinknose.Memgraph.Orb.Razor.Tests
dotnet test tests/Pinknose.Memgraph.Orb.Razor.BrowserTests
```

Expected: build clean with 0 warnings; both suites pass.

- [ ] **Step 7: Commit**

```bash
git add tests/ Pinknose.Memgraph.Orb.Razor/wwwroot/orbGraph.js Pinknose.Memgraph.Orb.Razor.slnx
git commit -m "test: add Playwright smoke suite for the interop layer"
```

---

## Deferred

Not part of this plan, tracked for later:

- **Source-generated `JsonSerializerContext` + a trimmed WASM sample to verify it.** Task 5
  ships reflection-based serialization; the spec's WASM trim-safety claim is unproven until
  this is done. Highest-priority item on this list.
- NuGet packaging metadata (`PackageId`, description, license, repository URL)
- README beyond its current single heading
- Removing the stray root-level `orb.min.js`, `memgraph-orb-1.0.2.tgz`, and gitignoring `package.json`/`package-lock.json`
- Staging the deletions of `Component1.razor`, `Component1.razor.css`, `ExampleJsInterop.cs`, `exampleJsInterop.js`
- Build-time assertion that the hash constant matches `orb.min.js.sha384`
- Removing the duplicate `<ImportMap />` in the sample's `App.razor`
- `OrbMapView` support
