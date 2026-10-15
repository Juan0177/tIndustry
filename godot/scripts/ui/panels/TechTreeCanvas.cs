using Godot;
using TIndustry.Shared;

namespace TIndustry.Godot;

/// <summary>Mindustry-style research tree: CORE on top, square icon nodes with names, elbow links.</summary>
public partial class TechTreeCanvas : Control
{
    private static readonly Color CanvasFill = new(20 / 255f, 24 / 255f, 23 / 255f, 1f);
    private static readonly Color CanvasBorder = new(48 / 255f, 56 / 255f, 52 / 255f, 1f);
    private static readonly Color HintSoft = new(164 / 255f, 173 / 255f, 168 / 255f, 1f);
    private static readonly Color PathEdge = new(235 / 255f, 200 / 255f, 110 / 255f, 1f);
    private static readonly Color Gold = new(211 / 255f, 164 / 255f, 76 / 255f, 1f);
    private static readonly Color NodeTitle = new(232 / 255f, 233 / 255f, 221 / 255f, 1f);

    private readonly Dictionary<string, Texture2D?> _iconCache = new(StringComparer.Ordinal);

    public TechTreeLayout.Graph? Graph { get; set; }
    public IResearchView? Research { get; set; }
    public string? SelectedId { get; set; }
    public float Zoom { get; private set; } = 1f;

    public event Action<string>? NodeSelected;
    public event Action? ViewChanged;

    private float _panX;
    private float _panY;
    private bool _panning;
    private bool _pressPending;
    private Vector2 _panAnchor;
    private float _panStartX;
    private float _panStartY;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Stop;
        ClipContents = true;
    }

    /// <summary>Fit the whole tree in view, centered (H / Home, and on open).</summary>
    public void ResetView()
    {
        _panning = false;
        _pressPending = false;
        if (Graph is null || Graph.Nodes.Count == 0 || Size.X < 1f || Size.Y < 1f)
        {
            // Not laid out yet: retry once the control has a size.
            Callable.From(FitWhenSized).CallDeferred();
            return;
        }

        var minX = Graph.Nodes.Min(n => (float)n.X);
        var minY = Graph.Nodes.Min(n => (float)n.Y);
        var labelOverhang = (TechTreeLayout.SlotWidth - TechTreeLayout.NodeSize) / 2f;
        minX -= labelOverhang;
        var maxX = Graph.Nodes.Max(n => (float)n.X) + TechTreeLayout.NodeWidth + labelOverhang;
        var maxY = Graph.Nodes.Max(n => (float)n.Y) + TechTreeLayout.NodeHeight + 28f; // + name label
        const float pad = 32f;
        var fit = Math.Min((Size.X - pad * 2) / (maxX - minX), (Size.Y - pad * 2) / (maxY - minY));
        // Readable first (names never shrink below ~0.7); a bigger tree is panned, like Mindustry.
        Zoom = TechTreeLayout.ClampZoom(Math.Clamp(fit, MinReadableZoom, 1f));
        _panX = (Size.X - (maxX - minX) * Zoom) / 2f - minX * Zoom;
        _panY = (maxY - minY) * Zoom <= Size.Y - pad * 2
            ? (Size.Y - (maxY - minY) * Zoom) / 2f - minY * Zoom
            : pad - minY * Zoom; // too tall: keep CORE at the top
        QueueRedraw();
        ViewChanged?.Invoke();
    }

    private void FitWhenSized()
    {
        if (Graph is not null && Size.X >= 1f && Size.Y >= 1f)
        {
            ResetView();
        }
    }

    /// <summary>Mindustry-style: drag (left/middle/right) pans, wheel zooms at the cursor, click selects.</summary>
    public override void _GuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton mb)
        {
            var at = mb.Position;
            if (mb.ButtonIndex is MouseButton.WheelUp or MouseButton.WheelDown)
            {
                if (mb.Pressed)
                {
                    ZoomAt(mb.ButtonIndex == MouseButton.WheelUp ? 1f : -1f, at);
                }

                AcceptEvent();
                return;
            }

            if (mb.ButtonIndex is MouseButton.Left or MouseButton.Middle or MouseButton.Right)
            {
                if (mb.Pressed)
                {
                    _pressPending = mb.ButtonIndex == MouseButton.Left;
                    _panning = mb.ButtonIndex != MouseButton.Left;
                    _panAnchor = at;
                    _panStartX = _panX;
                    _panStartY = _panY;
                }
                else
                {
                    if (_pressPending && mb.ButtonIndex == MouseButton.Left)
                    {
                        TrySelectAt(at);
                    }

                    _pressPending = false;
                    _panning = false;
                }

                AcceptEvent();
            }

            return;
        }

        if (@event is InputEventMouseMotion hover && !_panning)
        {
            UpdateHover(hover.Position);
        }

        if (@event is InputEventMouseMotion motion && (_panning || _pressPending))
        {
            // A left click turns into a drag after a few pixels (so small jitters still select).
            if (_pressPending && motion.Position.DistanceTo(_panAnchor) > DragThreshold)
            {
                _pressPending = false;
                _panning = true;
            }

            if (_panning)
            {
                _panX = _panStartX + (motion.Position.X - _panAnchor.X);
                _panY = _panStartY + (motion.Position.Y - _panAnchor.Y);
                QueueRedraw();
                ViewChanged?.Invoke();
            }

            AcceptEvent();
        }
    }

    private const float DragThreshold = 6f;
    private const float MinReadableZoom = 0.7f;

    private void ZoomAt(float direction, Vector2 mouse)
    {
        var before = TechTreeLayout.ClampZoom(Zoom);
        var after = TechTreeLayout.ClampZoom(before * (direction > 0 ? 1.12f : 1f / 1.12f));
        if (Math.Abs(after - before) < 0.0001f)
        {
            return;
        }

        _panX = mouse.X - (mouse.X - _panX) * (after / before);
        _panY = mouse.Y - (mouse.Y - _panY) * (after / before);
        Zoom = after;
        QueueRedraw();
        ViewChanged?.Invoke();
    }

    private string? NodeAt(Vector2 local)
    {
        if (Graph is null)
        {
            return null;
        }

        var zoom = TechTreeLayout.ClampZoom(Zoom);
        var size = TechTreeLayout.NodeSize * zoom;
        for (var i = Graph.Nodes.Count - 1; i >= 0; i--)
        {
            var node = Graph.Nodes[i];
            var rect = new Rect2(node.X * zoom + _panX, node.Y * zoom + _panY, size, size);
            if (rect.Grow(2f).HasPoint(local))
            {
                return node.Structure.Id;
            }
        }

        return null;
    }

    private void TrySelectAt(Vector2 local)
    {
        if (NodeAt(local) is { } id)
        {
            NodeSelected?.Invoke(id);
        }
    }

    /// <summary>Hover text (name · state · cost) supplied by the research panel.</summary>
    public Func<string, string>? TooltipFor { get; set; }

    public override string _GetTooltip(Vector2 atPosition) =>
        NodeAt(atPosition) is { } id && TooltipFor is not null ? TooltipFor(id) : "";

    private ResearchNodeState StateOf(TechTreeLayout.Node node) =>
        node.IsRoot ? ResearchNodeState.Unlocked : Research!.GetNodeState(node.Structure);

    // Mindustry palette: neutral greys for what you have, pale gold for what you can research next.
    private static readonly Color Accent = new(1f, 0.83f, 0.50f, 1f);
    private static readonly Color NodeBg = new(0.13f, 0.14f, 0.15f, 1f);
    private static readonly Color NodeBgLocked = new(0.08f, 0.085f, 0.09f, 1f);
    private static readonly Color BorderUnlocked = new(0.62f, 0.65f, 0.66f, 1f);
    private static readonly Color BorderLocked = new(0.24f, 0.26f, 0.27f, 1f);
    private static readonly Color LineUnlocked = new(0.55f, 0.58f, 0.60f, 1f);
    private static readonly Color LineAvailable = new(0.78f, 0.66f, 0.42f, 1f);
    private static readonly Color LineLocked = new(0.22f, 0.24f, 0.25f, 1f);
    private static readonly Color CoreCyan = new(0.35f, 0.78f, 0.9f, 1f);

    public override void _Draw()
    {
        var size = Size;
        DrawRect(new Rect2(Vector2.Zero, size), CanvasFill, true);
        DrawRect(new Rect2(Vector2.Zero, size), CanvasBorder, false, 1f);

        if (Graph is null || Research is null)
        {
            DrawString(ThemeDB.FallbackFont, new Vector2(24, 40),
                "Nessuna struttura nell'albero.", HorizontalAlignment.Left, -1, 16, HintSoft);
            return;
        }

        var zoom = TechTreeLayout.ClampZoom(Zoom);
        var nodeSize = TechTreeLayout.NodeSize * zoom;
        var byId = Graph.Nodes.ToDictionary(n => n.Structure.Id, StringComparer.Ordinal);
        var pathIds = string.IsNullOrEmpty(SelectedId)
            ? new HashSet<string>(StringComparer.Ordinal)
            : TechTreeLayout.CollectRelatedIds(Graph, SelectedId);
        Vector2 Screen(int x, int y) => new(x * zoom + _panX, y * zoom + _panY);

        // Edges first: extra prerequisites dashed underneath, tree edges on top.
        foreach (var edge in Graph.Edges.OrderBy(e => e.Primary))
        {
            var toState = StateOf(byId[edge.ToId]);
            var onPath = TechTreeLayout.IsEdgeOnPath(edge, pathIds);
            var color = onPath
                ? PathEdge
                : toState switch
                {
                    ResearchNodeState.Unlocked => LineUnlocked,
                    ResearchNodeState.Available => LineAvailable,
                    _ => LineLocked
                };
            var width = Mathf.Max(1.5f, (edge.Primary ? 3f : 2f) * zoom);
            var a = Screen(edge.FromX, edge.FromY);
            var b = Screen(edge.ToX, edge.ToY);
            var midY = (a.Y + b.Y) / 2f;
            Vector2[] pts = [a, new(a.X, midY), new(b.X, midY), b];
            for (var k = 0; k < pts.Length - 1; k++)
            {
                if (edge.Primary)
                {
                    DrawLine(pts[k], pts[k + 1], color, width);
                }
                else
                {
                    DrawDashedLine(pts[k], pts[k + 1], color with { A = 0.8f }, width, 8f * zoom);
                }
            }
        }

        var font = ThemeDB.FallbackFont;
        var labelSize = (int)Mathf.Clamp(13 * zoom, 11, 16);
        foreach (var node in Graph.Nodes)
        {
            var state = StateOf(node);
            var id = node.Structure.Id;
            var isSelected = string.Equals(id, SelectedId, StringComparison.Ordinal);
            var isHover = string.Equals(id, _hoverId, StringComparison.Ordinal);
            var rect = new Rect2(Screen(node.X, node.Y), new Vector2(nodeSize, nodeSize));

            var border = node.IsRoot
                ? CoreCyan
                : state switch
                {
                    ResearchNodeState.Unlocked => BorderUnlocked,
                    ResearchNodeState.Available => Accent,
                    _ => BorderLocked
                };
            if (isSelected || isHover)
            {
                DrawRect(rect.Grow(5f * zoom), border with { A = isSelected ? 0.35f : 0.18f }, true);
            }

            DrawRect(rect, state == ResearchNodeState.Locked ? NodeBgLocked : NodeBg, true);
            DrawRect(rect, border, false, Mathf.Max(1.5f, (isSelected ? 4f : 2.5f) * zoom));

            var inner = rect.Grow(-8f * zoom);
            if (state == ResearchNodeState.Locked)
            {
                // Like Mindustry: a lock until the prerequisites are researched.
                if (LockIcon is { } lockTex)
                {
                    DrawTextureRect(lockTex, inner.Grow(-inner.Size.X * 0.18f), false, BorderLocked.Lightened(0.25f));
                }
            }
            else if (ResolveIcon(id) is { } tex)
            {
                DrawTextureRect(tex, inner, false,
                    state == ResearchNodeState.Available ? new Color(1f, 1f, 1f, 0.82f) : Colors.White);
            }

            var label = node.IsRoot ? "CORE" : node.Structure.DisplayName;
            var labelColor = node.IsRoot
                ? CoreCyan
                : state switch
                {
                    ResearchNodeState.Unlocked => NodeTitle,
                    ResearchNodeState.Available => Accent,
                    _ => HintSoft with { A = 0.55f }
                };
            var maxW = (int)(TechTreeLayout.SlotWidth * zoom) - 2;
            var text = Truncate(label, maxW, labelSize);
            var tw = font.GetStringSize(text, HorizontalAlignment.Left, -1, labelSize).X;
            DrawString(font,
                new Vector2(rect.Position.X + (nodeSize - tw) / 2f, rect.End.Y + 6f * zoom + labelSize),
                text, HorizontalAlignment.Left, -1, labelSize, labelColor);
        }
    }

    private string? _hoverId;
    private Texture2D? _lockIcon;
    private bool _lockLoaded;

    private Texture2D? LockIcon
    {
        get
        {
            if (!_lockLoaded)
            {
                _lockLoaded = true;
                _lockIcon = ResourceLoader.Exists("res://assets/ui/locked.png")
                    ? GD.Load<Texture2D>("res://assets/ui/locked.png")
                    : null;
            }

            return _lockIcon;
        }
    }

    private void UpdateHover(Vector2 local)
    {
        var id = NodeAt(local);
        if (!string.Equals(id, _hoverId, StringComparison.Ordinal))
        {
            _hoverId = id;
            MouseDefaultCursorShape = id is null ? CursorShape.Arrow : CursorShape.PointingHand;
            QueueRedraw();
        }
    }

    private Texture2D? ResolveIcon(string structureId)
    {
        if (_iconCache.TryGetValue(structureId, out var cached))
        {
            return cached;
        }

        var path = structureId switch
        {
            "conveyor-basic" => "res://assets/conveyor-basic.png",
            "conveyor-fast" or "conveyor-express" => "res://assets/conveyor-fast.png",
            "conveyor-bridge" => "res://assets/bridge.png",
            "miner" => "res://assets/miner.png",
            "miner-advanced" => "res://assets/miner-advanced.png",
            "smelter" => "res://assets/smelter.png",
            "assembler" => "res://assets/assembler.png",
            "junction" => "res://assets/junction.png",
            "splitter" => "res://assets/splitter.png",
            "sorter" => "res://assets/sorter.png",
            "generator" => "res://assets/generator.png",
            "power-node" => "res://assets/power-node.png",
            "power-node-t2" => "res://assets/power-node-t2.png",
            "extractor" => "res://assets/extractor.png",
            "core" => "res://assets/core.png",
            _ => $"res://assets/{structureId}.png"
        };

        Texture2D? tex = null;
        if (ResourceLoader.Exists(path))
        {
            tex = GD.Load<Texture2D>(path);
        }

        _iconCache[structureId] = tex;
        return tex;
    }

    private static string Initials(string displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
        {
            return "?";
        }

        var parts = displayName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 1)
        {
            return parts[0].Length <= 2 ? parts[0].ToUpperInvariant() : parts[0][..2].ToUpperInvariant();
        }

        return string.Concat(parts.Take(2).Select(p => char.ToUpperInvariant(p[0])));
    }

    private static string Truncate(string text, int maxWidthPx, int fontSize)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text;
        }

        var font = ThemeDB.FallbackFont;
        if (font.GetStringSize(text, HorizontalAlignment.Left, -1, fontSize).X <= maxWidthPx)
        {
            return text;
        }

        const string ellipsis = "…";
        for (var len = text.Length - 1; len > 0; len--)
        {
            var candidate = text[..len] + ellipsis;
            if (font.GetStringSize(candidate, HorizontalAlignment.Left, -1, fontSize).X <= maxWidthPx)
            {
                return candidate;
            }
        }

        return ellipsis;
    }
}
