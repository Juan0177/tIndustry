using Godot;

namespace TIndustry.Godot;

/// <summary>
/// UI-only scale (G4): the HUD CanvasLayer is scaled by the factor and one root Control spans the window divided by
/// it, so every HUD control and panel is laid out in UI units against the scaled window and drawn magnified, while
/// the world, its camera and <see cref="Window.ContentScaleFactor"/> (always 1) stay as they are.
///
/// <para>Popups of the window (tooltips) are separate sub-windows: their content scale is set when they appear.</para>
/// </summary>
public static class UiScale
{
    /// <summary>The current factor (1 = 100%), for code that sizes art in screen pixels.</summary>
    public static float Factor { get; private set; } = 1f;

    private static Control? _root;
    private static CanvasLayer? _layer;
    private static Viewport? _viewport;

    /// <summary>
    /// The root Control of <paramref name="layer"/> (named "Interfaccia"), created on first use: the parent of every
    /// HUD control and panel. It carries the theme too, so the whole HUD inherits it.
    /// </summary>
    public static Control RootOf(CanvasLayer layer)
    {
        if (_root is not null && IsInstanceValid(_root) && _root.GetParent() == layer)
        {
            return _root;
        }

        if (layer.GetNodeOrNull<Control>("Interfaccia") is not { } root)
        {
            root = new Control { Name = "Interfaccia", MouseFilter = Control.MouseFilterEnum.Ignore };
            layer.AddChild(root);
        }

        Detach();
        _root = root;
        _layer = layer;
        _viewport = layer.GetViewport();
        _viewport.SizeChanged += Resize;
        Resize();
        return root;
    }

    /// <summary>Stops following the window size (the HUD leaves the tree): no handler left on the viewport.</summary>
    public static void Detach()
    {
        if (_viewport is not null && IsInstanceValid(_viewport))
        {
            _viewport.SizeChanged -= Resize;
        }

        _viewport = null;
    }

    /// <summary>Scales the HUD to <paramref name="percent"/> (already normalized by <see cref="ClientSettings"/>).</summary>
    public static void Apply(int percent)
    {
        Factor = percent / 100f;
        if (_layer is not null && IsInstanceValid(_layer))
        {
            _layer.Scale = new Vector2(Factor, Factor);
        }

        Resize();
    }

    /// <summary>A screen point (viewport coordinates, as input events carry them) in HUD units.</summary>
    public static Vector2 ToUi(Vector2 viewportPoint) => viewportPoint / Factor;

    /// <summary>
    /// For <see cref="SceneTree.NodeAdded"/>: a popup sub-window entering the tree (the tooltip Godot makes under the
    /// hovered control) gets the HUD's factor as its content scale, so tooltips match the HUD.
    /// </summary>
    public static void ScalePopup(Node node)
    {
        if (node is not Popup popup || Factor == 1f)
        {
            return;
        }

        popup.ContentScaleFactor = Factor;
        // Godot sizes the popup from its content's minimum size in UI units: once shown, it is grown by the factor
        // (kept inside the window) so the scaled content is not cut.
        popup.VisibilityChanged += () =>
        {
            if (popup.Visible)
            {
                Callable.From(() => FitPopup(popup)).CallDeferred();
            }
        };
    }

    private static void FitPopup(Popup popup)
    {
        if (!IsInstanceValid(popup) || !popup.Visible || popup.ContentScaleFactor <= 1f)
        {
            return;
        }

        var scale = popup.ContentScaleFactor;
        var wanted = (Vector2I)(popup.GetContentsMinimumSize() * scale).Ceil();
        if (wanted.X <= popup.Size.X && wanted.Y <= popup.Size.Y)
        {
            return;
        }

        var bounds = popup.GetParent<Node>()?.GetViewport()?.GetVisibleRect().Size ?? Vector2.Zero;
        var size = new Vector2I(Math.Max(popup.Size.X, wanted.X), Math.Max(popup.Size.Y, wanted.Y));
        var position = popup.Position;
        if (bounds != Vector2.Zero)
        {
            position.X = Math.Clamp(position.X, 0, Math.Max(0, (int)bounds.X - size.X));
            position.Y = Math.Clamp(position.Y, 0, Math.Max(0, (int)bounds.Y - size.Y));
        }

        popup.Size = size;
        popup.Position = position;
    }

    private static void Resize()
    {
        if (_root is null || !IsInstanceValid(_root) || !_root.IsInsideTree())
        {
            return;
        }

        _root.Position = Vector2.Zero;
        _root.Size = _root.GetViewport().GetVisibleRect().Size / Factor;
    }

    private static bool IsInstanceValid(GodotObject o) => GodotObject.IsInstanceValid(o);
}
