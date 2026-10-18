using System.Text.Json;
using System.Text.Json.Serialization;

namespace TIndustry.Godot;

// No Godot types in this file: it is also compiled into the xUnit project (linked file).

/// <summary>
/// Enum as its name in settings.json, read tolerantly: an unknown name (a future or mistyped value) or a number
/// becomes an undefined value instead of an exception, so the owner's <c>Normalize</c> resets that one field to its
/// default and every other setting in the file survives.
/// </summary>
public sealed class TolerantEnumConverter<T> : JsonConverter<T> where T : struct, Enum
{
    /// <summary>What an unknown name reads as: never a defined member, so Normalize can spot it.</summary>
    public static T Unknown => (T)Enum.ToObject(typeof(T), -1);

    public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.String:
                return Enum.TryParse<T>(reader.GetString(), ignoreCase: false, out var value) && Enum.IsDefined(value)
                    ? value
                    : Unknown;
            case JsonTokenType.Number when reader.TryGetInt32(out var number):
                var parsed = (T)Enum.ToObject(typeof(T), number);
                return Enum.IsDefined(parsed) ? parsed : Unknown;
            default:
                reader.Skip();
                return Unknown;
        }
    }

    public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToString());
}
