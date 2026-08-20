using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace Pinknose.Memgraph.Orb.Razor;

/// <summary>
/// Serialization for everything crossing to JavaScript. Only library-owned types appear
/// here, which keeps the consumer's domain types out of the payload. This is a reflection-
/// based (non-source-generated) approach, and WASM publish-trimming safety for this path
/// remains unverified.
/// </summary>
internal static class OrbJson
{
    internal static readonly JsonSerializerOptions Options = Build(includeLayoutConverter: true);

    /// <summary>Used by <see cref="OrbLayoutConverter"/> to write the inner options object
    /// without recursing back into itself.</summary>
    internal static readonly JsonSerializerOptions OptionsWithoutLayoutConverter =
        Build(includeLayoutConverter: false);

    public static string SerializeGraph(OrbGraphPayload payload)
        => JsonSerializer.Serialize(payload, Options);

    public static string SerializeSettings(OrbSettings settings)
        => JsonSerializer.Serialize(settings, Options);

    private static JsonSerializerOptions Build(bool includeLayoutConverter)
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,

            // .NET 8+ requires an explicit resolver before MakeReadOnly() below; without
            // this, JsonSerializerOptions.MakeReadOnly() throws at run time even though
            // the reflection-based (non-source-generated) approach is otherwise unchanged
            // from the spec's documented deviation.
            TypeInfoResolver = new DefaultJsonTypeInfoResolver()
        };

        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        options.Converters.Add(new OrbEdgeLineStyleConverter());

        if (includeLayoutConverter)
        {
            options.Converters.Add(new OrbLayoutConverter());
        }

        options.MakeReadOnly();
        return options;
    }
}
