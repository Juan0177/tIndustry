using Godot;

namespace TIndustry.Godot;

/// <summary>
/// Static miner building icon (same approach as Raylib): no drill loop / tip spin.
/// Real building animations can land later when intentionally designed — not idle trivella spin.
/// </summary>
public partial class StaticMiner : Node2D
{
    public override void _Ready()
    {
        var sprite = GetNode<Sprite2D>("Sprite");
        sprite.Texture = GD.Load<Texture2D>("res://assets/miner.png");
        sprite.Centered = true;
        sprite.Hframes = 1;
        sprite.Vframes = 1;
        sprite.Frame = 0;
        sprite.Scale = Vector2.One;
    }
}
