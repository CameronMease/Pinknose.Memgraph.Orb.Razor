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
