using System.Text;
using System.Text.RegularExpressions;
using TIndustry.Shared;
using Xunit;

namespace TIndustry.Shared.Tests;

/// <summary>
/// The client's texts (<c>godot/lingue/testi.csv</c>, columns <c>keys,it,en</c>): every key has both languages with the
/// same placeholders; every structure, item, recipe, deposit, campaign level, objective and refusal code has its keys;
/// every key the client code names exists; no visible text is left written in the client code.
/// </summary>
public class TestiTests
{
    private static readonly string Root = AppContext.BaseDirectory;

    private static readonly Lazy<Dictionary<string, (string It, string En)>> Csv = new(() =>
    {
        var rows = ParseCsv(File.ReadAllText(Path.Combine(Root, "lingue", "testi.csv"), Encoding.UTF8));
        Assert.Equal(["keys", "it", "en"], rows[0]);
        var keys = new Dictionary<string, (string, string)>(StringComparer.Ordinal);
        foreach (var row in rows.Skip(1))
        {
            Assert.True(row.Count == 3, $"riga con {row.Count} colonne: {string.Join("|", row)}");
            Assert.True(keys.TryAdd(row[0], (row[1], row[2])), $"chiave doppia: {row[0]}");
        }

        return keys;
    });

    [Fact]
    public void EveryKeyHasItalianAndEnglishWithTheSamePlaceholders()
    {
        foreach (var (key, (it, en)) in Csv.Value)
        {
            Assert.False(string.IsNullOrWhiteSpace(it), $"{key}: manca l'italiano");
            Assert.False(string.IsNullOrWhiteSpace(en), $"{key}: manca l'inglese");
            Assert.True(Placeholders(it).SetEquals(Placeholders(en)), $"{key}: segnaposti diversi tra it ed en");
        }
    }

    [Fact]
    public void ContentCampaignAndCodesHaveTheirKeys()
    {
        var content = FactoryContent.Load(Path.Combine(Root, "data", "content.json"));
        var campaign = CampaignCatalog.Load(Path.Combine(Root, "data", "campaign.json"));
        var missing = new List<string>();
        void Need(string key)
        {
            if (!Csv.Value.ContainsKey(key))
            {
                missing.Add(key);
            }
        }

        foreach (var structure in content.Structures)
        {
            Need($"struttura.{structure.Id}.nome");
            Need($"struttura.{structure.Id}.suggerimento");
            Need($"struttura.{structure.Id}.descrizione");
        }

        var items = content.Market.Select(m => m.ItemId)
            .Concat(content.Recipes.SelectMany(r => r.Inputs.Concat(r.Outputs)).Select(a => a.ItemId))
            .Distinct();
        foreach (var item in items)
        {
            Need($"oggetto.{item}.nome");
        }

        foreach (var recipe in content.Recipes)
        {
            Need($"ricetta.{recipe.Id}.nome");
        }

        // The HUD names the Core tier and its drone.
        foreach (var tier in content.CoreTiers)
        {
            Need($"struttura.{tier.Id}.nome");
            Need($"unita.{tier.UnitId}.nome");
        }

        foreach (var deposit in content.Terrain.Deposits)
        {
            Need($"giacimento.{deposit.Kind.ToString().ToLowerInvariant()}.nome");
        }

        foreach (var level in campaign.Levels)
        {
            Need($"campagna.{level.Id}.nome");
            Need($"campagna.{level.Id}.descrizione");
            for (var i = 1; i <= (level.Objectives?.Count ?? 0); i++)
            {
                Need($"obiettivo.{level.Id}.{i}");
            }
        }

        foreach (var code in Enum.GetValues<RefusalCode>())
        {
            Need($"rifiuto.{code}");
        }

        foreach (var code in Enum.GetValues<SaveLoadError>())
        {
            Need($"salvataggi.errore.{code}");
        }

        Assert.Empty(missing);
    }

    [Fact]
    public void ItalianNamesOfTheDataAreTheItalianColumn()
    {
        // The data files keep their Italian names (saves do not change): the CSV must say the same.
        var content = FactoryContent.Load(Path.Combine(Root, "data", "content.json"));
        foreach (var structure in content.Structures)
        {
            Assert.Equal(structure.DisplayName, Csv.Value[$"struttura.{structure.Id}.nome"].It);
        }

        foreach (var item in content.Market)
        {
            Assert.Equal(item.DisplayName, Csv.Value[$"oggetto.{item.ItemId}.nome"].It);
        }

        // Campaign texts too: objectives are keyed by their place in the level (campaign.json gives them no id), so
        // reordering or rewording them in the data without the CSV fails here.
        var campaign = CampaignCatalog.Load(Path.Combine(Root, "data", "campaign.json"));
        foreach (var level in campaign.Levels)
        {
            Assert.Equal(level.Name, Csv.Value[$"campagna.{level.Id}.nome"].It);
            Assert.Equal(level.Description, Csv.Value[$"campagna.{level.Id}.descrizione"].It);
            var objectives = level.Objectives ?? [];
            for (var i = 0; i < objectives.Count; i++)
            {
                Assert.Equal(objectives[i].Label, Csv.Value[$"obiettivo.{level.Id}.{i + 1}"].It);
            }

            Assert.False(Csv.Value.ContainsKey($"obiettivo.{level.Id}.{objectives.Count + 1}"),
                $"{level.Id}: il CSV ha più obiettivi del livello");
        }
    }

    [Fact]
    public void EveryTextSurvivesStringFormatWithItsArguments()
    {
        foreach (var (key, (it, en)) in Csv.Value)
        {
            foreach (var text in new[] { it, en })
            {
                var count = Placeholders(text).Select(p => int.Parse(p[1..^1], System.Globalization.CultureInfo.InvariantCulture) + 1)
                    .DefaultIfEmpty(0).Max();
                var args = Enumerable.Repeat<object?>("x", count).ToArray();
                var ex = Record.Exception(() => string.Format(System.Globalization.CultureInfo.InvariantCulture, text, args));
                Assert.True(ex is null, $"{key}: «{text}» non regge string.Format ({ex?.Message})");
            }
        }
    }

    [Fact]
    public void KeysNamedInTheClientExist()
    {
        var missing = ClientLiterals()
            .Where(l => KeyLike.IsMatch(l.Text) && !Csv.Value.ContainsKey(l.Text))
            .Select(l => $"{l.File}:{l.Line} {l.Text}")
            .ToList();
        Assert.True(missing.Count == 0, "chiavi mancanti nel CSV:\n" + string.Join("\n", missing));
    }

    /// <summary>
    /// A literal is visible text when it holds two words or an Italian accented letter; the exceptions are logs,
    /// errors for developers, the playtests (<c>dev/</c>) and the few literals listed in <see cref="Allowed"/>.
    /// </summary>
    [Fact]
    public void NoVisibleTextIsWrittenInTheClientCode()
    {
        var found = ClientLiterals()
            .Where(l => !l.File.StartsWith("dev", StringComparison.Ordinal))
            .Where(l => !DeveloperOnly.IsMatch(l.LineText))
            .Where(l => LooksVisible(l.Text) && !Allowed.Contains(l.Text))
            .Select(l => $"{l.File}:{l.Line} \"{l.Text}\"")
            .ToList();
        Assert.True(found.Count == 0, "testi scritti nel codice (usa una chiave di testi.csv):\n" + string.Join("\n", found));
    }

    /// <summary>Literals that are the same in every language (technical overlays, formats).</summary>
    private static readonly HashSet<string> Allowed = new(StringComparer.Ordinal)
    {
        "FPS —\nCPU —\nRAM —",
        "FPS {}\nCPU {}%\nRAM {}\nProc {} ms",
        "yyyy-MM-dd HH:mm",
    };

    /// <summary>Lines that never reach the player: logs, exceptions, playtest failures.</summary>
    private static readonly Regex DeveloperOnly = new(
        @"GD\.(Print|PrintErr|PushError|PushWarning)|throw |Exception\(|Fail\(|Debug\.", RegexOptions.Compiled);

    /// <summary>A translation key: dotted lower-case words (file names such as <c>content.json</c> are not).</summary>
    private static readonly Regex KeyLike = new(
        @"^[a-z][a-z0-9-]*(\.[a-zA-Z0-9_-]+)+$(?<!\.(png|json|cs|tres|fnt|gdshader|csv|txt|log|translation|import|svg|wav|ogg))",
        RegexOptions.Compiled);

    private static bool LooksVisible(string text)
    {
        // Interpolation holes and format slots are code, not text.
        var plain = Regex.Replace(text, @"\{[^{}]*\}", " ");
        if (KeyLike.IsMatch(text) || plain.Contains("res://", StringComparison.Ordinal))
        {
            return false;
        }

        return Regex.IsMatch(plain, @"[\p{L}]{2,}\s+[\p{L}]{2,}") || Regex.IsMatch(plain, "[àèéìíòóùúÀÈÉÌÒÙ]");
    }

    private static HashSet<string> Placeholders(string text) =>
        Regex.Matches(text, @"\{\d+\}").Select(m => m.Value).ToHashSet();

    private sealed record Literal(string File, int Line, string LineText, string Text);

    /// <summary>String literals of the client scripts (comments skipped; text of interpolated strings without holes).</summary>
    private static IEnumerable<Literal> ClientLiterals()
    {
        var dir = Path.Combine(Root, "client");
        foreach (var path in Directory.EnumerateFiles(dir, "*.cs", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
        {
            var rel = Path.GetRelativePath(dir, path).Replace('\\', '/');
            var source = File.ReadAllText(path, Encoding.UTF8).Replace("\r\n", "\n");
            var lines = source.Split('\n');
            foreach (var (line, text) in Literals(source))
            {
                // The line before too: a long call often puts its text on the next line.
                yield return new Literal(rel, line + 1, (line > 0 ? lines[line - 1] : "") + "\n" + lines[line], text);
            }
        }
    }

    /// <summary>(0-based line, value) of each string literal; verbatim and interpolated ones too, holes left out.</summary>
    internal static List<(int Line, string Text)> Literals(string s)
    {
        var result = new List<(int, string)>();
        var line = 0;
        var i = 0;
        while (i < s.Length)
        {
            var c = s[i];
            if (c == '\n')
            {
                line++;
                i++;
            }
            else if (c == '/' && i + 1 < s.Length && s[i + 1] == '/')
            {
                while (i < s.Length && s[i] != '\n')
                {
                    i++;
                }
            }
            else if (c == '/' && i + 1 < s.Length && s[i + 1] == '*')
            {
                var end = s.IndexOf("*/", i + 2, StringComparison.Ordinal);
                end = end < 0 ? s.Length : end + 2;
                line += s[i..end].Count(ch => ch == '\n');
                i = end;
            }
            else if (c == '\'')
            {
                // Char literal: '"' or '\'' must not open a string.
                i = s.IndexOf('\'', i + (s[i + 1] == '\\' ? 3 : 2)) + 1;
            }
            else if (c == '"' || ((c == '$' || c == '@') && i + 1 < s.Length && s[i + 1] is '"' or '$' or '@'))
            {
                var verbatim = false;
                var interpolated = false;
                while (s[i] is '$' or '@')
                {
                    verbatim |= s[i] == '@';
                    interpolated |= s[i] == '$';
                    i++;
                }

                i++; // opening quote
                var start = line;
                var text = new StringBuilder();
                var depth = 0;
                while (i < s.Length)
                {
                    var ch = s[i];
                    if (depth > 0)
                    {
                        depth += ch == '{' ? 1 : ch == '}' ? -1 : 0;
                        i++;
                        if (depth == 0)
                        {
                            text.Append("{}");
                        }

                        continue;
                    }

                    if (ch == '\n')
                    {
                        line++;
                    }

                    if (interpolated && ch == '{')
                    {
                        if (i + 1 < s.Length && s[i + 1] == '{')
                        {
                            text.Append('{');
                            i += 2;
                            continue;
                        }

                        depth = 1;
                        i++;
                        continue;
                    }

                    if (interpolated && ch == '}' && i + 1 < s.Length && s[i + 1] == '}')
                    {
                        text.Append('}');
                        i += 2;
                        continue;
                    }

                    if (verbatim && ch == '"')
                    {
                        if (i + 1 < s.Length && s[i + 1] == '"')
                        {
                            text.Append('"');
                            i += 2;
                            continue;
                        }

                        i++;
                        break;
                    }

                    if (!verbatim && ch == '\\')
                    {
                        text.Append(s[i + 1] switch { 'n' => '\n', 't' => '\t', var e => e });
                        i += 2;
                        continue;
                    }

                    if (!verbatim && ch == '"')
                    {
                        i++;
                        break;
                    }

                    text.Append(ch);
                    i++;
                }

                result.Add((start, text.ToString()));
            }
            else
            {
                i++;
            }
        }

        return result;
    }

    [Fact]
    public void LiteralScannerReadsTheCSharpForms()
    {
        var source = "var a = \"uno due\"; // \"commento\"\n/* \"blocco\" */ var b = $\"x {f(\"in\")} y {{z}}\";\n"
            + "var c = @\"a \"\"b\"\"\"; var d = '\"'; var e = \"\\\"q\\\"\";";
        Assert.Equal(
            [(0, "uno due"), (1, "x {} y {z}"), (2, "a \"b\""), (2, "\"q\"")],
            Literals(source));
    }

    /// <summary>RFC 4180: comma separated, double quotes around a field with commas, quotes or line breaks.</summary>
    private static List<List<string>> ParseCsv(string text)
    {
        var rows = new List<List<string>>();
        var row = new List<string>();
        var field = new StringBuilder();
        var quoted = false;
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (quoted)
            {
                if (c == '"' && i + 1 < text.Length && text[i + 1] == '"')
                {
                    field.Append('"');
                    i++;
                }
                else if (c == '"')
                {
                    quoted = false;
                }
                else
                {
                    field.Append(c);
                }
            }
            else if (c == '"')
            {
                quoted = true;
            }
            else if (c == ',')
            {
                row.Add(field.ToString());
                field.Clear();
            }
            else if (c == '\n' || c == '\r')
            {
                if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n')
                {
                    i++;
                }

                row.Add(field.ToString());
                field.Clear();
                rows.Add(row);
                row = [];
            }
            else
            {
                field.Append(c);
            }
        }

        if (field.Length > 0 || row.Count > 0)
        {
            row.Add(field.ToString());
            rows.Add(row);
        }

        return rows;
    }
}
