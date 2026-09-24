using Godot;
using TIndustry.Shared;

namespace TIndustry.Godot;

/// <summary>Draws tech-tree nodes/edges; pan/zoom/select (Raylib-parity).</summary>
public partial class TechTreeCanvas : Control
{
    private static readonly Color CanvasFill = new(20 / 255f, 24 / 255f, 23 / 255f, 1f);
    private static readonly Color CanvasBorder = new(48 / 255f, 56 / 255f, 52 / 255f, 1f);
    private static readonly Color HintSoft = new(164 / 255f, 173 / 255f, 168 / 255f, 1f);
    private static readonly Color PathEdge = new(235 / 255f, 200 / 255f, 110 / 255f, 1f);
    private static readonly Color Gold = new(211 / 255f, 164 / 255f, 76 / 255f, 1f);
    private static readonly Color NodeTitle = new(232 / 255f, 233 / 255f, 221 / 255f, 1f);

    public TechTreeLayout.Graph? Graph { get; set; }
    public ResearchState? Research { get; set; }
    public string? SelectedId { get; set; }
    public float Zoom { get; private set; } = 1f;

    public event Action<string>? NodeSelected;
    public event Action? ViewChanged;

    private float _panX;
    private float _panY;
    private bool _panning;
    private Vector2 _panAnchor;
    private float _panStartX;
    private float _panStartY;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Stop;
        ClipContents = true;
    }

    public void ResetView()
    {
        _panX = 0f;
        _panY = 0f;
        Zoom = 1f;
        _panning = false;
        QueueRedraw();
        ViewChanged?.Invoke();
    }

    public override void _GuiInput(InputEvent @event)
    {
        var mouse = GetLocalMousePosition();
        var over = new Rect2(Vector2.Zero, Size).HasPoint(mouse);

        if (@event is InputEventMouseButton mb)
        {
            if (mb.Pressed
                && (mb.ButtonIndex == MouseButton.Middle
                    || mb.ButtonIndex == MouseButton.Right
                    || (mb.ButtonIndex == MouseButton.Left && mb.ShiftPressed))
                && over)
            {
                _panning = true;
                _panAnchor = mouse;
                _panStartX = _panX;
                _panStartY = _panY;
                AcceptEvent();
                return;
            }

            if (!mb.Pressed
                && mb.ButtonIndex is MouseButton.Middle or MouseButton.Right or MouseButton.Left
                && _panning)
            {
                _panning = false;
                AcceptEvent();
                return;
            }

            if (mb.Pressed && mb.ButtonIndex == MouseButton.WheelUp && over)
            {
                ApplyWheel(1f, mouse, mb.CtrlPressed);
                AcceptEvent();
                return;
            }

            if (mb.Pressed && mb.ButtonIndex == MouseButton.WheelDown && over)
            {
                ApplyWheel(-1f, mouse, mb.CtrlPressed);
                AcceptEvent();
                return;
            }

            if (mb.Pressed && mb.ButtonIndex == MouseButton.Left && !mb.ShiftPressed && !_panning && over)
            {
                TrySelectAt(mouse);
                AcceptEvent();
            }
        }

        if (@event is InputEventMouseMotion && _panning)
        {
            var local = GetLocalMousePosition();
            _panX = _panStartX + (local.X - _panAnchor.X);
            _panY = _panStartY + (local.Y - _panAnchor.Y);
            QueueRedraw();
            ViewChanged?.Invoke();
            AcceptEvent();
        }
    }

    private void ApplyWheel(float direction, Vector2 mouse, bool ctrl)
    {
        if (ctrl)
        {
            var before = TechTreeLayout.ClampZoom(Zoom);
            var after = TechTreeLayout.ClampZoom(before * (direction > 0 ? 1.12f : 1f / 1.12f));
            if (Math.Abs(after - before) > 0.0001f)
            {
                _panX = mouse.X - (mouse.X - _panX) * (after / before);
                _panY = mouse.Y - (mouse.Y - _panY) * (after / before);
                Zoom = after;
                QueueRedraw();
                ViewChanged?.Invoke();
            }
        }
        else
        {
            _panY += direction * 36f;
            QueueRedraw();
            ViewChanged?.Invoke();
        }
    }

    private void TrySelectAt(Vector2 local)
    {
        if (Graph is null)
        {
            return;
        }

        var zoom = TechTreeLayout.ClampZoom(Zoom);
        var nodeW = TechTreeLayout.NodeWidth * zoom;
        var nodeH = TechTreeLayout.NodeHeight * zoom;
        for (var i = Graph.Nodes.Count - 1; i >= 0; i--)
        {
            var node = Graph.Nodes[i];
            var nx = node.X * zoom + _panX;
            var ny = node.Y * zoom + _panY;
            if (local.X >= nx && local.X <= nx + nodeW
                && local.Y >= ny && local.Y <= ny + nodeH)
            {
                NodeSelected?.Invoke(node.Structure.Id);
                return;
            }
        }
    }

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
        var pathIds = string.IsNullOrEmpty(SelectedId)
            ? new HashSet<string>(StringComparer.Ordinal)
            : TechTreeLayout.CollectRelatedIds(Graph, SelectedId);

        foreach (var edge in Graph.Edges)
        {
            var fromUnlocked = Research.IsUnlocked(edge.FromId);
            var toNode = Graph.Nodes.First(n => n.Structure.Id == edge.ToId);
            var toState = Research.GetNodeState(toNode.Structure);
            var onPath = TechTreeLayout.IsEdgeOnPath(edge, pathIds);
            var edgeColor = onPath
                ? PathEdge
                : toState == ResearchNodeState.Unlocked
                    ? new Color(80 / 255f, 160 / 255f, 110 / 255f, 160 / 255f)
                    : toState == ResearchNodeState.Available && fromUnlocked
                        ? new Color(200 / 255f, 160 / 255f, 90 / 255f, 140 / 255f)
                        : new Color(55 / 255f, 62 / 255f, 58 / 255f, 120 / 255f);
            var thickness = onPath ? 3.6f : 2.2f;
            var x1 = edge.FromX * zoom + _panX;
            var y1 = edge.FromY * zoom + _panY;
            var x2 = edge.ToX * zoom + _panX;
            var y2 = edge.ToY * zoom + _panY;
            var midX = (x1 + x2) * 0.5f;
            DrawLine(new Vector2(x1, y1), new Vector2(midX, y1), edgeColor, thickness);
            DrawLine(new Vector2(midX, y1), new Vector2(midX, y2), edgeColor, thickness);
            DrawLine(new Vector2(midX, y2), new Vector2(x2, y2), edgeColor, thickness);
            var tip = onPath ? 12f : 10f;
            DrawColoredPolygon(
                [
                    new Vector2(x2, y2),
                    new Vector2(x2 - tip, y2 - tip * 0.5f),
                    new Vector2(x2 - tip, y2 + tip * 0.5f)
                ],
                edgeColor);
        }

        var nodeW = TechTreeLayout.NodeWidth * zoom;
        var nodeH = TechTreeLayout.NodeHeight * zoom;
        var font = ThemeDB.FallbackFont;
        foreach (var node in Graph.Nodes)
        {
            var structure = node.Structure;
            var nx = node.X * zoom + _panX;
            var ny = node.Y * zoom + _panY;
            var state = Research.GetNodeState(structure);
            var isSelected = string.Equals(structure.Id, SelectedId, StringComparison.Ordinal);
            var onPath = pathIds.Contains(structure.Id);

            var fill = state switch
            {
                ResearchNodeState.Unlocked => new Color(36 / 255f, 62 / 255f, 48 / 255f),
                ResearchNodeState.Available => new Color(58 / 255f, 48 / 255f, 32 / 255f),
                _ => new Color(28 / 255f, 32 / 255f, 31 / 255f)
            };
            if (isSelected)
            {
                fill = state switch
                {
                    ResearchNodeState.Unlocked => new Color(48 / 255f, 84 / 255f, 64 / 255f),
                    ResearchNodeState.Available => new Color(78 / 255f, 64 / 255f, 40 / 255f),
                    _ => new Color(42 / 255f, 48 / 255f, 46 / 255f)
                };
            }
            else if (onPath)
            {
                fill = state switch
                {
                    ResearchNodeState.Unlocked => new Color(42 / 255f, 72 / 255f, 56 / 255f),
                    ResearchNodeState.Available => new Color(68 / 255f, 56 / 255f, 36 / 255f),
                    _ => new Color(36 / 255f, 40 / 255f, 38 / 255f)
                };
            }

            var border = state switch
            {
                ResearchNodeState.Unlocked => new Color(112 / 255f, 218 / 255f, 145 / 255f),
                ResearchNodeState.Available => new Color(220 / 255f, 170 / 255f, 110 / 255f),
                _ => new Color(90 / 255f, 96 / 255f, 92 / 255f)
            };

            if (isSelected)
            {
                DrawRect(new Rect2(nx - 3, ny - 3, nodeW + 6, nodeH + 6),
                    new Color(Gold.R, Gold.G, Gold.B, 90 / 255f), true);
            }
            else if (onPath)
            {
                DrawRect(new Rect2(nx - 2, ny - 2, nodeW + 4, nodeH + 4),
                    new Color(Gold.R, Gold.G, Gold.B, 40 / 255f), true);
            }

            DrawRect(new Rect2(nx, ny, nodeW, nodeH), fill, true);
            DrawRect(new Rect2(nx, ny, nodeW, nodeH), border, false, 1.5f);

            var statusLabel = state switch
            {
                ResearchNodeState.Unlocked => structure.IsStub ? "SBLOCCATO · segnaposto" : "SBLOCCATO",
                ResearchNodeState.Available => "DISPONIBILE",
                _ => "BLOCCATO"
            };
            var titleSize = zoom < 0.75f ? 12 : 14;
            var statusSize = zoom < 0.75f ? 10 : 11;
            DrawString(font, new Vector2(nx + 10, ny + 8 + titleSize),
                Truncate(structure.DisplayName, (int)(nodeW - 20), titleSize),
                HorizontalAlignment.Left, -1, titleSize, NodeTitle);
            DrawString(font, new Vector2(nx + 10, ny + Math.Max(30f, nodeH - 8)),
                Truncate(statusLabel, (int)(nodeW - 20), statusSize),
                HorizontalAlignment.Left, -1, statusSize, border);
        }
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
