using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace TIndustry.Shared;

/// <summary>
/// The game methods agents call on a <see cref="SimHost"/> (simulation engine, and the client's channel): look
/// (<c>stato</c>, <c>area</c>, <c>trova</c>, <c>eventi</c>), act with the player's commands (<c>piazza</c>,
/// <c>smonta</c>, <c>ruota</c>, <c>sblocca</c>, <c>vendi</c>, <c>potenzia_core</c>), move time (<c>avanza</c>)
/// and save the journal (<c>diario</c>). Commands go through <see cref="SimHost.Submit"/>. With
/// <c>tickPerComando</c> (simulation engine) a command call also runs the tick that applies it, so its answer
/// carries the outcome; without it (game engine, where the frames run the ticks) the call answers
/// <c>inAttesa</c> and <see cref="TryFinishPending"/> gives the outcome after the next tick.
/// Every <c>ok:false</c> is an <see cref="AgentException"/>: a <see cref="RefusalCode"/> name or one of
/// <see cref="AgentCodes"/>; no other exception leaves <see cref="Call"/>.
/// </summary>
public sealed class AgentGame : IDisposable
{
    public const int MaxAreaEdge = 64;
    public const int MaxListItems = 200;
    public const int MaxBufferedEvents = 500;
    public const int MaxAdvanceTicks = 60 * SimulationClock.TickRate * 10;
    private const int RecentRefusals = 10;
    private const int ListedQueueItems = 20;
    private const int PlayerId = 0;

    /// <summary>Methods that change the game, its time or the brush: the journal keeps only these.</summary>
    public static readonly IReadOnlySet<string> JournaledMethods = new HashSet<string>(StringComparer.Ordinal)
    {
        "piazza", "smonta", "ruota", "sblocca", "vendi", "potenzia_core", "avanza"
    };

    // Kinds the client places along a drag (World.Input), the plain belts aside.
    private static readonly BuildingKind[] DraggableKinds = [BuildingKind.Junction, BuildingKind.Splitter, BuildingKind.Sorter];

    private static readonly Regex JournalName = new("^[A-Za-z0-9][A-Za-z0-9_-]{0,59}(\\.json)?\\z", RegexOptions.CultureInvariant);

    // Windows device names: a file named so (any extension) opens the device instead.
    private static readonly Regex ReservedName = new(
        "^(CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])(\\.|\\z)", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    internal static readonly (char Symbol, string Meaning)[] Legend =
    [
        ('C', "Core"), ('?', "cantiere"), ('*', "piano di costruzione"),
        ('^', "nastro verso nord"), ('>', "nastro verso est"), ('v', "nastro verso sud"), ('<', "nastro verso ovest"),
        ('+', "incrocio"), ('Y', "divisore"), ('F', "selezionatore"), ('=', "ponte"),
        ('M', "minatore"), ('N', "minatore avanzato"), ('S', "forno"), ('A', "assemblatore"), ('G', "generatore"),
        ('E', "estrattore"), ('P', "nodo di energia"), ('Q', "nodo di energia T2"), ('L', "lampada"),
        ('!', "struttura senza simbolo"),
        ('#', "parete"), ('~', "acqua profonda"), (',', "acqua bassa"),
        ('i', "giacimento di ferro"), ('r', "giacimento di rame"), ('k', "giacimento di carbone"),
        ('p', "giacimento di piombo"), ('t', "giacimento di titanio"), ('.', "suolo libero")
    ];

    private readonly SimHost host;
    private readonly FactoryContent content;
    private readonly CampaignCatalog? campaign;
    private readonly string? journalDirectory;
    private readonly bool tickPerCommand;
    private readonly List<SimCommand> awaiting = [];
    private readonly List<Outcome> resolved = [];
    private readonly Queue<JsonObject> unreadEvents = new();
    private readonly Queue<JsonObject> refusals = new();
    private readonly Action<SimCommand> onApplied;
    private int droppedEvents;
    private Direction brush = Direction.East;
    private (List<SimCommand> Commands, Func<List<Outcome>, JsonObject> Build)? pending;
    private bool disposed;

    /// <param name="setup">How the game was made (journal header, for <c>rigioca</c>).</param>
    /// <param name="campaign">Catalog of the campaign levels, for the objectives in <c>stato</c>.</param>
    /// <param name="journalDirectory">The only folder <c>diario {nome}</c> writes into (the session's temporary profile).</param>
    /// <param name="tickPerComando">True for the simulation engine: each command call runs its tick.</param>
    public AgentGame(
        SimHost host,
        FactoryContent content,
        AgentGameSetup? setup = null,
        CampaignCatalog? campaign = null,
        string? journalDirectory = null,
        bool tickPerComando = true)
    {
        this.host = host ?? throw new ArgumentNullException(nameof(host));
        this.content = content ?? throw new ArgumentNullException(nameof(content));
        this.campaign = campaign;
        this.journalDirectory = journalDirectory;
        tickPerCommand = tickPerComando;
        Journal = new AgentJournal { Setup = setup is null ? null : setup with { TickPerCommand = tickPerComando } };
        onApplied = c => Resolve(c, null, host.LastCommandMoneyDelta);
        host.CommandApplied += onApplied;
        host.CommandRejected += OnRejected;
        host.EventRaised += OnEvent;
    }

    public AgentJournal Journal { get; }

    public SimHost Host => host;

    /// <summary>
    /// The real game: the client's own placement direction (what the HUD shows and R turns), read by <c>stato</c> and
    /// <c>piazza</c>; with it <see cref="RotateBrush"/> turns the client's. Null: the agent's own brush (simulation).
    /// </summary>
    public Func<Direction>? ReadBrush { get; init; }

    public Action? RotateBrush { get; init; }

    /// <summary>What <see cref="Replay(AgentJournal, SimHost, CampaignCatalog?, string?)"/> could not replay exactly.</summary>
    public IReadOnlyList<string> ReplayWarnings { get; private set; } = [];

    private Direction Brush => ReadBrush?.Invoke() ?? brush;

    /// <summary>Adds an event of the transport (a game loaded, a person at the window) to what <c>eventi</c> answers.</summary>
    public void Note(JsonObject e) => Buffer(e);

    /// <summary>Takes over the unread events of <paramref name="previous"/> (the game of the same session before it changed).</summary>
    public void CarryEventsFrom(AgentGame previous)
    {
        ArgumentNullException.ThrowIfNull(previous);
        foreach (var e in previous.unreadEvents)
        {
            Buffer(e);
        }

        droppedEvents += previous.droppedEvents;
    }

    private IGameView View => host.State;

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        host.CommandApplied -= onApplied;
        host.CommandRejected -= OnRejected;
        host.EventRaised -= OnEvent;
    }

    /// <summary>Makes the game of <paramref name="journal"/>'s header again and replays the journal on it.</summary>
    public static AgentGame Replay(
        AgentJournal journal,
        FactoryContent content,
        CampaignCatalog? campaign = null,
        string? mapsDirectory = null,
        string? journalDirectory = null)
    {
        ArgumentNullException.ThrowIfNull(journal);
        var setup = journal.Setup
            ?? throw new AgentException(AgentCodes.ParametriNonValidi, "Il diario non dice come era fatta la partita.");
        return Replay(journal, setup.CreateHost(content, campaign, mapsDirectory), campaign, journalDirectory);
    }

    /// <summary>Runs <paramref name="journal"/> again on <paramref name="host"/> (a new game), at the same ticks.</summary>
    public static AgentGame Replay(
        AgentJournal journal, SimHost host, CampaignCatalog? campaign = null, string? journalDirectory = null)
    {
        ArgumentNullException.ThrowIfNull(journal);
        ArgumentNullException.ThrowIfNull(host);
        var game = new AgentGame(
            host, host.State.Content, journal.Setup, campaign, journalDirectory, journal.Setup?.TickPerCommand ?? true);
        var warnings = new List<string>(journal.Warnings);
        foreach (var entry in journal.Entries)
        {
            AdvanceTo(host, entry.Tick);
            Tolerated(() => game.TryFinishPending(out _));
            if (entry.Replayable == false)
            {
                warnings.Add($"Tick {entry.Tick}: '{entry.Method}' fatto nel gioco vero, non rigiocabile.");
                continue;
            }

            Tolerated(() => game.Call(entry.Method, entry.Params));
        }

        Tolerated(() => game.TryFinishPending(out _));
        // The real game's time goes on after the last call (esegui, real time): replay it up to the journal's end.
        if (journal.EndTick is { } end && end > host.State.CurrentTick)
        {
            AdvanceTo(host, end);
            Tolerated(() => game.TryFinishPending(out _));
        }

        game.Journal.Warnings.AddRange(warnings);
        game.ReplayWarnings = warnings;
        return game;
    }

    private static void AdvanceTo(SimHost host, long tick)
    {
        var gap = tick - host.State.CurrentTick;
        if (gap is < 0 or > int.MaxValue)
        {
            throw new AgentException(AgentCodes.ParametriNonValidi, $"Diario fuori ordine al tick {tick}.");
        }

        host.AdvanceTicks((int)gap);
    }

    /// <summary>A refused call was refused the first time too (it still took its tick); an internal error is not.</summary>
    private static void Tolerated(Action call)
    {
        try
        {
            call();
        }
        catch (AgentException ex) when (ex.Code != AgentCodes.ErroreInterno)
        {
        }
    }

    /// <summary>One request as a response line: never throws.</summary>
    public AgentResponse Handle(AgentRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        try
        {
            return AgentResponse.Success(request.Id, Call(request.Method, request.Params));
        }
        catch (AgentException ex)
        {
            if (ex.Detail is not null)
            {
                LastInternalError = ex.Detail;
            }

            return AgentResponse.Failure(request.Id, ex);
        }
    }

    /// <summary>Full text (stack included) of the last <c>ErroreInterno</c> answered by <see cref="Handle"/>, for the transport's log.</summary>
    public string? LastInternalError { get; private set; }

    public JsonElement Call(string method, JsonElement @params)
    {
        var p = @params.ValueKind == JsonValueKind.Undefined ? AgentProtocol.EmptyObject : @params;
        var tick = View.CurrentTick;
        var journal = JournaledMethods.Contains(method);
        try
        {
            if (p.ValueKind != JsonValueKind.Object)
            {
                throw Bad("I parametri devono essere un oggetto.");
            }

            // The brush is not journaled in the real game (the player's R): a line keeps the direction it used.
            if (method == "piazza" && !Has(p, "direzione"))
            {
                var withDirection = JsonNode.Parse(p.GetRawText())!.AsObject();
                withDirection["direzione"] = DirectionName(Brush);
                p = JsonSerializer.SerializeToElement(withDirection);
            }

            var result = JsonSerializer.SerializeToElement(Dispatch(method, p));
            if (journal)
            {
                Journal.Add(tick, method, p);
            }

            return result;
        }
        catch (AgentException ex)
        {
            // Bad parameters change nothing; a refused command still took its tick.
            if (journal && ex.Code != AgentCodes.ParametriNonValidi)
            {
                Journal.Add(tick, method, p);
            }

            throw;
        }
        catch (Exception ex)
        {
            if (journal && View.CurrentTick != tick)
            {
                Journal.Add(tick, method, p);
            }

            throw Internal(ex);
        }
    }

    /// <summary>
    /// Without <c>tickPerComando</c>: the outcome of the last command call once its tick has run (true), or false while
    /// it has not. Throws like <see cref="Call"/> (the refusal code) when the command was refused.
    /// </summary>
    public bool TryFinishPending(out JsonElement result)
    {
        result = default;
        if (pending is not { } call || !call.Commands.All(c => resolved.Any(o => o.Command.Equals(c))))
        {
            return false;
        }

        pending = null;
        try
        {
            result = JsonSerializer.SerializeToElement(Finish(call.Commands, call.Build));
            return true;
        }
        catch (AgentException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw Internal(ex);
        }
    }

    /// <summary>Short text for the agent; the full exception (stack included) in <see cref="AgentException.Detail"/> for logs.</summary>
    private static AgentException Internal(Exception ex) =>
        new(AgentCodes.ErroreInterno, $"{ex.GetType().Name}: {ex.Message}") { Detail = ex.ToString() };

    private JsonObject Dispatch(string method, JsonElement p) => method switch
    {
        "stato" => Stato(),
        "area" => Area(p),
        "trova" => Trova(p),
        "eventi" => Eventi(),
        "catalogo" => Catalogo(),
        "piazza" => Piazza(p),
        "smonta" => Single(new Remove(PlayerId, MapCell(p, "cella"))),
        "ruota" => Ruota(),
        "sblocca" => Single(new UnlockStructure(PlayerId, Str(p, "struttura"))),
        "vendi" => Command(
            [new Sell(PlayerId, Str(p, "oggetto"), Int(p, "quantita"))],
            o => Accepted(o[0], r =>
            {
                r["incasso"] = o[0].MoneyDelta;
                r["soldi"] = View.Wallet.Money;
            })),
        "potenzia_core" => Single(new UpgradeCore(PlayerId), r => r["livello"] = View.CoreUpgradeLevel),
        "avanza" => Avanza(p),
        "diario" => Diario(p),
        _ => throw Bad($"Metodo sconosciuto: {method}.")
    };

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

        var logistics = View.Belts.Values.GroupBy(b => b.Kind).ToDictionary(g => g.Key, g => g.Count());
        var plans = View.PlansOf(PlayerId);
        var result = new JsonObject
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
                ["nastri"] = logistics.GetValueOrDefault(LogisticsKind.Belt),
                ["incroci"] = logistics.GetValueOrDefault(LogisticsKind.Junction),
                ["divisori"] = logistics.GetValueOrDefault(LogisticsKind.Splitter),
                ["selezionatori"] = logistics.GetValueOrDefault(LogisticsKind.Sorter),
                // Each bridge has two end cells.
                ["ponti"] = logistics.GetValueOrDefault(LogisticsKind.Bridge) / 2
            },
            ["costruzione"] = new JsonObject
            {
                ["piani"] = plans.Count,
                ["cantieri"] = View.Sites.Count,
                ["primiPiani"] = Array(plans.Take(ListedQueueItems).Select(plan =>
                {
                    var item = CellJson(plan.Spec.Origin);
                    item["struttura"] = plan.Spec.StructureId;
                    item["rimozione"] = plan.Breaking;
                    return item;
                })),
                ["primiCantieri"] = Array(View.Sites.Take(ListedQueueItems).Select(site =>
                {
                    var item = CellJson(site.Spec.Origin);
                    item["struttura"] = site.Spec.StructureId;
                    item["progresso"] = site.Progress;
                    item["smontaggio"] = site.Deconstructing;
                    return item;
                }))
            },
            ["core"] = new JsonObject
            {
                ["livello"] = View.CoreUpgradeLevel,
                ["consegnati"] = View.CoreDeliveredItems,
                ["venditaAutomatica"] = View.AutoSellAtCore
            },
            ["livelloCampagna"] = View.ActiveCampaignLevelId,
            ["direzione"] = DirectionName(Brush),
            ["rifiuti"] = Array(refusals),
            ["impronta"] = host.Checksum().ToString("x16", System.Globalization.CultureInfo.InvariantCulture)
        };

        // Where to start: the Core's lowest corner (smallest y, then x) and the map's edges.
        if (View.CoreTiles.Count > 0)
        {
            var corner = View.CoreTiles.OrderBy(t => t.Y).ThenBy(t => t.X).First();
            result["core"]!["cella"] = CellJson(corner);
        }

        if (View.Terrain is { } terrain)
        {
            result["mappa"] = new JsonObject { ["larghezza"] = terrain.Width, ["altezza"] = terrain.Height };
        }

        if (View.ActiveCampaignLevelId is { } levelId && campaign?.Find(levelId) is { } level)
        {
            result["obiettivi"] = Array((level.Objectives ?? []).Select(o =>
            {
                var current = CampaignProgress.GetObjectiveCurrent(o, View.Wallet, View.Session, View.Research);
                var target = CampaignProgress.GetObjectiveTarget(o);
                var item = new JsonObject { ["tipo"] = ObjectiveName(o.Type) };
                if (o.ItemId is not null)
                {
                    item["oggetto"] = o.ItemId;
                }

                if (o.StructureId is not null)
                {
                    item["struttura"] = o.StructureId;
                }

                item["attuale"] = current;
                item["richiesto"] = target;
                item["fatto"] = current >= target;
                return item;
            }));
            result["livelloCompletato"] =
                CampaignProgress.AreAllObjectivesComplete(level, View.Wallet, View.Session, View.Research);
        }

        return result;
    }

    private static string ObjectiveName(CampaignObjectiveType type) => type switch
    {
        CampaignObjectiveType.EarnMoney => "guadagna",
        CampaignObjectiveType.StockItem => "accumula",
        CampaignObjectiveType.UnlockResearch => "ricerca",
        CampaignObjectiveType.SellItem => "vendi",
        _ => type.ToString()
    };

    private JsonObject Area(JsonElement p)
    {
        var terrain = Terrain();
        var from = Cell(p, "da");
        var to = Cell(p, "a");
        var (x0, x1) = (Math.Min(from.X, to.X), Math.Max(from.X, to.X));
        var (y0, y1) = (Math.Min(from.Y, to.Y), Math.Max(from.Y, to.Y));
        if ((long)x1 - x0 + 1 > MaxAreaEdge || (long)y1 - y0 + 1 > MaxAreaEdge)
        {
            throw Bad($"Area al massimo {MaxAreaEdge}×{MaxAreaEdge} celle.");
        }

        if (!terrain.InBounds(new GridPosition(x0, y0)) || !terrain.InBounds(new GridPosition(x1, y1)))
        {
            throw Bad($"Area fuori dalla mappa ({terrain.Width}×{terrain.Height}).");
        }

        // One pass over the buildings and sites instead of a lookup per cell.
        var buildings = new Dictionary<GridPosition, char>();
        foreach (var (id, kind, origin, direction) in Buildings())
        {
            foreach (var tile in View.Footprint(new BuildSpec(id, kind, origin, direction)))
            {
                buildings[tile] = KindSymbol(kind);
            }
        }

        var sites = View.Sites.SelectMany(s => s.Tiles).ToHashSet();
        var withItems = Has(p, "oggetti") && Required(p, "oggetti").ValueKind == JsonValueKind.True;
        var used = new HashSet<char>();
        var rows = new JsonArray();
        var shapes = new List<JsonObject>();
        var items = new List<JsonObject>();
        var row = new char[x1 - x0 + 1];
        for (var y = y0; y <= y1; y++)
        {
            for (var x = x0; x <= x1; x++)
            {
                var cell = new GridPosition(x, y);
                var symbol = Symbol(terrain, cell, buildings, sites);
                row[x - x0] = symbol;
                used.Add(symbol);
                if (!View.Belts.TryGetValue(cell, out var belt) || symbol == 'C')
                {
                    continue;
                }

                if (belt.Kind == LogisticsKind.Belt && BeltShape(cell) is { } shape)
                {
                    shapes.Add(shape);
                }

                if (withItems && belt.Items.Count > 0)
                {
                    var item = CellJson(cell);
                    item["n"] = belt.Items.Count;
                    item["tipi"] = new JsonArray(belt.Items.Select(i => i.ItemId).Distinct().Order(StringComparer.Ordinal)
                        .Select(id => (JsonNode)JsonValue.Create(id)!).ToArray());
                    items.Add(item);
                }
            }

            rows.Add(new string(row));
        }

        var legend = new JsonObject();
        foreach (var (symbol, meaning) in Legend.Where(l => used.Contains(l.Symbol)))
        {
            legend[symbol.ToString()] = meaning;
        }

        var result = new JsonObject
        {
            ["da"] = CellJson(new GridPosition(x0, y0)),
            ["a"] = CellJson(new GridPosition(x1, y1)),
            ["righe"] = rows,
            ["legenda"] = legend
        };
        PutNewest(result, "forme", shapes, newest: false, truncatedKey: "formeTroncate");
        if (withItems)
        {
            PutNewest(result, "oggetti", items, newest: false, truncatedKey: "oggettiTroncati");
        }

        return result;
    }

    /// <summary>
    /// The belt's drawn shape when it is not straight (straight is the default and is left out): curva, t, or a side
    /// join (unione-sinistra / unione-destra: the dot block; unione-doppia: belts on both sides; unione-edificio-doppia:
    /// both sides with a building); <c>ingresso</c> is the heading of the item coming in.
    /// </summary>
    private JsonObject? BeltShape(GridPosition cell)
    {
        var shape = View.GetBeltShape(cell);
        if (shape == TIndustry.Shared.BeltShape.Straight)
        {
            return null;
        }

        var item = CellJson(cell);
        item["forma"] = shape switch
        {
            TIndustry.Shared.BeltShape.Corner => "curva",
            TIndustry.Shared.BeltShape.T => "t",
            TIndustry.Shared.BeltShape.JoinLeft => "unione-sinistra",
            TIndustry.Shared.BeltShape.JoinRight => "unione-destra",
            TIndustry.Shared.BeltShape.JoinBoth => "unione-doppia",
            TIndustry.Shared.BeltShape.JoinBuildingBoth => "unione-edificio-doppia",
            _ => shape.ToString()
        };
        if (View.TryGetBeltIncomingDirection(cell, out var incoming))
        {
            item["ingresso"] = DirectionName(incoming);
        }

        return item;
    }

    private char Symbol(TerrainMap terrain, GridPosition cell, Dictionary<GridPosition, char> buildings, HashSet<GridPosition> sites)
    {
        if (View.CoreTiles.Contains(cell))
        {
            return 'C';
        }

        if (sites.Contains(cell))
        {
            return '?';
        }

        if (View.Belts.TryGetValue(cell, out var belt))
        {
            return belt.Kind switch
            {
                LogisticsKind.Belt => belt.Direction switch
                {
                    Direction.North => '^',
                    Direction.East => '>',
                    Direction.South => 'v',
                    Direction.West => '<',
                    _ => '!'
                },
                LogisticsKind.Junction => KindSymbol(BuildingKind.Junction),
                LogisticsKind.Splitter => KindSymbol(BuildingKind.Splitter),
                LogisticsKind.Sorter => KindSymbol(BuildingKind.Sorter),
                LogisticsKind.Bridge => KindSymbol(BuildingKind.Bridge),
                _ => '!'
            };
        }

        if (buildings.TryGetValue(cell, out var symbol))
        {
            return symbol;
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

    internal static char KindSymbol(BuildingKind kind) => kind switch
    {
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
        BuildingKind.Lamp => 'L',
        _ => '!'
    };

    private JsonObject Trova(JsonElement p)
    {
        var what = Str(p, "cosa");
        var from = MapCell(p, "cella");
        var max = OptInt(p, "max") ?? 10;
        if (max is < 1 or > MaxListItems)
        {
            throw Bad($"max tra 1 e {MaxListItems}.");
        }

        var found = new List<GridPosition>();
        if (DepositFor(what) is { } deposit)
        {
            var terrain = Terrain();
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

    // Both cells are on the map (at most 2000 a side), so the sum fits an int.
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

    /// <summary>Every building (not belt pieces) with what its footprint needs.</summary>
    private IEnumerable<(string Id, BuildingKind Kind, GridPosition Origin, Direction Direction)> Buildings()
    {
        foreach (var m in View.Miners)
        {
            yield return (m.DefinitionId, m.DefinitionId == Miner.AdvancedId ? BuildingKind.MinerAdvanced : BuildingKind.Miner,
                m.Position, m.Direction);
        }

        foreach (var c in View.Smelters.Concat(View.Assemblers))
        {
            yield return (c.DefinitionId, c.IsAssembler ? BuildingKind.Assembler : BuildingKind.Smelter, c.Position, c.Direction);
        }

        foreach (var g in View.Generators)
        {
            yield return (Generator.BuildingId, BuildingKind.Generator, g.Position, g.Direction);
        }

        foreach (var e in View.Extractors)
        {
            yield return (Extractor.BuildingId, BuildingKind.Extractor, e.Position, e.Direction);
        }

        foreach (var n in View.PowerNodes)
        {
            yield return (n.DefinitionId, n.DefinitionId == PowerNode.Tier2Id ? BuildingKind.PowerNodeT2 : BuildingKind.PowerNode,
                n.Position, Direction.East);
        }

        foreach (var l in View.Lamps)
        {
            yield return (Lamp.BuildingId, BuildingKind.Lamp, l.Position, Direction.East);
        }
    }

    private IEnumerable<(string Id, GridPosition Origin)> StructureOrigins()
    {
        foreach (var b in Buildings())
        {
            yield return (b.Id, b.Origin);
        }

        foreach (var (cell, belt) in View.Belts)
        {
            // A bridge counts once, at its entry (the only piece whose spec lookup matters).
            if (belt.Kind != LogisticsKind.Bridge
                || (View.TryGetSpecAt(cell, out var spec, out _) && spec.Origin == cell))
            {
                yield return (belt.Definition.Id, cell);
            }
        }
    }

    private JsonObject Eventi()
    {
        var result = new JsonObject();
        PutNewest(result, "eventi", unreadEvents.ToList(), newest: true, extraTruncated: droppedEvents);
        unreadEvents.Clear();
        droppedEvents = 0;
        return result;
    }

    /// <summary>Every structure of the content: id, name, kind, build cost, unlock cost, prerequisites, unlocked.</summary>
    private JsonObject Catalogo()
    {
        var list = new JsonArray();
        foreach (var s in content.Structures.Where(s => !s.IsStub).OrderBy(s => s.Id, StringComparer.Ordinal))
        {
            var building = content.FindBuilding(s.Id);
            var conveyor = content.FindConveyor(s.Id);
            var item = new JsonObject
            {
                ["id"] = s.Id,
                ["nome"] = s.DisplayName,
                ["tipo"] = s.Kind,
                ["costo"] = Cost(building?.MoneyCost ?? conveyor?.MoneyCost ?? 0,
                    building?.BuildCost ?? conveyor?.EffectiveBuildCost ?? []),
                ["sblocco"] = s.Unlock is { } unlock ? Cost(unlock.Money, unlock.Materials) : null,
                ["prerequisiti"] = new JsonArray(s.Prerequisites.Select(id => (JsonNode)JsonValue.Create(id)!).ToArray()),
                ["sbloccato"] = View.Research.IsUnlocked(s.Id)
            };
            list.Add(item);
        }

        return new JsonObject { ["strutture"] = list };
    }

    private static JsonObject Cost(int money, IEnumerable<ResourceAmount> items)
    {
        var cost = new JsonObject { ["soldi"] = money };
        foreach (var item in items)
        {
            cost[item.ItemId] = item.Amount;
        }

        return cost;
    }

    /// <summary>
    /// What is missing for <paramref name="refusal"/>: the shortfall the game computed (InsufficientResources), or for
    /// Locked and UnlockUnaffordable the part of the unlock cost and of the prerequisites the player does not have.
    /// </summary>
    private JsonObject? Missing(Refusal refusal, SimCommand command)
    {
        // UnlockUnaffordable does not name its structure: the command does.
        refusal = refusal.StructureId is null && command is UnlockStructure unlockCommand
            ? refusal with { StructureId = unlockCommand.StructureId }
            : refusal;
        var missing = new JsonObject();
        if (refusal.MissingMoney > 0)
        {
            missing["soldi"] = refusal.MissingMoney;
        }

        foreach (var item in refusal.MissingItems ?? [])
        {
            missing[item.ItemId] = item.Amount;
        }

        if (refusal.Code is RefusalCode.Locked or RefusalCode.UnlockUnaffordable
            && refusal.StructureId is { } id && content.FindStructure(id) is { } structure)
        {
            var unlock = structure.Unlock;
            if (unlock is not null && unlock.Money > View.Wallet.Money)
            {
                missing["soldi"] = unlock.Money - View.Wallet.Money;
            }

            foreach (var item in unlock?.Materials ?? [])
            {
                if (item.Amount > View.Wallet.MaterialCount(item.ItemId))
                {
                    missing[item.ItemId] = item.Amount - View.Wallet.MaterialCount(item.ItemId);
                }
            }

            var prerequisites = structure.Prerequisites.Where(p => !View.Research.IsUnlocked(p)).ToList();
            if (prerequisites.Count > 0)
            {
                missing["prerequisiti"] = new JsonArray(prerequisites.Select(p => (JsonNode)JsonValue.Create(p)!).ToArray());
            }
        }

        return missing.Count > 0 ? missing : null;
    }

    private JsonObject Piazza(JsonElement p)
    {
        var structureId = OptStr(p, "tipo") ?? ConveyorOfTier(OptInt(p, "tier") ?? throw Bad("Serve 'tipo' o 'tier'."));
        var kind = KindOf(structureId);
        if (kind is null && content.FindConveyor(structureId) is null)
        {
            throw Bad($"Struttura sconosciuta: {structureId}.");
        }

        var direction = OptStr(p, "direzione") is { } d ? ParseDirection(d) : Brush;
        List<(GridPosition Cell, bool Crossing)> cells;
        GridPosition? dropped = null;
        if (Has(p, "cella"))
        {
            cells = [(MapCell(p, "cella"), false)];
        }
        else
        {
            if (kind is { } k && !DraggableKinds.Contains(k))
            {
                throw Bad($"'{structureId}' non si piazza in linea.");
            }

            var from = MapCell(p, "da");
            var to = MapCell(p, "a");
            // Length checked before the list is built: a far end must not allocate a huge line.
            if (Math.Abs((long)to.X - from.X) + Math.Abs((long)to.Y - from.Y) + 1 > MaxListItems)
            {
                throw Bad($"Linea al massimo di {MaxListItems} celle.");
            }

            // The client's drag: a belt over a perpendicular one becomes a crossing (BeltDrag, same as World.Placement).
            (cells, dropped) = BeltDrag.Line(View, from, to, direction, belts: kind is null);
        }

        var recipe = OptStr(p, "ricetta");
        var filter = OptStr(p, "filtro");
        var commands = cells.Select(c => kind is { } buildingKind
            ? (SimCommand)new PlaceBuilding(PlayerId, buildingKind, c.Cell, direction, recipe, filter)
            : new PlaceBelt(PlayerId, c.Cell, direction, structureId, c.Crossing)).ToList();

        return Command(commands, outcomes =>
        {
            var placed = new JsonArray();
            var refused = new JsonArray();
            Outcome? firstRefusal = null;
            foreach (var outcome in outcomes)
            {
                var cell = CellJson(CellOf(outcome.Command)!.Value);
                if (outcome.Refusal is not { } refusal)
                {
                    cell["direzione"] = DirectionName(direction);
                    if (outcome.Command is PlaceBelt { Crossing: true } crossing && JunctionAt(crossing.Position))
                    {
                        cell["incrocio"] = true;
                    }

                    placed.Add(cell);
                }
                else
                {
                    firstRefusal ??= outcome;
                    cell["codice"] = refusal.Code.ToString();
                    if (Missing(refusal, outcome.Command) is { } missing)
                    {
                        cell["mancano"] = missing;
                    }

                    refused.Add(cell);
                }
            }

            // The drag's last cell over a perpendicular belt: the client drops it (it would only be refused as occupied).
            if (dropped is { } last)
            {
                var cell = CellJson(last);
                cell["codice"] = nameof(RefusalCode.Occupied);
                refused.Add(cell);
            }

            if (placed.Count == 0 && firstRefusal is { } first)
            {
                throw Refused(first.Refusal!, first.Command);
            }

            return new JsonObject { ["tick"] = View.CurrentTick, ["celle"] = placed, ["rifiuti"] = refused };
        });
    }

    /// <summary>A junction stands, is being built or is planned on <paramref name="cell"/> (an automatic crossing).</summary>
    private bool JunctionAt(GridPosition cell)
    {
        var junction = BuildSpec.StructureIdFor(BuildingKind.Junction);
        return (View.Belts.TryGetValue(cell, out var belt) && belt.Kind == LogisticsKind.Junction)
            || View.Sites.Any(s => s.Spec.Origin == cell && s.Spec.StructureId == junction)
            || View.PlansOf(PlayerId).Any(plan => plan.Spec.Origin == cell && plan.Spec.StructureId == junction);
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
        // Like the player's R: turns the brush clockwise (in the real game the player's own); later placements
        // without `direzione` use it.
        if (RotateBrush is { } rotate)
        {
            rotate();
        }
        else
        {
            brush = DirectionMath.Right(brush);
        }

        return new JsonObject { ["direzione"] = DirectionName(Brush) };
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
        Journal.EndTick = View.CurrentTick;
        if (OptStr(p, "nome") is { } name)
        {
            if (journalDirectory is null)
            {
                throw Bad("Questa sessione non ha una cartella per i diari.");
            }

            if (!JournalName.IsMatch(name) || ReservedName.IsMatch(name))
            {
                throw Bad("'nome' è un nome di file semplice (lettere, cifre, - e _), senza cartelle.");
            }

            var path = Path.Combine(journalDirectory, name.EndsWith(".json", StringComparison.Ordinal) ? name : name + ".json");
            Journal.Save(path);
            result["percorso"] = path;
        }

        var items = Journal.Entries.Select(e => new JsonObject
        {
            ["tick"] = e.Tick,
            ["metodo"] = e.Method,
            ["parametri"] = JsonNode.Parse(e.Params.GetRawText())
        }).ToList();
        PutNewest(result, "voci", items, newest: true);
        result["fine"] = Journal.EndTick;
        // Without a header rigioca refuses the journal; the warnings say where a replay may differ.
        result["rigiocabile"] = Journal.Setup is not null;
        if (Journal.Warnings.Count > 0)
        {
            result["avvisi"] = new JsonArray(Journal.Warnings.Select(w => (JsonNode)JsonValue.Create(w)!).ToArray());
        }

        return result;
    }

    /// <summary>One command: <c>{tick}</c> (plus <paramref name="fill"/>) when applied, else its refusal.</summary>
    private JsonObject Single(SimCommand command, Action<JsonObject>? fill = null) =>
        Command([command], o => Accepted(o[0], fill));

    private JsonObject Accepted(Outcome outcome, Action<JsonObject>? fill)
    {
        if (outcome.Refusal is not null)
        {
            throw Refused(outcome.Refusal, outcome.Command);
        }

        var result = new JsonObject { ["tick"] = View.CurrentTick };
        fill?.Invoke(result);
        return result;
    }

    /// <summary>
    /// Submits <paramref name="commands"/> for the next tick. With a tick per command it runs that tick and returns
    /// <paramref name="build"/> of the outcomes (in submit order); otherwise it answers <c>inAttesa</c>.
    /// </summary>
    private JsonObject Command(IReadOnlyList<SimCommand> commands, Func<List<Outcome>, JsonObject> build)
    {
        if (pending is not null)
        {
            throw Bad("Comando in corso: aspetta il suo esito prima di mandarne un altro.");
        }

        var tick = View.CurrentTick + 1;
        var stamped = commands.Select(c => c with { Tick = tick }).ToList();
        awaiting.AddRange(stamped);
        foreach (var command in stamped)
        {
            host.Submit(command);
        }

        if (!tickPerCommand)
        {
            pending = (stamped, build);
            return new JsonObject { ["tick"] = tick, ["inAttesa"] = true };
        }

        host.AdvanceTicks(1);
        return Finish(stamped, build);
    }

    private JsonObject Finish(List<SimCommand> stamped, Func<List<Outcome>, JsonObject> build)
    {
        var outcomes = new List<Outcome>();
        foreach (var command in stamped)
        {
            var i = resolved.FindIndex(o => o.Command.Equals(command));
            if (i < 0)
            {
                throw new InvalidOperationException($"Esito mancante per {command}.");
            }

            outcomes.Add(resolved[i]);
            resolved.RemoveAt(i);
        }

        return build(outcomes);
    }

    /// <summary>Keeps the outcome only of commands this instance submitted (the player's own go elsewhere).</summary>
    private void Resolve(SimCommand command, Refusal? refusal, int moneyDelta)
    {
        var i = awaiting.IndexOf(command);
        if (i >= 0)
        {
            awaiting.RemoveAt(i);
            resolved.Add(new Outcome(command, refusal, moneyDelta));
        }
    }

    private void OnRejected(SimCommand command, Refusal refusal)
    {
        Resolve(command, refusal, 0);
        var e = new JsonObject
        {
            // The journal's convention: the tick when the command was sent (it applied in the next one).
            ["tick"] = command.Tick - 1,
            ["tipo"] = "rifiuto",
            ["comando"] = ToolName(command),
            ["codice"] = refusal.Code.ToString()
        };
        if (CellOf(command) is { } cell)
        {
            e["cella"] = CellJson(cell);
        }

        if (Missing(refusal, command) is { } missing)
        {
            e["mancano"] = missing;
        }

        refusals.Enqueue(e);
        if (refusals.Count > RecentRefusals)
        {
            refusals.Dequeue();
        }

        Buffer(e);
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
                PutSpec(e, finished.Spec);
                break;
            case DeconstructFinished taken:
                e["tipo"] = "smontato";
                e["cantiere"] = taken.SiteId;
                PutSpec(e, taken.Spec);
                break;
            default:
                e["tipo"] = "altro";
                e["nome"] = simEvent.GetType().Name;
                break;
        }

        Buffer(e);
    }

    private static void PutSpec(JsonObject e, BuildSpec? spec)
    {
        if (spec is not null)
        {
            e["struttura"] = spec.StructureId;
            e["cella"] = CellJson(spec.Origin);
        }
    }

    private void Buffer(JsonObject e)
    {
        unreadEvents.Enqueue(e);
        if (unreadEvents.Count > MaxBufferedEvents)
        {
            unreadEvents.Dequeue();
            droppedEvents++;
        }
    }

    /// <summary>The agents' tool that sends <paramref name="command"/> (the client sends the others).</summary>
    private static string ToolName(SimCommand command) => command switch
    {
        PlaceBelt or PlaceBuilding => "piazza",
        Remove => "smonta",
        UnlockStructure => "sblocca",
        Sell => "vendi",
        UpgradeCore => "potenzia_core",
        SetAutoSell => "venditaAutomatica",
        SetSorterFilter => "filtroSelezionatore",
        _ => "altro"
    };

    private static GridPosition? CellOf(SimCommand command) => command switch
    {
        PlaceBelt belt => belt.Position,
        PlaceBuilding building => building.Origin,
        Remove remove => remove.Tile,
        _ => null
    };

    /// <summary>
    /// Puts at most <see cref="MaxListItems"/> of <paramref name="items"/> (the newest, or the first) under
    /// <paramref name="key"/>, and how many were left out (plus <paramref name="extraTruncated"/>) under the truncation key.
    /// </summary>
    private static void PutNewest(
        JsonObject target, string key, IReadOnlyList<JsonObject> items, bool newest, int extraTruncated = 0,
        string truncatedKey = "troncato")
    {
        var skipped = Math.Max(0, items.Count - MaxListItems);
        target[key] = Array(newest ? items.Skip(skipped) : items.Take(MaxListItems));
        if (skipped + extraTruncated > 0)
        {
            target[truncatedKey] = skipped + extraTruncated;
        }
    }

    // A node has one parent: buffered nodes are copied into each answer.
    private static JsonArray Array(IEnumerable<JsonNode> items) => new(items.Select(i => i.DeepClone()).ToArray());

    private AgentException Refused(Refusal refusal, SimCommand command)
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

        return new AgentException(refusal.Code.ToString(), message + ".") { Missing = Missing(refusal, command) };
    }

    private static AgentException Bad(string message) => new(AgentCodes.ParametriNonValidi, message);

    private TerrainMap Terrain() => View.Terrain ?? throw Bad("La partita non ha una mappa.");

    private static JsonObject CellJson(GridPosition cell) => new() { ["x"] = cell.X, ["y"] = cell.Y };

    internal static string DirectionName(Direction direction) => direction switch
    {
        Direction.North => "nord",
        Direction.East => "est",
        Direction.South => "sud",
        _ => "ovest"
    };

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

    /// <summary>A cell that must lie on the map.</summary>
    private GridPosition MapCell(JsonElement p, string name)
    {
        var cell = Cell(p, name);
        var terrain = Terrain();
        return terrain.InBounds(cell) ? cell : throw Bad($"'{name}' fuori dalla mappa ({terrain.Width}×{terrain.Height}).");
    }

    private readonly record struct Outcome(SimCommand Command, Refusal? Refusal, int MoneyDelta);
}
