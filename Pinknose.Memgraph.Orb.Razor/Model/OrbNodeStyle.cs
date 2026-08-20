namespace Pinknose.Memgraph.Orb.Razor;

/// <summary>Mirrors Orb's <c>INodeStyle</c>. Null properties fall back to Orb's defaults.</summary>
/// <remarks>
/// Set only what you want to change. The component merges what you supply over Orb's own
/// default style, so leaving a property null keeps Orb's value for it rather than clearing it.
/// Colours are any CSS colour string.
/// </remarks>
public sealed class OrbNodeStyle
{
    /// <summary>Fill colour of the node.</summary>
    public string? Color { get; set; }

    /// <summary>Fill colour while the pointer is over the node.</summary>
    public string? ColorHover { get; set; }

    /// <summary>Fill colour while the node is selected.</summary>
    public string? ColorSelected { get; set; }

    /// <summary>Colour of the node's outline.</summary>
    public string? BorderColor { get; set; }

    /// <summary>Outline colour while the pointer is over the node.</summary>
    public string? BorderColorHover { get; set; }

    /// <summary>Outline colour while the node is selected.</summary>
    public string? BorderColorSelected { get; set; }

    /// <summary>Thickness of the node's outline, in graph units.</summary>
    public double? BorderWidth { get; set; }

    /// <summary>Outline thickness while the node is selected.</summary>
    public double? BorderWidthSelected { get; set; }

    /// <summary>Colour drawn behind the label text, for legibility over edges and nodes.</summary>
    public string? FontBackgroundColor { get; set; }

    /// <summary>Colour of the label text.</summary>
    public string? FontColor { get; set; }

    /// <summary>Font family of the label, as a CSS font-family value.</summary>
    public string? FontFamily { get; set; }

    /// <summary>Font size of the label.</summary>
    public double? FontSize { get; set; }

    /// <summary>Image drawn in place of the node's shape.</summary>
    /// <remarks>The URL must be reachable from the browser; Orb loads it as an ordinary image.</remarks>
    public string? ImageUrl { get; set; }

    /// <summary>Image drawn while the node is selected.</summary>
    public string? ImageUrlSelected { get; set; }

    /// <summary>Colour of the node's drop shadow.</summary>
    /// <remarks>
    /// Shadows also have to be enabled on the renderer; see
    /// <see cref="OrbRenderSettings.ShadowIsEnabled"/>.
    /// </remarks>
    public string? ShadowColor { get; set; }

    /// <summary>Blur radius of the drop shadow.</summary>
    public double? ShadowSize { get; set; }

    /// <summary>Horizontal offset of the drop shadow.</summary>
    public double? ShadowOffsetX { get; set; }

    /// <summary>Vertical offset of the drop shadow.</summary>
    public double? ShadowOffsetY { get; set; }

    /// <summary>Shape the node is drawn as.</summary>
    public OrbNodeShape? Shape { get; set; }

    /// <summary>Radius of the node, in graph units.</summary>
    /// <remarks>
    /// Also the node's hit area: Orb tests clicks and hovers against this radius, so a node
    /// with a size of zero is both invisible and unclickable.
    /// </remarks>
    public double? Size { get; set; }

    /// <summary>How strongly the node resists being moved by the layout simulation.</summary>
    /// <remarks>
    /// Heavier nodes are pushed around less by their neighbours. Only meaningful for a
    /// force-directed layout.
    /// </remarks>
    public double? Mass { get; set; }

    /// <summary>Draw order. Higher values are drawn on top of lower ones.</summary>
    public double? ZIndex { get; set; }
}
