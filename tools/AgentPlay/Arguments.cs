using System.Text.Json;

namespace TIndustry.AgentPlay;

/// <summary>
/// Checks a tool call's arguments against the tool's input schema before the SDK binds them, so a wrong call gets an
/// Italian message that names the parameter (the SDK's own errors do not).
/// </summary>
public static class Arguments
{
    /// <summary>What is wrong with <paramref name="args"/> (unknown name, missing, wrong type), or null when they fit.</summary>
    public static string? Problem(JsonElement schema, IEnumerable<KeyValuePair<string, JsonElement>>? args) =>
        Problem(schema, args?.Select(a => (a.Key, a.Value)).ToList() ?? [], prefix: "");

    // Parameters taken as a free JsonElement: the SDK's schema gives them no type, so it is said here.
    private static readonly Dictionary<string, string[]> FreeTypes = new(StringComparer.Ordinal)
    {
        ["misura"] = ["integer", "string", "null"],
        ["verso"] = ["object", "string", "null"]
    };

    /// <summary><paramref name="schema"/> with a <c>type</c> on the free parameters (<c>misura</c>, <c>verso</c>) that lack one.</summary>
    public static JsonElement WithTypes(JsonElement schema)
    {
        if (!Properties(schema).Any(p => FreeTypes.ContainsKey(p.Name) && !p.Value.TryGetProperty("type", out _)))
        {
            return schema;
        }

        var node = System.Text.Json.Nodes.JsonNode.Parse(schema.GetRawText())!.AsObject();
        foreach (var (name, property) in node["properties"]!.AsObject())
        {
            if (FreeTypes.TryGetValue(name, out var types) && property is System.Text.Json.Nodes.JsonObject p && !p.ContainsKey("type"))
            {
                p["type"] = new System.Text.Json.Nodes.JsonArray(types.Select(t => (System.Text.Json.Nodes.JsonNode)t).ToArray());
            }
        }

        return JsonSerializer.SerializeToElement(node);
    }

    /// <summary>The tool's parameters with their types, for a call that failed in a way the check did not foresee.</summary>
    public static string Summary(string tool, JsonElement schema)
    {
        var required = Required(schema);
        var parts = Properties(schema).Select(p =>
            $"{p.Name} ({TypeName(p.Value)}{(required.Contains(p.Name) ? ", obbligatorio" : "")})");
        return $"Parametri di '{tool}': {string.Join(", ", parts)}.";
    }

    private static string? Problem(JsonElement schema, List<(string Name, JsonElement Value)> args, string prefix)
    {
        var properties = Properties(schema).ToDictionary(p => p.Name, p => p.Value, StringComparer.Ordinal);
        foreach (var (name, _) in args.Where(a => !properties.ContainsKey(a.Name)))
        {
            return properties.Count == 0
                ? $"'{prefix}{name}' non è un parametro: questo strumento non ne ha."
                : $"'{prefix}{name}' non è un parametro: quelli validi sono {string.Join(", ", properties.Keys)}.";
        }

        foreach (var name in Required(schema).Where(r => args.All(a => a.Name != r)))
        {
            return $"manca '{prefix}{name}' ({TypeName(properties[name])}).";
        }

        foreach (var (name, value) in args)
        {
            var property = properties[name];
            if (!Fits(property, value))
            {
                return $"'{prefix}{name}' ha il tipo sbagliato: atteso {TypeName(property)}.";
            }

            // Only an object schema that names its fields (a cell) is checked inside; a free value ("verso": {x,y} or
            // "x,y") is the engine's to read.
            if (value.ValueKind == JsonValueKind.Object && property.TryGetProperty("properties", out _)
                && Problem(property, value.EnumerateObject().Select(p => (p.Name, p.Value)).ToList(), $"{prefix}{name}.") is { } inner)
            {
                return inner;
            }
        }

        return null;
    }

    private static IEnumerable<JsonProperty> Properties(JsonElement schema) =>
        schema.ValueKind == JsonValueKind.Object && schema.TryGetProperty("properties", out var p) && p.ValueKind == JsonValueKind.Object
            ? p.EnumerateObject()
            : [];

    private static HashSet<string> Required(JsonElement schema) =>
        schema.ValueKind == JsonValueKind.Object && schema.TryGetProperty("required", out var r) && r.ValueKind == JsonValueKind.Array
            ? r.EnumerateArray().Select(e => e.GetString() ?? "").ToHashSet(StringComparer.Ordinal)
            : [];

    // The schema's "type": one name or a list (["string", "null"]); none means any value.
    private static string[] Types(JsonElement property) =>
        !property.TryGetProperty("type", out var type) ? []
        : type.ValueKind == JsonValueKind.String ? [type.GetString()!]
        : type.ValueKind == JsonValueKind.Array ? type.EnumerateArray().Select(t => t.GetString() ?? "").ToArray()
        : [];

    private static bool Fits(JsonElement property, JsonElement value)
    {
        var types = Types(property);
        return types.Length == 0 || types.Any(type => type switch
        {
            "string" => value.ValueKind == JsonValueKind.String,
            "integer" => value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out _),
            "number" => value.ValueKind == JsonValueKind.Number,
            "boolean" => value.ValueKind is JsonValueKind.True or JsonValueKind.False,
            "object" => value.ValueKind == JsonValueKind.Object,
            "array" => value.ValueKind == JsonValueKind.Array,
            "null" => value.ValueKind == JsonValueKind.Null,
            _ => true
        });
    }

    private static string TypeName(JsonElement property)
    {
        var names = Types(property).Where(t => t != "null").Select(type => type switch
        {
            "string" => "testo",
            "integer" => "numero intero",
            "number" => "numero",
            "boolean" => "vero o falso",
            "object" => Properties(property).Any() ? $"oggetto {{{string.Join(", ", Properties(property).Select(p => p.Name))}}}" : "oggetto",
            "array" => "lista",
            _ => type
        }).ToList();
        return names.Count == 0 ? "testo o numero" : string.Join(" o ", names);
    }
}
