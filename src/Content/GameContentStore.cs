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

        if (!File.Exists(UserJsonPath))
        {
            var seed = SeedJsonPath;
            if (!File.Exists(seed))
            {
                throw new FileNotFoundException(
                    "Seed contenuti mancante: content.json non trovato nel package.",
                    seed);
            }

            File.Copy(seed, UserJsonPath, overwrite: false);
        }

        // Materialize Excel next to the user JSON so modders can edit offline.
        // Prefer regenerating when missing; never ship this file in publish output.
        if (!File.Exists(UserExcelPath))
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

        return UserJsonPath;
    }
}
