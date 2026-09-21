using System.Text.Json;
using System.Text.Json.Serialization;

namespace TIndustry.Logistics;

/// <summary>
/// Ships a minimal <c>content.json</c> seed with the build; materializes a writable
/// copy under LocalApplicationData on first launch. Runtime never depends on a
/// pre-baked Excel workbook in the release zip.
/// </summary>
public static class GameContentStore
{
    public const string JsonFileName = "content.json";
    public const string ExcelFileName = "game-data.xlsx";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public static string ContentDirectory
    {
        get
        {
            var path = Path.Combine(GameSettings.SettingsDirectory, "content");
            Directory.CreateDirectory(path);
            return path;
        }
    }

    public static string UserJsonPath => Path.Combine(ContentDirectory, JsonFileName);

    public static string UserExcelPath => Path.Combine(ContentDirectory, ExcelFileName);

    public static string SeedJsonPath =>
        Path.Combine(AppContext.BaseDirectory, "data", JsonFileName);

    /// <summary>
    /// Ensures AppData content exists (seeded from the shipped JSON template), then
    /// returns the path the game should load. Optional Excel is written beside it
    /// for local editing; it is never required for launch.
    /// </summary>
    public static string EnsureUserContent()
    {
        Directory.CreateDirectory(ContentDirectory);

        var seed = SeedJsonPath;
        if (!File.Exists(seed))
        {
            throw new FileNotFoundException(
                "Seed contenuti mancante: content.json non trovato nel package.",
                seed);
        }

        if (!File.Exists(UserJsonPath))
        {
            File.Copy(seed, UserJsonPath, overwrite: false);
        }
        else if (MergeMissingSeedEntries(UserJsonPath, seed))
        {
            // Keep optional Excel in sync when we patched missing seed ids.
            TryWriteUserExcel();
        }

        if (!File.Exists(UserExcelPath))
        {
            TryWriteUserExcel();
        }

        return UserJsonPath;
    }

    /// <summary>
    /// Adds seed definitions whose ids are absent from the user JSON (additive only).
    /// Preserves user edits to existing entries. Returns true when the file was updated.
    /// </summary>
    public static bool MergeMissingSeedEntries(string userJsonPath, string seedJsonPath)
    {
        var user = GameContent.Load(userJsonPath);
        var seed = GameContent.Load(seedJsonPath);

        var conveyors = user.Conveyors.ToList();
        var recipes = user.Recipes.ToList();
        var structures = user.Structures.ToList();
        var buildings = user.Buildings.ToList();
        var market = user.Market.ToList();

        var added = false;
        added |= AppendMissing(conveyors, seed.Conveyors, entry => entry.Id);
        added |= AppendMissing(recipes, seed.Recipes, entry => entry.Id);
        added |= AppendMissing(structures, seed.Structures, entry => entry.Id);
        added |= AppendMissing(buildings, seed.Buildings, entry => entry.Id);
        added |= AppendMissing(market, seed.Market, entry => entry.ItemId);

        if (!added)
        {
            return false;
        }

        var merged = new GameContent
        {
            Conveyors = conveyors,
            Recipes = recipes,
            Structures = structures,
            Buildings = buildings,
            Market = market,
            Economy = user.Economy ?? seed.Economy
        };

        File.WriteAllText(userJsonPath, JsonSerializer.Serialize(merged, JsonOptions));
        return true;
    }

    private static bool AppendMissing<T>(
        List<T> target,
        IEnumerable<T> seedEntries,
        Func<T, string> idSelector)
    {
        var known = new HashSet<string>(target.Select(idSelector), StringComparer.Ordinal);
        var added = false;
        foreach (var entry in seedEntries)
        {
            if (known.Add(idSelector(entry)))
            {
                target.Add(entry);
                added = true;
            }
        }

        return added;
    }

    private static void TryWriteUserExcel()
    {
        try
        {
            ExcelContentStore.Save(UserExcelPath, GameContent.Load(UserJsonPath));
        }
        catch
        {
            // Excel is optional; JSON alone is enough to play.
        }
    }
}
