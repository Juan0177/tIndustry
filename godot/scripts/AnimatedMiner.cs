using Godot;

namespace TIndustry.Godot;

/// <summary>
/// Miner building driven by a real AnimationPlayer (sprite-sheet frames),
/// not a Raylib-style procedural GetTime() fake.
/// </summary>
public partial class AnimatedMiner : Node2D
{
    public override void _Ready()
    {
        var sprite = GetNode<Sprite2D>("Sprite");
        var player = GetNode<AnimationPlayer>("AnimationPlayer");

        var sheet = GD.Load<Texture2D>("res://assets/miner_drill_sheet.png");
        sprite.Texture = sheet;
        sprite.Hframes = 8;
        sprite.Vframes = 1;
        sprite.Frame = 0;
        sprite.Centered = true;

        var anim = new Animation
        {
            Length = 0.8f,
            LoopMode = Animation.LoopModeEnum.Linear
        };

        var frameTrack = anim.AddTrack(Animation.TrackType.Value);
        anim.TrackSetPath(frameTrack, new NodePath("Sprite:frame"));
        for (var i = 0; i < 8; i++)
        {
            anim.TrackInsertKey(frameTrack, i * 0.1f, i);
        }

        var scaleTrack = anim.AddTrack(Animation.TrackType.Value);
        anim.TrackSetPath(scaleTrack, new NodePath("Sprite:scale"));
        anim.TrackInsertKey(scaleTrack, 0f, new Vector2(1f, 1f));
        anim.TrackInsertKey(scaleTrack, 0.4f, new Vector2(1.06f, 0.96f));
        anim.TrackInsertKey(scaleTrack, 0.8f, new Vector2(1f, 1f));

        var library = new AnimationLibrary();
        library.AddAnimation("drill", anim);
        if (player.HasAnimationLibrary("miner"))
        {
            player.RemoveAnimationLibrary("miner");
        }

        player.AddAnimationLibrary("miner", library);
        player.Play("miner/drill");
    }
}
