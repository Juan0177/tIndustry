using Godot;

namespace TIndustry.Godot;

/// <summary>
/// Graphics setting "Scala di rendering": draws the world (the root viewport's default canvas, i.e. World and
/// every node under it that is not on a CanvasLayer) at a fraction of the window resolution and stretches it to the
/// window; the HUD stays at full resolution.
///
/// <para>Why not <c>Window.ContentScaleMode = Viewport</c>: in that mode the root renders at its logical size
/// (base size / UI scale), so the resolution follows the UI scale and the window, not a percentage, and changing it
/// would change the HUD layout. Here a <see cref="SubViewport"/> shares the root's <see cref="World2D"/> and draws
/// the same canvas, the root culls the world through a visibility layer, and a <see cref="TextureRect"/> on a
/// CanvasLayer under everything shows the result. Input, camera and picking stay on the root, untouched.</para>
/// </summary>
public partial class RenderScale : Node
{
    /// <summary>Visibility layer bit only the world root carries while the scale is on (layer 20).</summary>
    private const uint WorldLayer = 1u << 19;

    private CanvasItem? _world;
    private uint _worldLayers;
    private uint _rootMask;
    private SubViewport? _target;
    private CanvasLayer? _layer;
    private TextureRect? _view;

    /// <summary>Render resolution percent of the window (100 = off, the root draws the world itself).</summary>
    public int Percent { get; private set; } = 100;

    /// <summary>The reduced-resolution viewport, or null at 100%.</summary>
    public SubViewport? Target => _target;

    /// <summary>Filter of the final stretch to the window (null at 100%).</summary>
    public CanvasItem.TextureFilterEnum? StretchFilter => _view?.TextureFilter;

    /// <summary><paramref name="world"/> is the canvas item at the root of the world (World itself).</summary>
    public void Setup(CanvasItem world) => _world = world;

    public override void _EnterTree() => RenderingServer.FramePreDraw += SyncTarget;

    public override void _ExitTree()
    {
        RenderingServer.FramePreDraw -= SyncTarget;
        Apply(100);
    }

    public void Apply(int percent)
    {
        if (_world is null || !IsInsideTree())
        {
            return;
        }

        var root = GetTree().Root;
        if (percent >= 100)
        {
            if (_target is not null)
            {
                _world.VisibilityLayer = _worldLayers;
                root.CanvasCullMask = _rootMask;
                _layer?.QueueFree();
                _target.QueueFree();
                _layer = null;
                _view = null;
                _target = null;
            }

            Percent = 100;
            return;
        }

        if (_target is null)
        {
            _worldLayers = _world.VisibilityLayer;
            _rootMask = root.CanvasCullMask;
            _target = new SubViewport
            {
                Name = "MondoRidotto",
                World2D = root.World2D,
                RenderTargetUpdateMode = SubViewport.UpdateMode.Always,
                GuiDisableInput = true,
                CanvasCullMask = uint.MaxValue,
                // A new SubViewport defaults to Linear filtering: copy the root's 2D settings (the project's Nearest)
                // so the world is drawn as at 100%, only smaller.
                CanvasItemDefaultTextureFilter = root.CanvasItemDefaultTextureFilter,
                CanvasItemDefaultTextureRepeat = root.CanvasItemDefaultTextureRepeat,
                Snap2DTransformsToPixel = root.Snap2DTransformsToPixel,
                Snap2DVerticesToPixel = root.Snap2DVerticesToPixel,
                Msaa2D = root.Msaa2D
            };
            AddChild(_target);
            // Below the default canvas (layer 0, culled on the root) and the HUD.
            _layer = new CanvasLayer { Name = "MondoRidottoStrato", Layer = -10 };
            _view = new TextureRect
            {
                Name = "Immagine",
                Texture = _target.GetTexture(),
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.Scale,
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            _view.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            _layer.AddChild(_view);
            AddChild(_layer);
            _world.VisibilityLayer = WorldLayer;
            root.CanvasCullMask = _rootMask & ~WorldLayer;
        }

        Percent = percent;
        // Exactly 2x (50%): every target pixel becomes a 2x2 block, crisp pixel art. Other factors (75%) would
        // repeat rows and columns unevenly with Nearest, so they are smoothed.
        _view!.TextureFilter = percent == 50 ? CanvasItem.TextureFilterEnum.Nearest : CanvasItem.TextureFilterEnum.Linear;
        SyncTarget();
    }

    /// <summary>
    /// Before every draw (after the camera moved): the target's size follows the window, its transform the root's
    /// camera, scaled from logical units to target pixels.
    /// </summary>
    private void SyncTarget()
    {
        if (_target is null || !IsInsideTree())
        {
            return;
        }

        var root = GetTree().Root;
        var logical = root.GetVisibleRect().Size;
        if (logical.X <= 0 || logical.Y <= 0)
        {
            return;
        }

        // Window pixels per logical unit (canvas_items stretch: window size / (base size / UI scale)).
        var stretch = root.GetFinalTransform().Scale;
        var size = new Vector2I(
            Math.Max(1, Mathf.RoundToInt(logical.X * stretch.X * Percent / 100f)),
            Math.Max(1, Mathf.RoundToInt(logical.Y * stretch.Y * Percent / 100f)));
        if (_target.Size != size)
        {
            _target.Size = size;
        }

        _target.GlobalCanvasTransform = Transform2D.Identity.Scaled(new Vector2(size.X / logical.X, size.Y / logical.Y));
        _target.CanvasTransform = root.CanvasTransform;
    }
}
