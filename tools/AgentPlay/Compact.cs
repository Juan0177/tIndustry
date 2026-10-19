using System.Text.Json.Nodes;

namespace TIndustry.AgentPlay;

/// <summary>
/// The compact answers, the default of the game tools (<c>dettaglio: true</c> gives the full form). Nothing an agent
/// needs is lost, only repeats and defaults:
/// <list type="bullet">
/// <item>everywhere: no <c>null</c>, no empty list or object (a missing list is an empty one);</item>
/// <item><c>stato</c>: only the building counts above 0, no <c>false</c> flags (<c>rimozione</c>, <c>smontaggio</c>,
/// <c>venditaAutomatica</c>), <c>progresso</c> to two decimals, refusals without their <c>tipo</c>;</item>
/// <item><c>eventi</c>: no <c>giocatore</c> 0 and no site id (<c>cantiere</c>); a run of <c>costruito</c> or
/// <c>smontato</c> equal but for tick and cell (a line built cell by cell) is one entry with <c>celle</c> and the last
/// tick in <c>fino</c> (refusals stay one by one, with their own tick);</item>
/// <item><c>piazza</c>: the direction once (<c>direzione</c>) when every placed cell has the same;</item>
/// <item><c>diario</c> saved to a file: <c>numeroVoci</c> instead of the entries (they are in the file);</item>
/// <item><c>ui</c>: the shortest end of the name that <c>ui {clic}</c> still finds alone, <c>rett</c> as
/// <c>[x, y, larghezza, altezza]</c>, no empty <c>testo</c>.</item>
/// </list>
/// </summary>
public static class Compact
{
    public static void Apply(string tool, JsonObject answer)
    {
        switch (tool)
        {
            case "stato":
                Stato(answer);
                break;
            case "eventi":
                if (answer["eventi"] is JsonArray events)
                {
                    answer["eventi"] = Events(events);
                }

                break;
            case "piazza":
                Piazza(answer);
                break;
            case "diario":
                if (answer.ContainsKey("percorso") && answer["voci"] is JsonArray entries)
                {
                    answer.Remove("voci");
                    answer["numeroVoci"] = entries.Count;
                }

                break;
            case "ui":
                if (answer["controlli"] is JsonArray controls)
                {
                    Ui(controls);
                }

                break;
        }

        Prune(answer);
    }

    /// <summary>Removes nulls, empty lists and empty objects (after their own contents were pruned).</summary>
    public static void Prune(JsonNode? node)
    {
        if (node is JsonObject obj)
        {
            foreach (var (key, value) in obj.ToList())
            {
                Prune(value);
                if (value is null || value is JsonArray { Count: 0 } || value is JsonObject { Count: 0 })
                {
                    obj.Remove(key);
                }
            }
        }
        else if (node is JsonArray array)
        {
            foreach (var item in array)
            {
                Prune(item);
            }
        }
    }

    private static void Stato(JsonObject answer)
    {
        if (answer["edifici"] is JsonObject buildings)
        {
            foreach (var (key, value) in buildings.ToList())
            {
                if (value is JsonValue v && v.TryGetValue(out int count) && count == 0)
                {
                    buildings.Remove(key);
                }
            }
        }

        if (answer["costruzione"] is JsonObject building)
        {
            DropFalse(building["primiPiani"], "rimozione");
            DropFalse(building["primiCantieri"], "smontaggio");
            foreach (var site in building["primiCantieri"] as JsonArray ?? [])
            {
                if (site?["progresso"] is JsonValue p && p.TryGetValue(out double progress))
                {
                    site["progresso"] = Math.Round(progress, 2);
                }
            }
        }

        if (answer["core"] is JsonObject core && IsFalse(core["venditaAutomatica"]))
        {
            core.Remove("venditaAutomatica");
        }

        foreach (var refusal in answer["rifiuti"] as JsonArray ?? [])
        {
            (refusal as JsonObject)?.Remove("tipo");
        }
    }

    /// <summary>Events without defaults, runs of equal builds on different cells merged (<c>celle</c>, <c>fino</c>).</summary>
    public static JsonArray Events(JsonArray events)
    {
        var merged = new List<(JsonObject First, long LastTick, List<JsonNode> Cells)>();
        foreach (var e in events.OfType<JsonObject>())
        {
            var item = (JsonObject)e.DeepClone();
            if (item["giocatore"] is JsonValue g && g.TryGetValue(out int player) && player == 0)
            {
                item.Remove("giocatore");
            }

            item.Remove("cantiere");
            var tick = item["tick"] is JsonValue t && t.TryGetValue(out long value) ? value : 0;
            if (merged.Count > 0 && item["cella"] is { } cell && merged[^1].First["cella"] is not null
                && item["tipo"]?.GetValue<string>() is "costruito" or "smontato" && SameButTickAndCell(merged[^1].First, item))
            {
                merged[^1].Cells.Add(cell.DeepClone());
                merged[^1] = merged[^1] with { LastTick = tick };
                continue;
            }

            merged.Add((item, tick, item["cella"] is { } c ? [c.DeepClone()] : []));
        }

        var result = new JsonArray();
        foreach (var (first, lastTick, cells) in merged)
        {
            if (cells.Count < 2)
            {
                result.Add(first);
                continue;
            }

            var run = new JsonObject { ["tick"] = first["tick"]!.DeepClone(), ["fino"] = lastTick };
            foreach (var (key, value) in first.Where(kv => kv.Key is not ("tick" or "cella")).ToList())
            {
                run[key] = value?.DeepClone();
            }

            run["celle"] = new JsonArray(cells.ToArray());
            result.Add(run);
        }

        return result;
    }

    private static bool SameButTickAndCell(JsonObject a, JsonObject b)
    {
        var keys = a.Select(kv => kv.Key).Where(k => k is not ("tick" or "cella")).ToHashSet(StringComparer.Ordinal);
        return keys.SetEquals(b.Select(kv => kv.Key).Where(k => k is not ("tick" or "cella")))
            && keys.All(k => JsonNode.DeepEquals(a[k], b[k]));
    }

    private static void Piazza(JsonObject answer)
    {
        if (answer["celle"] is not JsonArray { Count: > 0 } cells
            || cells[0]?["direzione"]?.GetValue<string>() is not { } direction
            || !cells.All(c => c?["direzione"] is JsonValue d && d.TryGetValue(out string? s) && s == direction))
        {
            return;
        }

        foreach (var cell in cells.OfType<JsonObject>())
        {
            cell.Remove("direzione");
        }

        answer["direzione"] = direction;
    }

    private static void Ui(JsonArray controls)
    {
        var all = controls.OfType<JsonObject>().Select(c => (
            Name: c["nome"]?.GetValue<string>() ?? "",
            Text: c["testo"]?.GetValue<string>() ?? "",
            Tooltip: c["suggerimento"]?.GetValue<string>() ?? "")).ToList();
        foreach (var control in controls.OfType<JsonObject>())
        {
            var name = control["nome"]?.GetValue<string>() ?? "";
            control["nome"] = ShortName(name, all);
            if (control["rett"] is JsonObject r)
            {
                control["rett"] = new JsonArray(r["x"]?.DeepClone(), r["y"]?.DeepClone(), r["larghezza"]?.DeepClone(), r["altezza"]?.DeepClone());
            }

            if (control["testo"] is JsonValue t && t.TryGetValue(out string? text) && text.Length == 0)
            {
                control.Remove("testo");
            }
        }
    }

    /// <summary>
    /// The shortest end of <paramref name="name"/> (whole path segments) that the bridge's <c>ui {clic}</c> resolves to
    /// this control alone: no other listed control has it as name, as name end, as text or as tooltip (with or without
    /// its key hint). The whole name always works (an exact name wins there).
    /// </summary>
    public static string ShortName(string name, IReadOnlyList<(string Name, string Text, string Tooltip)> all)
    {
        var parts = name.Split('/');
        for (var take = 1; take < parts.Length; take++)
        {
            var end = string.Join('/', parts[^take..]);
            if (all.Count(c => Matches(c, end)) == 1)
            {
                return end;
            }
        }

        return name;
    }

    // Mirrors AgentBridge.FindControl (godot/scripts/dev/AgentBridge.cs).
    private static bool Matches((string Name, string Text, string Tooltip) c, string name)
    {
        static bool Same(string a, string b) => string.Equals(a.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase);
        static string WithoutHotkey(string tooltip) =>
            tooltip.LastIndexOfAny(['[', '(']) is var cut and > 0 ? tooltip[..cut] : tooltip;
        return c.Name == name || c.Name.EndsWith("/" + name, StringComparison.Ordinal) || Same(c.Text, name)
            || (c.Tooltip.Length > 0 && (Same(c.Tooltip, name) || Same(WithoutHotkey(c.Tooltip), name)));
    }

    private static void DropFalse(JsonNode? list, string flag)
    {
        foreach (var item in list as JsonArray ?? [])
        {
            if (item is JsonObject obj && IsFalse(obj[flag]))
            {
                obj.Remove(flag);
            }
        }
    }

    private static bool IsFalse(JsonNode? node) => node is JsonValue v && v.TryGetValue(out bool b) && !b;
}
