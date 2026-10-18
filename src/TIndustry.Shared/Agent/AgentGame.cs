using System.Text.Json;
using System.Text.Json.Nodes;

namespace TIndustry.Shared;

/// <summary>
/// The game methods agents call on a <see cref="SimHost"/> (simulation engine, and the client's channel): look
/// (<c>stato</c>, <c>area</c>, <c>trova</c>, <c>eventi</c>), act with the player's commands (<c>piazza</c>,
/// <c>smonta</c>, <c>ruota</c>, <c>sblocca</c>, <c>vendi</c>, <c>potenzia_core</c>), move time (<c>avanza</c>)
/// and read the journal (<c>diario</c>). A command call submits through <see cref="SimHost.Submit"/> and runs the
/// one tick that applies it, so its answer carries the outcome. Every <c>ok:false</c> is an
/// <see cref="AgentException"/>: a <see cref="RefusalCode"/> name or one of <see cref="AgentCodes"/>.
/// </summary>
public sealed class AgentGame
{
    public const int MaxAreaEdge = 64;
    public const int MaxListItems = 200;
    public const int MaxAdvanceTicks = 60 * SimulationClock.TickRate * 10;
    private const int RecentRefusals = 10;
    private const int PlayerId = 0;

    /// <summary>Methods that change the game, its time or the brush: the journal keeps only these.</summary>
    public static readonly IReadOnlySet<string> JournaledMethods = new HashSet<string>(StringComparer.Ordinal)
    {
        "piazza", "smonta", "ruota", "sblocca", "vendi", "potenzia_core", "avanza"
    };

    // Kinds the client places along a drag (World.Input), the plain belts aside.
    private static readonly BuildingKind[] DraggableKinds = [BuildingKind.Junction, BuildingKind.Splitter, BuildingKind.Sorter];

    private static readonly (char Symbol, string Meaning)[] Legend =
    [
        ('C', "Core"), ('?', "cantiere"), ('*', "piano di costruzione"),
        ('^', "nastro verso nord"), ('>', "nastro verso est"), ('v', "nastro verso sud"), ('<', "nastro verso ovest"),
        ('+', "incrocio"), ('Y', "divisore"), ('F', "selezionatore"), ('=', "ponte"),
        ('M', "minatore"), ('N', "minatore avanzato"), ('S', "forno"), ('A', "assemblatore"), ('G', "generatore"),
        ('E', "estrattore"), ('P', "nodo di energia"), ('Q', "nodo di energia T2"), ('L', "lampada"),
        ('#', "parete"), ('~', "acqua profonda"), (',', "acqua bassa"),
        ('i', "giacimento di ferro"), ('r', "giacimento di rame"), ('k', "giacimento di carbone"),
        ('p', "giacimento di piombo"), ('t', "giacimento di titanio"), ('.', "suolo libero")
    ];

    private readonly SimHost host;
    private readonly FactoryContent content;
    private readonly List<(SimCommand Command, Refusal? Refusal, int MoneyDelta)> outcomes = [];
    private readonly List<JsonObject> unreadEvents = [];
    private readonly List<(long Tick, Refusal Refusal)> refusals = [];
    private Direction brush = Direction.East;

    public AgentGame(SimHost host, FactoryContent content)
    {
        this.host = host ?? throw new ArgumentNullException(nameof(host));
        this.content = content ?? throw new ArgumentNullException(nameof(content));
        host.CommandApplied += c => outcomes.Add((c, null, host.LastCommandMoneyDelta));
        host.CommandRejected += OnRejected;
        host.EventRaised += OnEvent;
    }

    public AgentJournal Journal { get; } = new();

    private IGameView View => host.State;

    /// <summary>Runs <paramref name="journal"/> again on <paramref name="host"/> (a new game), at the same ticks.</summary>
    public static AgentGame Replay(AgentJournal journal, SimHost host)
    {
        ArgumentNullException.ThrowIfNull(journal);
        ArgumentNullException.ThrowIfNull(host);
        var game = new AgentGame(host, host.State.Content);
        foreach (var entry in journal.Entries)
        {
            var gap = entry.Tick - host.State.CurrentTick;
            if (gap < 0)
            {
                throw new AgentException(AgentCodes.ParametriNonValidi, $"Diario fuori ordine al tick {entry.Tick}.");
            }

            host.AdvanceTicks(checked((int)gap));
            try
            {
                game.Call(entry.Method, entry.Params);
            }
            catch (AgentException)
            {
                // A refused call was refused the first time too: it still took its tick.
            }
        }

        return game;
    }

    public JsonElement Call(string method, JsonElement @params)
    {
        var p = @params.ValueKind == JsonValueKind.Undefined ? JsonDocument.Parse("{}").RootElement : @params;
        if (p.ValueKind != JsonValueKind.Object)
        {
            throw Bad("I parametri devono essere un oggetto.");
        }

        if (JournaledMethods.Contains(method))
        {
            Journal.Add(View.CurrentTick, method, p);
        }

        JsonObject result = method switch
        {
            "stato" => Stato(),
            "area" => Area(p),
            "trova" => Trova(p),
            "eventi" => Eventi(),
            "piazza" => Piazza(p),
            "smonta" => RunOne(new Remove(PlayerId, Cell(p, "cella"))),
            "ruota" => Ruota(),
            "sblocca" => RunOne(new UnlockStructure(PlayerId, Str(p, "struttura"))),
            "vendi" => Vendi(p),
            "potenzia_core" => RunOne(new UpgradeCore(PlayerId), r => r["livello"] = View.CoreUpgradeLevel),
            "avanza" => Avanza(p),
            "diario" => Diario(p),
            _ => throw Bad($"Metodo sconosciuto: {method}.")
        };
        return JsonSerializer.SerializeToElement(result);
    }

    private JsonObject Stato()
    {
        var resources = new JsonObject();
        foreach (var (item, amount) in View.Wallet.MaterialsSnapshot().OrderBy(kv => kv.Key, StringComparer.Ordinal))
        {
            resources[item] = amount;
        }

        var research = new JsonArray();
        foreach (var id in View.Research.UnlockedIds.Order(StringComparer.Ordinal))
        {
            research.Add(id);
        }

        var recent = new JsonArray();
        foreach (var (tick, refusal) in refusals)
        {
            recent.Add(new JsonObject { ["tick"] = tick, ["codice"] = refusal.Code.ToString() });
        }

        return new JsonObject
        {
            ["tick"] = View.CurrentTick,
            ["soldi"] = View.Wallet.Money,
            ["risorse"] = resources,
            ["energia"] = new JsonObject
            {
                ["accumulata"] = View.PowerBuffer,
                ["capacita"] = View.PowerCapacity,
                ["generazionePerSecondo"] = View.PowerGenerationPerSecond
            },
            ["ricerca"] = research,
            ["edifici"] = new JsonObject
            {
                ["minatori"] = View.Miners.Count,
                ["forni"] = View.Smelters.Count,
                ["assemblatori"] = View.Assemblers.Count,
                ["generatori"] = View.Generators.Count,
                ["estrattori"] = View.Extractors.Count,
                ["nodi"] = View.PowerNodes.Count,
                ["lampade"] = View.Lamps.Count,
                ["nastri"] = View.Belts.Count
            },
            ["costruzione"] = new JsonObject
            {
                ["piani"] = View.PlansOf(PlayerId).Count,
                ["cantieri"] = View.Sites.Count
            },
            ["core"] = new JsonObject
            {
                ["livello"] = View.CoreUpgradeLevel,
                ["consegnati"] = View.CoreDeliveredItems,
                ["venditaAutomatica"] = View.AutoSellAtCore
            },
            ["livello"] = View.ActiveCampaignLevelId,
            ["direzione"] = brush.ToString(),
            ["rifiuti"] = recent
        };
    }

    private JsonObject Area(JsonElement p)
    {
        var terrain = View.Terrain ?? throw Bad("La partita non ha una mappa.");
        var from = Cell(p, "da");
        var to = Cell(p, "a");
        var (x0, x1) = (Math.Min(from.X, to.X), Math.Max(from.X, to.X));
        var (y0, y1) = (Math.Min(from.Y, to.Y), Math.Max(from.Y, to.Y));
        if (x1 - x0 + 1 > MaxAreaEdge || y1 - y0 + 1 > MaxAreaEdge)
        {
            throw Bad($"Area al massimo {MaxAreaEdge}×{MaxAreaEdge} celle.");
        }

        if (!terrain.InBounds(new GridPosition(x0, y0)) || !terrain.InBounds(new GridPosition(x1, y1)))
        {
            throw Bad($"Area fuori dalla mappa ({terrain.Width}×{terrain.Height}).");
        }

        var used = new HashSet<char>();
        var rows = new JsonArray();
        var row = new char[x1 - x0 + 1];
        for (var y = y0; y <= y1; y++)
        {
            for (var x = x0; x <= x1; x++)
            {
                row[x - x0] = Symbol(terrain, new GridPosition(x, y));
                used.Add(row[x - x0]);
            }

            rows.Add(new string(row));
        }

        var legend = new JsonObject();
        foreach (var (symbol, meaning) in Legend.Where(l => used.Contains(l.Symbol)))
        {
            legend[symbol.ToString()] = meaning;
        }

        return new JsonObject
        {
            ["da"] = CellJson(new GridPosition(x0, y0)),
            ["a"] = CellJson(new GridPosition(x1, y1)),
            ["righe"] = rows,
            ["legenda"] = legend
        };
    }

    private char Symbol(TerrainMap terrain, GridPosition cell)
    {
        if (View.CoreTiles.Contains(cell))
        {
            return 'C';
        }

        if (View.TryGetSiteAt(cell, out _))
        {
            return '?';
        }

        if (View.TryGetSpecAt(cell, out var spec, out _))
        {
            return spec.Kind switch
            {
                null => spec.Direction switch
                {
                    Direction.North => '^',
                    Direction.East => '>',
                    Direction.South => 'v',
                    _ => '<'
                },
                BuildingKind.Junction => '+',
                BuildingKind.Splitter => 'Y',
                BuildingKind.Sorter => 'F',
                BuildingKind.Bridge => '=',
                BuildingKind.Miner => 'M',
                BuildingKind.MinerAdvanced => 'N',
                BuildingKind.Smelter => 'S',
                BuildingKind.Assembler => 'A',
                BuildingKind.Generator => 'G',
                BuildingKind.Extractor => 'E',
                BuildingKind.PowerNode => 'P',
                BuildingKind.PowerNodeT2 => 'Q',
                _ => 'L'
            };
        }

        if (View.IsPlannedTile(cell))
        {
            return '*';
        }

        var tile = terrain[cell];
        if (tile.Wall != WallKind.None)
        {
            return '#';
        }

        if (tile.Floor == FloorKind.DeepWater)
        {
            return '~';
        }

        return tile.Deposit switch
        {
            DepositKind.Iron => 'i',
            DepositKind.Copper => 'r',
            DepositKind.Coal => 'k',
            DepositKind.Lead => 'p',
            DepositKind.Titanium => 't',
            _ => tile.Floor == FloorKind.ShallowWater ? ',' : '.'
        };
    }

    private JsonObject Trova(JsonElement p)
    {
        var what = Str(p, "cosa");
        var from = Cell(p, "cella");
        var max = OptInt(p, "max") ?? 10;
        if (max is < 1 or > MaxListItems)
        {
            throw Bad($"max tra 1 e {MaxListItems}.");
        }

        var found = new List<GridPosition>();
        if (DepositFor(what) is { } deposit)
        {
            var terrain = View.Terrain ?? throw Bad("La partita non ha una mappa.");
            for (var y = 0; y < terrain.Height; y++)
            {
                for (var x = 0; x < terrain.Width; x++)
                {
                    if (terrain[x, y].Deposit == deposit)
                    {
                        found.Add(new GridPosition(x, y));
                    }
                }
            }
        }
        else if (content.FindStructure(what) is not null)
        {
            found.AddRange(StructureOrigins().Where(s => s.Id == what).Select(s => s.Origin));
        }
        else
        {
            throw Bad($"'{what}' non è un giacimento né una struttura.");
        }

        var results = new JsonArray();
        foreach (var cell in found.OrderBy(c => Distance(c, from)).ThenBy(c => c.Y).ThenBy(c => c.X).Take(max))
        {
            var item = CellJson(cell);
            item["distanza"] = Distance(cell, from);
            results.Add(item);
        }

        return new JsonObject { ["cosa"] = what, ["trovati"] = found.Count, ["risultati"] = results };
    }

    private static int Distance(GridPosition a, GridPosition b) => Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y);

    /// <summary>A deposit by its kind name (<c>Iron</c>) or its ore id (<c>iron-ore</c>); null otherwise.</summary>
    private static DepositKind? DepositFor(string what)
    {
        foreach (var kind in Enum.GetValues<DepositKind>())
        {
            if (kind != DepositKind.None
                && (string.Equals(kind.ToString(), what, StringComparison.OrdinalIgnoreCase)
                    || TerrainMap.ItemIdForDeposit(kind) == what))
            {
                return kind;
            }
        }

        return null;
    }

    private IEnumerable<(string Id, GridPosition Origin)> StructureOrigins()
    {
        foreach (var m in View.Miners)
        {
            yield return (m.DefinitionId, m.Position);
        }

        foreach (var c in View.Smelters.Concat(View.Assemblers))
        {
            yield return (c.DefinitionId, c.Position);
        }

        foreach (var g in View.Generators)
        {
            yield return (Generator.BuildingId, g.Position);
        }

        foreach (var e in View.Extractors)
        {
            yield return (Extractor.BuildingId, e.Position);
        }

        foreach (var n in View.PowerNodes)
        {
            yield return (n.DefinitionId, n.Position);
        }

        foreach (var l in View.Lamps)
        {
            yield return (Lamp.BuildingId, l.Position);
        }

        foreach (var cell in View.Belts.Keys)
        {
            // A bridge counts once, at its entry.
            if (View.TryGetSpecAt(cell, out var spec, out _) && spec.Origin == cell)
            {
                yield return (spec.StructureId, cell);
            }
        }
    }

    private JsonObject Eventi()
    {
        // shortcut: events pile up until the agent reads them; cap the buffer if sessions ever run unread for hours.
        var skipped = Math.Max(0, unreadEvents.Count - MaxListItems);
        var events = new JsonArray();
        foreach (var e in unreadEvents.Skip(skipped))
        {
            events.Add(e);
        }

        unreadEvents.Clear();
        var result = new JsonObject { ["eventi"] = events };
        if (skipped > 0)
        {
            result["troncato"] = skipped;
        }

        return result;
    }

    private JsonObject Piazza(JsonElement p)
    {
        var structureId = OptStr(p, "tipo") ?? ConveyorOfTier(OptInt(p, "tier") ?? throw Bad("Serve 'tipo' o 'tier'."));
        var kind = KindOf(structureId);
        if (kind is null && content.FindConveyor(structureId) is null)
        {
            throw Bad($"Struttura sconosciuta: {structureId}.");
        }

        var direction = OptStr(p, "direzione") is { } d ? ParseDirection(d) : brush;
        List<GridPosition> cells;
        if (Has(p, "cella"))
        {
            cells = [Cell(p, "cella")];
        }
        else
        {
            if (kind is { } k && !DraggableKinds.Contains(k))
            {
                throw Bad($"'{structureId}' non si piazza in linea.");
            }

            var from = Cell(p, "da");
            cells = [from, .. DragLine.Cells(from, Cell(p, "a"))];
            if (cells.Count > MaxListItems)
            {
                throw Bad($"Linea al massimo di {MaxListItems} celle.");
            }
        }

        var recipe = OptStr(p, "ricetta");
        var filter = OptStr(p, "filtro");
        var commands = cells.Select(cell => kind is { } buildingKind
            ? (SimCommand)new PlaceBuilding(PlayerId, buildingKind, cell, direction, recipe, filter)
            : new PlaceBelt(PlayerId, cell, direction, structureId)).ToList();

        var placed = new JsonArray();
        var refused = new JsonArray();
        Refusal? firstRefusal = null;
        foreach (var (command, refusal, _) in Run(commands))
        {
            var cell = CellJson(CellOf(command)!.Value);
            if (refusal is null)
            {
                cell["direzione"] = direction.ToString();
                placed.Add(cell);
            }
            else
            {
                firstRefusal ??= refusal;
                cell["codice"] = refusal.Code.ToString();
                refused.Add(cell);
            }
        }

        if (placed.Count == 0 && firstRefusal is not null)
        {
            throw Refused(firstRefusal);
        }

        return new JsonObject { ["tick"] = View.CurrentTick, ["celle"] = placed, ["rifiuti"] = refused };
    }

    private string ConveyorOfTier(int tier) =>
        content.Conveyors.FirstOrDefault(c => c.Tier == tier && KindOf(c.Id) is null)?.Id
        ?? throw Bad($"Nessun nastro di livello {tier}.");

    private static BuildingKind? KindOf(string structureId)
    {
        foreach (var kind in Enum.GetValues<BuildingKind>())
        {
            if (BuildSpec.StructureIdFor(kind) == structureId)
            {
                return kind;
            }
        }

        return null;
    }

    private JsonObject Ruota()
    {
        // Like the player's R: turns the brush clockwise; later placements without `direzione` use it.
        brush = DirectionMath.Right(brush);
        return new JsonObject { ["direzione"] = brush.ToString() };
    }

    private JsonObject Vendi(JsonElement p)
    {
        var command = new Sell(PlayerId, Str(p, "oggetto"), Int(p, "quantita"));
        var (_, refusal, moneyDelta) = Run([command])[0];
        if (refusal is not null)
        {
            throw Refused(refusal);
        }

        return new JsonObject { ["tick"] = View.CurrentTick, ["incasso"] = moneyDelta, ["soldi"] = View.Wallet.Money };
    }

    private JsonObject Avanza(JsonElement p)
    {
        var ticks = Int(p, "tick");
        if (ticks is < 0 or > MaxAdvanceTicks)
        {
            throw Bad($"tick tra 0 e {MaxAdvanceTicks}.");
        }

        var before = View.CurrentTick;
        host.AdvanceTicks(ticks);
        return new JsonObject { ["tickPrima"] = before, ["tick"] = View.CurrentTick };
    }

    private JsonObject Diario(JsonElement p)
    {
        var result = new JsonObject();
        if (OptStr(p, "percorso") is { } path)
        {
            Journal.Save(path);
            result["percorso"] = path;
        }

        var entries = Journal.Entries;
        var skipped = Math.Max(0, entries.Count - MaxListItems);
        var items = new JsonArray();
        foreach (var e in entries.Skip(skipped))
        {
            items.Add(new JsonObject
            {
                ["tick"] = e.Tick,
                ["metodo"] = e.Method,
                ["parametri"] = JsonNode.Parse(e.Params.GetRawText())
            });
        }

        result["voci"] = items;
        if (skipped > 0)
        {
            result["troncato"] = skipped;
        }

        return result;
    }

    /// <summary>One command: <c>{tick}</c> (plus <paramref name="fill"/>) when applied, else its refusal.</summary>
    private JsonObject RunOne(SimCommand command, Action<JsonObject>? fill = null)
    {
        var refusal = Run([command])[0].Refusal;
        if (refusal is not null)
        {
            throw Refused(refusal);
        }

        var result = new JsonObject { ["tick"] = View.CurrentTick };
        fill?.Invoke(result);
        return result;
    }

    /// <summary>Submits <paramref name="commands"/> for the next tick, runs it and returns each one's outcome, in order.</summary>
    private List<(SimCommand Command, Refusal? Refusal, int MoneyDelta)> Run(IReadOnlyList<SimCommand> commands)
    {
        var tick = View.CurrentTick + 1;
        var stamped = commands.Select(c => c with { Tick = tick }).ToList();
        foreach (var command in stamped)
        {
            host.Submit(command);
        }

        outcomes.Clear();
        host.AdvanceTicks(1);
        var results = new List<(SimCommand, Refusal?, int)>();
        foreach (var command in stamped)
        {
            // Value equality: another source (the player in the game) may have sent other commands this tick.
            var i = outcomes.FindIndex(o => o.Command.Equals(command));
            if (i < 0)
            {
                throw new InvalidOperationException($"Esito mancante per {command}.");
            }

            results.Add(outcomes[i]);
            outcomes.RemoveAt(i);
        }

        return results;
    }

    private void OnRejected(SimCommand command, Refusal refusal)
    {
        outcomes.Add((command, refusal, 0));
        refusals.Add((command.Tick, refusal));
        if (refusals.Count > RecentRefusals)
        {
            refusals.RemoveAt(0);
        }

        var e = new JsonObject
        {
            ["tick"] = command.Tick,
            ["tipo"] = "rifiuto",
            ["comando"] = command.GetType().Name,
            ["codice"] = refusal.Code.ToString()
        };
        if (CellOf(command) is { } cell)
        {
            e["cella"] = CellJson(cell);
        }

        unreadEvents.Add(e);
    }

    private void OnEvent(SimEvent simEvent)
    {
        var e = new JsonObject { ["tick"] = View.CurrentTick, ["giocatore"] = simEvent.PlayerId };
        switch (simEvent)
        {
            case PlanDropped dropped:
                e["tipo"] = "pianoScartato";
                e["codice"] = dropped.Reason.ToString();
                e["struttura"] = dropped.Plan.Spec.StructureId;
                e["cella"] = CellJson(dropped.Plan.Spec.Origin);
                break;
            case ConstructFinished finished:
                e["tipo"] = "costruito";
                e["cantiere"] = finished.SiteId;
                break;
            case DeconstructFinished taken:
                e["tipo"] = "smontato";
                e["cantiere"] = taken.SiteId;
                break;
            default:
                e["tipo"] = simEvent.GetType().Name;
                break;
        }

        unreadEvents.Add(e);
    }

    private static GridPosition? CellOf(SimCommand command) => command switch
    {
        PlaceBelt belt => belt.Position,
        PlaceBuilding building => building.Origin,
        Remove remove => remove.Tile,
        _ => null
    };

    private static AgentException Refused(Refusal refusal)
    {
        var message = $"Comando rifiutato: {refusal.Code}";
        if (refusal.StructureId is not null)
        {
            message += $" ({refusal.StructureId})";
        }

        if (refusal.MissingMoney > 0)
        {
            message += $", mancano {refusal.MissingMoney} soldi";
        }

        foreach (var item in refusal.MissingItems ?? [])
        {
            message += $", mancano {item.Amount} {item.ItemId}";
        }

        return new AgentException(refusal.Code.ToString(), message + ".");
    }

    private static AgentException Bad(string message) => new(AgentCodes.ParametriNonValidi, message);

    private static JsonObject CellJson(GridPosition cell) => new() { ["x"] = cell.X, ["y"] = cell.Y };

    private static Direction ParseDirection(string text) => text.ToLowerInvariant() switch
    {
        "north" or "nord" => Direction.North,
        "east" or "est" => Direction.East,
        "south" or "sud" => Direction.South,
        "west" or "ovest" => Direction.West,
        _ => throw Bad($"Direzione sconosciuta: {text}.")
    };

    private static bool Has(JsonElement p, string name) =>
        p.TryGetProperty(name, out var value) && value.ValueKind != JsonValueKind.Null;

    private static JsonElement Required(JsonElement p, string name) =>
        p.TryGetProperty(name, out var value) && value.ValueKind != JsonValueKind.Null
            ? value
            : throw Bad($"Manca il parametro '{name}'.");

    private static int Int(JsonElement p, string name) =>
        Required(p, name) is { ValueKind: JsonValueKind.Number } value && value.TryGetInt32(out var number)
            ? number
            : throw Bad($"'{name}' deve essere un numero intero.");

    private static int? OptInt(JsonElement p, string name) => Has(p, name) ? Int(p, name) : null;

    private static string Str(JsonElement p, string name) =>
        Required(p, name) is { ValueKind: JsonValueKind.String } value && value.GetString() is { Length: > 0 } text
            ? text
            : throw Bad($"'{name}' deve essere un testo.");

    private static string? OptStr(JsonElement p, string name) => Has(p, name) ? Str(p, name) : null;

    private static GridPosition Cell(JsonElement p, string name)
    {
        var value = Required(p, name);
        if (value.ValueKind != JsonValueKind.Object)
        {
            throw Bad($"'{name}' deve essere una cella {{x, y}}.");
        }

        return new GridPosition(Int(value, "x"), Int(value, "y"));
    }
}
