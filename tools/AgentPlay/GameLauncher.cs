using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using TIndustry.Shared;

namespace TIndustry.AgentPlay;

/// <summary>
/// How the real game is opened: interface (lingua, scala %, colori) and game (a sandbox misura/seme/generatore, a
/// campaign level, or a save file that is copied into the profile first).
/// </summary>
public sealed record GameOptions(
    string Lingua = "it",
    int ScalaPercento = 100,
    string Colori = "Normale",
    string? Livello = null,
    int Seme = 1,
    int Misura = (int)MapSize.Media,
    string Generatore = MapGenerator.Name,
    string? Salvataggio = null,
    bool Conserva = false);

/// <summary>
/// A started game: the Godot process (and the job that dies with this server), its channel (port, secret), its
/// temporary profile.
/// </summary>
public sealed record GameProcess(Process Process, KillOnCloseJob Job, int Port, string Token, string ProfileDir);

/// <summary>
/// Starts Godot (<c>godot4 --path godot</c>, or <c>TINDUSTRY_GODOT</c>) like tools/dev/playtest.sh: a fresh temporary
/// profile (<c>TINDUSTRY_DATA_DIR</c>, <c>APPDATA</c>, <c>--log-file</c>), a 1280×720 window, the agents' channel on a
/// free port with a fresh secret, and the game quitting when the launcher's connection drops. Never playtest mode,
/// never the player's real data.
/// </summary>
public static class GameLauncher
{
    public const string GodotEnv = "TINDUSTRY_GODOT";
    public const string LogFile = "godot.log";

    /// <summary>What apri_gioco answers without Godot: exactly what to put in .mcp.json (no machine path in the repo).</summary>
    public const string GodotMissingMessage =
        "godot4 non trovato nel PATH del server: il gioco vero non si può aprire. Imposta la variabile d'ambiente "
        + GodotEnv + " sull'eseguibile di Godot 4.4 mono (.exe, per esempio C:\\Godot\\godot4.exe): .mcp.json la passa già al server "
        + "con \"" + GodotEnv + "\": \"${" + GodotEnv + ":-}\". In alternativa scrivi il percorso nell'env del server tindustry "
        + "in .mcp.json: \"" + GodotEnv + "\": \"C:\\\\Godot\\\\godot4.exe\" (senza farne il commit). Poi riavvia Claude Code.";

    /// <summary>Slot of the save copied into the profile (<c>godot-saves/agente.json</c>).</summary>
    public const string SaveSlot = "agente";

    /// <summary>Colour variants of the client (<c>VarianteColori</c> in godot/scripts/ui/UiColors.cs; a test keeps them in sync).</summary>
    public static readonly string[] Colours = ["Normale", "Daltonici", "AltoContrasto"];

    /// <summary>UI scale limits in percent (<c>ClientSettings.UiScaleMin/Max/Step</c>; a test keeps them in sync).</summary>
    public const int ScaleMin = 75, ScaleMax = 200, ScaleStep = 5;

    // cmd.exe reads these even inside quoted arguments: a .cmd/.bat wrapper would split or expand the command line.
    private const string CmdSpecial = "&|^%!<>\"";

    /// <summary>The Godot executable: <c>TINDUSTRY_GODOT</c>, else <c>godot4</c> on the PATH; null when there is none.</summary>
    public static string? FindGodot()
    {
        // A client that does not expand .mcp.json's "${TINDUSTRY_GODOT:-}" passes it literally: treat it as unset.
        if (Environment.GetEnvironmentVariable(GodotEnv) is { Length: > 0 } custom && !custom.StartsWith("${", StringComparison.Ordinal))
        {
            return custom;
        }

        var names = OperatingSystem.IsWindows() ? new[] { "godot4.exe", "godot4.cmd", "godot4.bat" } : new[] { "godot4" };
        return (Environment.GetEnvironmentVariable("PATH") ?? "")
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .SelectMany(dir => names.Select(name => Path.Combine(dir.Trim('"'), name)))
            .FirstOrDefault(File.Exists);
    }

    /// <summary>The repository's <c>godot</c> folder, found up from the working directory or from the server's own folder.</summary>
    public static string? FindProject() =>
        new[] { Environment.CurrentDirectory, AppContext.BaseDirectory }
            .SelectMany(Ancestors)
            .Select(dir => Path.Combine(dir, "godot"))
            .FirstOrDefault(dir => File.Exists(Path.Combine(dir, "project.godot")));

    /// <summary>The project folder, or <c>MotoreNonDisponibile</c> when the server does not run inside the repository.</summary>
    public static string RequireProject() => FindProject() ?? throw new AgentException(
        AgentCodes.MotoreNonDisponibile,
        "Progetto Godot non trovato (godot/project.godot): avvia il server dalla radice del repository.");

    /// <summary>
    /// A fresh checkout has no imported texts and no built client: Godot would open a grey window that never answers
    /// (World._Ready fails loading the font), so say what to run instead of waiting for the start timeout.
    /// </summary>
    public static void RequireImported(string project)
    {
        var missing = new[]
        {
            Path.Combine(project, "lingue", "testi.it.translation"),
            Path.Combine(project, ".godot", "mono", "temp", "bin", "Debug", "TIndustry.Godot.dll")
        }.FirstOrDefault(file => !File.Exists(file));
        if (missing is not null)
        {
            throw new AgentException(
                AgentCodes.MotoreNonDisponibile,
                $"Il progetto Godot non è importato o compilato (manca {missing}). Dalla radice del repository esegui "
                + "APPDATA=\"$(mktemp -d)\" godot4 --headless --path godot --import, poi "
                + "APPDATA=\"$(mktemp -d)\" godot4 --headless --path godot --build-solutions --quit, e riapri il gioco (vedi CLAUDE.md).");
        }
    }

    /// <summary>The languages of the texts (header of godot/lingue/testi.csv: <c>keys,it,en</c>).</summary>
    public static IReadOnlyList<string> Languages(string project) =>
        File.ReadLines(Path.Combine(project, "lingue", "testi.csv")).First().Split(',').Skip(1).Select(l => l.Trim()).ToList();

    public static GameProcess Start(GameOptions options)
    {
        var godot = FindGodot() ?? throw new AgentException(
            AgentCodes.MotoreNonDisponibile, GodotMissingMessage);
        var project = RequireProject();
        RequireImported(project);
        var profile = Path.Combine(SimEngine.TempRoot, "gioco-" + Path.GetRandomFileName());
        // Checked before any folder exists, so nothing is ever created inside the player's data.
        if (IsUnderRealData(profile))
        {
            throw new AgentException(
                AgentCodes.ErroreInterno, $"Il profilo temporaneo {profile} cade nei dati veri del giocatore: rifiutato.");
        }

        if (IsBatch(godot) && new[] { project, profile }.FirstOrDefault(p => p.IndexOfAny(CmdSpecial.ToCharArray()) >= 0) is { } bad)
        {
            throw new AgentException(
                AgentCodes.MotoreNonDisponibile,
                $"{godot} è uno script cmd e il percorso {bad} contiene uno di {CmdSpecial}: cmd.exe lo spezzerebbe. "
                + $"Imposta {GodotEnv} sull'eseguibile di Godot (.exe).");
        }

        Directory.CreateDirectory(profile);
        try
        {
            if (options.Conserva)
            {
                File.WriteAllText(Path.Combine(profile, SimEngine.KeepMarker), "");
            }

            WriteSettings(profile, options);
            if (options.Salvataggio is { } save)
            {
                CopySave(save, profile); // read once here: a damaged save is refused before Godot starts
            }

            var port = FreePort();
            var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
            var info = new ProcessStartInfo(godot)
            {
                WorkingDirectory = Path.GetDirectoryName(project)!,
                UseShellExecute = false,
                // stdin and stdout are the MCP channel: the game must never inherit them.
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            foreach (var arg in new[] { "--path", project, "--log-file", Path.Combine(profile, LogFile), "--resolution", "1280x720" })
            {
                info.ArgumentList.Add(arg);
            }

            info.Environment["TINDUSTRY_AGENT_PORT"] = port.ToString(CultureInfo.InvariantCulture);
            info.Environment["TINDUSTRY_AGENT_TOKEN"] = token;
            info.Environment["TINDUSTRY_AGENT_ESCI_SENZA_CLIENT"] = "1";
            info.Environment[GameDataPaths.DataDirEnv] = profile;
            // Godot builds user:// (shader cache, vulkan, recovery lock) from %APPDATA% on Windows.
            info.Environment["APPDATA"] = profile;
            foreach (var name in new[] { GameDataPaths.PlaytestEnv, "TINDUSTRY_FRESH", "TINDUSTRY_SEED_CORE_LINE" })
            {
                info.Environment.Remove(name);
            }

            var job = new KillOnCloseJob();
            Process? process = null;
            try
            {
                process = Process.Start(info) ?? throw new AgentException(AgentCodes.MotoreNonDisponibile, $"{godot} non è partito.");
                job.Add(process);
                process.StandardInput.Close();
                // The engine log is in the profile (--log-file); the console copy is drained so Godot never blocks on it.
                process.OutputDataReceived += (_, _) => { };
                process.ErrorDataReceived += (_, _) => { };
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                return new GameProcess(process, job, port, token, profile);
            }
            catch
            {
                // A setup step after the start failed: nothing may outlive it.
                job.Dispose();
                if (process is not null)
                {
                    GameEngine.Kill(process);
                    process.Dispose();
                }

                throw;
            }
        }
        catch
        {
            DeleteFolder(profile);
            throw;
        }
    }

    /// <summary>True when <paramref name="path"/> is (inside) one of the player's real data folders.</summary>
    public static bool IsUnderRealData(string path)
    {
        var full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path)) + Path.DirectorySeparatorChar;
        return Tools.RealDataRoots().Any(root => full.StartsWith(root, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>The last <paramref name="count"/> lines of the game's log (read while Godot may still write it).</summary>
    public static string LogTail(string profile, int count) => string.Join("\n", LogLines(profile).TakeLast(count));

    public static IReadOnlyList<string> LogLines(string profile)
    {
        try
        {
            using var stream = new FileStream(
                Path.Combine(profile, LogFile), FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd().Split('\n').Select(l => l.TrimEnd('\r')).Where(l => l.Length > 0).ToList();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return [$"(log non leggibile: {ex.Message})"];
        }
    }

    /// <summary>Deletes a folder, retrying while the exiting game still holds its files.</summary>
    public static void DeleteFolder(string folder)
    {
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                if (Directory.Exists(folder))
                {
                    Directory.Delete(folder, recursive: true);
                }

                return;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                if (attempt >= 20)
                {
                    Console.Error.WriteLine($"Cartella non cancellata: {folder}: {ex.Message}");
                    return;
                }

                Thread.Sleep(250);
            }
        }
    }

    private static bool IsBatch(string exe) =>
        exe.EndsWith(".cmd", StringComparison.OrdinalIgnoreCase) || exe.EndsWith(".bat", StringComparison.OrdinalIgnoreCase);

    // The save is copied into the profile's saves folder (the game loads the copy); its snapshot comes from the copy too.
    private static string CopySave(string source, string profile)
    {
        var saves = Directory.CreateDirectory(Path.Combine(profile, "godot-saves")).FullName;
        var copy = Path.Combine(saves, SaveSlot + ".json");
        try
        {
            File.Copy(source, copy);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new AgentException(AgentCodes.ParametriNonValidi, $"Salvataggio non copiabile: {ex.Message}");
        }

        return Tools.SaveSnapshot(copy);
    }

    // The client's settings.json (ClientSettings, camelCase): the chosen interface, no tutorial, a window.
    private static void WriteSettings(string profile, GameOptions options)
    {
        var dir = Directory.CreateDirectory(Path.Combine(profile, "tIndustry")).FullName;
        File.WriteAllText(Path.Combine(dir, "settings.json"), JsonSerializer.Serialize(new
        {
            lingua = options.Lingua,
            uiScalePercent = options.ScalaPercento,
            colori = options.Colori,
            tutorialCompleted = true,
            schermoIntero = false
        }));
    }

    // The OS picks a free port (bind 0); the game binds it right after.
    private static int FreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            return ((IPEndPoint)listener.LocalEndpoint).Port;
        }
        finally
        {
            listener.Stop();
        }
    }

    private static IEnumerable<string> Ancestors(string start)
    {
        for (var dir = new DirectoryInfo(Path.GetFullPath(start)); dir is not null; dir = dir.Parent)
        {
            yield return dir.FullName;
        }
    }
}

/// <summary>
/// Windows job object with KILL_ON_JOB_CLOSE: the game (wrapper, console exe, Godot) dies when this server closes the
/// handle, on <see cref="Dispose"/> or because the server itself died. Does nothing on other systems (there the
/// bridge's quit-without-client covers a dead server).
/// </summary>
public sealed class KillOnCloseJob : IDisposable
{
    private const int ExtendedLimitInformation = 9;
    private const uint KillOnJobClose = 0x2000;
    private IntPtr handle;

    public KillOnCloseJob()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        handle = CreateJobObject(IntPtr.Zero, null);
        var info = new ExtendedLimit { Basic = new BasicLimit { LimitFlags = KillOnJobClose } };
        if (handle == IntPtr.Zero || !SetInformationJobObject(handle, ExtendedLimitInformation, ref info, Marshal.SizeOf<ExtendedLimit>()))
        {
            Console.Error.WriteLine($"Job object non creato (errore {Marshal.GetLastWin32Error()}): il gioco non muore col server.");
            Dispose();
        }
    }

    /// <summary>
    /// Puts <paramref name="process"/> in the job; children it starts from now on join it too.
    /// shortcut: a child spawned before this call (a .cmd wrapper is a few ms slower) stays out; the bridge's
    /// quit-without-client still closes it; upgrade to CREATE_SUSPENDED if that is ever seen.
    /// </summary>
    public void Add(Process process)
    {
        if (handle != IntPtr.Zero && !AssignProcessToJobObject(handle, process.Handle))
        {
            Console.Error.WriteLine($"Processo {process.Id} non aggiunto al job (errore {Marshal.GetLastWin32Error()}).");
        }
    }

    public void Dispose()
    {
        if (handle != IntPtr.Zero)
        {
            CloseHandle(handle);
            handle = IntPtr.Zero;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BasicLimit
    {
        public long PerProcessUserTimeLimit;
        public long PerJobUserTimeLimit;
        public uint LimitFlags;
        public UIntPtr MinimumWorkingSetSize;
        public UIntPtr MaximumWorkingSetSize;
        public uint ActiveProcessLimit;
        public UIntPtr Affinity;
        public uint PriorityClass;
        public uint SchedulingClass;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct IoCounters
    {
        public ulong ReadOperationCount, WriteOperationCount, OtherOperationCount, ReadTransferCount, WriteTransferCount, OtherTransferCount;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ExtendedLimit
    {
        public BasicLimit Basic;
        public IoCounters Io;
        public UIntPtr ProcessMemoryLimit;
        public UIntPtr JobMemoryLimit;
        public UIntPtr PeakProcessMemoryUsed;
        public UIntPtr PeakJobMemoryUsed;
    }

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr CreateJobObject(IntPtr attributes, string? name);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetInformationJobObject(IntPtr job, int infoClass, ref ExtendedLimit info, int length);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AssignProcessToJobObject(IntPtr job, IntPtr process);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr handle);
}
