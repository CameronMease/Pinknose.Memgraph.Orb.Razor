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
        JsonSerializer.Serialize(writer, value, value.GetType(), OrbJson.OptionsWithoutLayoutConverter);

        writer.WriteEndObject();
    }
}
