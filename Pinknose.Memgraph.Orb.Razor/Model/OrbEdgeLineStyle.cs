namespace Pinknose.Memgraph.Orb.Razor;

/// <summary>How an edge's line is drawn: solid, dashed, dotted, or a custom dash pattern.</summary>
/// <remarks>
/// Mirrors Orb's <c>IEdgeLineStyle</c> discriminated union. Use the static members rather than
/// constructing one: <see cref="Solid"/>, <see cref="Dashed"/> and <see cref="Dotted"/> are
/// shared instances, and <see cref="Custom"/> builds a pattern.
/// </remarks>
public sealed class OrbEdgeLineStyle
{
    private OrbEdgeLineStyle(string kind, double[]? pattern = null)
    {
        Kind = kind;
        Pattern = pattern;
    }

    /// <summary>The wire value Orb receives: <c>solid</c>, <c>dashed</c>, <c>dotted</c> or <c>custom</c>.</summary>
    public string Kind { get; }

    /// <summary>The dash pattern for a custom style; <see langword="null"/> for the others.</summary>
    /// <remarks>
    /// Alternating lengths of drawn and blank segments, as the HTML canvas
    /// <c>setLineDash</c> takes them.
    /// </remarks>
    public double[]? Pattern { get; }

    /// <summary>An unbroken line. Orb's default.</summary>
    public static OrbEdgeLineStyle Solid { get; } = new("solid");

    /// <summary>A dashed line, using Orb's dash lengths.</summary>
    public static OrbEdgeLineStyle Dashed { get; } = new("dashed");

    /// <summary>A dotted line, using Orb's dot spacing.</summary>
    public static OrbEdgeLineStyle Dotted { get; } = new("dotted");

    /// <summary>A line with your own dash pattern.</summary>
    /// <param name="pattern">
    /// Alternating drawn and blank lengths, as the HTML canvas <c>setLineDash</c> takes them.
    /// A single value produces equal dashes and gaps.
    /// </param>
    /// <returns>A line style carrying that pattern.</returns>
    /// <exception cref="ArgumentException"><paramref name="pattern"/> is empty.</exception>
    /// <example>
    /// <code>
    /// Style = new OrbEdgeStyle { LineStyle = OrbEdgeLineStyle.Custom(6, 3) }
    /// </code>
    /// </example>
    public static OrbEdgeLineStyle Custom(params double[] pattern)
    {
        if (pattern is null || pattern.Length == 0)
        {
            throw new ArgumentException("A custom line style needs at least one dash length.", nameof(pattern));
        }

        return new OrbEdgeLineStyle("custom", pattern);
    }
}
