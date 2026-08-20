using System.Text.Json.Serialization;

namespace Pinknose.Memgraph.Orb.Razor;

/// <summary>Base for Orb's layout union. Serializes to <c>{ type, options }</c>.</summary>
/// <remarks>
/// Assign one of the derived layouts to <see cref="OrbSettings.Layout"/>. Switching to a
/// different layout type discards the nodes' current positions and lays the graph out again;
/// changing options within the same type does not.
/// </remarks>
public abstract class OrbLayout
{
    /// <summary>The discriminator Orb expects in <c>layout.type</c>.</summary>
    /// <remarks>
    /// Ignored during serialization: <see cref="OrbLayoutConverter"/> already writes this
    /// value into the outer <c>type</c> key. Without <see cref="JsonIgnoreAttribute"/>,
    /// reflection-based serialization would also emit it as a redundant <c>layoutType</c>
    /// key inside <c>options</c>, which Orb does not expect.
    /// </remarks>
    [JsonIgnore]
    public abstract string LayoutType { get; }

    /// <summary>Where the graph sits horizontally within the view.</summary>
    public OrbAnchor? AnchorX { get; set; }

    /// <summary>Where the graph sits vertically within the view.</summary>
    public OrbAnchor? AnchorY { get; set; }
}

/// <summary>A force-directed layout, where nodes repel and edges pull. Orb's default.</summary>
/// <remarks>
/// Wraps d3-force. Positions are simulated rather than computed, so they settle over a few
/// frames and differ slightly between runs unless the simulation is disabled.
/// </remarks>
public sealed class OrbForceLayout : OrbLayout
{
    /// <inheritdoc />
    [JsonIgnore]
    public override string LayoutType => "force";

    /// <summary>Whether nodes keep moving after the layout settles. Orb's default is false.</summary>
    /// <remarks>
    /// With this off, Orb pins nodes where they land, which is what keeps positions stable
    /// across data updates. Turning it on makes the graph react continuously and means a node
    /// may drift after an unrelated change.
    /// </remarks>
    public bool? IsPhysicsEnabled { get; set; }

    /// <summary>Whether adding or removing data restarts the simulation.</summary>
    public bool? IsSimulatingOnDataUpdate { get; set; }

    /// <summary>Whether changing settings restarts the simulation.</summary>
    public bool? IsSimulatingOnSettingsUpdate { get; set; }

    /// <summary>Whether releasing a pinned node restarts the simulation.</summary>
    public bool? IsSimulatingOnUnstick { get; set; }

    /// <summary>Whether to run the simulation on the GPU.</summary>
    /// <remarks>Faster for large graphs, and unavailable in browsers without the support it needs.</remarks>
    // Orb's key is "useGPU". CamelCase would render UseGpu as "useGpu" (it lowercases only
    // the leading uppercase run), so the wire name is pinned.
    [JsonPropertyName("useGPU")]
    public bool? UseGpu { get; set; }

    /// <summary>The spring force along edges.</summary>
    public OrbForceLinks? Links { get; set; }

    /// <summary>The force nodes exert on each other, normally repulsion.</summary>
    public OrbForceManyBody? ManyBody { get; set; }

    /// <summary>The force that stops nodes overlapping.</summary>
    public OrbForceCollision? Collision { get; set; }

    /// <summary>How quickly the simulation cools down and stops.</summary>
    public OrbForceAlpha? Alpha { get; set; }

    /// <summary>The force pulling the whole graph towards a point.</summary>
    public OrbForceCentering? Centering { get; set; }

    /// <summary>Per-axis forces pulling nodes towards a coordinate.</summary>
    public OrbForcePositioning? Positioning { get; set; }
}

/// <summary>Arranges nodes in a regular grid, ignoring edges.</summary>
public sealed class OrbGridLayout : OrbLayout
{
    /// <inheritdoc />
    [JsonIgnore]
    public override string LayoutType => "grid";

    /// <summary>Vertical space between rows.</summary>
    public double? RowGap { get; set; }

    /// <summary>Horizontal space between columns.</summary>
    public double? ColGap { get; set; }
}

/// <summary>Arranges nodes evenly around a circle.</summary>
public sealed class OrbCircularLayout : OrbLayout
{
    /// <inheritdoc />
    [JsonIgnore]
    public override string LayoutType => "circular";

    /// <summary>Radius of the circle.</summary>
    public double? Radius { get; set; }

    /// <summary>Horizontal centre of the circle.</summary>
    public double? CenterX { get; set; }

    /// <summary>Vertical centre of the circle.</summary>
    public double? CenterY { get; set; }
}

/// <summary>Arranges nodes in levels, following edge direction. Suits trees and DAGs.</summary>
public sealed class OrbHierarchicalLayout : OrbLayout
{
    /// <inheritdoc />
    [JsonIgnore]
    public override string LayoutType => "hierarchical";

    /// <summary>Space between neighbouring nodes on the same level.</summary>
    public double? NodeGap { get; set; }

    /// <summary>Space between levels.</summary>
    public double? LevelGap { get; set; }

    /// <summary>Space between separate trees when the graph is disconnected.</summary>
    public double? TreeGap { get; set; }

    /// <summary>Whether levels stack vertically or run horizontally.</summary>
    public OrbLayoutOrientation? Orientation { get; set; }

    /// <summary>Whether to lay levels out in the opposite direction.</summary>
    public bool? Reversed { get; set; }
}

/// <summary>The spring force along edges. Mirrors d3-force's link force.</summary>
public sealed class OrbForceLinks
{
    /// <summary>The length each edge tries to settle at.</summary>
    public double? Distance { get; set; }

    /// <summary>How firmly edges hold that length, from 0 to 1.</summary>
    public double? Strength { get; set; }

    /// <summary>Passes per frame. More is more accurate and slower.</summary>
    public double? Iterations { get; set; }
}

/// <summary>The force every node exerts on every other. Mirrors d3-force's many-body force.</summary>
public sealed class OrbForceManyBody
{
    /// <summary>Negative values repel and spread the graph out; positive values attract.</summary>
    public double? Strength { get; set; }

    /// <summary>Approximation threshold for the Barnes-Hut optimisation. Lower is more accurate and slower.</summary>
    public double? Theta { get; set; }

    /// <summary>Distance below which the force stops growing, which prevents violent nudges between close nodes.</summary>
    public double? DistanceMin { get; set; }

    /// <summary>Distance beyond which nodes stop affecting each other.</summary>
    public double? DistanceMax { get; set; }

    /// <summary>Whether edge midpoints repel as well as nodes, which reduces edge crossings.</summary>
    public bool? EdgeMidpointRepulsion { get; set; }
}

/// <summary>The force that stops nodes overlapping. Mirrors d3-force's collision force.</summary>
public sealed class OrbForceCollision
{
    /// <summary>Radius of each node's exclusion zone.</summary>
    public double? Radius { get; set; }

    /// <summary>How firmly overlaps are pushed apart, from 0 to 1.</summary>
    public double? Strength { get; set; }

    /// <summary>Passes per frame. More is more accurate and slower.</summary>
    public double? Iterations { get; set; }
}

/// <summary>How the simulation cools down. Mirrors d3-force's alpha parameters.</summary>
/// <remarks>
/// Alpha is the simulation's temperature: it starts high, decays each frame, and the
/// simulation stops once it falls below the minimum.
/// </remarks>
public sealed class OrbForceAlpha
{
    /// <summary>Starting temperature.</summary>
    public double? Alpha { get; set; }

    /// <summary>Temperature at which the simulation stops.</summary>
    public double? AlphaMin { get; set; }

    /// <summary>How fast the temperature falls each frame. Higher settles sooner and rougher.</summary>
    public double? AlphaDecay { get; set; }

    /// <summary>Temperature the simulation converges towards; above the minimum, it never stops.</summary>
    public double? AlphaTarget { get; set; }
}

/// <summary>The force pulling the whole graph towards a point.</summary>
public sealed class OrbForceCentering
{
    /// <summary>Horizontal coordinate to centre on.</summary>
    public double? X { get; set; }

    /// <summary>Vertical coordinate to centre on.</summary>
    public double? Y { get; set; }

    /// <summary>How firmly the graph is held there, from 0 to 1.</summary>
    public double? Strength { get; set; }
}

/// <summary>Mirrors Orb's <c>IForceLayoutPositioning</c> — per-axis pinning forces.</summary>
public sealed class OrbForcePositioning
{
    /// <summary>Pulls nodes towards a horizontal coordinate.</summary>
    public OrbForceXPosition? ForceX { get; set; }

    /// <summary>Pulls nodes towards a vertical coordinate.</summary>
    public OrbForceYPosition? ForceY { get; set; }
}

/// <summary>A force pulling nodes towards a horizontal coordinate.</summary>
public sealed class OrbForceXPosition
{
    /// <summary>The coordinate to pull towards.</summary>
    public double? X { get; set; }

    /// <summary>How firmly, from 0 to 1.</summary>
    public double? Strength { get; set; }
}

/// <summary>A force pulling nodes towards a vertical coordinate.</summary>
public sealed class OrbForceYPosition
{
    /// <summary>The coordinate to pull towards.</summary>
    public double? Y { get; set; }

    /// <summary>How firmly, from 0 to 1.</summary>
    public double? Strength { get; set; }
}
