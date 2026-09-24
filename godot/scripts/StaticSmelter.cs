using Godot;

namespace TIndustry.Godot;

/// <summary>Static forno icon — no craft animation yet.</summary>
public partial class StaticSmelter : Node2D
{
    public override void _Ready()
    {
        EnsureSprite();
    }

    public void EnsureSprite()
    {
        Sprite2D sprite;
        if (HasNode("Sprite"))
        {
            sprite = GetNode<Sprite2D>("Sprite");
        }
        else
        {
            sprite = new Sprite2D { Name = "Sprite", Centered = true };
            AddChild(sprite);
        }

        sprite.Texture = GD.Load<Texture2D>("res://assets/smelter.png");
        sprite.Centered = true;
        sprite.Scale = Vector2.One * 1.15f;
    }
}
