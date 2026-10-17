using Godot;
using TIndustry.Shared;

namespace TIndustry.Godot;

/// <summary>
/// One construction drone (Mindustry's alpha): a winged triangle in the accent colour with a dark edge,
/// a 3-dot engine trail and, while it works, the build beam to its site. Position and heading are
/// interpolated between simulation ticks so flight stays smooth above 60 fps; an idle drone hovers
/// ±0.05 tile (graphics only). The node itself is never rotated: it draws in world-aligned local space.
/// </summary>
public partial class DroneVisual : Node2D
{
    /// <summary>Wingspan ≈ 0.9 tile.</summary>
    private const float NoseTiles = 0.42f;
    private const float TailTiles = 0.30f;
    private const float HoverTiles = 0.05f;
    private const float HoverRadPerSec = 2.6f;
    private const int TrailDots = 3;
    private const float TrailSampleSeconds = 0.06f;
    private const float BeamFillAlpha = 0.15f;
    private const float SnapTiles = 4f;

    private static readonly Color Edge = new(0.16f, 0.13f, 0.09f, 1f);

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
        var side = new Vector2(-heading.Y, heading.X);

        DrawBeam(heading * (NoseTiles * t));

        // Engine trail: oldest dot faintest and smallest.
        var i = 0;
        foreach (var dot in _trail)
        {
            var k = (i + 1) / (float)TrailDots;
            DrawCircle(dot - Position, t * 0.07f * k + 1.5f, new Color(ConstructionLayer.Accent, 0.55f * k));
            i++;
        }

        Vector2 P(float fwd, float across) => heading * (fwd * t) + side * (across * t);

        Vector2[] leftWing = [P(0.06f, -0.08f), P(-0.26f, -0.45f), P(-0.36f, -0.40f), P(-0.20f, -0.07f)];
        Vector2[] rightWing = [P(0.06f, 0.08f), P(-0.20f, 0.07f), P(-0.36f, 0.40f), P(-0.26f, 0.45f)];
        Vector2[] body = [P(NoseTiles, 0f), P(-TailTiles, -0.17f), P(-0.20f, 0f), P(-TailTiles, 0.17f)];
        var wingColor = ConstructionLayer.Accent.Darkened(0.18f);
        foreach (var poly in new[] { leftWing, rightWing, body })
        {
            DrawColoredPolygon(poly, poly == body ? ConstructionLayer.Accent : wingColor);
            DrawPolyline([.. poly, poly[0]], Edge, 2f);
        }
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
        Vector2[] corners = [r.Position, new(r.End.X, r.Position.Y), r.End, new(r.Position.X, r.End.Y)];
        var hull = Geometry2D.ConvexHull([nose, .. corners]);
        if (hull.Length >= 3)
        {
            DrawColoredPolygon(hull, new Color(_beamColor, BeamFillAlpha * flicker));
        }

        var line = new Color(_beamColor, flicker);
        foreach (var corner in corners)
        {
            DrawLine(nose, corner, line, -1f);
        }
    }

    private float Alpha() => Mathf.Clamp(_sinceTick / SimulationClock.TickSeconds, 0f, 1f);

    private Vector2 InterpolatedPosition() => _from.Lerp(_to, Alpha());

    private float InterpolatedRotation() => Mathf.LerpAngle(_fromRot, _toRot, Alpha());

    private Vector2 Heading() => Vector2.FromAngle(InterpolatedRotation());
}
