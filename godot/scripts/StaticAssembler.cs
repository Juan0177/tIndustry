using Godot;

namespace TIndustry.Godot;

/// <summary>Static assembler icon — craft animation later.</summary>
public partial class StaticAssembler : Node2D
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

        sprite.Texture = GD.Load<Texture2D>("res://assets/assembler.png");
        sprite.Centered = true;
        sprite.Scale = Vector2.One * 1.15f;
    }
}
