using Godot;

namespace TIndustry.Godot;

/// <summary>WASD / middle-drag pan + wheel zoom for the spike map.</summary>
public partial class SpikeCamera : Camera2D
{
    [Export] public float PanSpeed { get; set; } = 420f;
    [Export] public float MinZoom { get; set; } = 0.35f;
    [Export] public float MaxZoom { get; set; } = 2.5f;
    [Export] public float ZoomStep { get; set; } = 0.1f;

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
        }
    }

    private void ApplyZoom(float factor)
    {
        var z = Mathf.Clamp(Zoom.X * factor, MinZoom, MaxZoom);
        Zoom = new Vector2(z, z);
    }
}
