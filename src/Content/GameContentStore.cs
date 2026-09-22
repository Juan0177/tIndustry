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
        else
        {
            var changed = false;
            changed |= MergeMissingSeedEntries(UserJsonPath, seed);
            changed |= SyncSeedDisplayFields(UserJsonPath, seed);
            changed |= SyncSeedPrerequisites(UserJsonPath, seed);
            changed |= SyncSeedUnlockCosts(UserJsonPath, seed);
            changed |= SyncSeedBuildingCosts(UserJsonPath, seed);
            changed |= SyncSeedConveyorCosts(UserJsonPath, seed);
            if (changed)
            {
                // Keep optional Excel in sync when we patched AppData from seed.
                TryWriteUserExcel();
            }
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

        WriteMerged(userJsonPath, conveyors, recipes, structures, buildings, market, user.Economy ?? seed.Economy);
        return true;
    }

    /// <summary>
    /// Overwrites user <c>displayName</c> (structures + market) when the seed differs.
    /// Fixes stale AppData labels like "Minatore avanzato" / "Nastro base" after tier renames
    /// without wiping unlock costs or other user edits.
    /// </summary>
    public static bool SyncSeedDisplayFields(string userJsonPath, string seedJsonPath)
    {
        var user = GameContent.Load(userJsonPath);
        var seed = GameContent.Load(seedJsonPath);

        var structures = user.Structures.ToList();
        var market = user.Market.ToList();
        var changed = false;

        var seedStructures = seed.Structures.ToDictionary(s => s.Id, StringComparer.Ordinal);
        for (var i = 0; i < structures.Count; i++)
        {
            if (!seedStructures.TryGetValue(structures[i].Id, out var fromSeed))
            {
                continue;
            }

            if (string.Equals(structures[i].DisplayName, fromSeed.DisplayName, StringComparison.Ordinal))
            {
                continue;
            }

            structures[i] = structures[i] with { DisplayName = fromSeed.DisplayName };
            changed = true;
        }

        var seedMarket = seed.Market.ToDictionary(m => m.ItemId, StringComparer.Ordinal);
        for (var i = 0; i < market.Count; i++)
        {
            if (!seedMarket.TryGetValue(market[i].ItemId, out var fromSeed))
            {
                continue;
            }

            if (string.Equals(market[i].DisplayName, fromSeed.DisplayName, StringComparison.Ordinal))
            {
                continue;
            }

            market[i] = market[i] with { DisplayName = fromSeed.DisplayName };
            changed = true;
        }

        if (!changed)
        {
            return false;
        }

        WriteMerged(
            userJsonPath,
            user.Conveyors.ToList(),
            user.Recipes.ToList(),
            structures,
            user.Buildings.ToList(),
            market,
            user.Economy ?? seed.Economy);
        return true;
    }

    /// <summary>
    /// Overwrites user structure <c>prerequisites</c> when they differ from the seed.
    /// Fixes pre-tech-tree AppData (missing edges) and midgame id renames without wiping costs.
    /// </summary>
    public static bool SyncSeedPrerequisites(string userJsonPath, string seedJsonPath)
    {
        var user = GameContent.Load(userJsonPath);
        var seed = GameContent.Load(seedJsonPath);

        var structures = user.Structures.ToList();
        var seedStructures = seed.Structures.ToDictionary(s => s.Id, StringComparer.Ordinal);
        var changed = false;

        for (var i = 0; i < structures.Count; i++)
        {
            if (!seedStructures.TryGetValue(structures[i].Id, out var fromSeed))
            {
                continue;
            }

            if (PrerequisitesEqual(structures[i].Requires, fromSeed.Requires))
            {
                continue;
            }

            structures[i] = structures[i] with
            {
                Prerequisites = fromSeed.Requires.Count == 0
                    ? Array.Empty<string>()
                    : fromSeed.Requires.ToArray()
            };
            changed = true;
        }

        if (!changed)
        {
            return false;
        }

        WriteMerged(
            userJsonPath,
            user.Conveyors.ToList(),
            user.Recipes.ToList(),
            structures,
            user.Buildings.ToList(),
            user.Market.ToList(),
            user.Economy ?? seed.Economy);
        return true;
    }

    /// <summary>
    /// Overwrites structure unlock money/materials when they differ from the seed.
    /// Fixes progression softlocks (e.g. assembler requiring copper-wire it alone produces).
    /// </summary>
    public static bool SyncSeedUnlockCosts(string userJsonPath, string seedJsonPath)
    {
        var user = GameContent.Load(userJsonPath);
        var seed = GameContent.Load(seedJsonPath);

        var structures = user.Structures.ToList();
        var seedStructures = seed.Structures.ToDictionary(s => s.Id, StringComparer.Ordinal);
        var changed = false;

        for (var i = 0; i < structures.Count; i++)
        {
            if (!seedStructures.TryGetValue(structures[i].Id, out var fromSeed))
            {
                continue;
            }

            if (UnlockEqual(structures[i].Unlock, fromSeed.Unlock))
            {
                continue;
            }

            structures[i] = structures[i] with { Unlock = CloneUnlock(fromSeed.Unlock) };
            changed = true;
        }

        // Conveyor unlocks live on ConveyorDefinition as well as StructureDefinition.
        var conveyors = user.Conveyors.ToList();
        var seedConveyors = seed.Conveyors.ToDictionary(c => c.Id, StringComparer.Ordinal);
        for (var i = 0; i < conveyors.Count; i++)
        {
            if (!seedConveyors.TryGetValue(conveyors[i].Id, out var fromSeed))
            {
                continue;
            }

            if (UnlockEqual(conveyors[i].Unlock, fromSeed.Unlock))
            {
                continue;
            }

            conveyors[i] = conveyors[i] with { Unlock = CloneUnlock(fromSeed.Unlock) };
            changed = true;
        }

        if (!changed)
        {
            return false;
        }

        WriteMerged(
            userJsonPath,
            conveyors,
            user.Recipes.ToList(),
            structures,
            user.Buildings.ToList(),
            user.Market.ToList(),
            user.Economy ?? seed.Economy);
        return true;
    }

    /// <summary>
    /// Overwrites building money/build costs when they differ from the seed.
    /// Keeps placement costs aligned with progression fixes (place = materials only).
    /// </summary>
    public static bool SyncSeedBuildingCosts(string userJsonPath, string seedJsonPath)
    {
        var user = GameContent.Load(userJsonPath);
        var seed = GameContent.Load(seedJsonPath);

        var buildings = user.Buildings.ToList();
        var seedBuildings = seed.Buildings.ToDictionary(b => b.Id, StringComparer.Ordinal);
        var changed = false;

        for (var i = 0; i < buildings.Count; i++)
        {
            if (!seedBuildings.TryGetValue(buildings[i].Id, out var fromSeed))
            {
                continue;
            }

            if (buildings[i].MoneyCost == fromSeed.MoneyCost
                && ResourceAmountsEqual(buildings[i].BuildCost, fromSeed.BuildCost)
                && buildings[i].RefundPercent == fromSeed.RefundPercent)
            {
                continue;
            }

            buildings[i] = buildings[i] with
            {
                MoneyCost = fromSeed.MoneyCost,
                BuildCost = CloneAmounts(fromSeed.BuildCost),
                RefundPercent = fromSeed.RefundPercent
            };
            changed = true;
        }

        if (!changed)
        {
            return false;
        }

        WriteMerged(
            userJsonPath,
            user.Conveyors.ToList(),
            user.Recipes.ToList(),
            user.Structures.ToList(),
            buildings,
            user.Market.ToList(),
            user.Economy ?? seed.Economy);
        return true;
    }

    /// <summary>
    /// Overwrites conveyor place money/build costs when they differ from the seed.
    /// </summary>
    public static bool SyncSeedConveyorCosts(string userJsonPath, string seedJsonPath)
    {
        var user = GameContent.Load(userJsonPath);
        var seed = GameContent.Load(seedJsonPath);

        var conveyors = user.Conveyors.ToList();
        var seedConveyors = seed.Conveyors.ToDictionary(c => c.Id, StringComparer.Ordinal);
        var changed = false;

        for (var i = 0; i < conveyors.Count; i++)
        {
            if (!seedConveyors.TryGetValue(conveyors[i].Id, out var fromSeed))
            {
                continue;
            }

            if (conveyors[i].MoneyCost == fromSeed.MoneyCost
                && ResourceAmountsEqual(conveyors[i].BuildCost, fromSeed.BuildCost))
            {
                continue;
            }

            conveyors[i] = conveyors[i] with
            {
                MoneyCost = fromSeed.MoneyCost,
                BuildCost = CloneAmounts(fromSeed.BuildCost)
            };
            changed = true;
        }

        if (!changed)
        {
            return false;
        }

        WriteMerged(
            userJsonPath,
            conveyors,
            user.Recipes.ToList(),
            user.Structures.ToList(),
            user.Buildings.ToList(),
            user.Market.ToList(),
            user.Economy ?? seed.Economy);
        return true;
    }

    private static bool PrerequisitesEqual(IReadOnlyList<string> left, IReadOnlyList<string> right)
    {
        if (left.Count != right.Count)
        {
            return false;
        }

        for (var i = 0; i < left.Count; i++)
        {
            if (!string.Equals(left[i], right[i], StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    private static bool UnlockEqual(UnlockRequirement? left, UnlockRequirement? right)
    {
        if (left is null && right is null)
        {
            return true;
        }

        if (left is null || right is null)
        {
            return false;
        }

        return left.Money == right.Money && ResourceAmountsEqual(left.Materials, right.Materials);
    }

    private static bool ResourceAmountsEqual(
        IReadOnlyList<ResourceAmount> left,
        IReadOnlyList<ResourceAmount> right)
    {
        if (left.Count != right.Count)
        {
            return false;
        }

        for (var i = 0; i < left.Count; i++)
        {
            if (!string.Equals(left[i].ItemId, right[i].ItemId, StringComparison.Ordinal)
                || left[i].Amount != right[i].Amount)
            {
                return false;
            }
        }

        return true;
    }

    private static UnlockRequirement? CloneUnlock(UnlockRequirement? unlock) =>
        unlock is null
            ? null
            : new UnlockRequirement(unlock.Money, CloneAmounts(unlock.Materials));

    private static IReadOnlyList<ResourceAmount> CloneAmounts(IReadOnlyList<ResourceAmount> amounts) =>
        amounts.Select(entry => new ResourceAmount(entry.ItemId, entry.Amount)).ToArray();

    private static void WriteMerged(
        string userJsonPath,
        List<ConveyorDefinition> conveyors,
        List<RecipeDefinition> recipes,
        List<StructureDefinition> structures,
        List<BuildingDefinition> buildings,
        List<MarketItemDefinition> market,
        EconomyConfig? economy)
    {
        var merged = new GameContent
        {
            Conveyors = conveyors,
            Recipes = recipes,
            Structures = structures,
            Buildings = buildings,
            Market = market,
            Economy = economy
        };

        File.WriteAllText(userJsonPath, JsonSerializer.Serialize(merged, JsonOptions));
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
