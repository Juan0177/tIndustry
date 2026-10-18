using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text.Json;
using TIndustry.Shared;

namespace TIndustry.AgentPlay;

/// <summary>How the real game is opened: interface (lingua, scala %, colori) and game (sandbox misura/seme or a level).</summary>
public sealed record GameOptions(
    string Lingua = "it",
    int ScalaPercento = 100,
    string Colori = "Normale",
    string? Livello = null,
    int Seme = 1,
    int Misura = (int)MapSize.Media,
    bool Conserva = false);

/// <summary>A started game: the Godot process, its channel (port, secret) and its temporary profile.</summary>
public sealed record GameProcess(Process Process, int Port, string Token, string ProfileDir);

/// <summary>
/// Starts Godot (<c>godot4 --path godot</c>, or <c>TINDUSTRY_GODOT</c>) like tools/dev/playtest.sh: a fresh temporary
/// profile (<c>TINDUSTRY_DATA_DIR</c>, <c>APPDATA</c>, <c>--log-file</c>), a 1280×720 window, the agents' channel on a
/// free port with a fresh secret. Never playtest mode, never the player's real data.
/// </summary>
public static class GameLauncher
{
    public const string GodotEnv = "TINDUSTRY_GODOT";
    public const string LogFile = "godot.log";

    /// <summary>Colour variants of the client (<c>VarianteColori</c> in godot/scripts/ui/UiColors.cs).</summary>
    public static readonly string[] Colours = ["Normale", "Daltonici", "AltoContrasto"];

    /// <summary>The Godot executable: <c>TINDUSTRY_GODOT</c>, else <c>godot4</c> on the PATH; null when there is none.</summary>
    public static string? FindGodot()
    {
        if (Environment.GetEnvironmentVariable(GodotEnv) is { Length: > 0 } custom)
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

    /// <summary>The languages of the texts (header of godot/lingue/testi.csv: <c>keys,it,en</c>).</summary>
    public static IReadOnlyList<string> Languages(string project) =>
        File.ReadLines(Path.Combine(project, "lingue", "testi.csv")).First().Split(',').Skip(1).Select(l => l.Trim()).ToList();

    public static GameProcess Start(GameOptions options)
    {
        var godot = FindGodot() ?? throw new AgentException(
            AgentCodes.MotoreNonDisponibile, $"godot4 non trovato nel PATH (o imposta {GodotEnv}): il gioco vero non si può aprire.");
        var project = FindProject() ?? throw new AgentException(
            AgentCodes.MotoreNonDisponibile, "Cartella godot/ del repository non trovata: avvia il server dalla radice del repository.");

        var profile = Directory.CreateDirectory(Path.Combine(SimEngine.TempRoot, "gioco-" + Path.GetRandomFileName())).FullName;
        try
        {
            if (IsUnderRealData(profile))
            {
                throw new AgentException(
                    AgentCodes.ErroreInterno, $"Il profilo temporaneo {profile} cade nei dati veri del giocatore: rifiutato.");
            }

            if (options.Conserva)
            {
                File.WriteAllText(Path.Combine(profile, SimEngine.KeepMarker), "");
            }

            WriteSettings(profile, options);
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

            info.Environment["TINDUSTRY_AGENT_PORT"] = port.ToString(System.Globalization.CultureInfo.InvariantCulture);
            info.Environment["TINDUSTRY_AGENT_TOKEN"] = token;
            info.Environment[GameDataPaths.DataDirEnv] = profile;
            // Godot builds user:// (shader cache, vulkan, recovery lock) from %APPDATA% on Windows.
            info.Environment["APPDATA"] = profile;
            foreach (var name in new[] { GameDataPaths.PlaytestEnv, "TINDUSTRY_FRESH", "TINDUSTRY_SEED_CORE_LINE" })
            {
                info.Environment.Remove(name);
            }

            var process = Process.Start(info) ?? throw new AgentException(AgentCodes.MotoreNonDisponibile, $"{godot} non è partito.");
            process.StandardInput.Close();
            // The engine log is in the profile (--log-file); the console copy is drained so Godot never blocks on it.
            process.OutputDataReceived += (_, _) => { };
            process.ErrorDataReceived += (_, _) => { };
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            return new GameProcess(process, port, token, profile);
        }
        catch
        {
            DeleteProfile(profile);
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
    public static string LogTail(string profile, int count)
    {
        try
        {
            using var stream = new FileStream(
                Path.Combine(profile, LogFile), FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(stream);
            var lines = reader.ReadToEnd().Split('\n').Select(l => l.TrimEnd('\r')).Where(l => l.Length > 0).ToList();
            return string.Join("\n", lines.TakeLast(count));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return $"(log non leggibile: {ex.Message})";
        }
    }

    /// <summary>Deletes the profile, retrying while the exiting game still holds its files.</summary>
    public static void DeleteProfile(string profile)
    {
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                if (Directory.Exists(profile))
                {
                    Directory.Delete(profile, recursive: true);
                }

                return;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                if (attempt >= 20)
                {
                    Console.Error.WriteLine($"Profilo del gioco non cancellato: {profile}: {ex.Message}");
                    return;
                }

                Thread.Sleep(250);
            }
        }
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
