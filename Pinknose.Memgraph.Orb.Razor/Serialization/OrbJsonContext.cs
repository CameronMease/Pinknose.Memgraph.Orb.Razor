using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace Pinknose.Memgraph.Orb.Razor;

/// <summary>
/// Source-generated metadata for everything the component sends to JavaScript.
/// </summary>
// Every type listed here is library-owned: the consumer's domain types are projected into
// these payloads first, so they never reach the serializer and never need registering.
//
// Source-generated rather than reflective because the component is expected to run in a
// trimmed WebAssembly publish, where the trimmer removes members nothing statically
// references. A reflective serializer reaches property getters only by reflection, so the
// trimmer cannot see the need for them, cannot prove them live, and warns (IL2026) that it
// might be removing something required. The generated resolver below is that static
// reference: the metadata exists in compiled code the trimmer can follow.
//
// The naming policy and ignore condition are declared here as well as on the options built in
// OrbJson. That is not redundant: the generator bakes property names into the generated
// metadata at compile time using the policy from this attribute, so a policy set only on the
// options would not rename anything.
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(OrbGraphPayload))]
[JsonSerializable(typeof(OrbSettings))]
// The concrete layouts are registered individually because OrbLayoutConverter writes whichever
// one the caller supplied by its runtime type; the abstract base alone would not give the
// generator anything to emit.
[JsonSerializable(typeof(OrbForceLayout))]
[JsonSerializable(typeof(OrbGridLayout))]
[JsonSerializable(typeof(OrbCircularLayout))]
[JsonSerializable(typeof(OrbHierarchicalLayout))]
[JsonSerializable(typeof(OrbNodePayload))]
[JsonSerializable(typeof(OrbEdgePayload))]
internal sealed partial class OrbJsonContext : JsonSerializerContext;

/// <summary>Serialization for everything crossing to JavaScript.</summary>
internal static class OrbJson
{
    internal static readonly JsonSerializerOptions Options = Build(includeLayoutConverter: true);

    /// <summary>Used by <see cref="OrbLayoutConverter"/> to write the inner options object
    /// without recursing back into itself.</summary>
    internal static readonly JsonSerializerOptions OptionsWithoutLayoutConverter =
        Build(includeLayoutConverter: false);

    public static string SerializeGraph(OrbGraphPayload payload)
        => JsonSerializer.Serialize(payload, TypeInfo<OrbGraphPayload>(Options));

    public static string SerializeSettings(OrbSettings settings)
        => JsonSerializer.Serialize(settings, TypeInfo<OrbSettings>(Options));

    // Serialized individually so an update can compare node against node and send only what
    // differs. The comparison must read exactly what gets sent, which is why this shares the same
    // options and the same generated metadata as SerializeGraph rather than being reimplemented.
    public static string SerializeNode(OrbNodePayload payload)
        => JsonSerializer.Serialize(payload, TypeInfo<OrbNodePayload>(Options));

    public static string SerializeEdge(OrbEdgePayload payload)
        => JsonSerializer.Serialize(payload, TypeInfo<OrbEdgePayload>(Options));

    /// <summary>The generated metadata for <typeparamref name="T"/>, bound to these options.</summary>
    // Resolved through the options rather than off OrbJsonContext.Default directly, so the
    // returned metadata carries the converters registered below. Reading it from the context's
    // own options would silently drop them.
    internal static JsonTypeInfo<T> TypeInfo<T>(JsonSerializerOptions options)
        => (JsonTypeInfo<T>)options.GetTypeInfo(typeof(T));

    private static JsonSerializerOptions Build(bool includeLayoutConverter)
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            TypeInfoResolver = OrbJsonContext.Default
        };

        options.Converters.Add(new OrbEdgeLineStyleConverter());

        if (includeLayoutConverter)
        {
            options.Converters.Add(new OrbLayoutConverter());
        }

        options.MakeReadOnly();
        return options;
    }
}
