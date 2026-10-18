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
    /// <summary>Name of the shadow layer under a block (the playtest counts them).</summary>
    public const string ShadowName = "Ombra";

    /// <summary>Shadow of a block: its own sprite in black at 30 %, moved down-right (light from the top-left).</summary>
    public static readonly Vector2 ShadowOffset = new(6f, 6f);

    private static readonly Color ShadowColor = new(0f, 0f, 0f, 0.3f);

    /// <summary>
    /// Shadow z relative to the block (z 3 in the buildings layer at 8, so 11): 6 in the world, above the belts (≤ 5)
    /// and below the items on them (7), so a shadow falls on the belt and never over what it carries.
    /// </summary>
    private const int ShadowZ = -5;

    /// <summary>
    /// Graphics setting "Ombre" (off in playtests unless effects are forced on): blocks built from now on get a shadow
    /// layer. World rebuilds the building visuals when it changes, so none is left behind.
    /// </summary>
    public static bool Shadows { get; set; } = true;

    /// <summary>
    /// Brightness of the glowing parts (furnace mouth, Core nucleus, lamp dome): 1 without "Bagliore"; above 1 with it,
    /// an HDR colour that the glow of the world environment blooms.
    /// </summary>
    public static float GlowBoost { get; set; } = 1f;

    public static Node2D Create(string id, int footprintTiles, int tileSize)
    {
        var root = new Node2D { ZIndex = 3 };
        var texture = Sprites.For(id);
        AddShadow(root, texture, footprintTiles, tileSize);
        root.AddChild(Layer("Block", texture, footprintTiles, tileSize, 0));
        return root;
    }

    /// <summary>
    /// Shadow under <paramref name="root"/> (a block centred on its footprint) drawn from <paramref name="texture"/>,
    /// its base sprite; nothing when <see cref="Shadows"/> is off.
    /// </summary>
    public static void AddShadow(Node2D root, Texture2D texture, int cells, int tileSize)
    {
        if (!Shadows)
        {
            return;
        }

        var shadow = Layer(ShadowName, texture, cells, tileSize, ShadowZ);
        shadow.Position = ShadowOffset;
        shadow.Modulate = ShadowColor;
        root.AddChild(shadow);
    }

    /// <summary>Shadow of a sprite drawn from its top-left corner at <paramref name="scale"/> (the Core).</summary>
    public static void AddShadow(Node2D root, Texture2D texture, Vector2 scale)
    {
        if (!Shadows)
        {
            return;
        }

        root.AddChild(new Sprite2D
        {
            Name = ShadowName,
            Texture = texture,
            Centered = false,
            TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
            Scale = scale,
            Position = ShadowOffset,
            Modulate = ShadowColor,
            ZIndex = ShadowZ
        });
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
