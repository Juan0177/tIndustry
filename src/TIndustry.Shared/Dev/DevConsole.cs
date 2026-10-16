using System.Globalization;

namespace TIndustry.Shared;

/// <summary>What a console command asks the client to do besides printing <see cref="Lines"/>.</summary>
public sealed class DevCommandResult
{
    public List<string> Lines { get; } = [];

    /// <summary>Research unlocks changed: the client must refresh palette locks.</summary>
    public bool ResearchChanged { get; set; }

    /// <summary>The simulation advanced or the wallet changed outside the normal frame loop.</summary>
    public bool WorldChanged { get; set; }

    /// <summary>Jump this many seconds of game time ahead (the client runs it with a progress bar).</summary>
    public int? JumpSeconds { get; set; }

    /// <summary>Move the camera to this cell.</summary>
    public GridPosition? CameraTarget { get; set; }

    /// <summary>Move the camera back to the CORE.</summary>
    public bool CameraToCore { get; set; }

    /// <summary>Mark the active campaign level as completed.</summary>
    public bool CompleteLevel { get; set; }

    public bool ClearLog { get; set; }

    public void Add(string line) => Lines.Add(line);
}

/// <summary>
/// Dev-only console (<c>TINDUSTRY_DEV=1</c>): drives the game's <see cref="GameSpeed"/> (speed, pause, single-step) and time jumps, plus cheats on a
/// <see cref="Factory"/> (money, items, unlocks, power). Commands are Italian with English aliases.
/// Pure logic: the Godot client owns the window, input and camera.
/// </summary>
public sealed class DevConsole
{
    public const string EnvVar = "TINDUSTRY_DEV";
    /// <summary>Commands run at the start of every game, separated by <c>;</c> (e.g. <c>sblocca tutto; soldi 10000</c>).</summary>
    public const string StartupEnvVar = "TINDUSTRY_DEV_AVVIO";
    public const int DefaultGiveAmount = 100;

    public static readonly string[] CommandNames =
    [
        "aiuto", "velocita", "pausa", "passo", "avanti", "soldi", "dai", "sblocca", "energia",
        "info", "oggetti", "blocchi", "vai", "vinci", "pulisci"
    ];

    private static readonly string[] HelpLines =
    [
        "velocita <x>        velocità della simulazione (0.1–60, 0 = pausa); senza x mostra quella attuale",
        "pausa               mette in pausa / riprende",
        "passo [n]           in pausa: avanza di n tick (1 tick = 1/60 s)",
        "avanti <secondi>    salta avanti di <secondi> di gioco (max 21600 = 6 h), con barra di caricamento",
        "soldi <n>           aggiunge n ai fondi (negativo per toglierli)",
        "dai <oggetto|tutto> [n]  aggiunge n oggetti al CORE (default 100)",
        "sblocca <blocco|tutto>   sblocca in Ricerca senza pagare",
        "energia             riempie il buffer di energia",
        "info                tick, velocità, fondi, edifici",
        "oggetti · blocchi   elenca gli id validi",
        "vai <x> <y> | core  sposta la visuale",
        "vinci               completa il livello di campagna attivo",
        "pulisci             svuota la console",
        "Tasti: F1 console · Spazio pausa · F3/F4 più lento/veloce · . un tick (in pausa)"
    ];

    public DevConsole(GameSpeed speed)
    {
        Speed = speed;
    }

    /// <summary>True when the environment asks for dev mode (the client also requires a debug build).</summary>
    public static bool RequestedByEnvironment => Environment.GetEnvironmentVariable(EnvVar) == "1";

    /// <summary>The game's speed, shared with the speed bar and Space.</summary>
    public GameSpeed Speed { get; }

    /// <summary>Commands from <see cref="StartupEnvVar"/>, split on <c>;</c>.</summary>
    public static IReadOnlyList<string> StartupCommands() =>
        SplitCommands(Environment.GetEnvironmentVariable(StartupEnvVar));

    public static IReadOnlyList<string> SplitCommands(string? text) =>
        (text ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    /// <summary>Short status for the on-screen overlay.</summary>
    public string StatusText(Factory factory)
    {
        var state = Speed.Paused ? "PAUSA" : $"{FormatSpeed(Speed.Speed)}×";
        return $"DEV · {state} · tick {factory.CurrentTick} · {TimeJump.FormatGameTime(factory.CurrentTick)}";
    }

    public DevCommandResult Execute(string line, Factory factory)
    {
        var result = new DevCommandResult();
        var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
        {
            return result;
        }

        var args = parts[1..];
        switch (parts[0].ToLowerInvariant())
        {
            case "aiuto" or "help" or "?":
                result.Lines.AddRange(HelpLines);
                break;
            case "velocita" or "velocità" or "vel" or "speed":
                SpeedCommand(args, result);
                break;
            case "pausa" or "pause" or "p":
                Speed.TogglePause();
                result.Add(Speed.Paused ? "In pausa." : $"Ripresa a {FormatSpeed(Speed.Speed)}×.");
                break;
            case "passo" or "step":
                var n = 1;
                if (args.Length > 0 && !TryInt(args[0], out n, result))
                {
                    break;
                }

                Speed.QueueSteps(n);
                result.Add($"In pausa: avanzo di {Math.Max(0, n)} tick.");
                break;
            case "avanti" or "ff":
                FastForward(args, result);
                break;
            case "soldi" or "money":
                if (args.Length == 0)
                {
                    result.Add($"Fondi: ${factory.Wallet.Money}");
                }
                else if (TryInt(args[0], out var money, result))
                {
                    factory.Wallet.AddMoney(money);
                    result.WorldChanged = true;
                    result.Add($"Fondi: ${factory.Wallet.Money}");
                }

                break;
            case "dai" or "give":
                Give(args, factory, result);
                break;
            case "sblocca" or "unlock":
                Unlock(args, factory, result);
                break;
            case "energia" or "power":
                factory.SetPowerBuffer(factory.PowerCapacity);
                result.WorldChanged = true;
                result.Add($"Energia: {factory.PowerBuffer:0}/{factory.PowerCapacity:0}");
                break;
            case "info":
                Info(factory, result);
                break;
            case "oggetti" or "items":
                result.Add(string.Join(", ", KnownItemIds(factory.Content)));
                break;
            case "blocchi" or "blocks":
                result.Add(string.Join(", ", factory.Content.Structures.Select(s =>
                    factory.IsStructureUnlocked(s.Id) ? s.Id : $"{s.Id} (bloccato)")));
                break;
            case "vai" or "tp":
                GoTo(args, result);
                break;
            case "vinci" or "win":
                if (factory.ActiveCampaignLevelId is null)
                {
                    result.Add("Nessun livello di campagna attivo.");
                }
                else
                {
                    result.CompleteLevel = true;
                    result.Add($"Livello {factory.ActiveCampaignLevelId} completato.");
                }

                break;
            case "pulisci" or "clear" or "cls":
                result.ClearLog = true;
                break;
            default:
                result.Add($"Comando sconosciuto: {parts[0]} (scrivi aiuto)");
                break;
        }

        return result;
    }

    /// <summary>Command names starting with <paramref name="prefix"/>, for Tab completion.</summary>
    public static IReadOnlyList<string> Complete(string prefix)
    {
        var p = prefix.Trim().ToLowerInvariant();
        return CommandNames.Where(c => c.StartsWith(p, StringComparison.Ordinal)).ToList();
    }

    /// <summary>Items the content knows: market entries plus every recipe input and output.</summary>
    public static IReadOnlyList<string> KnownItemIds(FactoryContent content) =>
        content.Market.Select(m => m.ItemId)
            .Concat(content.Recipes.SelectMany(r => r.Inputs.Concat(r.Outputs)).Select(a => a.ItemId))
            .Distinct(StringComparer.Ordinal)
            .ToList();

    private static string FormatSpeed(float s) => s.ToString("0.##", CultureInfo.InvariantCulture);

    private void SpeedCommand(string[] args, DevCommandResult result)
    {
        if (args.Length == 0)
        {
            result.Add(Speed.Paused
                ? $"In pausa (velocità {FormatSpeed(Speed.Speed)}×)."
                : $"Velocità {FormatSpeed(Speed.Speed)}×.");
            return;
        }

        if (!TryFloat(args[0], out var value, result))
        {
            return;
        }

        Speed.SetSpeed(value);
        result.Add(Speed.Paused ? "In pausa." : $"Velocità {FormatSpeed(Speed.Speed)}×.");
        if (value > GameSpeed.MaxSpeed)
        {
            result.Add($"(massimo {FormatSpeed(GameSpeed.MaxSpeed)}×; per saltare avanti usa: avanti <secondi>)");
        }
    }

    private static void FastForward(string[] args, DevCommandResult result)
    {
        if (args.Length == 0)
        {
            result.Add("Uso: avanti <secondi>");
            return;
        }

        if (!TryInt(args[0], out var seconds, result))
        {
            return;
        }

        if (seconds <= 0 || seconds > TimeJump.MaxSeconds)
        {
            result.Add($"Secondi tra 1 e {TimeJump.MaxSeconds}.");
            return;
        }

        result.JumpSeconds = seconds;
        result.Add($"Salto avanti di {TimeJump.FormatGameTime((long)seconds * SimulationClock.TickRate)}.");
    }

    private static void Give(string[] args, Factory factory, DevCommandResult result)
    {
        if (args.Length == 0)
        {
            result.Add("Uso: dai <oggetto|tutto> [quantità] · elenco: oggetti");
            return;
        }

        var amount = DefaultGiveAmount;
        if (args.Length > 1 && !TryInt(args[1], out amount, result))
        {
            return;
        }

        if (amount <= 0)
        {
            result.Add("Quantità maggiore di 0.");
            return;
        }

        var known = KnownItemIds(factory.Content);
        var ids = args[0] is "tutto" or "all" ? known : known.Where(id => id == args[0]).ToList();
        if (ids.Count == 0)
        {
            result.Add($"Oggetto sconosciuto: {args[0]} · elenco: oggetti");
            return;
        }

        foreach (var id in ids)
        {
            factory.Wallet.AddMaterial(id, amount);
        }

        result.WorldChanged = true;
        result.Add(ids.Count == 1
            ? $"+{amount} {factory.Content.DisplayName(ids[0])} (ora {factory.Wallet.MaterialCount(ids[0])})"
            : $"+{amount} di ogni oggetto ({ids.Count}).");
    }

    private static void Unlock(string[] args, Factory factory, DevCommandResult result)
    {
        if (args.Length == 0)
        {
            result.Add("Uso: sblocca <blocco|tutto> · elenco: blocchi");
            return;
        }

        var structures = args[0] is "tutto" or "all"
            ? factory.Content.Structures.ToList()
            : factory.Content.Structures.Where(s => s.Id == args[0]).ToList();
        if (structures.Count == 0)
        {
            result.Add($"Blocco sconosciuto: {args[0]} · elenco: blocchi");
            return;
        }

        var unlocked = 0;
        foreach (var structure in structures.Where(s => !factory.IsStructureUnlocked(s.Id)))
        {
            factory.Research.ForceUnlock(structure.Id);
            unlocked++;
        }

        result.ResearchChanged = unlocked > 0;
        result.Add(unlocked == 0 ? "Già sbloccato." : $"Sbloccati {unlocked} blocchi.");
    }

    private void Info(Factory factory, DevCommandResult result)
    {
        result.Add(StatusText(factory));
        result.Add($"Fondi ${factory.Wallet.Money} · energia {factory.PowerBuffer:0}/{factory.PowerCapacity:0}"
            + $" (+{factory.PowerGenerationPerSecond:0}/s) · consegnati al CORE {factory.CoreDeliveredItems}");
        result.Add($"Minatori {factory.Miners.Count} · forni {factory.Smelters.Count} · assemblatori {factory.Assemblers.Count}"
            + $" · generatori {factory.Generators.Count} · estrattori {factory.Extractors.Count} · nodi {factory.PowerNodes.Count}");
        result.Add($"Oggetti creati {factory.NextItemId} · livello {factory.ActiveCampaignLevelId ?? "sandbox"}");
    }

    private static void GoTo(string[] args, DevCommandResult result)
    {
        if (args.Length == 1 && args[0] is "core")
        {
            result.CameraToCore = true;
            return;
        }

        if (args.Length != 2)
        {
            result.Add("Uso: vai <x> <y> | vai core");
            return;
        }

        if (TryInt(args[0], out var x, result) && TryInt(args[1], out var y, result))
        {
            result.CameraTarget = new GridPosition(x, y);
            result.Add($"Visuale su {x},{y}.");
        }
    }

    private static bool TryInt(string text, out int value, DevCommandResult result)
    {
        if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
        {
            return true;
        }

        result.Add($"Non è un numero intero: {text}");
        return false;
    }

    private static bool TryFloat(string text, out float value, DevCommandResult result)
    {
        if (float.TryParse(text.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out value)
            && float.IsFinite(value))
        {
            return true;
        }

        result.Add($"Non è un numero: {text}");
        return false;
    }
}
