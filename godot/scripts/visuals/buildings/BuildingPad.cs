using Godot;
using TIndustry.Shared;

namespace TIndustry.Godot;

/// <summary>
/// Building sprites in the world: <see cref="Layer"/> is one full-footprint layer of a block (base, moving
/// part, glow), <see cref="Create"/> a still block drawn by its <c>{id}.png</c> alone (power nodes), the same
/// sprite as the build palette and the research tree.
/// </summary>
public static class BuildingPad
{
    public static Node2D Create(string id, int footprintTiles, int tileSize)
    {
        var root = new Node2D { ZIndex = 3 };
        root.AddChild(Layer("Block", Sprites.For(id), footprintTiles, tileSize, 0));
        return root;
    }

    /// <summary>
    /// Start phase, in seconds, of a block's animation clock, derived from its origin cell so that neighbouring
    /// blocks do not turn in lockstep (every rebuild of the visuals restarts the clocks). Always 0 in playtests,
    /// so screenshots stay deterministic.
    /// </summary>
    public static float Phase(GridPosition origin)
    {
        if (GameDataPaths.IsPlaytest)
        {
            return 0f;
        }

        unchecked
        {
            var h = (uint)origin.X * 0x9E3779B1u ^ (uint)origin.Y * 0x85EBCA77u;
            h ^= h >> 15;
            h *= 0x2C1B3C6Du;
            h ^= h >> 12;
            return h % 10_000 / 1000f; // 0-10 s: longer than any loop, so every frame is a possible start
        }
    }

    /// <summary>Sprite layer spanning <paramref name="cells"/> cells, centred on the block (nearest filter).</summary>
    public static Sprite2D Layer(string name, Texture2D texture, int cells, int tileSize, int zIndex) =>
        new()
        {
            Name = name,
            Texture = texture,
            Centered = true,
            TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
            Scale = Vector2.One * Sprites.ScaleToCells(texture, cells, tileSize),
            ZIndex = zIndex
        };
}
