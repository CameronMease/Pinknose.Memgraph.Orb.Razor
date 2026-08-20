using System.Text.Json.Serialization;

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

    // Orb's key is "useGPU". CamelCase would render UseGpu as "useGpu" (it lowercases only
    // the leading uppercase run), so the wire name is pinned.
    [JsonPropertyName("useGPU")]
    public bool? UseGpu { get; set; }

    public OrbForceLinks? Links { get; set; }
    public OrbForceManyBody? ManyBody { get; set; }
    public OrbForceCollision? Collision { get; set; }
    public OrbForceAlpha? Alpha { get; set; }
    public OrbForceCentering? Centering { get; set; }
    public OrbForcePositioning? Positioning { get; set; }
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
    public bool? EdgeMidpointRepulsion { get; set; }
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

/// <summary>Mirrors Orb's <c>IForceLayoutPositioning</c> — per-axis pinning forces.</summary>
public sealed class OrbForcePositioning
{
    public OrbForceXPosition? ForceX { get; set; }
    public OrbForceYPosition? ForceY { get; set; }
}

public sealed class OrbForceXPosition
{
    public double? X { get; set; }
    public double? Strength { get; set; }
}

public sealed class OrbForceYPosition
{
    public double? Y { get; set; }
    public double? Strength { get; set; }
}
