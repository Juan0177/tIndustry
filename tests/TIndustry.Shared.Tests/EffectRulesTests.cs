using System.Text.RegularExpressions;
using TIndustry.Godot;
using Xunit;

namespace TIndustry.Shared.Tests;

/// <summary>
/// Pure rules of the client effect manager (godot/scripts/visuals/effects/EffectRules.cs, compiled here as a linked
/// file: effects are client-only and must not live in Shared).
/// </summary>
public sealed class EffectRulesTests
{
    private const string Valid = """
        {
          "effetti": {
            "fumo": { "sprite": "fx-fumo", "numero": 9, "durata": 1.5, "velocita": [10, 20], "colore": "#80808080", "dispersione": 30, "gravita": [0, -20], "qualita": "Bassi" },
            "neve": { "sprite": "fx-fiocco", "numero": 4, "durata": 6, "velocita": [5, 8], "colore": "#ffffff", "dispersione": 180, "gravita": [2, 6], "qualita": "Alti" }
          }
        }
        """;

    [Fact]
    public void ValidFileIsRead()
    {
        var defs = EffectRules.Parse(Valid);

        Assert.Equal(2, defs.Count);
        var fumo = defs["fumo"];
        Assert.Equal("fumo", fumo.Name);
        Assert.Equal("fx-fumo", fumo.Sprite);
        Assert.Equal(9, fumo.Amount);
        Assert.Equal(1.5f, fumo.Lifetime);
        Assert.Equal(10f, fumo.SpeedMin);
        Assert.Equal(20f, fumo.SpeedMax);
        Assert.Equal(30f, fumo.Spread);
        Assert.Equal(0f, fumo.GravityX);
        Assert.Equal(-20f, fumo.GravityY);
        Assert.Equal(EffectsQuality.Bassi, fumo.MinQuality);
        Assert.Equal(128 / 255f, fumo.R, 4);
        Assert.Equal(128 / 255f, fumo.A, 4);
        Assert.Equal(1f, defs["neve"].A);
        Assert.Equal(EffectsQuality.Alti, defs["neve"].MinQuality);
    }

    [Theory]
    [InlineData("sprite")]
    [InlineData("numero")]
    [InlineData("durata")]
    [InlineData("velocita")]
    [InlineData("colore")]
    [InlineData("dispersione")]
    [InlineData("gravita")]
    [InlineData("qualita")]
    public void MissingFieldIsAnItalianError(string field)
    {
        var fields = new Dictionary<string, string>
        {
            ["sprite"] = "\"fx-fumo\"", ["numero"] = "9", ["durata"] = "1.5", ["velocita"] = "[10, 20]",
            ["colore"] = "\"#808080\"", ["dispersione"] = "30", ["gravita"] = "[0, -20]", ["qualita"] = "\"Bassi\""
        };
        fields.Remove(field);
        var json = "{ \"effetti\": { \"fumo\": { " + string.Join(", ", fields.Select(f => $"\"{f.Key}\": {f.Value}")) + " } } }";

        var error = Assert.Throws<InvalidDataException>(() => EffectRules.Parse(json));
        Assert.Contains("«fumo»", error.Message);
        Assert.Contains($"manca il campo «{field}»", error.Message);
    }

    [Theory]
    [InlineData("\"numero\": 9", "\"numero\": 0", "numero")]
    [InlineData("\"numero\": 9", "\"numero\": 9.5", "numero")]
    [InlineData("\"numero\": 9", "\"numero\": 4001", "numero")]
    [InlineData("\"numero\": 9", "\"numero\": 1e12", "numero")]
    [InlineData("\"numero\": 9", "\"numero\": \"9\"", "numero")]
    [InlineData("\"durata\": 1.5", "\"durata\": 61", "durata")]
    [InlineData("\"velocita\": [10, 20]", "\"velocita\": [10, 1e9]", "velocita")]
    [InlineData("\"velocita\": [10, 20]", "\"velocita\": [10]", "velocita")]
    [InlineData("\"gravita\": [0, -20]", "\"gravita\": [0, -1e300]", "gravita")]
    [InlineData("\"durata\": 1.5", "\"durata\": -1", "durata")]
    [InlineData("\"velocita\": [10, 20]", "\"velocita\": [20, 10]", "velocita")]
    [InlineData("\"colore\": \"#80808080\"", "\"colore\": \"grigio\"", "colore")]
    [InlineData("\"dispersione\": 30", "\"dispersione\": 270", "dispersione")]
    [InlineData("\"qualita\": \"Bassi\"", "\"qualita\": \"Spenti\"", "qualita")]
    [InlineData("\"sprite\": \"fx-fumo\"", "\"sprite\": \"\"", "sprite")]
    public void InvalidValueIsAnItalianError(string from, string to, string field)
    {
        var error = Assert.Throws<InvalidDataException>(() => EffectRules.Parse(Valid.Replace(from, to)));
        Assert.Contains("«fumo»", error.Message);
        Assert.Contains($"«{field}»", error.Message);
    }

    [Fact]
    public void DuplicateEffectNameIsAnItalianError()
    {
        var json = Valid.Replace("\"neve\":", "\"fumo\":");

        var error = Assert.Throws<InvalidDataException>(() => EffectRules.Parse(json));
        Assert.Contains("«fumo»", error.Message);
        Assert.Contains("due volte", error.Message);
    }

    [Fact]
    public void MissingEffectsTableIsAnError()
    {
        var error = Assert.Throws<InvalidDataException>(() => EffectRules.Parse("{}"));
        Assert.Contains("«effetti»", error.Message);
        Assert.Throws<InvalidDataException>(() => EffectRules.Parse("non è json"));
    }

    [Fact]
    public void QualitySpentiAllowsNoEffect()
    {
        foreach (var def in EffectRules.Parse(Valid).Values)
        {
            Assert.False(EffectRules.Allowed(def, EffectsQuality.Spenti));
            Assert.Equal(0, EffectRules.ParticlesFor(def, EffectsQuality.Spenti));
        }

        Assert.Equal(0, EffectRules.ParticleCap(EffectsQuality.Spenti));
        Assert.Equal(0, EffectRules.Grant(10, 0, EffectsQuality.Spenti));
    }

    [Fact]
    public void MinimumQualityFiltersEffects()
    {
        var defs = EffectRules.Parse(Valid);

        Assert.True(EffectRules.Allowed(defs["fumo"], EffectsQuality.Bassi));
        Assert.True(EffectRules.Allowed(defs["fumo"], EffectsQuality.Alti));
        Assert.False(EffectRules.Allowed(defs["neve"], EffectsQuality.Bassi));
        Assert.True(EffectRules.Allowed(defs["neve"], EffectsQuality.Alti));
        // Bassi: fewer particles (half, rounded up), never zero for an allowed effect.
        Assert.Equal(9, EffectRules.ParticlesFor(defs["fumo"], EffectsQuality.Alti));
        Assert.Equal(5, EffectRules.ParticlesFor(defs["fumo"], EffectsQuality.Bassi));
        Assert.Equal(0, EffectRules.ParticlesFor(defs["neve"], EffectsQuality.Bassi));
    }

    [Fact]
    public void ParticleCapIsRespected()
    {
        Assert.Equal(4000, EffectRules.ParticleCap(EffectsQuality.Alti));
        Assert.Equal(1200, EffectRules.ParticleCap(EffectsQuality.Bassi));

        Assert.Equal(30, EffectRules.Grant(30, 0, EffectsQuality.Alti));
        Assert.Equal(20, EffectRules.Grant(30, 3980, EffectsQuality.Alti));
        Assert.Equal(0, EffectRules.Grant(30, 4000, EffectsQuality.Alti));
        Assert.Equal(0, EffectRules.Grant(30, 4100, EffectsQuality.Alti));
        Assert.Equal(0, EffectRules.Grant(30, 1200, EffectsQuality.Bassi));
        Assert.Equal(0, EffectRules.Grant(0, 0, EffectsQuality.Alti));

        // However many requests arrive, the granted total never passes the cap.
        var active = 0;
        for (var i = 0; i < 500; i++)
        {
            active += EffectRules.Grant(17, active, EffectsQuality.Bassi);
        }

        Assert.Equal(1200, active);
    }

    [Fact]
    public void ShippedEffectsFileIsValidAndItsSpritesExist()
    {
        var root = RepoRoot();
        var defs = EffectRules.Parse(File.ReadAllText(Path.Combine(root, "godot", "effetti", "effetti.json")));

        Assert.Contains("prova", defs.Keys);
        foreach (var def in defs.Values)
        {
            Assert.StartsWith("fx-", def.Sprite);
            Assert.True(File.Exists(Path.Combine(root, "godot", "assets", def.Sprite + ".png")), $"sprite mancante: {def.Sprite}");
        }
    }

    /// <summary>
    /// Every effect the client asks for by name (<c>.Play("…")</c>, <c>.Keep(…, "…", …)</c>, the biome table of
    /// AmbientEffects) is in effetti.json: an unknown name would only warn at run time and draw nothing.
    /// </summary>
    [Fact]
    public void EffectsNamedByTheClientAreDefined()
    {
        var root = RepoRoot();
        var defs = EffectRules.Parse(File.ReadAllText(Path.Combine(root, "godot", "effetti", "effetti.json")));
        var named = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var file in Directory.EnumerateFiles(Path.Combine(root, "godot", "scripts"), "*.cs", SearchOption.AllDirectories))
        {
            foreach (var line in File.ReadLines(file))
            {
                var call = Regex.Match(line, @"\.(?:Play|Keep)\((.*)$");
                var biome = Regex.Match(line, @"^\s*\[""[a-z]+""\] = ""([a-z-]+)""");
                if (call.Success)
                {
                    foreach (Match literal in Regex.Matches(call.Groups[1].Value, @"""([a-z]+(?:-[a-z]+)*)"""))
                    {
                        named.Add(literal.Groups[1].Value);
                    }
                }
                else if (biome.Success && file.EndsWith("AmbientEffects.cs", StringComparison.Ordinal))
                {
                    named.Add(biome.Groups[1].Value);
                }
            }
        }

        Assert.Contains("fumo-forno", named);
        Assert.Contains("neve", named);
        foreach (var name in named)
        {
            Assert.True(defs.ContainsKey(name), $"effetto usato dal client ma non definito: {name}");
        }
    }

    /// <summary>effetti.json is not a Godot resource: the export presets must include it explicitly.</summary>
    [Fact]
    public void ExportPresetsShipTheEffectsFile()
    {
        var presets = File.ReadAllText(Path.Combine(RepoRoot(), "godot", "export_presets.cfg"));
        var filters = Regex.Matches(presets, "^include_filter=\"([^\"]*)\"", RegexOptions.Multiline);

        Assert.NotEmpty(filters);
        foreach (Match m in filters)
        {
            Assert.Contains("effetti/*.json", m.Groups[1].Value);
        }
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "tIndustry.sln")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("tIndustry.sln not found above the test binaries.");
    }
}
