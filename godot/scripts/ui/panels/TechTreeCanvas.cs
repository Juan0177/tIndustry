using Godot;
using TIndustry.Shared;

namespace TIndustry.Godot;

/// <summary>Mindustry-style research tree: CORE on top, square icon nodes with names, elbow links.</summary>
public partial class TechTreeCanvas : Control
{
    private static readonly Color CanvasFill = UiColors.Fondo;
    private static readonly Color CanvasBorder = UiColors.Bordo;
    private static readonly Color HintSoft = UiColors.TestoTenue;
    private static readonly Color PathEdge = UiColors.RicercaPercorso;
    private static readonly Color NodeTitle = UiColors.Testo;

    /// <summary>Node names: the text font at its native size (crisp at every zoom of the tree).</summary>
    private const int LabelSize = UiTheme.TextSize;

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

    /// <summary>
    /// Fit the tree in view, centered. On open the zoom stays readable (names fit their slot from zoom 1) and a bigger
    /// tree is panned to the selected node; with <paramref name="wholeTree"/> (H / Home) the zoom goes below that to
    /// show every node, and the names are left out until the player zooms back in (hover still names a node).
    /// </summary>
    public void ResetView(bool wholeTree = false)
    {
        _wholeTree = wholeTree;
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
        var maxY = Graph.Nodes.Max(n => (float)n.Y) + TechTreeLayout.NodeHeight + LabelBlock; // + name label
        const float pad = 32f;
        var fit = Math.Min((Size.X - pad * 2) / (maxX - minX), (Size.Y - pad * 2) / (maxY - minY));
        // Readable first (names fit their slot from zoom 1); a bigger tree is panned, like Mindustry.
        Zoom = TechTreeLayout.ClampZoom(wholeTree ? Math.Min(fit, 1f) : Math.Clamp(fit, MinReadableZoom, 1f));
        _panX = (Size.X - (maxX - minX) * Zoom) / 2f - minX * Zoom;
        _panY = (maxY - minY) * Zoom <= Size.Y - pad * 2
            ? (Size.Y - (maxY - minY) * Zoom) / 2f - minY * Zoom
            : pad - minY * Zoom; // too tall: keep CORE at the top
        BringSelectedIntoView(pad);
        QueueRedraw();
        ViewChanged?.Invoke();
    }

    /// <summary>True when every node square is inside the view (playtests: H shows the whole tree).</summary>
    internal bool WholeTreeInView()
    {
        if (Graph is null || Graph.Nodes.Count == 0)
        {
            return true;
        }

        var zoom = TechTreeLayout.ClampZoom(Zoom);
        var view = new Rect2(Vector2.Zero, Size).Grow(0.5f);
        return Graph.Nodes.All(n => view.Encloses(new Rect2(
            n.X * zoom + _panX, n.Y * zoom + _panY, TechTreeLayout.NodeSize * zoom, TechTreeLayout.NodeSize * zoom)));
    }

    /// <summary>Space under a node square for its name (two lines of the text font).</summary>
    private const float LabelBlock = LabelGap + LabelLine * 2f;

    private const float LabelGap = 4f;

    /// <summary>Distance between the baselines of a two-line name (the glyphs are 22 px tall with accents).</summary>
    private const float LabelLine = 20f;

    /// <summary>When the tree does not fit, pans the least that brings the selected node (and its name) into view.</summary>
    private void BringSelectedIntoView(float pad)
    {
        if (Graph is null || SelectedId is null
            || Graph.Nodes.FirstOrDefault(n => n.Structure.Id == SelectedId) is not { } node)
        {
            return;
        }

        var zoom = TechTreeLayout.ClampZoom(Zoom);
        var left = node.X * zoom + _panX - pad;
        var right = (node.X + TechTreeLayout.NodeSize) * zoom + _panX + pad;
        var top = node.Y * zoom + _panY - pad;
        var bottom = (node.Y + TechTreeLayout.NodeSize + LabelBlock) * zoom + _panY + pad;
        _panX -= left < 0 ? left : right > Size.X ? right - Size.X : 0f;
        _panY -= top < 0 ? top : bottom > Size.Y ? bottom - Size.Y : 0f;
    }

    private bool _wholeTree;

    private void FitWhenSized()
    {
        if (Graph is not null && Size.X >= 1f && Size.Y >= 1f)
        {
            ResetView(_wholeTree);
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
    // The pixel font keeps its size at every zoom: from 1 the names fit their slot (on two lines at most).
    private const float MinReadableZoom = 1f;

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
    private static readonly Color Accent = UiColors.Accento;
    private static readonly Color NodeBg = UiColors.RicercaNodo;
    private static readonly Color NodeBgLocked = UiColors.RicercaNodoBloccato;
    private static readonly Color BorderUnlocked = UiColors.RicercaSbloccato;
    private static readonly Color BorderLocked = UiColors.RicercaBloccato;
    private static readonly Color LineUnlocked = UiColors.RicercaLineaSbloccata;
    private static readonly Color LineAvailable = UiColors.RicercaLineaDisponibile;
    private static readonly Color LineLocked = UiColors.RicercaLineaBloccata;
    private static readonly Color CoreCyan = UiColors.Core;

    public override void _Draw()
    {
        var size = Size;
        DrawRect(new Rect2(Vector2.Zero, size), CanvasFill, true);
        DrawRect(new Rect2(Vector2.Zero, size), CanvasBorder, false, 1f);

        if (Graph is null || Research is null)
        {
            DrawString(UiTheme.TextFont, new Vector2(24, 40),
                "Nessuna struttura nell'albero.", HorizontalAlignment.Left, -1, LabelSize, HintSoft);
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

        // The pixel names keep their size: below the readable zoom (whole-tree view) they would cover each other.
        if (zoom >= MinReadableZoom - 0.001f)
        {
            DrawLabels(zoom, nodeSize, Screen);
        }

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
            else if (Sprites.TryFor(node.IsRoot ? "core-1" : id) is { } tex)
            {
                DrawTextureRect(tex, inner, false,
                    state == ResearchNodeState.Available ? new Color(1f, 1f, 1f, 0.82f) : Colors.White);
            }

        }
    }

    /// <summary>
    /// Names under the nodes, on one or two lines (cut with an ellipsis only when a word is wider than the slot), each
    /// on a patch of the canvas colour so the edges passing under a name never cross its letters. Drawn before the
    /// nodes, which stay on top.
    /// </summary>
    private void DrawLabels(float zoom, float nodeSize, Func<int, int, Vector2> screen)
    {
        var font = UiTheme.TextFont;
        var maxW = (int)(TechTreeLayout.SlotWidth * zoom) - 4;
        foreach (var node in Graph!.Nodes)
        {
            var state = StateOf(node);
            var label = node.IsRoot ? "CORE" : node.Structure.DisplayName;
            var color = node.IsRoot
                ? CoreCyan
                : state switch
                {
                    ResearchNodeState.Unlocked => NodeTitle,
                    ResearchNodeState.Available => Accent,
                    _ => HintSoft with { A = 0.55f }
                };
            var origin = screen(node.X, node.Y);
            var centreX = origin.X + nodeSize / 2f;
            var baseline = origin.Y + nodeSize + LabelGap * zoom + LabelSize;
            foreach (var line in WrapTwo(label, maxW))
            {
                var w = font.GetStringSize(line, HorizontalAlignment.Left, -1, LabelSize).X;
                DrawRect(new Rect2(centreX - w / 2f - 2f, baseline - LabelSize - 2f, w + 4f, LabelLine + 2f), CanvasFill);
                DrawString(font, new Vector2(centreX - w / 2f, baseline), line, HorizontalAlignment.Left, -1, LabelSize, color);
                baseline += LabelLine;
            }
        }
    }

    /// <summary>The name on one line if it fits, else split at the last space that fits; each line cut to the width.</summary>
    private static string[] WrapTwo(string text, int maxWidthPx)
    {
        var font = UiTheme.TextFont;
        if (font.GetStringSize(text, HorizontalAlignment.Left, -1, LabelSize).X <= maxWidthPx)
        {
            return [text];
        }

        for (var i = text.LastIndexOf(' '); i > 0; i = text.LastIndexOf(' ', i - 1))
        {
            var first = text[..i];
            if (font.GetStringSize(first, HorizontalAlignment.Left, -1, LabelSize).X <= maxWidthPx)
            {
                return [first, Truncate(text[(i + 1)..], maxWidthPx, LabelSize)];
            }
        }

        return [Truncate(text, maxWidthPx, LabelSize)];
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
                _lockIcon = UiTheme.Icon("lucchetto");
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

        var font = UiTheme.TextFont;
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
