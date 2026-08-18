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
