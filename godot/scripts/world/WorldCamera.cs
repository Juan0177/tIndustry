using Godot;

namespace TIndustry.Godot;

/// <summary>
/// The world camera (Mindustry): it follows the player's drone softly (<see cref="FollowTarget"/>); the middle button held
/// drags the view away and on release it goes back to the drone; the wheel zooms (without Ctrl it may turn the block in
/// hand instead, <see cref="WheelZooms"/>); clamps to the map AABB (Raylib ClampToMap). WASD moves the drone, not the
/// camera (World).
/// </summary>
public partial class WorldCamera : Camera2D
{
    [Export] public float MinZoom { get; set; } = 0.08f;
    [Export] public float MaxZoom { get; set; } = 2.5f;
    [Export] public float ZoomStep { get; set; } = 0.1f;

    /// <summary>
    /// How fast the view catches up with the drone: the gap shrinks by <c>1 − e^(−rate·dt)</c> each frame (about 95% in
    /// a third of a second), so the follow is soft at any frame rate.
    /// </summary>
    [Export] public float FollowRate { get; set; } = 9f;

    /// <summary>World size in pixels (map tiles × tile size). Zero = no clamp.</summary>
    public float MapPixelWidth { get; set; }
    public float MapPixelHeight { get; set; }

    /// <summary>False while the Home is open: no pan, no zoom from the player.</summary>
    public bool InputEnabled { get; set; } = true;

    /// <summary>
    /// Where the view follows (the drone's centre, world pixels), set by the world every frame; null = the camera stays
    /// where it is (Home, playtests that frame their own captures).
    /// </summary>
    public Vector2? FollowTarget { get; set; }

    /// <summary>
    /// The view was sent somewhere on purpose (the build queue's focus, the agents' <c>telecamera</c>): it stays there
    /// instead of following, until the drone moves or the middle button drags the view again.
    /// </summary>
    public bool FreeLook { get; set; }

    /// <summary>True while the middle button drags the view (no follow until it goes up).</summary>
    public bool Dragging => _dragging;

    /// <summary>
    /// Home background: screen px per second the camera drifts on its own, bouncing off the map edges (0 = still).
    /// </summary>
    public float DriftSpeed { get; set; }

    private Vector2 _driftDir = new Vector2(1f, 0.55f).Normalized();

    /// <summary>
    /// Asked on every wheel step without Ctrl: false lets the step through to the world (it turns the block in hand).
    /// With Ctrl the wheel always zooms.
    /// </summary>
    public Func<bool> WheelZooms { get; set; } = () => true;

    private bool _dragging;
    private Vector2 _dragLast;

    /// <summary>
    /// How fast a focus (<see cref="BeginFocus"/>) moves and zooms the view: the gap shrinks by <c>1 − e^(−rate·dt)</c>
    /// each frame (about 95% in 0.75 s, settled within the jackpot sequence's one-second zoom).
    /// </summary>
    [Export] public float FocusRate { get; set; } = 4f;

    // Where a focus holds the view and at which zoom (null: no focus), the zoom to go back to when it ends, and the zoom
    // the view is easing back to after it (null: none).
    private Vector2? _focus;
    private float _focusZoom = 1f;
    private float _zoomBeforeFocus = 1f;
    private float? _zoomBack;

    /// <summary>True while a focus holds the view (<see cref="BeginFocus"/> … <see cref="EndFocus"/>).</summary>
    public bool Focusing => _focus is not null;

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!InputEnabled)
        {
            _dragging = false;
            return;
        }

        if (@event is InputEventMouseButton mouseButton)
        {
            if (mouseButton.ButtonIndex == MouseButton.Middle)
            {
                // Held: the view leaves the drone; released: it goes back (soft follow), whatever sent it away before.
                _dragging = mouseButton.Pressed;
                _dragLast = mouseButton.Position;
                FreeLook = false;
                GetViewport().SetInputAsHandled();
            }
            else if (mouseButton.ButtonIndex is (MouseButton.WheelUp or MouseButton.WheelDown)
                && !mouseButton.CtrlPressed && !WheelZooms())
            {
                // Left unhandled: the world turns the block in hand.
            }
            else if (mouseButton.Pressed && mouseButton.ButtonIndex == MouseButton.WheelUp)
            {
                ApplyZoom(1f + ZoomStep);
                GetViewport().SetInputAsHandled();
            }
            else if (mouseButton.Pressed && mouseButton.ButtonIndex == MouseButton.WheelDown)
            {
                ApplyZoom(1f - ZoomStep);
                GetViewport().SetInputAsHandled();
            }
        }
        else if (@event is InputEventMouseMotion motion && _dragging)
        {
            var mouse = motion.Position;
            var delta = (_dragLast - mouse) / Zoom;
            Position += delta;
            ClampToMap();
            _dragLast = mouse;
            GetViewport().SetInputAsHandled();
        }
    }

    /// <summary>The window lost the focus: the middle button's release will not arrive, the view goes back to the drone.</summary>
    public override void _Notification(int what)
    {
        if (what == NotificationApplicationFocusOut)
        {
            _dragging = false;
        }
    }

    public override void _Process(double delta)
    {
        if (DriftSpeed > 0f)
        {
            Drift((float)delta);
        }

        if (_focus is { } focus)
        {
            // A focus (the jackpot sequence) wins over the follow, the drag and the free look.
            var t = 1f - MathF.Exp(-FocusRate * (float)delta);
            Zoom = Vector2.One * Mathf.Lerp(Zoom.X, _focusZoom, t);
            Position = Position.Lerp(focus, t);
            ClampToMap();
            return;
        }

        if (_zoomBack is { } back)
        {
            var t = 1f - MathF.Exp(-FocusRate * (float)delta);
            var z = Mathf.Lerp(Zoom.X, back, t);
            if (Mathf.Abs(z - back) < 0.001f)
            {
                z = back;
                _zoomBack = null;
            }

            Zoom = new Vector2(z, z);
            ClampToMap();
        }

        if (FollowTarget is { } target && !_dragging && !FreeLook && DriftSpeed <= 0f)
        {
            Follow(target, (float)delta);
        }
    }

    /// <summary>
    /// Holds the view on <paramref name="target"/> at <paramref name="zoom"/>, reached smoothly (<see cref="FocusRate"/>);
    /// the zoom of the moment is kept for <see cref="EndFocus"/>. A drag in progress is dropped.
    /// </summary>
    public void BeginFocus(Vector2 target, float zoom)
    {
        if (_focus is null)
        {
            _zoomBeforeFocus = _zoomBack ?? Zoom.X;
        }

        _focus = target;
        _focusZoom = Mathf.Clamp(zoom, MinZoom, MaxZoom);
        _zoomBack = null;
        _dragging = false;
    }

    /// <summary>
    /// Ends a focus: the view goes back to following the drone (soft follow) and to the zoom it had before, smoothly or,
    /// with <paramref name="instant"/>, at once. Nothing happens without a focus.
    /// </summary>
    public void EndFocus(bool instant)
    {
        if (_focus is null)
        {
            return;
        }

        _focus = null;
        FreeLook = false;
        if (instant)
        {
            _zoomBack = null;
            Zoom = new Vector2(_zoomBeforeFocus, _zoomBeforeFocus);
            ClampToMap();
        }
        else
        {
            _zoomBack = _zoomBeforeFocus;
        }
    }

    /// <summary>Snaps the view onto the drone at once (H, a new game), dropping a free look.</summary>
    public void CentreOn(Vector2 target)
    {
        FreeLook = false;
        Position = target;
        ClampToMap();
    }

    private void Follow(Vector2 target, float delta)
    {
        var t = 1f - MathF.Exp(-FollowRate * delta);
        Position = Position.Lerp(target, t);
        if (Position.DistanceSquaredTo(target) < 0.01f)
        {
            Position = target;
        }

        ClampToMap();
    }

    /// <summary>Moves along the drift direction; an axis stopped by the map edge turns back.</summary>
    private void Drift(float delta)
    {
        var want = Position + _driftDir * DriftSpeed * delta / Zoom.X;
        Position = want;
        ClampToMap();
        if (!Mathf.IsEqualApprox(Position.X, want.X))
        {
            _driftDir.X = -_driftDir.X;
        }

        if (!Mathf.IsEqualApprox(Position.Y, want.Y))
        {
            _driftDir.Y = -_driftDir.Y;
        }
    }

    private void ApplyZoom(float factor)
    {
        _zoomBack = null; // the player's own zoom ends the way back after a focus
        var z = Mathf.Clamp(Zoom.X * factor, MinZoom, MaxZoom);
        Zoom = new Vector2(z, z);
        ClampToMap();
    }

    /// <summary>Keep camera center inside the world AABB (Raylib WorldCamera.ClampToMap).</summary>
    public void ClampToMap()
    {
        if (MapPixelWidth <= 0f || MapPixelHeight <= 0f)
        {
            return;
        }

        var vp = GetViewport().GetVisibleRect().Size;
        var halfW = (vp.X / Math.Max(0.01f, Zoom.X)) * 0.5f;
        var halfH = (vp.Y / Math.Max(0.01f, Zoom.Y)) * 0.5f;
        // If viewport larger than map, pin to center.
        float minX, maxX, minY, maxY;
        if (halfW * 2f >= MapPixelWidth)
        {
            minX = maxX = MapPixelWidth * 0.5f;
        }
        else
        {
            minX = halfW;
            maxX = MapPixelWidth - halfW;
        }

        if (halfH * 2f >= MapPixelHeight)
        {
            minY = maxY = MapPixelHeight * 0.5f;
        }
        else
        {
            minY = halfH;
            maxY = MapPixelHeight - halfH;
        }

        Position = new Vector2(
            Mathf.Clamp(Position.X, minX, maxX),
            Mathf.Clamp(Position.Y, minY, maxY));
    }
}
