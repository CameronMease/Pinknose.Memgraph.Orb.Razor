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
