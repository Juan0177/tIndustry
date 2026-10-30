using Godot;

namespace TIndustry.Godot;

/// <summary>WASD / middle-drag (or left-drag in cursor mode, whose click selects) pan + wheel zoom (with Ctrl, or no block in hand to turn); middle click picks a block; clamps to map AABB (Raylib ClampToMap).</summary>
public partial class WorldCamera : Camera2D
{
    [Export] public float PanSpeed { get; set; } = 420f;
    [Export] public float MinZoom { get; set; } = 0.08f;
    [Export] public float MaxZoom { get; set; } = 2.5f;
    [Export] public float ZoomStep { get; set; } = 0.1f;

    /// <summary>World size in pixels (map tiles × tile size). Zero = no clamp.</summary>
    public float MapPixelWidth { get; set; }
    public float MapPixelHeight { get; set; }

    /// <summary>Set by the world while the cursor tool is active: left-drag pans like middle-drag.</summary>
    public bool LeftDragPans { get; set; }

    /// <summary>False while the Home is open: no pan, no zoom from the player.</summary>
    public bool InputEnabled { get; set; } = true;

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

    /// <summary>The middle button went up less than <see cref="ClickSlop"/> px from where it went down (screen position).</summary>
    public event Action<Vector2>? MiddleClicked;

    /// <summary>
    /// The left button, pressed while it pans (<see cref="LeftDragPans"/>), went up less than <see cref="ClickSlop"/> px
    /// from where it went down (screen position): a click of the cursor tool, not a pan.
    /// </summary>
    public event Action<Vector2>? LeftClicked;

    private const float ClickSlop = 4f;

    private bool _dragging;
    private Vector2 _dragLast;
    private Vector2? _middleDown;
    private Vector2? _leftDown;

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!InputEnabled)
        {
            _dragging = false;
            return;
        }

        if (@event is InputEventMouseButton mouseButton)
        {
            var leftPan = mouseButton.ButtonIndex == MouseButton.Left
                && (mouseButton.Pressed ? LeftDragPans : _dragging);
            if (mouseButton.ButtonIndex == MouseButton.Middle || leftPan)
            {
                _dragging = mouseButton.Pressed;
                _dragLast = mouseButton.Position;
                if (mouseButton.ButtonIndex == MouseButton.Middle)
                {
                    // A middle click picks the block under the cursor; a middle drag only pans.
                    if (!mouseButton.Pressed && _middleDown is { } down && down.DistanceTo(mouseButton.Position) < ClickSlop)
                    {
                        MiddleClicked?.Invoke(mouseButton.Position);
                    }

                    _middleDown = mouseButton.Pressed ? mouseButton.Position : null;
                }
                else
                {
                    if (!mouseButton.Pressed && _leftDown is { } down && down.DistanceTo(mouseButton.Position) < ClickSlop)
                    {
                        LeftClicked?.Invoke(mouseButton.Position);
                    }

                    _leftDown = mouseButton.Pressed ? mouseButton.Position : null;
                }

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

    public override void _Process(double delta)
    {
        if (DriftSpeed > 0f)
        {
            Drift((float)delta);
        }

        if (!InputEnabled)
        {
            return;
        }

        var dir = Vector2.Zero;
        if (Input.IsActionPressed("pan_up")) dir.Y -= 1;
        if (Input.IsActionPressed("pan_down")) dir.Y += 1;
        if (Input.IsActionPressed("pan_left")) dir.X -= 1;
        if (Input.IsActionPressed("pan_right")) dir.X += 1;
        if (dir != Vector2.Zero)
        {
            Position += dir.Normalized() * PanSpeed * (float)delta / Zoom.X;
            ClampToMap();
        }
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
