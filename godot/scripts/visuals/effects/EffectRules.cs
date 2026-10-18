using System.Globalization;
using System.Text.Json;

namespace TIndustry.Godot;

/// <summary>
/// The pure part of the effect manager: reading and checking <c>godot/effetti/effetti.json</c>, which effects a
/// quality level shows and with how many particles, and the per-screen particle cap. No Godot types: the file is
/// compiled into the test project as a linked file (effects are client-only, so it cannot live in Shared).
/// </summary>
public static class EffectRules
{
    /// <summary>Effects start only inside the view grown by this many cells on each side.</summary>
    public const int ViewMarginCells = 4;

    // Upper bounds of the numeric fields: anything past them is a typo, and they keep every value far from overflow.
    public const int MaxAmount = 4000;
    public const double MaxLifetime = 60;
    public const double MaxSpeed = 10000;
    public const double MaxGravity = 10000;

    private static readonly string[] Fields =
        ["sprite", "numero", "durata", "velocita", "colore", "dispersione", "gravita", "qualita"];

    /// <summary>Most particles alive on screen at once for <paramref name="quality"/>.</summary>
    public static int ParticleCap(EffectsQuality quality) => quality switch
    {
        EffectsQuality.Alti => 4000,
        EffectsQuality.Bassi => 1200,
        _ => 0
    };

    /// <summary>True when <paramref name="quality"/> shows <paramref name="def"/> (never with Spenti).</summary>
    public static bool Allowed(EffectDef def, EffectsQuality quality) =>
        quality != EffectsQuality.Spenti && quality >= def.MinQuality;

    /// <summary>Particles of one burst or emitter of <paramref name="def"/>: all with Alti, half (rounded up) with Bassi.</summary>
    public static int ParticlesFor(EffectDef def, EffectsQuality quality)
    {
        if (!Allowed(def, quality))
        {
            return 0;
        }

        return quality == EffectsQuality.Alti ? def.Amount : (def.Amount + 1) / 2;
    }

    /// <summary>Particles a new request for <paramref name="wanted"/> may take with <paramref name="active"/> already alive.</summary>
    public static int Grant(int wanted, int active, EffectsQuality quality) =>
        Math.Max(0, Math.Min(wanted, ParticleCap(quality) - active));

    /// <summary>
    /// Effects of an <c>effetti.json</c> text by name. Throws <see cref="InvalidDataException"/> with an Italian
    /// message naming the effect and the field for a missing or wrong field.
    /// </summary>
    public static IReadOnlyDictionary<string, EffectDef> Parse(string json)
    {
        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(json, new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip });
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"effetti.json non è un JSON valido: {ex.Message}");
        }

        using (doc)
        {
            if (doc.RootElement.ValueKind != JsonValueKind.Object
                || !doc.RootElement.TryGetProperty("effetti", out var table)
                || table.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidDataException("effetti.json: manca la tabella «effetti».");
            }

            var defs = new Dictionary<string, EffectDef>(StringComparer.Ordinal);
            foreach (var entry in table.EnumerateObject())
            {
                if (defs.ContainsKey(entry.Name))
                {
                    throw new InvalidDataException($"effetti.json: l'effetto «{entry.Name}» è definito due volte.");
                }

                defs[entry.Name] = ParseEffect(entry.Name, entry.Value);
            }

            return defs;
        }
    }

    private static EffectDef ParseEffect(string name, JsonElement e)
    {
        string Where(string field) => $"effetti.json: effetto «{name}», campo «{field}»";

        if (e.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException($"effetti.json: l'effetto «{name}» non è un oggetto.");
        }

        foreach (var field in Fields)
        {
            if (!e.TryGetProperty(field, out _))
            {
                throw new InvalidDataException($"effetti.json: effetto «{name}»: manca il campo «{field}».");
            }
        }

        var sprite = e.GetProperty("sprite").ValueKind == JsonValueKind.String ? e.GetProperty("sprite").GetString() : null;
        if (string.IsNullOrWhiteSpace(sprite))
        {
            throw new InvalidDataException($"{Where("sprite")}: serve il nome di uno sprite.");
        }

        var amount = Number(e, "numero", Where);
        if (amount < 1 || amount > MaxAmount || amount != Math.Floor(amount))
        {
            throw new InvalidDataException($"{Where("numero")}: serve un intero da 1 a {MaxAmount}.");
        }

        var lifetime = Number(e, "durata", Where);
        if (lifetime <= 0 || lifetime > MaxLifetime)
        {
            throw new InvalidDataException($"{Where("durata")}: servono secondi maggiori di 0 e al massimo {MaxLifetime}.");
        }

        var (speedMin, speedMax) = Pair(e, "velocita", Where);
        if (speedMin < 0 || speedMax < speedMin || speedMax > MaxSpeed)
        {
            throw new InvalidDataException($"{Where("velocita")}: serve [minima, massima] con 0 <= minima <= massima <= {MaxSpeed}.");
        }

        var spread = Number(e, "dispersione", Where);
        if (spread is < 0 or > 180)
        {
            throw new InvalidDataException($"{Where("dispersione")}: servono gradi da 0 a 180.");
        }

        var (gravityX, gravityY) = Pair(e, "gravita", Where);
        if (Math.Abs(gravityX) > MaxGravity || Math.Abs(gravityY) > MaxGravity)
        {
            throw new InvalidDataException($"{Where("gravita")}: ogni componente deve stare tra -{MaxGravity} e {MaxGravity}.");
        }
        var (r, g, b, a) = Colour(e.GetProperty("colore"), Where("colore"));

        var qualityText = e.GetProperty("qualita").ValueKind == JsonValueKind.String ? e.GetProperty("qualita").GetString() : null;
        var quality = qualityText switch
        {
            "Bassi" => EffectsQuality.Bassi,
            "Alti" => EffectsQuality.Alti,
            _ => throw new InvalidDataException($"{Where("qualita")}: serve \"Bassi\" o \"Alti\" (livello minimo che lo mostra).")
        };

        return new EffectDef(name, sprite, (int)amount, (float)lifetime, (float)speedMin, (float)speedMax,
            r, g, b, a, (float)spread, (float)gravityX, (float)gravityY, quality);
    }

    private static double Number(JsonElement e, string field, Func<string, string> where)
    {
        var v = e.GetProperty(field);
        if (v.ValueKind != JsonValueKind.Number)
        {
            throw new InvalidDataException($"{where(field)}: serve un numero.");
        }

        return v.GetDouble();
    }

    private static (double, double) Pair(JsonElement e, string field, Func<string, string> where)
    {
        var v = e.GetProperty(field);
        if (v.ValueKind != JsonValueKind.Array || v.GetArrayLength() != 2
            || v[0].ValueKind != JsonValueKind.Number || v[1].ValueKind != JsonValueKind.Number)
        {
            throw new InvalidDataException($"{where(field)}: servono due numeri, come [0, 10].");
        }

        return (v[0].GetDouble(), v[1].GetDouble());
    }

    /// <summary>"#rrggbb" or "#rrggbbaa" to 0..1 channels.</summary>
    private static (float, float, float, float) Colour(JsonElement v, string where)
    {
        var text = v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";
        if (text.Length is not (7 or 9) || text[0] != '#'
            || !uint.TryParse(text.AsSpan(1), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var bits))
        {
            throw new InvalidDataException($"{where}: serve un colore \"#rrggbb\" o \"#rrggbbaa\".");
        }

        if (text.Length == 7)
        {
            bits = (bits << 8) | 0xFF;
        }

        return (((bits >> 24) & 0xFF) / 255f, ((bits >> 16) & 0xFF) / 255f, ((bits >> 8) & 0xFF) / 255f, (bits & 0xFF) / 255f);
    }
}
