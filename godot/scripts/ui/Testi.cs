using System.Globalization;
using Godot;
using TIndustry.Shared;

namespace TIndustry.Godot;

/// <summary>
/// Player-visible texts. Every text comes from a key of <c>godot/lingue/testi.csv</c> (columns <c>keys,it,en</c>, Italian
/// default); names of structures, items, recipes, deposits and campaign levels are looked up by id, with the data
/// file's Italian name as fallback; refusal codes of the simulation (<see cref="Refusal"/>) are turned into text here.
/// </summary>
public static class Testi
{
    /// <summary>Languages of the CSV, in the order the settings list them.</summary>
    public static readonly string[] Lingue = ["it", "en"];

    public const string LinguaPredefinita = "it";

    /// <summary>The text of <paramref name="key"/> in the current language (the key itself when missing).</summary>
    public static string T(string key) => TranslationServer.Translate(key);

    /// <summary>The text of <paramref name="key"/> with <c>{0}</c>, <c>{1}</c>… filled in (invariant number format).</summary>
    public static string F(string key, params object?[] args) =>
        string.Format(CultureInfo.InvariantCulture, T(key), args);

    /// <summary><paramref name="key"/>'s text, or <paramref name="fallback"/> when the CSV has no such key.</summary>
    private static string Or(string key, string fallback)
    {
        var text = T(key);
        return text == key ? fallback : text;
    }

    public static string Struttura(string structureId, FactoryContent? content = null) =>
        Or($"struttura.{structureId}.nome", content?.FindStructure(structureId)?.DisplayName ?? structureId);

    public static string Oggetto(string itemId, FactoryContent? content = null) =>
        Or($"oggetto.{itemId}.nome", content?.DisplayName(itemId) ?? itemId);

    /// <summary>Lower-case deposit name ("ferro"), from <c>terrain.deposits</c>.</summary>
    public static string Giacimento(DepositKind kind) =>
        Or($"giacimento.{kind.ToString().ToLowerInvariant()}.nome", kind.ToString().ToLowerInvariant());

    /// <summary>
    /// "Scava: ferro, rame…": the ores <paramref name="minerId"/> digs, from <c>terrain.deposits</c> and
    /// <see cref="FactoryContent.MinerCanDig"/> (empty without content).
    /// </summary>
    public static string Scava(FactoryContent? content, string minerId)
    {
        if (content is null)
        {
            return "";
        }

        var ores = content.Terrain.Deposits.Where(d => content.MinerCanDig(minerId, d.Kind)).Select(d => Giacimento(d.Kind));
        return F("struttura.scava", string.Join(", ", ores));
    }

    public static string LivelloNome(CampaignLevelDefinition level) => Or($"campagna.{level.Id}.nome", level.Name);

    public static string LivelloDescrizione(CampaignLevelDefinition level) =>
        Or($"campagna.{level.Id}.descrizione", level.Description);

    /// <summary>Label of objective <paramref name="index"/> (0-based) of <paramref name="level"/>; key <c>obiettivo.&lt;id&gt;.&lt;index+1&gt;</c>.</summary>
    public static string Obiettivo(CampaignLevelDefinition level, int index, FactoryContent? content = null)
    {
        var objective = level.Objectives![index];
        var key = $"obiettivo.{level.Id}.{index + 1}";
        var text = T(key);
        if (text != key)
        {
            return text;
        }

        if (!string.IsNullOrWhiteSpace(objective.Label))
        {
            return objective.Label!;
        }

        string Item() => string.IsNullOrWhiteSpace(objective.ItemId) ? "?" : Oggetto(objective.ItemId!, content);
        return objective.Type switch
        {
            CampaignObjectiveType.EarnMoney => F("obiettivo.guadagna", objective.Amount),
            CampaignObjectiveType.StockItem => F("obiettivo.magazzino", objective.Amount, Item()),
            CampaignObjectiveType.UnlockResearch => F("obiettivo.sblocca",
                string.IsNullOrWhiteSpace(objective.StructureId) ? "?" : Struttura(objective.StructureId!, content)),
            CampaignObjectiveType.SellItem => F("obiettivo.vendi", objective.Amount, Item()),
            _ => T("obiettivo.generico")
        };
    }

    /// <summary>The level's objectives on one line ("… · …"), or "no objectives".</summary>
    public static string Obiettivi(CampaignLevelDefinition level, FactoryContent? content = null)
    {
        var count = level.Objectives?.Count ?? 0;
        return count == 0
            ? T("obiettivo.nessuno")
            : string.Join(" · ", Enumerable.Range(0, count).Select(i => Obiettivo(level, i, content)));
    }

    public static string Direzione(Direction direction) => direction switch
    {
        Direction.North => T("direzione.nord"),
        Direction.East => T("direzione.est"),
        Direction.South => T("direzione.sud"),
        Direction.West => T("direzione.ovest"),
        _ => "?"
    };

    /// <summary>"$12 · 3× Lastra di ferro": what is missing (money first), empty when nothing is.</summary>
    public static string Mancanze(int money, IReadOnlyList<ResourceAmount>? items, FactoryContent? content)
    {
        var parts = new List<string>();
        if (money > 0)
        {
            parts.Add($"${money}");
        }

        foreach (var entry in items ?? [])
        {
            parts.Add($"{entry.Amount}× {Oggetto(entry.ItemId, content)}");
        }

        return string.Join(" · ", parts);
    }

    /// <summary>"2.5 s" in English, "2,5 s" in Italian: the decimal mark of the chosen language, not of the system.</summary>
    public static string Secondi(double seconds) => Decimale(seconds.ToString("0.#", CultureInfo.InvariantCulture)) + " s";

    /// <summary>A number written with the invariant "." given the decimal mark of the language (key <c>formato.decimale</c>).</summary>
    public static string Decimale(string invariant) => invariant.Replace(".", T("formato.decimale"), StringComparison.Ordinal);

    /// <summary>
    /// Why a save did not load: key <c>salvataggi.errore.&lt;Code&gt;</c> with the map, level or generator it is about.
    /// <paramref name="mapName"/> gives the name of the level that uses a campaign map (the map id when there is none).
    /// </summary>
    public static string Salvataggio(SaveLoadFailure failure, Func<string, string?>? mapName = null)
    {
        var argument = failure.Argument ?? "";
        if (failure.Code is SaveLoadError.MapsFolderMissing or SaveLoadError.CampaignMapMissing
            or SaveLoadError.CampaignMapChanged or SaveLoadError.CampaignMapBadName)
        {
            argument = mapName?.Invoke(argument) ?? argument;
        }

        return F($"salvataggi.errore.{failure.Code}", argument);
    }

    /// <summary>Text of a refusal: key <c>rifiuto.&lt;Code&gt;</c>, filled with its arguments.</summary>
    public static string Rifiuto(Refusal refusal, IGameView? view = null)
    {
        var content = view?.Content;
        switch (refusal.Code)
        {
            case RefusalCode.Locked:
                return F("rifiuto.Locked", Struttura(refusal.StructureId ?? "?", content));
            case RefusalCode.InsufficientResources:
                var missing = Mancanze(refusal.MissingMoney, refusal.MissingItems, content);
                return missing.Length == 0 ? T("rifiuto.InsufficientResources") : F("rifiuto.servono", missing);
            case RefusalCode.CoreAlreadyUpgraded:
                return F("rifiuto.CoreAlreadyUpgraded", view?.CoreUpgradeLevel ?? 1);
            case RefusalCode.SellAllCooldown:
                return F("rifiuto.SellAllCooldown", refusal.WaitSeconds);
            default:
                return T($"rifiuto.{refusal.Code}");
        }
    }
}
