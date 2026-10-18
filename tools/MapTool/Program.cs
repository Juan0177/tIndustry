using System.Globalization;
using TIndustry.Shared;

// Campaign map files (data/maps/): generate from the biome generator, validate, preview.
// dotnet run --project tools/MapTool -- genera <id> --misura N --seme S --biomi a,b
//                                         [--pareti P] [--acqua P] [--giacimenti P] [--escludi titanio,…] [--nota "..."]
//                                       valida [id…]
//                                       anteprima [id…]
Console.OutputEncoding = System.Text.Encoding.UTF8;
try
{
    return Run(args);
}
catch (Exception e) when (e is ArgumentException or InvalidDataException or IOException or FormatException)
{
    Console.Error.WriteLine($"Errore: {e.Message}");
    return 2;
}

static int Run(string[] args)
{
    if (args.Length == 0)
    {
        return Usage();
    }

    var root = RepoRoot();
    var data = Path.Combine(root, "data");
    var maps = Path.Combine(data, "maps");
    var content = FactoryContent.Load(Path.Combine(data, "content.json"));
    var catalog = CampaignCatalog.Load(Path.Combine(data, "campaign.json"));
    var rest = args[1..];
    return args[0] switch
    {
        "genera" => Generate(rest, maps, content),
        "valida" => Validate(rest, maps, content, catalog),
        "anteprima" => Preview(rest, maps, Path.Combine(root, ".dev", "mappe"), content, catalog),
        _ => Usage()
    };
}

static int Usage()
{
    Console.Error.WriteLine("""
        Uso: MapTool genera <id> --misura N --seme S --biomi a,b
                    [--pareti P] [--acqua P] [--giacimenti P]   (percentuali, 100 = normale)
                    [--escludi titanio,…] [--nota "testo"]
             MapTool valida [id…]        (senza id: le mappe di tutti i livelli di campaign.json)
             MapTool anteprima [id…]     (scrive .dev/mappe/<id>.png ingrandita)
        """);
    return 2;
}

static int Generate(string[] args, string maps, FactoryContent content)
{
    if (args.Length == 0)
    {
        return Usage();
    }

    var id = args[0];
    var options = Options(args[1..]);
    var size = int.Parse(Require(options, "misura"), CultureInfo.InvariantCulture);
    var seed = int.Parse(Require(options, "seme"), CultureInfo.InvariantCulture);
    var biomes = Require(options, "biomi").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    var settings = new MapGenSettings(
        size, size, seed, biomes,
        Percent(options, "pareti"), Percent(options, "acqua"), Percent(options, "giacimenti"));
    var core = Factory.CenteredCoreOrigin(size, size, CampaignMapFile.CoreSize);
    var map = NewGame.Sandbox(content, core, CampaignMapFile.CoreSize, map: settings).Terrain!;
    var excluded = options.GetValueOrDefault("escludi") ?? "";
    if (excluded.Length > 0)
    {
        map = WithoutOres(map, excluded.Split(',', StringSplitOptions.TrimEntries).Select(OreByName).ToHashSet());
    }

    var note = options.GetValueOrDefault("nota")
        ?? $"Generata con MapTool: misura {size}, seme {seed}, biomi {string.Join(",", biomes)}, pareti " +
        $"{settings.WallDensityPercent}%, acqua {settings.WaterPercent}%, giacimenti {settings.DepositPercent}%" +
        (excluded.Length > 0 ? $", senza {excluded}." : ".");
    CampaignMapFile.Save(maps, id, map, core, biomes, note, content);
    Console.WriteLine($"Scritta {id}: {size}×{size}, Core in ({core.X}, {core.Y}).");
    return 0;
}

static int Validate(string[] ids, string maps, FactoryContent content, CampaignCatalog catalog)
{
    var targets = Targets(ids, catalog);
    var failed = 0;
    foreach (var (id, level) in targets)
    {
        var problems = CampaignMapFile.Validate(maps, id, content, level);
        if (problems.Count == 0)
        {
            Console.WriteLine($"OK {id}");
            continue;
        }

        failed++;
        Console.WriteLine($"KO {id}");
        foreach (var problem in problems)
        {
            Console.WriteLine($"  - {problem}");
        }
    }

    return failed == 0 ? 0 : 1;
}

static int Preview(string[] ids, string maps, string outDir, FactoryContent content, CampaignCatalog catalog)
{
    Directory.CreateDirectory(outDir);
    foreach (var (id, _) in Targets(ids, catalog))
    {
        var map = CampaignMapFile.Load(maps, id, content, out var core);
        var path = Path.Combine(outDir, id + ".png");
        DrawPreview(map, core, content, path);
        Console.WriteLine(path);
    }

    return 0;
}

// Floors and walls in their table colours, deposits in their ore colours, the Core in white and the
// ore radius of the validator as a magenta ring; scaled up so small maps read at ~1000 px.
static void DrawPreview(TerrainMap map, GridPosition core, FactoryContent content, string path)
{
    var zoom = Math.Max(1, (1000 + map.Width - 1) / map.Width);
    var w = map.Width * zoom;
    var h = map.Height * zoom;
    var rgba = new byte[w * h * 4];
    var cx2 = 2 * core.X + CampaignMapFile.CoreSize;
    var cy2 = 2 * core.Y + CampaignMapFile.CoreSize;
    var r2 = 2L * CampaignMapFile.OreRadius;
    for (var y = 0; y < map.Height; y++)
    {
        for (var x = 0; x < map.Width; x++)
        {
            var tile = map[x, y];
            (byte R, byte G, byte B) colour;
            if (x >= core.X && x < core.X + CampaignMapFile.CoreSize && y >= core.Y && y < core.Y + CampaignMapFile.CoreSize)
            {
                colour = (255, 255, 255);
            }
            else if (tile.Deposit != DepositKind.None)
            {
                colour = ItemVisualColors.Rgb(TerrainMap.ItemIdForDeposit(tile.Deposit));
            }
            else
            {
                colour = Hex(tile.Wall != WallKind.None ? content.Terrain.Wall(tile.Wall)!.Color : content.Terrain.Floor(tile.Floor)!.Color);
            }

            long dx = 2 * x + 1 - cx2;
            long dy = 2 * y + 1 - cy2;
            var d = dx * dx + dy * dy;
            if (d <= r2 * r2 && d > (r2 - 2) * (r2 - 2))
            {
                colour = (224, 32, 160);
            }

            for (var py = y * zoom; py < (y + 1) * zoom; py++)
            {
                for (var px = x * zoom; px < (x + 1) * zoom; px++)
                {
                    var o = (py * w + px) * 4;
                    (rgba[o], rgba[o + 1], rgba[o + 2], rgba[o + 3]) = (colour.R, colour.G, colour.B, 255);
                }
            }
        }
    }

    PngWriter.WriteRgba(path, w, h, rgba);
}

// The requested ids (each with its level, if a level uses it), or every level's map.
static List<(string Id, CampaignLevelDefinition? Level)> Targets(string[] ids, CampaignCatalog catalog)
{
    if (ids.Length == 0)
    {
        return catalog.Levels.Where(l => l.Map is not null).Select(l => (l.Map!, (CampaignLevelDefinition?)l)).ToList();
    }

    return ids.Select(id => (id, catalog.Levels.FirstOrDefault(l => l.Map == id))).ToList();
}

static int Percent(Dictionary<string, string> options, string name) =>
    options.TryGetValue(name, out var value) ? int.Parse(value, CultureInfo.InvariantCulture) : 100;

static DepositKind OreByName(string name) => name switch
{
    "ferro" => DepositKind.Iron,
    "rame" => DepositKind.Copper,
    "carbone" => DepositKind.Coal,
    "piombo" => DepositKind.Lead,
    "titanio" => DepositKind.Titanium,
    _ => throw new ArgumentException($"Minerale '{name}' sconosciuto (ferro, rame, carbone, piombo, titanio).")
};

// The same map with the given ores removed (the floor under them stays).
static TerrainMap WithoutOres(TerrainMap map, HashSet<DepositKind> ores)
{
    var tiles = new TerrainTile[map.Width, map.Height];
    for (var y = 0; y < map.Height; y++)
    {
        for (var x = 0; x < map.Width; x++)
        {
            var tile = map[x, y];
            tiles[x, y] = ores.Contains(tile.Deposit) ? tile with { Deposit = DepositKind.None } : tile;
        }
    }

    return TerrainMap.FromTiles(tiles, map.Seed, map.Generator);
}

static Dictionary<string, string> Options(string[] args)
{
    var options = new Dictionary<string, string>(StringComparer.Ordinal);
    for (var i = 0; i < args.Length; i++)
    {
        if (!args[i].StartsWith("--", StringComparison.Ordinal) || i + 1 >= args.Length)
        {
            throw new ArgumentException($"Opzione non valida: '{args[i]}'.");
        }

        options[args[i][2..]] = args[++i];
    }

    return options;
}

static string Require(Dictionary<string, string> options, string name) =>
    options.TryGetValue(name, out var value) ? value : throw new ArgumentException($"Manca l'opzione --{name}.");

static (byte R, byte G, byte B) Hex(string hex) =>
    (Convert.ToByte(hex.Substring(1, 2), 16), Convert.ToByte(hex.Substring(3, 2), 16), Convert.ToByte(hex.Substring(5, 2), 16));

// The folder holding tIndustry.sln, from the working directory or the tool's own folder upwards.
static string RepoRoot()
{
    foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
    {
        for (var dir = new DirectoryInfo(start); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "tIndustry.sln")))
            {
                return dir.FullName;
            }
        }
    }

    throw new IOException("Cartella del repo non trovata (manca tIndustry.sln): avvia MapTool dal repo tIndustry.");
}
