using System.Text.Json;
using System.Text.Json.Serialization;

namespace Pinknose.Memgraph.Orb.Razor;

internal sealed class OrbEdgeLineStyleConverter : JsonConverter<OrbEdgeLineStyle>
{
    public override OrbEdgeLineStyle? Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options)
        => throw new NotSupportedException("Line styles are write-only.");

    public override void Write(Utf8JsonWriter writer, OrbEdgeLineStyle value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteString("type", value.Kind);

        if (value.Pattern is { Count: > 0 })
        {
            writer.WritePropertyName("pattern");
            writer.WriteStartArray();
            foreach (var dash in value.Pattern)
            {
                writer.WriteNumberValue(dash);
            }

            writer.WriteEndArray();
        }

        writer.WriteEndObject();
    }
}
