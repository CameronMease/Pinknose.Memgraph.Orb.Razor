using System.Text.Json;
using System.Text.Json.Serialization;

namespace Pinknose.Memgraph.Orb.Razor;

/// <summary>Writes an enum as its camelCased name, e.g. <c>TriangleDown</c> as "triangleDown".</summary>
// The generic JsonStringEnumConverter<TEnum> is the trim-safe one, but [JsonConverter] cannot
// pass it a naming policy -- attributes take a Type, not a constructed instance. This subclass
// exists to bind that policy so each enum can name its converter by type. Orb's wire values are
// camelCase, and any member whose camelCased name is still wrong for Orb pins itself with
// [JsonStringEnumMemberName], which this converter honours.
internal sealed class CamelCaseEnumConverter<TEnum> : JsonStringEnumConverter<TEnum>
    where TEnum : struct, Enum
{
    public CamelCaseEnumConverter()
        : base(JsonNamingPolicy.CamelCase)
    {
    }
}
