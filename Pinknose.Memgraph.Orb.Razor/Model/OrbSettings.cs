using System.Text.Json.Serialization;

namespace Pinknose.Memgraph.Orb.Razor;

/// <summary>Mirrors Orb's <c>IOrbViewSettingsInit</c>. Null members fall back to Orb's defaults.</summary>
/// <remarks>
/// Supply only the sections and properties you care about. Setting the component's
/// <c>Settings</c> parameter back to <see langword="null"/> after having supplied one returns
/// the whole view to Orb's defaults.
/// </remarks>
public sealed class OrbSettings
{
    /// <summary>How the graph is drawn: renderer, frame rate, zoom limits, labels, shadows.</summary>
    public OrbRenderSettings? Render { get; set; }

    /// <summary>Whether the user can drag nodes and zoom the view.</summary>
    public OrbInteractionSettings? Interaction { get; set; }

    /// <summary>Orb's built-in select and hover behaviour.</summary>
    // Orb's key is "strategy"; we expose it as Selection because that is what it controls.
    // Without this attribute the property would serialize as "selection" and Orb would
    // silently ignore every selection setting.
    [JsonPropertyName("strategy")]
    public OrbSelectionSettings? Selection { get; set; }

    /// <summary>Which layout positions the nodes, and its options.</summary>
    /// <remarks>
    /// One of <see cref="OrbForceLayout"/>, <see cref="OrbGridLayout"/>,
    /// <see cref="OrbCircularLayout"/> or <see cref="OrbHierarchicalLayout"/>. Changing the
    /// layout type discards existing positions and lays the graph out again.
    /// </remarks>
    public OrbLayout? Layout { get; set; }

    /// <summary>How long the animated zoom-to-fit takes, in milliseconds. Orb's default is 200.</summary>
    public int? ZoomFitTransitionMs { get; set; }

    /// <summary>Whether nodes can be dragged outside the visible area. Orb's default is false.</summary>
    public bool? IsOutOfBoundsDragEnabled { get; set; }

    /// <summary>Whether node coordinates are rounded to whole numbers. Orb's default is true.</summary>
    /// <remarks>Rounding avoids sub-pixel blurring at the cost of very slightly coarser motion.</remarks>
    public bool? AreCoordinatesRounded { get; set; }
}

/// <summary>How the graph is drawn. Mirrors Orb's renderer settings.</summary>
public sealed class OrbRenderSettings
{
    /// <summary>Which renderer to use. Orb's default is <see cref="OrbRendererType.Canvas"/>.</summary>
    public OrbRendererType? Type { get; set; }

    /// <summary>Frames per second the renderer targets. Orb's default is 60.</summary>
    public double? Fps { get; set; }

    /// <summary>How far the user can zoom out. Orb's default is 0.25.</summary>
    public double? MinZoom { get; set; }

    /// <summary>How far the user can zoom in. Orb's default is 8.</summary>
    public double? MaxZoom { get; set; }

    /// <summary>Padding left around the graph when zooming to fit, as a fraction. Orb's default is 0.2.</summary>
    public double? FitZoomMargin { get; set; }

    /// <summary>Whether labels are drawn at all. Orb's default is true.</summary>
    public bool? LabelsIsEnabled { get; set; }

    /// <summary>Whether labels are drawn during hover and selection. Orb's default is true.</summary>
    public bool? LabelsOnEventIsEnabled { get; set; }

    /// <summary>Whether shadows are drawn. Orb's default is true.</summary>
    /// <remarks>Turning this off ignores the shadow properties on node and edge styles.</remarks>
    public bool? ShadowIsEnabled { get; set; }

    /// <summary>Whether shadows are drawn during hover and selection. Orb's default is true.</summary>
    public bool? ShadowOnEventIsEnabled { get; set; }

    /// <summary>Opacity applied to everything not involved in a hover or selection. Orb's default is 0.3.</summary>
    /// <remarks>This is what dims the rest of the graph to make a selection stand out.</remarks>
    public double? ContextAlphaOnEvent { get; set; }

    /// <summary>Whether that dimming happens at all. Orb's default is true.</summary>
    public bool? ContextAlphaOnEventIsEnabled { get; set; }

    /// <summary>Canvas background colour. Orb's default is none, leaving it transparent.</summary>
    public string? BackgroundColor { get; set; }

    /// <summary>Pixel ratio to render at. Orb's default follows the display.</summary>
    /// <remarks>Set to 1 to trade sharpness on high-density displays for rendering speed.</remarks>
    public double? DevicePixelRatio { get; set; }

    /// <summary>Whether a container that measures zero is tolerated. Orb's default is false.</summary>
    /// <remarks>
    /// Useful when the graph starts inside a hidden or not-yet-sized element, where the
    /// alternative is Orb refusing to render.
    /// </remarks>
    public bool? AreCollapsedContainerDimensionsAllowed { get; set; }
}

/// <summary>What the user is allowed to do with the pointer.</summary>
public sealed class OrbInteractionSettings
{
    /// <summary>Whether nodes can be dragged. Orb's default is true.</summary>
    public bool? IsDragEnabled { get; set; }

    /// <summary>Whether the view can be zoomed and panned. Orb's default is true.</summary>
    public bool? IsZoomEnabled { get; set; }
}

/// <summary>Orb calls this "strategy"; it controls built-in select and hover behaviour.</summary>
/// <remarks>
/// These govern what Orb does on its own. The component's events fire regardless, so turning
/// selection off still lets you handle clicks and render your own highlighting.
/// </remarks>
public sealed class OrbSelectionSettings
{
    /// <summary>Whether clicking selects. Orb's default is true.</summary>
    public bool? IsDefaultSelectEnabled { get; set; }

    /// <summary>Whether hovering highlights. Orb's default is true.</summary>
    public bool? IsDefaultHoverEnabled { get; set; }

    /// <summary>Whether more than one item can be selected at once. Orb's default is false.</summary>
    public bool? IsDefaultMultiSelectEnabled { get; set; }

    /// <summary>Whether selecting a node also selects its edges and their nodes. Orb's default is true.</summary>
    public bool? IsDefaultSelectCascadeEnabled { get; set; }
}
