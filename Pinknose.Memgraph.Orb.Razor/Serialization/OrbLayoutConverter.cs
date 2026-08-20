using System.Text.Json;
using System.Text.Json.Serialization;

namespace Pinknose.Memgraph.Orb.Razor;

/// <summary>Writes Orb's <c>{ type, options }</c> layout shape; anchors live inside options.</summary>
internal sealed class OrbLayoutConverter : JsonConverter<OrbLayout>
{
    public override OrbLayout? Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options)
        => throw new NotSupportedException("Layout settings are write-only.");

    public override void Write(Utf8JsonWriter writer, OrbLayout value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteString("type", value.LayoutType);

        writer.WritePropertyName("options");

        // Written through the generated metadata for the concrete layout type rather than the
        // reflective Serialize(writer, value, Type, options) overload, which the trimmer warns
        // about because it cannot tell which type will show up here. Every concrete layout is
        // registered in OrbJsonContext, so the resolver has metadata for whichever one this is.
        JsonSerializer.Serialize(
            writer,
            value,
            OrbJson.OptionsWithoutLayoutConverter.GetTypeInfo(value.GetType()));

        writer.WriteEndObject();
    }
}
