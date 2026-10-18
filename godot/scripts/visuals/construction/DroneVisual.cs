using Godot;
using TIndustry.Shared;

namespace TIndustry.Godot;

/// <summary>
/// One construction drone (Mindustry's alpha): the <c>drone-1</c> sprite turned to its heading over its own
/// dark shadow, a 3-dot engine trail and, while it works, the build beam to its site. Position and heading are
/// interpolated between simulation ticks so flight stays smooth above 60 fps; an idle drone hovers
/// ±0.05 tile (graphics only). The node itself is never rotated: it draws in world-aligned local space and turns
/// only the sprite.
/// </summary>
public partial class DroneVisual : Node2D
{
    /// <summary>Alpha, the tier of the starting Core (beta and gamma: <c>drone-2</c>, <c>drone-3</c>).</summary>
    private const string SpriteId = "drone-1";

    /// <summary>Nose and engine of <c>drone-1</c> from its centre: 10 and 8 art px, drawn ×2 (64 px per tile).</summary>
    private const float NoseTiles = 10f / Sprites.PixelsPerCell;
    private const float TailTiles = 8f / Sprites.PixelsPerCell;

    /// <summary>Shadow: the same sprite in black at 35 %, 6 world px down-right (light from the top-left).</summary>
    private static readonly Vector2 ShadowOffset = new(6f, 6f);
    private static readonly Color ShadowColor = new(0f, 0f, 0f, 0.35f);

    private const float HoverTiles = 0.05f;
    private const float HoverRadPerSec = 2.6f;
    private const int TrailDots = 3;
    private const float TrailSampleSeconds = 0.06f;
    private const float BeamFillAlpha = 0.15f;
    private const float SnapTiles = 4f;

    private readonly Vector2[] _corners = new Vector2[4];
    private readonly Vector2[] _hullScratch = new Vector2[5];
    private readonly Vector2[][] _hulls = [new Vector2[3], new Vector2[4], new Vector2[5]];

    private Vector2 _from;
    private Vector2 _to;
    private float _fromRot;
    private float _toRot;
    private float _sinceTick;
    private long _lastTick = -1;
    private float _hover;
    private float _clock;
    private float _trailTimer;
    private readonly Queue<Vector2> _trail = new();
    private Rect2? _beamTarget;
    private Color _beamColor;

    public long BuilderId { get; private set; }

    public static DroneVisual Create(BuilderUnit builder) =>
        new() { Name = $"Drone_{builder.Id}", BuilderId = builder.Id, ZIndex = 2 };

    /// <summary>
    /// Takes the drone's state for this frame. <paramref name="beamTarget"/> is the site it works on
    /// (world rect), drawn with <paramref name="beamColor"/>; null when flying or idle.
    /// </summary>
    public void Sync(BuilderUnit builder, long tick, float delta, Rect2? beamTarget, Color beamColor)
    {
        var target = new Vector2(builder.X, builder.Y) * World.TileSize;
        var rot = Mathf.DegToRad(builder.Rotation);
        if (_lastTick < 0 || _to.DistanceTo(target) > SnapTiles * World.TileSize)
        {
            // First frame or a jump (load): no fly-in from the old place.
            _from = _to = target;
            _fromRot = _toRot = rot;
            _trail.Clear();
        }
        else if (tick != _lastTick)
        {
            // New tick: fly on from where the drone is drawn now towards the new position.
            _from = InterpolatedPosition();
            _fromRot = InterpolatedRotation();
            _to = target;
            _toRot = rot;
            _sinceTick = 0f;
        }

        _lastTick = tick;
        _sinceTick += delta;
        _clock += delta;

        var moving = _from.DistanceSquaredTo(_to) > 0.01f;
        _hover = Mathf.MoveToward(_hover, moving ? 0f : 1f, delta * 3f);

        var pos = InterpolatedPosition() + new Vector2(0f, Mathf.Sin(_clock * HoverRadPerSec) * HoverTiles * World.TileSize * _hover);
        Position = pos;

        _trailTimer += delta;
        if (_trailTimer >= TrailSampleSeconds)
        {
            _trailTimer = 0f;
            _trail.Enqueue(pos + Heading() * (-TailTiles * World.TileSize));
            while (_trail.Count > TrailDots)
            {
                _trail.Dequeue();
            }
        }

        _beamTarget = beamTarget;
        _beamColor = beamColor;
        QueueRedraw();
    }

    public override void _Draw()
    {
        var t = World.TileSize;
        var heading = Heading();
        var tex = Sprites.For(SpriteId);
        var scale = Vector2.One * (t / (float)Sprites.PixelsPerCell);
        var corner = -tex.GetSize() * 0.5f;

        DrawSetTransform(ShadowOffset, heading.Angle(), scale);
        DrawTexture(tex, corner, ShadowColor);
        DrawSetTransform(Vector2.Zero, 0f, Vector2.One);

        DrawBeam(heading * (NoseTiles * t));

        // Engine trail: oldest dot faintest and smallest.
        var i = 0;
        foreach (var dot in _trail)
        {
            var k = (i + 1) / (float)TrailDots;
            DrawCircle(dot - Position, t * 0.07f * k + 1.5f, new Color(ConstructionLayer.Accent, 0.55f * k));
            i++;
        }

        DrawSetTransform(Vector2.Zero, heading.Angle(), scale);
        DrawTexture(tex, corner);
        DrawSetTransform(Vector2.Zero, 0f, Vector2.One);
    }

    /// <summary>Four 1 px lines from the nose to the site's corners over a faint fan, with a flicker.</summary>
    private void DrawBeam(Vector2 nose)
    {
        if (_beamTarget is not { } rect)
        {
            return;
        }

        var flicker = 0.85f + 0.15f * Mathf.Sin(_clock * 23f);
        var r = new Rect2(rect.Position - Position, rect.Size);
        _corners[0] = r.Position;
        _corners[1] = new Vector2(r.End.X, r.Position.Y);
        _corners[2] = r.End;
        _corners[3] = new Vector2(r.Position.X, r.End.Y);
        if (FanOutline(nose, r) is { } fan)
        {
            DrawColoredPolygon(fan, new Color(_beamColor, BeamFillAlpha * flicker));
        }

        var line = new Color(_beamColor, flicker);
        foreach (var corner in _corners)
        {
            DrawLine(nose, corner, line, -1f);
        }
    }

    /// <summary>
    /// Convex hull of the nose and the site rect, in a reused buffer: the rect itself when the nose is inside,
    /// otherwise the nose plus the rect corners it does not hide (walked clockwise from the nose).
    /// </summary>
    private Vector2[]? FanOutline(Vector2 nose, Rect2 r)
    {
        if (r.HasPoint(nose))
        {
            return _corners;
        }

        // The nose sees a run of edges from outside. The corners inside that run (both edges seen) leave
        // the hull; the walk starts at the corner where the seen run ends, then goes round to where it begins.
        var start = 0;
        for (var i = 0; i < 4; i++)
        {
            if (SeesEdge((i + 3) % 4, nose) && !SeesEdge(i, nose))
            {
                start = i;
                break;
            }
        }

        var count = 0;
        _hullScratch[count++] = nose;
        for (var step = 0; step < 4; step++)
        {
            var i = (start + step) % 4;
            if (!(SeesEdge((i + 3) % 4, nose) && SeesEdge(i, nose)))
            {
                _hullScratch[count++] = _corners[i];
            }
        }

        if (count < 3)
        {
            return null;
        }

        var hull = _hulls[count - 3];
        Array.Copy(_hullScratch, hull, count);
        return hull;
    }

    /// <summary>
    /// True when <paramref name="nose"/> lies strictly outside edge <paramref name="i"/> (corner i → i+1).
    /// The corners run clockwise on screen (y down), so outside is where the cross product is negative.
    /// </summary>
    private bool SeesEdge(int i, Vector2 nose)
    {
        var a = _corners[i];
        var b = _corners[(i + 1) % 4];
        return (b - a).Cross(nose - a) < 0f;
    }

    private float Alpha() => Mathf.Clamp(_sinceTick / SimulationClock.TickSeconds, 0f, 1f);

    private Vector2 InterpolatedPosition() => _from.Lerp(_to, Alpha());

    private float InterpolatedRotation() => Mathf.LerpAngle(_fromRot, _toRot, Alpha());

    private Vector2 Heading() => Vector2.FromAngle(InterpolatedRotation());
}
