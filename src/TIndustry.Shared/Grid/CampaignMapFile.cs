using System.Text.Json;
using System.Text.Json.Serialization;

namespace TIndustry.Shared;

/// <summary>
/// A campaign map saved as files in <c>data/maps/</c>:
/// <list type="bullet">
/// <item><c>&lt;id&gt;.png</c>: one exact colour per floor and per wall (the <c>terrain</c> table of content.json).
/// A wall pixel stands on the first floor of the wall's biome.</item>
/// <item><c>&lt;id&gt;.giacimenti.png</c>: same size, transparent except the deposits, one colour per ore
/// (<see cref="ItemVisualColors"/>).</item>
/// <item><c>&lt;id&gt;.json</c>: <c>{ "core": {"x", "y"}, "biomes": [...], "note": "..." }</c>; the Core is
/// <see cref="CoreSize"/>×<see cref="CoreSize"/> with its top-left cell at <c>core</c>.</item>
/// </list>
/// Loaded maps have <see cref="TerrainMap.Generator"/> = <c>"campagna:&lt;id&gt;"</c>.
/// </summary>
public static class CampaignMapFile
{
    public const string GeneratorPrefix = "campagna:";

    public const int CoreSize = 2;

    /// <summary>Required ores must lie within this many cells of the Core centre…</summary>
    public const int OreRadius = 60;

    /// <summary>…with at least this many cells reachable over land.</summary>
    public const int OreThreshold = 8;

    /// <summary>Least share (percent) of the cells within <see cref="OreRadius"/> the Core must reach over land.</summary>
    public const int MinLandPercent = 50;

    private const int MaxListed = 5;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private static readonly DepositKind[] Ores =
        [DepositKind.Iron, DepositKind.Copper, DepositKind.Coal, DepositKind.Lead, DepositKind.Titanium];

    public static string TerrainPath(string dir, string mapId) => Path.Combine(dir, mapId + ".png");

    public static string DepositPath(string dir, string mapId) => Path.Combine(dir, mapId + ".giacimenti.png");

    public static string InfoPath(string dir, string mapId) => Path.Combine(dir, mapId + ".json");

    /// <summary>Reads the map; <paramref name="coreOrigin"/> is the Core's top-left cell.</summary>
    /// <exception cref="InvalidDataException">The files are missing or do not form a valid map (Italian text).</exception>
    public static TerrainMap Load(string dir, string mapId, FactoryContent content, out GridPosition coreOrigin)
    {
        var problems = new List<string>();
        var parsed = Parse(dir, mapId, content, problems);
        if (parsed is null || problems.Count > 0)
        {
            throw new InvalidDataException($"Mappa '{mapId}' non valida: {string.Join(" ", problems)}");
        }

        coreOrigin = parsed.Value.Core;
        return parsed.Value.Map;
    }

    /// <summary>
    /// Problems of the map, in Italian (empty = valid): files, colours, size, Core and start area, deposits on
    /// walls or deep water, a Core cut off from most of its surroundings, deposits near the Core cut off from it. With <paramref name="level"/>, also the ores
    /// its objectives need (<see cref="RequiredOres"/>) near the Core.
    /// </summary>
    public static IReadOnlyList<string> Validate(
        string dir, string mapId, FactoryContent content, CampaignLevelDefinition? level = null)
    {
        var problems = new List<string>();
        var parsed = Parse(dir, mapId, content, problems);
        if (parsed is not { } p)
        {
            return problems;
        }

        var map = p.Map;
        var reachable = Reachable(map, p.Core);
        var (cx2, cy2) = (2 * p.Core.X + CoreSize, 2 * p.Core.Y + CoreSize);
        var counts = new int[Ores.Length + 1];
        var enclosed = new List<string>();
        var enclosedCount = 0;
        var area = 0;
        var land = 0;
        for (var y = Math.Max(0, p.Core.Y - OreRadius - 1); y <= Math.Min(map.Height - 1, p.Core.Y + OreRadius + 1); y++)
        {
            for (var x = Math.Max(0, p.Core.X - OreRadius - 1); x <= Math.Min(map.Width - 1, p.Core.X + OreRadius + 1); x++)
            {
                if (!WithinRadius(x, y, cx2, cy2))
                {
                    continue;
                }

                area++;
                if (reachable[y * map.Width + x])
                {
                    land++;
                }

                var deposit = map[x, y].Deposit;
                if (deposit == DepositKind.None)
                {
                    continue;
                }

                if (reachable[y * map.Width + x])
                {
                    counts[(int)deposit]++;
                }
                else if (!map[x, y].BlocksBuilding)
                {
                    enclosedCount++;
                    if (enclosed.Count < MaxListed)
                    {
                        enclosed.Add($"({x}, {y})");
                    }
                }
            }
        }

        if (land * 100L < area * (long)MinLandPercent)
        {
            problems.Add(
                $"Il Core è chiuso: solo il {land * 100L / Math.Max(1, area)}% delle celle entro {OreRadius} è " +
                $"raggiungibile a terra (serve almeno il {MinLandPercent}%).");
        }

        if (enclosedCount > 0)
        {
            problems.Add(
                $"{enclosedCount} celle di giacimento entro {OreRadius} dal Core sono chiuse da pareti o acqua " +
                $"profonda: {string.Join(", ", enclosed)}{More(enclosedCount)}.");
        }

        if (level is not null)
        {
            foreach (var ore in RequiredOres(content, level))
            {
                if (counts[(int)ore] < OreThreshold)
                {
                    problems.Add(
                        $"Servono almeno {OreThreshold} celle di {OreName(ore)} raggiungibili entro {OreRadius} dal " +
                        $"Core per '{level.Id}': ce ne sono {counts[(int)ore]}.");
                }
            }
        }

        return problems;
    }

    /// <summary>
    /// Ores a level needs: iron and copper always (building costs), plus the ores behind every objective item
    /// and research unlock (cost and prerequisites), following the recipes; smelting also needs coal.
    /// </summary>
    public static IReadOnlyList<DepositKind> RequiredOres(FactoryContent content, CampaignLevelDefinition level)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(level);
        var needed = new SortedSet<DepositKind> { DepositKind.Iron, DepositKind.Copper };
        var seenItems = new HashSet<string>(StringComparer.Ordinal);
        var seenStructures = new HashSet<string>(StringComparer.Ordinal);
        foreach (var objective in level.Objectives ?? [])
        {
            if (objective.ItemId is { } item)
            {
                AddItem(item);
            }

            if (objective.StructureId is { } structure)
            {
                AddStructure(structure);
            }
        }

        return [.. needed];

        void AddItem(string itemId)
        {
            if (!seenItems.Add(itemId))
            {
                return;
            }

            foreach (var ore in Ores)
            {
                if (TerrainMap.ItemIdForDeposit(ore) == itemId)
                {
                    needed.Add(ore);
                    return;
                }
            }

            foreach (var recipe in content.Recipes.Where(r => r.Outputs.Any(o => o.ItemId == itemId)))
            {
                if (recipe.Id.StartsWith("smelt-", StringComparison.Ordinal))
                {
                    AddItem(Crafter.FuelItemId);
                }

                foreach (var input in recipe.Inputs)
                {
                    AddItem(input.ItemId);
                }
            }
        }

        void AddStructure(string structureId)
        {
            if (!seenStructures.Add(structureId))
            {
                return;
            }

            if (content.Structures.FirstOrDefault(s => s.Id == structureId) is not { } definition)
            {
                return;
            }

            foreach (var material in definition.Unlock?.Materials ?? [])
            {
                AddItem(material.ItemId);
            }

            foreach (var cost in content.Buildings.FirstOrDefault(b => b.Id == structureId)?.BuildCost ?? [])
            {
                AddItem(cost.ItemId);
            }

            foreach (var prerequisite in definition.Prerequisites)
            {
                AddStructure(prerequisite);
            }
        }
    }

    /// <summary>Writes <paramref name="map"/> as the three files of <paramref name="mapId"/> (tools and tests).</summary>
    public static void Save(
        string dir, string mapId, TerrainMap map, GridPosition coreOrigin, IReadOnlyList<string> biomes,
        string? note, FactoryContent content)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(content);
        CheckId(mapId);
        Directory.CreateDirectory(dir);
        var terrain = new byte[map.Width * map.Height * 4];
        var deposits = new byte[map.Width * map.Height * 4];
        for (var y = 0; y < map.Height; y++)
        {
            for (var x = 0; x < map.Width; x++)
            {
                var tile = map[x, y];
                var hex = tile.Wall != WallKind.None
                    ? content.Terrain.Wall(tile.Wall)?.Color
                    : content.Terrain.Floor(tile.Floor)?.Color;
                var (r, g, b) = Hex(hex ?? throw new InvalidDataException("content.json: tabella terrain incompleta."));
                var o = (y * map.Width + x) * 4;
                (terrain[o], terrain[o + 1], terrain[o + 2], terrain[o + 3]) = (r, g, b, 255);
                if (tile.Deposit != DepositKind.None)
                {
                    var (dr, dg, db) = ItemVisualColors.Rgb(TerrainMap.ItemIdForDeposit(tile.Deposit));
                    (deposits[o], deposits[o + 1], deposits[o + 2], deposits[o + 3]) = (dr, dg, db, 255);
                }
            }
        }

        PngWriter.WriteRgba(TerrainPath(dir, mapId), map.Width, map.Height, terrain);
        PngWriter.WriteRgba(DepositPath(dir, mapId), map.Width, map.Height, deposits);
        var info = new InfoDto
        {
            Core = new CoreDto { X = coreOrigin.X, Y = coreOrigin.Y },
            Biomes = [.. biomes],
            Note = note
        };
        File.WriteAllText(InfoPath(dir, mapId), JsonSerializer.Serialize(info, JsonOptions) + "\n");
    }

    private static (TerrainMap Map, GridPosition Core)? Parse(
        string dir, string mapId, FactoryContent content, List<string> problems)
    {
        ArgumentNullException.ThrowIfNull(content);
        try
        {
            CheckId(mapId);
        }
        catch (ArgumentException e)
        {
            problems.Add(e.Message);
            return null;
        }

        if (ColourClash(content) is { } clash)
        {
            problems.Add(clash);
            return null;
        }

        var info = ReadInfo(InfoPath(dir, mapId), problems);
        var terrain = ReadPng(TerrainPath(dir, mapId), problems);
        var deposits = ReadPng(DepositPath(dir, mapId), problems);
        if (info is null || terrain is null || deposits is null)
        {
            return null;
        }

        var (w, h, ground) = terrain.Value;
        if (deposits.Value.Width != w || deposits.Value.Height != h)
        {
            problems.Add(
                $"{mapId}.giacimenti.png è {deposits.Value.Width}×{deposits.Value.Height}, {mapId}.png è {w}×{h}: " +
                "devono avere la stessa misura.");
            return null;
        }

        if (w < 8 || h < 8 || w > Factory.MaxMapEdge || h > Factory.MaxMapEdge)
        {
            problems.Add($"Mappa {w}×{h}: il lato va da 8 a {Factory.MaxMapEdge}.");
            return null;
        }

        var floors = new Dictionary<int, FloorKind>();
        foreach (var f in content.Terrain.Floors)
        {
            floors.TryAdd(Key(Hex(f.Color)), f.Kind);
        }

        var walls = new Dictionary<int, (WallKind Wall, FloorKind Floor)>();
        foreach (var wall in content.Terrain.Walls)
        {
            var under = content.Terrain.Floors.FirstOrDefault(f => f.Biome == wall.Biome)?.Kind ?? FloorKind.Stone;
            walls.TryAdd(Key(Hex(wall.Color)), (wall.Kind, under));
        }

        var ores = new Dictionary<int, DepositKind>();
        foreach (var ore in Ores)
        {
            ores.TryAdd(Key(ItemVisualColors.Rgb(TerrainMap.ItemIdForDeposit(ore))), ore);
        }

        var map = new TerrainMap(w, h, 0, GeneratorPrefix + mapId);
        var unknownGround = new Issues();
        var unknownOre = new Issues();
        var oreOnBlocked = new Issues();
        for (var y = 0; y < h; y++)
        {
            for (var x = 0; x < w; x++)
            {
                var o = (y * w + x) * 4;
                var key = Key((ground[o], ground[o + 1], ground[o + 2]));
                var tile = new TerrainTile(FloorKind.Stone, WallKind.None, DepositKind.None);
                if (ground[o + 3] != 255)
                {
                    unknownGround.Add(x, y, "trasparente");
                }
                else if (floors.TryGetValue(key, out var floor))
                {
                    tile = tile with { Floor = floor };
                }
                else if (walls.TryGetValue(key, out var wall))
                {
                    tile = new TerrainTile(wall.Floor, wall.Wall, DepositKind.None);
                }
                else
                {
                    unknownGround.Add(x, y, HexOf(ground, o));
                }

                var d = deposits.Value.Rgba;
                if (d[o + 3] == 255)
                {
                    if (ores.TryGetValue(Key((d[o], d[o + 1], d[o + 2])), out var ore))
                    {
                        if (tile.BlocksBuilding)
                        {
                            oreOnBlocked.Add(x, y, null);
                        }
                        else
                        {
                            tile = tile with { Deposit = ore };
                        }
                    }
                    else
                    {
                        unknownOre.Add(x, y, HexOf(d, o));
                    }
                }
                else if (d[o + 3] != 0)
                {
                    unknownOre.Add(x, y, "semitrasparente");
                }

                map.Set(x, y, tile);
            }
        }

        unknownGround.Report(problems, $"{mapId}.png: colori sconosciuti (né suolo né parete della tabella terrain)");
        unknownOre.Report(problems, $"{mapId}.giacimenti.png: colori sconosciuti (né trasparente né un minerale)");
        oreOnBlocked.Report(problems, $"{mapId}.giacimenti.png: giacimenti su pareti o acqua profonda");

        var core = new GridPosition(info.Core!.X, info.Core.Y);
        CheckStart(map, core, problems);
        foreach (var biome in info.Biomes ?? [])
        {
            if (!MapGenSettings.AllBiomes.Contains(biome, StringComparer.Ordinal))
            {
                problems.Add($"{mapId}.json: bioma '{biome}' sconosciuto ({string.Join(", ", MapGenSettings.AllBiomes)}).");
            }
        }

        return (map, core);
    }

    /// <summary>
    /// Map files tell cells apart by colour alone: every floor and wall of <c>terrain</c> needs its own colour in
    /// <c>&lt;id&gt;.png</c>, every ore its own in <c>&lt;id&gt;.giacimenti.png</c>. Null when they do.
    /// </summary>
    public static string? ColourClash(FactoryContent content)
    {
        ArgumentNullException.ThrowIfNull(content);
        var ground = content.Terrain.Floors.Select(f => (f.Id, f.Color))
            .Concat(content.Terrain.Walls.Select(w => (w.Id, w.Color)))
            .Select(e => (e.Id, Key: Key(Hex(e.Color))));
        var ores = Ores.Select(o => (Id: TerrainMap.ItemIdForDeposit(o), Key: Key(ItemVisualColors.Rgb(TerrainMap.ItemIdForDeposit(o)))));
        foreach (var (entries, file) in new[] { (ground, "<id>.png"), (ores, "<id>.giacimenti.png") })
        {
            var seen = new Dictionary<int, string>();
            foreach (var (id, key) in entries)
            {
                if (!seen.TryAdd(key, id))
                {
                    return $"Colori duplicati per le mappe ({file}): '{seen[key]}' e '{id}' hanno lo stesso colore " +
                        $"#{key:X6}.";
                }
            }
        }

        return null;
    }

    /// <summary>The Core must be inside the map, off walls and deep water, with a free start disc around it.</summary>
    private static void CheckStart(TerrainMap map, GridPosition core, List<string> problems)
    {
        if (CoreProblem(map, core, CoreSize) is { } problem)
        {
            problems.Add(problem);
            return;
        }

        var blocked = new Issues();
        var r = MapGenerator.StartRadius;
        for (var y = Math.Max(0, core.Y - r); y <= Math.Min(map.Height - 1, core.Y + CoreSize - 1 + r); y++)
        {
            for (var x = Math.Max(0, core.X - r); x <= Math.Min(map.Width - 1, core.X + CoreSize - 1 + r); x++)
            {
                var dx = x < core.X ? core.X - x : Math.Max(0, x - (core.X + CoreSize - 1));
                var dy = y < core.Y ? core.Y - y : Math.Max(0, y - (core.Y + CoreSize - 1));
                if (dx * dx + dy * dy <= r * r && map[x, y].BlocksBuilding)
                {
                    blocked.Add(x, y, null);
                }
            }
        }

        blocked.Report(problems, $"Area di partenza (raggio {r} attorno al Core) con pareti o acqua profonda");
    }

    /// <summary>Why a Core of <paramref name="size"/> cannot stand at <paramref name="core"/> (outside the map, on a wall or deep water), or null.</summary>
    public static string? CoreProblem(TerrainMap map, GridPosition core, int size)
    {
        ArgumentNullException.ThrowIfNull(map);
        if (core.X < 0 || core.Y < 0 || core.X + size > map.Width || core.Y + size > map.Height)
        {
            return $"Il Core in ({core.X}, {core.Y}) è fuori dalla mappa {map.Width}×{map.Height}.";
        }

        for (var y = core.Y; y < core.Y + size; y++)
        {
            for (var x = core.X; x < core.X + size; x++)
            {
                if (map[x, y].BlocksBuilding)
                {
                    return $"Il Core in ({core.X}, {core.Y}) è su " +
                        $"{(map[x, y].Wall != WallKind.None ? "una parete" : "acqua profonda")} in ({x}, {y}).";
                }
            }
        }

        return null;
    }

    /// <summary>Cells reachable from the Core over buildable cells (4 neighbours).</summary>
    private static bool[] Reachable(TerrainMap map, GridPosition core)
    {
        var w = map.Width;
        var seen = new bool[w * map.Height];
        var queue = new Queue<int>();
        if (!map.InBounds(core) || map[core].BlocksBuilding)
        {
            return seen;
        }

        seen[core.Y * w + core.X] = true;
        queue.Enqueue(core.Y * w + core.X);
        while (queue.Count > 0)
        {
            var i = queue.Dequeue();
            var x = i % w;
            var y = i / w;
            Visit(x - 1, y);
            Visit(x + 1, y);
            Visit(x, y - 1);
            Visit(x, y + 1);
        }

        return seen;

        void Visit(int nx, int ny)
        {
            if (nx < 0 || ny < 0 || nx >= w || ny >= map.Height)
            {
                return;
            }

            var j = ny * w + nx;
            if (!seen[j] && !map[nx, ny].BlocksBuilding)
            {
                seen[j] = true;
                queue.Enqueue(j);
            }
        }
    }

    // Cell centre (2x+1, 2y+1) against the Core centre, both doubled to stay in integers.
    private static bool WithinRadius(int x, int y, int cx2, int cy2)
    {
        long dx = 2 * x + 1 - cx2;
        long dy = 2 * y + 1 - cy2;
        return dx * dx + dy * dy <= 4L * OreRadius * OreRadius;
    }

    private static InfoDto? ReadInfo(string path, List<string> problems)
    {
        if (!File.Exists(path))
        {
            problems.Add($"Manca il file {Path.GetFileName(path)}.");
            return null;
        }

        try
        {
            var info = JsonSerializer.Deserialize<InfoDto>(File.ReadAllText(path), JsonOptions);
            if (info?.Core is null)
            {
                problems.Add($"{Path.GetFileName(path)}: manca la posizione del Core (\"core\": {{\"x\", \"y\"}}).");
                return null;
            }

            return info;
        }
        catch (JsonException e)
        {
            problems.Add($"{Path.GetFileName(path)}: JSON non valido ({e.Message}).");
            return null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            problems.Add($"{Path.GetFileName(path)}: impossibile leggere il file ({e.Message}).");
            return null;
        }
    }

    private static (int Width, int Height, byte[] Rgba)? ReadPng(string path, List<string> problems)
    {
        if (!File.Exists(path))
        {
            problems.Add($"Manca il file {Path.GetFileName(path)}.");
            return null;
        }

        try
        {
            using var stream = File.OpenRead(path);
            return PngReader.Read(stream);
        }
        catch (InvalidDataException e)
        {
            problems.Add($"{Path.GetFileName(path)}: {e.Message}");
            return null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            problems.Add($"{Path.GetFileName(path)}: impossibile leggere il file ({e.Message}).");
            return null;
        }
    }

    /// <summary>
    /// The one rule for map ids (file names inside the maps folder, never a path): lower-case letters, digits
    /// and '-'. Throws <see cref="ArgumentException"/> otherwise.
    /// </summary>
    internal static void CheckId(string mapId)
    {
        if (string.IsNullOrEmpty(mapId) || !mapId.All(c => c is >= 'a' and <= 'z' or >= '0' and <= '9' or '-'))
        {
            throw new ArgumentException($"Id di mappa '{mapId}' non valido: solo lettere minuscole, cifre e '-'.");
        }
    }

    private static string OreName(DepositKind ore) => ore switch
    {
        DepositKind.Iron => "ferro",
        DepositKind.Copper => "rame",
        DepositKind.Coal => "carbone",
        DepositKind.Lead => "piombo",
        DepositKind.Titanium => "titanio",
        _ => ore.ToString()
    };

    private static string More(int count) => count > MaxListed ? $" e altre {count - MaxListed}" : "";

    private static int Key((byte R, byte G, byte B) c) => c.R << 16 | c.G << 8 | c.B;

    private static (byte R, byte G, byte B) Hex(string hex) =>
        (Convert.ToByte(hex.Substring(1, 2), 16), Convert.ToByte(hex.Substring(3, 2), 16), Convert.ToByte(hex.Substring(5, 2), 16));

    private static string HexOf(byte[] rgba, int o) => $"#{rgba[o]:X2}{rgba[o + 1]:X2}{rgba[o + 2]:X2}";

    /// <summary>Problem cells of one kind: the first few listed, the rest counted.</summary>
    private sealed class Issues
    {
        private readonly List<string> listed = [];
        private int count;

        public void Add(int x, int y, string? detail)
        {
            count++;
            if (listed.Count < MaxListed)
            {
                listed.Add(detail is null ? $"({x}, {y})" : $"{detail} in ({x}, {y})");
            }
        }

        public void Report(List<string> problems, string what)
        {
            if (count > 0)
            {
                problems.Add($"{what}: {string.Join(", ", listed)}{More(count)}.");
            }
        }
    }

    private sealed class InfoDto
    {
        public CoreDto? Core { get; set; }
        public List<string>? Biomes { get; set; }
        public string? Note { get; set; }
    }

    private sealed class CoreDto
    {
        public int X { get; set; }
        public int Y { get; set; }
    }
}
