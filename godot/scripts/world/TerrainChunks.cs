using System.Diagnostics;
using Godot;
using TIndustry.Shared;

namespace TIndustry.Godot;

/// <summary>
/// Terrain drawn as chunks, at two levels of detail. Colour chunks (<see cref="ChunkCells"/> cells a side) are a
/// <see cref="Sprite2D"/> with a one-pixel-per-cell <see cref="ImageTexture"/> scaled by the tile size (Nearest);
/// they are always kept for the view and are all there is when a cell is under <see cref="ArtMinCellPx"/> on
/// screen. From there up, art chunks (<see cref="ArtChunkCells"/> cells a side, 32 px per cell, composed by
/// <see cref="TerrainComposer"/> from the terrain sprites) are drawn over them with <c>water.gdshader</c>; at most
/// <see cref="ArtChunksPerFrame"/> are composed per frame, nearest the view first, and until its art is ready a
/// chunk shows its colours (no holes). Each kind has its own LRU cache; textures are disposed on eviction (an
/// evicted art chunk lends its texture to the next one), map change and exit. <see cref="BuildChunkImage"/> and
/// <see cref="TerrainComposer.Compose"/> are the only places that turn cells into pixels.
/// </summary>
public partial class TerrainChunks : Node2D
{
    public const int ChunkCells = 64;

    /// <summary>
    /// Most colour chunks kept alive; the view at minimum zoom needs well under this on a 1080p screen. A bigger
    /// view keeps at least the chunks it touches (no rebuild every frame).
    /// </summary>
    public const int MaxCachedChunks = 256;

    /// <summary>Cells a side of an art chunk: 512×512 px, 1 MiB of texture.</summary>
    public const int ArtChunkCells = 16;

    /// <summary>
    /// Most art chunks kept alive, or the ones the view and its margin touch if more (about 190 on a 4K screen at
    /// zoom 0.25), so a big screen never evicts a chunk it is showing.
    /// </summary>
    public const int MaxArtChunks = 96;

    /// <summary>Art chunks composed per frame at most, so a zoom-in or a fast pan never stalls a frame.</summary>
    public const int ArtChunksPerFrame = 4;

    /// <summary>
    /// Composition time per frame (ms) past which no further art chunk starts in that frame (at least one always
    /// does): on a slower machine fewer than <see cref="ArtChunksPerFrame"/> per frame, never a longer frame.
    /// </summary>
    public const double ArtFrameBudgetMs = 3.0;

    /// <summary>Cell size on screen (px) from which the terrain is drawn with its art (zoom 0.25 with 64-unit cells).</summary>
    public const float ArtMinCellPx = 16f;

    /// <summary>Chunks built around the view on each side, so panning rarely shows a missing one.</summary>
    private const int MarginChunks = 1;

    private static readonly Color MissingColor = new(0.1f, 0.12f, 0.1f);

    private readonly ChunkCache _colour = new();
    private readonly ChunkCache _art = new();
    private readonly List<(Vector2I Key, long Order)> _missingArt = [];
    private readonly Stopwatch _composeClock = new();
    private readonly Stopwatch _frameClock = new();
    private Node2D _colourLayer = null!;
    private Node2D _artLayer = null!;
    private ShaderMaterial _water = null!;
    private TerrainMap? _map;
    private TerrainTable? _table;
    private TerrainLook? _look;
    private TerrainSprites? _sprites;
    private Color[] _floorColors = [];
    private Color[] _wallColors = [];
    private int _tileSize = 64;
    private double _waterTime;

    /// <summary>Colour chunks alive now (for the performance probe).</summary>
    public int ChunkCount => _colour.Count;

    /// <summary>Colour chunk images built since the map was set (for the performance probe).</summary>
    public int BuiltSinceReset { get; private set; }

    /// <summary>Art chunks alive now.</summary>
    public int ArtChunkCount => _art.Count;

    /// <summary>Art chunks composed since the map was set.</summary>
    public int ArtBuiltSinceReset { get; private set; }

    /// <summary>Most art chunks alive at once since <see cref="ResetProbe"/> (or the map was set).</summary>
    public int ArtPeak { get; private set; }

    /// <summary>Slowest art chunk (composition and texture upload, ms) since <see cref="ResetProbe"/>.</summary>
    public double SlowestArtMs { get; private set; }

    /// <summary>Art chunks of the view still showing their colours after the last <see cref="Sync"/>.</summary>
    public int ArtPending { get; private set; }

    /// <summary>True when the last <see cref="Sync"/> drew the art level of detail.</summary>
    public bool ArtShown => _artLayer.Visible;

    /// <summary>
    /// Period (s) of the water ripples: <c>water.gdshader</c> repeats exactly after it (lattice of 256 cells, drift
    /// speeds in hundredths), so the clock wraps without a jump.
    /// </summary>
    public const double WaterPeriod = 25600.0;

    /// <summary>True when cell (<paramref name="x"/>, <paramref name="y"/>) is drawn with its art now.</summary>
    public bool HasArtAt(int x, int y) =>
        _artLayer.Visible && _art.Contains(new Vector2I(x / ArtChunkCells, y / ArtChunkCells));

    /// <summary>Water ripples move (false with Effetti Spenti: still water, the shader time stays where it is).</summary>
    public bool AnimateWater { get; set; } = true;

    public override void _Ready()
    {
        // World draws deposits, grid and ghost in its own _Draw: the terrain goes under them.
        ShowBehindParent = true;
        TextureFilter = TextureFilterEnum.Nearest;
        _colourLayer = new Node2D { Name = "Colori" };
        _water = new ShaderMaterial { Shader = GD.Load<Shader>("res://shaders/water.gdshader") };
        _artLayer = new Node2D { Name = "Arte", Material = _water, Visible = false };
        AddChild(_colourLayer);
        AddChild(_artLayer);
    }

    public override void _Process(double delta)
    {
        // Playtests keep the ripples still, like the other clocks, so captures are stable; Effetti Spenti too.
        if (_artLayer.Visible && AnimateWater && !GameDataPaths.IsPlaytest)
        {
            _waterTime = (_waterTime + delta) % WaterPeriod;
            _water.SetShaderParameter("time", (float)_waterTime);
        }
    }

    /// <summary>
    /// Points the layer at <paramref name="map"/> with the colours and art of <paramref name="table"/>; a different
    /// map or table drops every chunk (they are rebuilt on demand). The same pair is a no-op.
    /// </summary>
    public void SetMap(TerrainMap? map, TerrainTable table, int tileSize)
    {
        if (ReferenceEquals(map, _map) && ReferenceEquals(table, _table) && tileSize == _tileSize)
        {
            return;
        }

        Clear();
        if (!ReferenceEquals(table, _table))
        {
            _look = table.Floors.Count > 0 ? new TerrainLook(table) : null;
            _sprites = _look is null ? null : new TerrainSprites(_look.SpriteNames);
            // Colour chunks take the average colour of each floor's and wall's art, so the terrain keeps its tone
            // across the two levels of detail; without art, the table colour (which itself never changes: it keys
            // the campaign map PNGs).
            _floorColors = Enum.GetValues<FloorKind>()
                .Select(k => _look is not null && _sprites is not null
                    ? AverageColor(Enumerable.Range(0, TerrainLook.Variants).Select(v => (_look.FloorSprite(k, v), 0)))
                    : table.Floor(k) is { } floor ? Color.FromHtml(floor.Color) : MissingColor)
                .ToArray();
            _wallColors = Enum.GetValues<WallKind>()
                .Select(k => _look?.WallSprite(k) is { } wallSprite && _sprites is not null
                    ? AverageColor([(wallSprite, 15)])
                    : table.Wall(k) is { } wall ? Color.FromHtml(wall.Color) : MissingColor)
                .ToArray();
        }

        _map = map;
        _table = table;
        _tileSize = tileSize;
        _water.SetShaderParameter("art_pixel", tileSize / (float)TerrainLook.CellPixels);
        BuiltSinceReset = 0;
        ArtBuiltSinceReset = 0;
        ResetProbe();
    }

    /// <summary>Average colour of the opaque pixels of the given pieces (sprite, piece index) of the terrain strips.</summary>
    private Color AverageColor(IEnumerable<(string Sprite, int Index)> pieces)
    {
        long r = 0, g = 0, b = 0, n = 0;
        foreach (var (name, index) in pieces)
        {
            var strip = _sprites!.Get(name);
            var x0 = index * TerrainLook.CellPixels;
            if (x0 + TerrainLook.CellPixels > strip.Width)
            {
                continue;
            }

            for (var y = 0; y < Math.Min(TerrainLook.CellPixels, strip.Height); y++)
            {
                for (var x = x0; x < x0 + TerrainLook.CellPixels; x++)
                {
                    var i = (y * strip.Width + x) * 4;
                    if (strip.Rgba[i + 3] == 255)
                    {
                        r += strip.Rgba[i];
                        g += strip.Rgba[i + 1];
                        b += strip.Rgba[i + 2];
                        n++;
                    }
                }
            }
        }

        return n == 0 ? MissingColor : Color.Color8((byte)(r / n), (byte)(g / n), (byte)(b / n));
    }

    /// <summary>Restarts <see cref="ArtPeak"/> and <see cref="SlowestArtMs"/> (performance probe).</summary>
    public void ResetProbe()
    {
        ArtPeak = _art.Count;
        SlowestArtMs = 0;
    }

    /// <summary>
    /// Builds the chunks covering cells <paramref name="minX"/>..<paramref name="maxY"/> plus the margin: colour
    /// chunks always, art chunks too when a cell is <paramref name="cellPx"/> ≥ <see cref="ArtMinCellPx"/> on screen.
    /// </summary>
    public void Sync(int minX, int minY, int maxX, int maxY, float cellPx)
    {
        if (_map is not { } map)
        {
            _artLayer.Visible = false;
            ArtPending = 0;
            return;
        }

        var colourTouched = TouchRange(_colour, ChunkCells, map, minX, minY, maxX, maxY, null);
        _colour.Evict(Math.Max(MaxCachedChunks, colourTouched));

        var art = cellPx >= ArtMinCellPx && _look is not null && _sprites is not null;
        _artLayer.Visible = art;
        ArtPending = 0;
        if (!art)
        {
            return;
        }

        _missingArt.Clear();
        var artTouched = TouchRange(_art, ArtChunkCells, map, minX, minY, maxX, maxY, _missingArt);
        // Chunks in view first, then the margin; nearest the view centre first within each.
        _missingArt.Sort((a, b) => a.Order.CompareTo(b.Order));
        var cap = Math.Max(MaxArtChunks, artTouched);
        var composed = 0;
        _frameClock.Restart();
        while (composed < Math.Min(ArtChunksPerFrame, _missingArt.Count)
               && (composed == 0 || _frameClock.Elapsed.TotalMilliseconds < ArtFrameBudgetMs))
        {
            AddArtChunk(map, _missingArt[composed++].Key, cap);
        }

        ArtPending = _missingArt.Count - composed;

        _art.Evict(cap);
        ArtPeak = Math.Max(ArtPeak, _art.Count);
    }

    /// <summary>
    /// Touches the chunks of <paramref name="cache"/> over the cell range plus the margin and returns how many there
    /// are. Missing colour chunks are built at once; missing art chunks go to <paramref name="missing"/> with their
    /// drawing order instead.
    /// </summary>
    private int TouchRange(
        ChunkCache cache, int cells, TerrainMap map, int minX, int minY, int maxX, int maxY,
        List<(Vector2I Key, long Order)>? missing)
    {
        var lastCx = (map.Width - 1) / cells;
        var lastCy = (map.Height - 1) / cells;
        var v0x = Math.Max(0, minX / cells);
        var v0y = Math.Max(0, minY / cells);
        var v1x = Math.Min(lastCx, maxX / cells);
        var v1y = Math.Min(lastCy, maxY / cells);
        var c0x = Math.Max(0, v0x - MarginChunks);
        var c0y = Math.Max(0, v0y - MarginChunks);
        var c1x = Math.Min(lastCx, v1x + MarginChunks);
        var c1y = Math.Min(lastCy, v1y + MarginChunks);
        // Twice the view centre, in chunks (integer distances).
        var mx2 = v0x + v1x;
        var my2 = v0y + v1y;
        for (var cy = c0y; cy <= c1y; cy++)
        {
            for (var cx = c0x; cx <= c1x; cx++)
            {
                var key = new Vector2I(cx, cy);
                if (cache.Touch(key))
                {
                    continue;
                }

                if (missing is null)
                {
                    AddColourChunk(key);
                    continue;
                }

                var inView = cx >= v0x && cx <= v1x && cy >= v0y && cy <= v1y;
                long dx = 2 * cx - mx2;
                long dy = 2 * cy - my2;
                missing.Add((key, (inView ? 0L : 1L << 40) + dx * dx + dy * dy));
            }
        }

        return (c1x - c0x + 1) * (c1y - c0y + 1);
    }

    private void AddColourChunk(Vector2I key)
    {
        // The texture keeps its own copy: free the image now instead of leaving it to the finalizer.
        using var image = BuildChunkImage(key.X, key.Y);
        var texture = ImageTexture.CreateFromImage(image);
        var sprite = new Sprite2D
        {
            Texture = texture,
            Centered = false,
            Position = new Vector2(key.X * ChunkCells * _tileSize, key.Y * ChunkCells * _tileSize),
            Scale = new Vector2(_tileSize, _tileSize),
            TextureFilter = TextureFilterEnum.Nearest
        };
        _colourLayer.AddChild(sprite);
        _colour.Add(new Chunk(key, sprite, texture));
        BuiltSinceReset++;
    }

    /// <summary>
    /// Composes art chunk <paramref name="key"/>. With the cache full (<paramref name="cap"/>), the least recently
    /// used chunk (never one the view touched this frame) hands over its sprite and texture, updated in place, so a
    /// long pan does not allocate and free a 1 MiB texture per chunk.
    /// </summary>
    private void AddArtChunk(TerrainMap map, Vector2I key, int cap)
    {
        _composeClock.Restart();
        var old = _art.Count >= cap ? _art.TakeOldest() : null;
        using var image = TerrainComposer.Compose(_look!, map, key.X, key.Y, _sprites!);
        var position = new Vector2(key.X * ArtChunkCells * _tileSize, key.Y * ArtChunkCells * _tileSize);
        Chunk chunk;
        if (old is not null && old.Texture.GetSize() == image.GetSize())
        {
            old.Texture.Update(image);
            old.Sprite.Position = position;
            chunk = old with { Key = key };
        }
        else
        {
            old?.Free();
            var texture = ImageTexture.CreateFromImage(image);
            var scale = _tileSize / (float)TerrainLook.CellPixels;
            var sprite = new Sprite2D
            {
                Texture = texture,
                Centered = false,
                Position = position,
                Scale = new Vector2(scale, scale),
                TextureFilter = TextureFilterEnum.Nearest,
                UseParentMaterial = true
            };
            _artLayer.AddChild(sprite);
            chunk = new Chunk(key, sprite, texture);
        }

        _art.Add(chunk);
        ArtBuiltSinceReset++;
        SlowestArtMs = Math.Max(SlowestArtMs, _composeClock.Elapsed.TotalMilliseconds);
    }

    private void Clear()
    {
        _colour.Clear();
        _art.Clear();
    }

    /// <summary>
    /// Image of chunk (<paramref name="cx"/>, <paramref name="cy"/>), one pixel per cell; edge chunks are cut to the
    /// map. Floors and walls take the average colour of their art (the table colour without art) with a small
    /// variation hashed from the cell (the same on every build); walls are darkened with a lighter rim where they
    /// meet open ground.
    /// </summary>
    private Image BuildChunkImage(int cx, int cy)
    {
        var map = _map!;
        var x0 = cx * ChunkCells;
        var y0 = cy * ChunkCells;
        var w = Math.Min(ChunkCells, map.Width - x0);
        var h = Math.Min(ChunkCells, map.Height - y0);
        var bytes = new byte[w * h * 3];
        var i = 0;
        for (var y = y0; y < y0 + h; y++)
        {
            for (var x = x0; x < x0 + w; x++)
            {
                var c = CellColor(map, x, y);
                bytes[i++] = (byte)Math.Clamp((int)MathF.Round(c.R * 255f), 0, 255);
                bytes[i++] = (byte)Math.Clamp((int)MathF.Round(c.G * 255f), 0, 255);
                bytes[i++] = (byte)Math.Clamp((int)MathF.Round(c.B * 255f), 0, 255);
            }
        }

        return Image.CreateFromData(w, h, false, Image.Format.Rgb8, bytes);
    }

    private Color CellColor(TerrainMap map, int x, int y)
    {
        var cell = map[x, y];
        var walled = cell.Wall != WallKind.None;
        Color baseColor;
        if (walled)
        {
            var wall = (int)cell.Wall < _wallColors.Length ? _wallColors[(int)cell.Wall] : MissingColor;
            baseColor = IsWallEdge(map, x, y) ? wall.Lightened(0.22f) : wall.Darkened(0.12f);
        }
        else
        {
            baseColor = (int)cell.Floor < _floorColors.Length ? _floorColors[(int)cell.Floor] : MissingColor;
        }

        var n1 = Hash01(x, y, 917);
        var n2 = Hash01(x * 3 + 7, y * 5 - 3, 421);
        var delta = ((n1 - 0.5f) * 22f + (n2 - 0.5f) * 10f) / 255f;
        var water = !walled && cell.Floor is FloorKind.ShallowWater or FloorKind.DeepWater;
        if (water)
        {
            delta *= 0.5f;
        }

        return new Color(
            Math.Clamp(baseColor.R + delta, 0f, 1f),
            Math.Clamp(baseColor.G + delta, 0f, 1f),
            Math.Clamp(baseColor.B + delta, 0f, 1f));
    }

    /// <summary>A wall cell with an open (non-wall) neighbour on one of its four sides; the map edge counts as wall.</summary>
    private static bool IsWallEdge(TerrainMap map, int x, int y) =>
        Open(map, x - 1, y) || Open(map, x + 1, y) || Open(map, x, y - 1) || Open(map, x, y + 1);

    private static bool Open(TerrainMap map, int x, int y) =>
        (uint)x < (uint)map.Width && (uint)y < (uint)map.Height && map[x, y].Wall == WallKind.None;

    /// <summary>Deterministic per-cell noise in [0, 1].</summary>
    internal static float Hash01(int x, int y, int salt)
    {
        unchecked
        {
            var v = (uint)(x * 374761393 + y * 668265263 + salt * 1442695041);
            v = (v ^ (v >> 13)) * 1274126177u;
            return (v ^ (v >> 16)) / (float)uint.MaxValue;
        }
    }

    public override void _ExitTree() => Clear();

    /// <summary>Chunks by key with an LRU order (most recently used first; eviction takes from the end).</summary>
    private sealed class ChunkCache
    {
        private readonly Dictionary<Vector2I, LinkedListNode<Chunk>> _chunks = [];
        private readonly LinkedList<Chunk> _lru = new();

        public int Count => _chunks.Count;

        public bool Contains(Vector2I key) => _chunks.ContainsKey(key);

        /// <summary>Moves chunk <paramref name="key"/> to the front; false when it is not built.</summary>
        public bool Touch(Vector2I key)
        {
            if (!_chunks.TryGetValue(key, out var node))
            {
                return false;
            }

            if (node != _lru.First)
            {
                _lru.Remove(node);
                _lru.AddFirst(node);
            }

            return true;
        }

        public void Add(Chunk chunk) => _chunks[chunk.Key] = _lru.AddFirst(chunk);

        /// <summary>Removes the least recently used chunk without freeing it (the caller reuses or frees it).</summary>
        public Chunk? TakeOldest()
        {
            if (_lru.Last is not { } oldest)
            {
                return null;
            }

            _lru.RemoveLast();
            _chunks.Remove(oldest.Value.Key);
            return oldest.Value;
        }

        /// <summary>Frees the least recently used chunks past <paramref name="cap"/> (the ones just touched are at the front).</summary>
        public void Evict(int cap)
        {
            while (_lru.Count > cap && _lru.Last is { } oldest)
            {
                _lru.RemoveLast();
                _chunks.Remove(oldest.Value.Key);
                oldest.Value.Free();
            }
        }

        public void Clear()
        {
            foreach (var chunk in _lru)
            {
                chunk.Free();
            }

            _lru.Clear();
            _chunks.Clear();
        }
    }

    /// <summary>A chunk's sprite and the texture it owns.</summary>
    private sealed record Chunk(Vector2I Key, Sprite2D Sprite, ImageTexture Texture)
    {
        /// <summary>
        /// Frees the sprite and releases the texture now: left to the .NET finalizer, the texture stays in memory
        /// until a GC, and wrappers still pending at exit crash Godot's shutdown.
        /// </summary>
        public void Free()
        {
            Sprite.Texture = null;
            Sprite.QueueFree();
            Texture.Dispose();
        }
    }
}
