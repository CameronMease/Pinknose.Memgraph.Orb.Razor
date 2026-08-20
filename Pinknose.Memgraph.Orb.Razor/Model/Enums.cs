using System.Text.Json.Serialization;

namespace Pinknose.Memgraph.Orb.Razor;

[JsonConverter(typeof(CamelCaseEnumConverter<OrbNodeShape>))]
public enum OrbNodeShape { Circle, Dot, Square, Diamond, Triangle, TriangleDown, Star, Hexagon }

[JsonConverter(typeof(CamelCaseEnumConverter<OrbRendererType>))]
public enum OrbRendererType
{
    Canvas,

    // JsonNamingPolicy.CamelCase would turn "WebGl" into "webGl" — it only lowercases the
    // leading uppercase run, and stops at the 'e'. Orb's RendererType wants "webgl", so the
    // wire value is pinned explicitly rather than renaming the member to "Webgl".
    [JsonStringEnumMemberName("webgl")]
    WebGl
}

[JsonConverter(typeof(CamelCaseEnumConverter<OrbLayoutOrientation>))]
public enum OrbLayoutOrientation { Horizontal, Vertical }

[JsonConverter(typeof(CamelCaseEnumConverter<OrbAnchor>))]
public enum OrbAnchor { Start, Center, End }
