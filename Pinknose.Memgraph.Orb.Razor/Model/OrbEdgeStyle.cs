namespace Pinknose.Memgraph.Orb.Razor;

/// <summary>Mirrors Orb's <c>IEdgeStyle</c>. Null properties fall back to Orb's defaults.</summary>
/// <remarks>
/// Set only what you want to change. The component merges what you supply over Orb's own
/// default style, so leaving a property null keeps Orb's value for it rather than clearing it.
/// Colours are any CSS colour string.
/// </remarks>
public sealed class OrbEdgeStyle
{
    /// <summary>Colour of the line.</summary>
    public string? Color { get; set; }

    /// <summary>Line colour while the pointer is over the edge.</summary>
    public string? ColorHover { get; set; }

    /// <summary>Line colour while the edge is selected.</summary>
    public string? ColorSelected { get; set; }

    /// <summary>Thickness of the line, in graph units.</summary>
    /// <remarks>An edge with a width of zero is not drawn at all.</remarks>
    public double? Width { get; set; }

    /// <summary>Line thickness while the pointer is over the edge.</summary>
    public double? WidthHover { get; set; }

    /// <summary>Line thickness while the edge is selected.</summary>
    public double? WidthSelected { get; set; }

    /// <summary>Size of the arrowhead at the edge's end.</summary>
    public double? ArrowSize { get; set; }

    /// <summary>Colour drawn behind the label text, for legibility over the line.</summary>
    public string? FontBackgroundColor { get; set; }

    /// <summary>Colour of the label text.</summary>
    public string? FontColor { get; set; }

    /// <summary>Font family of the label, as a CSS font-family value.</summary>
    public string? FontFamily { get; set; }

    /// <summary>Font size of the label.</summary>
    public double? FontSize { get; set; }

    /// <summary>Colour of the edge's drop shadow.</summary>
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

    /// <summary>Draw order. Higher values are drawn on top of lower ones.</summary>
    public double? ZIndex { get; set; }

    /// <summary>Whether the line is solid, dashed, dotted, or a custom dash pattern.</summary>
    public OrbEdgeLineStyle? LineStyle { get; set; }
}
