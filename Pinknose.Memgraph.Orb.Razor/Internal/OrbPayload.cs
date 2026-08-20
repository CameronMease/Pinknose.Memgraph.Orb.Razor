namespace Pinknose.Memgraph.Orb.Razor;

/// <summary>What actually crosses to JavaScript. Consumer types never do.</summary>
internal sealed class OrbGraphPayload
{
    public List<OrbNodePayload> Nodes { get; set; } = [];
    public List<OrbEdgePayload> Edges { get; set; } = [];
}

internal sealed class OrbNodePayload
{
    public required string Id { get; set; }
    public OrbNodeStylePayload? Style { get; set; }
}

internal sealed class OrbEdgePayload
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
internal sealed class OrbNodeStylePayload
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
internal sealed class OrbEdgeStylePayload
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
