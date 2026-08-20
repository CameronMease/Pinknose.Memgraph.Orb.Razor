using System.Text.Json.Serialization;

namespace Pinknose.Memgraph.Orb.Razor;

/// <summary>The shape a node is drawn as.</summary>
[JsonConverter(typeof(CamelCaseEnumConverter<OrbNodeShape>))]
public enum OrbNodeShape
{
    /// <summary>A circle. Orb's default.</summary>
    Circle,

    /// <summary>A filled circle with no border.</summary>
    Dot,

    /// <summary>A square.</summary>
    Square,

    /// <summary>A diamond — a square on its corner.</summary>
    Diamond,

    /// <summary>A triangle pointing up.</summary>
    Triangle,

    /// <summary>A triangle pointing down.</summary>
    TriangleDown,

    /// <summary>A five-pointed star.</summary>
    Star,

    /// <summary>A six-sided hexagon.</summary>
    Hexagon
}

/// <summary>Which renderer draws the graph.</summary>
[JsonConverter(typeof(CamelCaseEnumConverter<OrbRendererType>))]
public enum OrbRendererType
{
    /// <summary>The 2D canvas renderer. Orb's default, and the best supported.</summary>
    Canvas,

    /// <summary>The WebGL renderer, which handles larger graphs at the cost of broader support.</summary>
    // JsonNamingPolicy.CamelCase would turn "WebGl" into "webGl" — it only lowercases the
    // leading uppercase run, and stops at the 'e'. Orb's RendererType wants "webgl", so the
    // wire value is pinned explicitly rather than renaming the member to "Webgl".
    [JsonStringEnumMemberName("webgl")]
    WebGl
}

/// <summary>Which way a hierarchical layout stacks its levels.</summary>
[JsonConverter(typeof(CamelCaseEnumConverter<OrbLayoutOrientation>))]
public enum OrbLayoutOrientation
{
    /// <summary>Levels run left to right.</summary>
    Horizontal,

    /// <summary>Levels run top to bottom.</summary>
    Vertical
}

/// <summary>Where a layout anchors the graph along an axis.</summary>
[JsonConverter(typeof(CamelCaseEnumConverter<OrbAnchor>))]
public enum OrbAnchor
{
    /// <summary>Against the low edge — left, or top.</summary>
    Start,

    /// <summary>Centred on the axis.</summary>
    Center,

    /// <summary>Against the high edge — right, or bottom.</summary>
    End
}
