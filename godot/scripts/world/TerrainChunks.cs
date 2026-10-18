using Godot;
using TIndustry.Shared;

namespace TIndustry.Godot;

/// <summary>
/// Terrain drawn as chunks of <see cref="ChunkCells"/>×<see cref="ChunkCells"/> cells: each chunk is a
/// <see cref="Sprite2D"/> with a one-pixel-per-cell <see cref="ImageTexture"/> scaled by the tile size (Nearest).
/// Only the chunks in view (plus a margin) are built; far ones are freed past an LRU cap. Chunk images are
/// built once per map (new game or load), never per frame. <see cref="BuildChunkImage"/> is the one place that
/// turns cells into pixels, so real terrain art (G2b) can replace it with composed sprite chunks.
/// </summary>
public partial class TerrainChunks : Node2D
{
    public const int ChunkCells = 64;

    /// <summary>
    /// Most chunks kept alive; the view at minimum zoom needs well under this on a 1080p screen. A bigger view keeps
    /// at least the chunks it touches (no rebuild every frame).
    /// </summary>
    public const int MaxCachedChunks = 256;

    /// <summary>Chunks built around the view on each side, so panning rarely shows a missing one.</summary>
    private const int MarginChunks = 1;

    private static readonly Color MissingColor = new(0.1f, 0.12f, 0.1f);

    private readonly Dictionary<Vector2I, LinkedListNode<Chunk>> _chunks = [];
    // Most recently used first; eviction takes from the end.
    private readonly LinkedList<Chunk> _lru = new();
    private TerrainMap? _map;
    private TerrainTable? _table;
    private Color[] _floorColors = [];
    private Color[] _wallColors = [];
    private int _tileSize = 64;

    /// <summary>Chunks alive now (for the performance probe).</summary>
    public int ChunkCount => _chunks.Count;

    /// <summary>Chunk images built since the map was set (for the performance probe).</summary>
    public int BuiltSinceReset { get; private set; }

    public override void _Ready()
    {
        // World draws deposits, grid and ghost in its own _Draw: the terrain goes under them.
        ShowBehindParent = true;
        TextureFilter = TextureFilterEnum.Nearest;
    }

    /// <summary>
    /// Points the layer at <paramref name="map"/> with the colours of <paramref name="table"/>; a different map or
    /// table drops every chunk (they are rebuilt on demand). The same pair is a no-op.
    /// </summary>
    public void SetMap(TerrainMap? map, TerrainTable table, int tileSize)
    {
        if (ReferenceEquals(map, _map) && ReferenceEquals(table, _table) && tileSize == _tileSize)
        {
            return;
        }

        Clear();
        _map = map;
        _table = table;
        _tileSize = tileSize;
        _floorColors = Enum.GetValues<FloorKind>()
            .Select(k => table.Floor(k) is { } floor ? Color.FromHtml(floor.Color) : MissingColor)
            .ToArray();
        _wallColors = Enum.GetValues<WallKind>()
            .Select(k => table.Wall(k) is { } wall ? Color.FromHtml(wall.Color) : MissingColor)
            .ToArray();
        BuiltSinceReset = 0;
    }

    /// <summary>Builds the chunks covering cells <paramref name="minX"/>..<paramref name="maxY"/> plus the margin.</summary>
    public void Sync(int minX, int minY, int maxX, int maxY)
    {
        if (_map is not { } map)
        {
            return;
        }

        var lastCx = (map.Width - 1) / ChunkCells;
        var lastCy = (map.Height - 1) / ChunkCells;
        var c0x = Math.Max(0, minX / ChunkCells - MarginChunks);
        var c0y = Math.Max(0, minY / ChunkCells - MarginChunks);
        var c1x = Math.Min(lastCx, maxX / ChunkCells + MarginChunks);
        var c1y = Math.Min(lastCy, maxY / ChunkCells + MarginChunks);
        var touched = (c1x - c0x + 1) * (c1y - c0y + 1);
        for (var cy = c0y; cy <= c1y; cy++)
        {
            for (var cx = c0x; cx <= c1x; cx++)
            {
                Touch(new Vector2I(cx, cy));
            }
        }

        // The ones just touched are at the front: evict from the back only past the cap, never one in view.
        var cap = Math.Max(MaxCachedChunks, touched);
        while (_lru.Count > cap && _lru.Last is { } oldest)
        {
            _lru.RemoveLast();
            _chunks.Remove(oldest.Value.Key);
            oldest.Value.Free();
        }
    }

    private void Touch(Vector2I key)
    {
        if (_chunks.TryGetValue(key, out var node))
        {
            if (node != _lru.First)
            {
                _lru.Remove(node);
                _lru.AddFirst(node);
            }

            return;
        }

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
        AddChild(sprite);
        _chunks[key] = _lru.AddFirst(new Chunk(key, sprite, texture));
        BuiltSinceReset++;
    }

    private void Clear()
    {
        foreach (var chunk in _lru)
        {
            chunk.Free();
        }

        _lru.Clear();
        _chunks.Clear();
    }

    /// <summary>
    /// Image of chunk (<paramref name="cx"/>, <paramref name="cy"/>), one pixel per cell; edge chunks are cut to the
    /// map. Floors and walls take the terrain table colour with a small variation hashed from the cell (the same on
    /// every build); walls are darkened with a lighter rim where they meet open ground.
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
