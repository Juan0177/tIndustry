namespace TIndustry.Shared;

/// <summary>What a terrain piece is: the client composes them in <see cref="TerrainLook.Plan"/> order.</summary>
public enum TerrainPieceKind : byte
{
    /// <summary>The opaque floor variant of a land cell.</summary>
    Floor,

    /// <summary>The opaque still bed of a water cell; the client animates ripples where it stays uncovered.</summary>
    Water,

    Edge,
    Deposit,
    Decoration,
    Wall
}

/// <summary>
/// One sprite piece of the terrain art: piece <paramref name="Index"/> (32 px square) of the strip
/// <paramref name="Sprite"/> (<c>godot/assets/&lt;Sprite&gt;.png</c>), its top-left corner at art pixel
/// (<paramref name="Px"/>, <paramref name="Py"/>) of the map, 32 art pixels per cell.
/// </summary>
public readonly record struct TerrainPiece(string Sprite, int Index, int Px, int Py, TerrainPieceKind Kind);

/// <summary>
/// The terrain art choices: floor variant, edges from higher floors, dual-grid walls, deposit looks and decorations,
/// all from the map and a hash of the cell (the same look on every load and platform). Exact port of the reference
/// composition of <c>tools/art/tavola.py</c> (<c>pezzi_mappa</c>) and <c>tools/art/blocchi/terreno.py</c>
/// (<c>hash_cella</c>, salts, <c>pezzi_bordo</c>); the rule is in <c>tools/art/README.md</c> and the fixture of
/// <c>tools/art/fixture_terreno.py</c> keeps both sides equal. Graphics only: never read by the simulation.
/// </summary>
public sealed class TerrainLook
{
    /// <summary>Art pixels per cell.</summary>
    public const int CellPixels = 32;

    public const int Variants = 4;
    public const int DepositLooks = 3;
    public const int DecorationLooks = 3;

    public const int SaltVariant = 1;
    public const int SaltDecoration = 2;
    public const int SaltDecorationKind = 3;
    public const int SaltDeposit = 4;

    /// <summary>Biome of the water floors: no decoration, animated bed.</summary>
    public const string WaterBiome = "acqua";

    /// <summary>Percentage of decorated land cells per biome (<c>DENSITA_DECO</c> in terreno.py).</summary>
    public static readonly IReadOnlyDictionary<string, int> DecorationDensity = new Dictionary<string, int>(StringComparer.Ordinal)
    {
        ["rocce"] = 10,
        ["sabbia"] = 8,
        ["neve"] = 7,
        ["prati"] = 14
    };

    // Neighbour offsets in edge-strip order: N, E, S, O, NE, SE, SO, NO (y grows southwards).
    private static readonly (int Dx, int Dy)[] Neighbours =
        [(0, -1), (1, 0), (0, 1), (-1, 0), (1, -1), (1, 1), (-1, 1), (-1, -1)];

    private readonly int[] _priority;
    private readonly bool[] _water;
    private readonly int[] _density;
    private readonly string[][] _floorSprites;
    private readonly string[] _edgeSprites;
    private readonly string[][] _decorationSprites;
    private readonly string[][] _depositSprites;
    private readonly (WallKind Kind, string Sprite)[] _walls;

    public TerrainLook(TerrainTable table)
    {
        ArgumentNullException.ThrowIfNull(table);
        var floors = Enum.GetValues<FloorKind>();
        var n = floors.Max(f => (int)f) + 1;
        _priority = new int[n];
        _water = new bool[n];
        _density = new int[n];
        _floorSprites = new string[n][];
        _edgeSprites = new string[n];
        _decorationSprites = new string[n][];
        foreach (var kind in floors)
        {
            var def = table.Floor(kind);
            var id = def?.Id ?? kind.ToString();
            var i = (int)kind;
            _priority[i] = def?.EdgePriority ?? 0;
            _water[i] = def?.Biome == WaterBiome;
            _density[i] = def is not null && DecorationDensity.TryGetValue(def.Biome, out var d) ? d : 0;
            _floorSprites[i] = Enumerable.Range(0, Variants).Select(v => $"terreno-{id}-{v}").ToArray();
            _edgeSprites[i] = $"terreno-{id}-bordo";
            _decorationSprites[i] = Enumerable.Range(0, DecorationLooks).Select(k => $"deco-{def?.Biome}-{k}").ToArray();
        }

        var deposits = Enum.GetValues<DepositKind>();
        _depositSprites = new string[deposits.Max(d => (int)d) + 1][];
        foreach (var deposit in deposits)
        {
            var item = TerrainMap.ItemIdForDeposit(deposit);
            _depositSprites[(int)deposit] = Enumerable.Range(0, DepositLooks).Select(v => $"giacimento-{item}-{v}").ToArray();
        }

        // Walls drawn one kind after the other, in id order (as the reference sorts them).
        _walls = table.Walls
            .Where(w => w.Kind != WallKind.None)
            .OrderBy(w => w.Id, StringComparer.Ordinal)
            .Select(w => (w.Kind, $"parete-{w.Id}"))
            .ToArray();
    }

    /// <summary>Every sprite name <see cref="Plan"/> can use (for loading them up front).</summary>
    public IEnumerable<string> SpriteNames =>
        _floorSprites.SelectMany(s => s)
            .Concat(_edgeSprites)
            .Concat(Enumerable.Range(0, _water.Length).Where(i => !_water[i] && _density[i] > 0)
                .SelectMany(i => _decorationSprites[i]))
            .Concat(_depositSprites.Skip(1).SelectMany(s => s))
            .Concat(_walls.Select(w => w.Sprite))
            .Distinct(StringComparer.Ordinal);

    /// <summary>32-bit hash of a cell (<c>hash_cella</c> in terreno.py, bit for bit).</summary>
    public static uint CellHash(int x, int y, int salt)
    {
        unchecked
        {
            var h = ((uint)x * 0x27D4EB2Du) ^ ((uint)y * 0x165667B1u) ^ ((uint)salt * 0x9E3779B1u);
            h ^= h >> 15;
            h *= 0x2C1B3C6Du;
            h ^= h >> 12;
            h *= 0x297A2D39u;
            h ^= h >> 15;
            return h;
        }
    }

    /// <summary>Floor variant (0..3) of cell (<paramref name="x"/>, <paramref name="y"/>).</summary>
    public static int Variant(int x, int y) => (int)(CellHash(x, y, SaltVariant) % Variants);

    /// <summary>
    /// Dual-grid piece (0..15) of <paramref name="wall"/> on corner (<paramref name="x"/>, <paramref name="y"/>), the
    /// top-left corner of cell (x, y): bit NO=1 for cell (x-1, y-1), NE=2 for (x, y-1), SE=4 for (x, y), SO=8 for
    /// (x-1, y), set when that cell has the wall. No wall outside the map.
    /// </summary>
    public static int WallMask(TerrainMap m, WallKind wall, int x, int y)
    {
        ArgumentNullException.ThrowIfNull(m);
        return (Has(x - 1, y - 1) ? 1 : 0) | (Has(x, y - 1) ? 2 : 0) | (Has(x, y) ? 4 : 0) | (Has(x - 1, y) ? 8 : 0);

        bool Has(int cx, int cy) =>
            (uint)cx < (uint)m.Width && (uint)cy < (uint)m.Height && m[cx, cy].Wall == wall;
    }

    /// <summary>
    /// Edge pieces drawn on cell (<paramref name="x"/>, <paramref name="y"/>), lowest priority first (then strip order):
    /// a side piece (0..3 = N, E, S, O) for each side neighbour with a higher <paramref name="priority"/>, a corner
    /// piece (4..7 = NE, SE, SO, NO) for a higher diagonal neighbour when neither side next to that corner has the
    /// diagonal's floor. A neighbour past the map border counts as the cell itself.
    /// </summary>
    public static IEnumerable<(FloorKind Floor, int Piece)> EdgesOn(
        TerrainMap m, int x, int y, IReadOnlyDictionary<FloorKind, int> priority)
    {
        ArgumentNullException.ThrowIfNull(m);
        ArgumentNullException.ThrowIfNull(priority);
        var table = new int[Enum.GetValues<FloorKind>().Max(f => (int)f) + 1];
        foreach (var (kind, p) in priority)
        {
            table[(int)kind] = p;
        }

        Span<int> found = stackalloc int[8];
        var count = Edges(m, x, y, table, found);
        var result = new (FloorKind, int)[count];
        for (var i = 0; i < count; i++)
        {
            result[i] = ((FloorKind)(found[i] & 0xFF), (found[i] >> 8) & 0xFF);
        }

        return result;
    }

    /// <summary>Decoration look (0..2) of a land cell without wall or deposit, by biome density; null for none.</summary>
    public int? Decoration(TerrainMap m, int x, int y)
    {
        ArgumentNullException.ThrowIfNull(m);
        var cell = m[x, y];
        var f = (int)cell.Floor;
        if (cell.Wall != WallKind.None || cell.Deposit != DepositKind.None || _water[f] || _density[f] == 0
            || CellHash(x, y, SaltDecoration) % 100 >= (uint)_density[f])
        {
            return null;
        }

        return (int)(CellHash(x, y, SaltDecorationKind) % DecorationLooks);
    }

    /// <summary>
    /// Appends to <paramref name="into"/> the pieces of the window of cells (<paramref name="x0"/>,
    /// <paramref name="y0"/>) .. (x0 + <paramref name="w"/> - 1, y0 + <paramref name="h"/> - 1) in drawing order: per
    /// cell in row order its floor (or water bed), edges, then deposit or decoration; then, per wall kind, the
    /// dual-grid piece of every corner (x0..x0+w, y0..y0+h), centred on it (half a cell up and left of its cell).
    /// Neighbours come from the whole map, so windows drawn side by side and clipped to their cells match.
    /// </summary>
    public void Plan(TerrainMap m, int x0, int y0, int w, int h, List<TerrainPiece> into)
    {
        ArgumentNullException.ThrowIfNull(m);
        ArgumentNullException.ThrowIfNull(into);
        const int t = CellPixels;
        Span<int> edges = stackalloc int[8];
        for (var y = y0; y < y0 + h; y++)
        {
            for (var x = x0; x < x0 + w; x++)
            {
                var cell = m[x, y];
                var f = (int)cell.Floor;
                var px = x * t;
                var py = y * t;
                into.Add(new TerrainPiece(
                    _floorSprites[f][Variant(x, y)], 0, px, py, _water[f] ? TerrainPieceKind.Water : TerrainPieceKind.Floor));
                var count = Edges(m, x, y, _priority, edges);
                for (var i = 0; i < count; i++)
                {
                    into.Add(new TerrainPiece(
                        _edgeSprites[edges[i] & 0xFF], (edges[i] >> 8) & 0xFF, px, py, TerrainPieceKind.Edge));
                }

                if (cell.Deposit != DepositKind.None)
                {
                    var look = (int)(CellHash(x, y, SaltDeposit) % DepositLooks);
                    into.Add(new TerrainPiece(_depositSprites[(int)cell.Deposit][look], 0, px, py, TerrainPieceKind.Deposit));
                }
                else if (Decoration(m, x, y) is { } k)
                {
                    into.Add(new TerrainPiece(_decorationSprites[f][k], 0, px, py, TerrainPieceKind.Decoration));
                }
            }
        }

        foreach (var (kind, sprite) in _walls)
        {
            for (var y = y0; y <= y0 + h; y++)
            {
                for (var x = x0; x <= x0 + w; x++)
                {
                    var mask = WallMask(m, kind, x, y);
                    if (mask != 0)
                    {
                        into.Add(new TerrainPiece(sprite, mask, x * t - t / 2, y * t - t / 2, TerrainPieceKind.Wall));
                    }
                }
            }
        }
    }

    /// <summary>
    /// The edge rule (<c>pezzi_bordo</c>): fills <paramref name="into"/> with (piece &lt;&lt; 8 | floor), sorted by the
    /// neighbour's priority then piece, and returns the count.
    /// </summary>
    private static int Edges(TerrainMap m, int x, int y, int[] priority, Span<int> into)
    {
        var self = m[x, y].Floor;
        Span<FloorKind> around = stackalloc FloorKind[8];
        for (var i = 0; i < 8; i++)
        {
            var nx = x + Neighbours[i].Dx;
            var ny = y + Neighbours[i].Dy;
            around[i] = (uint)nx < (uint)m.Width && (uint)ny < (uint)m.Height ? m[nx, ny].Floor : self;
        }

        Span<long> keys = stackalloc long[8];
        var count = 0;
        for (var i = 0; i < 8; i++)
        {
            var other = around[i];
            if (priority[(int)other] <= priority[(int)self])
            {
                continue;
            }

            // Corner i (4..7 = NE, SE, SO, NO) sits between sides i-4 and i-3 (NO: N and O).
            if (i >= 4 && (around[i - 4] == other || around[i == 7 ? 0 : i - 3] == other))
            {
                continue;
            }

            keys[count++] = ((long)priority[(int)other] << 16) | ((long)i << 8) | (byte)other;
        }

        keys[..count].Sort();
        for (var i = 0; i < count; i++)
        {
            into[i] = (int)(keys[i] & 0xFFFF);
        }

        return count;
    }
}
