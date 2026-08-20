using System.Text.Json.Serialization;

namespace Pinknose.Memgraph.Orb.Razor;

/// <summary>Mirrors Orb's <c>IOrbViewSettingsInit</c>. Null members fall back to Orb's defaults.</summary>
public sealed class OrbSettings
{
    public OrbRenderSettings? Render { get; set; }
    public OrbInteractionSettings? Interaction { get; set; }

    // Orb's key is "strategy"; we expose it as Selection because that is what it controls.
    // Without this attribute the property would serialize as "selection" and Orb would
    // silently ignore every selection setting.
    [JsonPropertyName("strategy")]
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
