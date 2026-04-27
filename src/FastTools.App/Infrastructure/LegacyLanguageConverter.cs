using System.Text.Json;
using System.Text.Json.Serialization;

namespace FastTools.App.Infrastructure;

/// <summary>
/// Reads the LauncherSettings.Language value, tolerating legacy settings files
/// where the value was serialized as the old AppLanguage enum (integer or
/// PascalCase string such as "ZhCn"/"EnUs").
/// </summary>
internal sealed class LegacyLanguageConverter : JsonConverter<string>
{
    public override string Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return reader.TokenType switch
        {
            JsonTokenType.String => MapLegacy(reader.GetString() ?? string.Empty),
            JsonTokenType.Number when reader.TryGetInt32(out var integer) => MapLegacyOrdinal(integer),
            JsonTokenType.Null => "zh-CN",
            _ => "zh-CN",
        };
    }

    public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value);
    }

    private static string MapLegacy(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "zh-CN";
        }

        return value switch
        {
            "ZhCn" => "zh-CN",
            "EnUs" => "en-US",
            _ => value,
        };
    }

    private static string MapLegacyOrdinal(int ordinal)
    {
        return ordinal switch
        {
            0 => "zh-CN",
            1 => "en-US",
            _ => "zh-CN",
        };
    }
}
