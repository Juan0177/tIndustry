using Godot;

namespace TIndustry.Godot;

/// <summary>WASD / middle-drag pan + wheel zoom; clamps to map AABB (Raylib ClampToMap).</summary>
public partial class SpikeCamera : Camera2D
{
    [Export] public float PanSpeed { get; set; } = 420f;
    [Export] public float MinZoom { get; set; } = 0.08f;
    [Export] public float MaxZoom { get; set; } = 2.5f;
    [Export] public float ZoomStep { get; set; } = 0.1f;

    /// <summary>World size in pixels (map tiles × tile size). Zero = no clamp.</summary>
    public float MapPixelWidth { get; set; }
    public float MapPixelHeight { get; set; }

    private bool _dragging;
    private Vector2 _dragLast;

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton mouseButton)
        {
            if (mouseButton.ButtonIndex == MouseButton.Middle)
            {
                _dragging = mouseButton.Pressed;
                _dragLast = GetViewport().GetMousePosition();
                GetViewport().SetInputAsHandled();
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
            var mouse = GetViewport().GetMousePosition();
            var delta = (_dragLast - mouse) / Zoom;
            Position += delta;
            ClampToMap();
            _dragLast = mouse;
            GetViewport().SetInputAsHandled();
        }
    }

    public override void _Process(double delta)
    {
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
