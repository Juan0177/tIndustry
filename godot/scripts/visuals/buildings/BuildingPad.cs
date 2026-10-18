using Godot;

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
