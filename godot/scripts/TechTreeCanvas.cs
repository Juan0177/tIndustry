using Godot;
using TIndustry.Shared;

namespace TIndustry.Godot;

/// <summary>Draws tech-tree icon nodes + orthogonal edges (Mindustry-leaning).</summary>
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

            // Icon cell centered in the layout footprint (Mindustry node feel).
            var iconBox = Mathf.Min(nodeH - 4f * zoom, 52f * zoom);
            var cellX = nx + (nodeW - iconBox) * 0.5f;
            var cellY = ny + 2f * zoom;

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
                DrawRect(new Rect2(cellX - 4, cellY - 4, iconBox + 8, iconBox + 8),
                    new Color(Gold.R, Gold.G, Gold.B, 90 / 255f), true);
            }
            else if (onPath)
            {
                DrawRect(new Rect2(cellX - 2, cellY - 2, iconBox + 4, iconBox + 4),
                    new Color(Gold.R, Gold.G, Gold.B, 40 / 255f), true);
            }

            DrawRect(new Rect2(cellX, cellY, iconBox, iconBox), fill, true);
            DrawRect(new Rect2(cellX, cellY, iconBox, iconBox), border, false, isSelected ? 2.4f : 1.5f);

            var tex = ResolveIcon(structure.Id);
            var pad = 6f * zoom;
            if (tex is not null)
            {
                var modulate = state == ResearchNodeState.Locked
                    ? new Color(0.55f, 0.55f, 0.55f, 0.85f)
                    : Colors.White;
                DrawTextureRect(
                    tex,
                    new Rect2(cellX + pad, cellY + pad, iconBox - pad * 2, iconBox - pad * 2),
                    false,
                    modulate);
            }
            else
            {
                // Letter plate fallback when no sprite ships yet.
                var initials = Initials(structure.DisplayName);
                var fs = (int)Mathf.Clamp(14 * zoom, 10, 18);
                var tw = font.GetStringSize(initials, HorizontalAlignment.Left, -1, fs).X;
                DrawString(font,
                    new Vector2(cellX + (iconBox - tw) * 0.5f, cellY + iconBox * 0.55f),
                    initials, HorizontalAlignment.Left, -1, fs, NodeTitle);
            }

            // Compact status pip under icon (no long text card).
            var pip = state switch
            {
                ResearchNodeState.Unlocked => "●",
                ResearchNodeState.Available => "○",
                _ => "×"
            };
            var pipSize = (int)Mathf.Clamp(11 * zoom, 9, 14);
            var pipW = font.GetStringSize(pip, HorizontalAlignment.Left, -1, pipSize).X;
            DrawString(font,
                new Vector2(cellX + (iconBox - pipW) * 0.5f, cellY + iconBox + 2 + pipSize),
                pip, HorizontalAlignment.Left, -1, pipSize, border);

            if (isSelected || zoom >= 0.9f)
            {
                var titleSize = zoom < 0.75f ? 10 : 11;
                var title = Truncate(structure.DisplayName, (int)(nodeW - 8), titleSize);
                var titleW = font.GetStringSize(title, HorizontalAlignment.Left, -1, titleSize).X;
                DrawString(font,
                    new Vector2(nx + (nodeW - titleW) * 0.5f, ny + nodeH - 2),
                    title, HorizontalAlignment.Left, -1, titleSize, NodeTitle);
            }
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
            "conveyor-basic" or "conveyor-fast" or "conveyor-express" => "res://assets/conveyor-basic.png",
            "conveyor-bridge" => "res://assets/bridge.png",
            "miner" or "miner-advanced" => "res://assets/miner.png",
            "smelter" => "res://assets/smelter.png",
            "assembler" => "res://assets/assembler.png",
            "junction" => "res://assets/junction.png",
            "splitter" => "res://assets/splitter.png",
            "sorter" => "res://assets/sorter.png",
            "generator" => "res://assets/generator.png",
            "power-node" or "power-node-t2" => "res://assets/generator.png",
            "extractor" => "res://assets/miner.png",
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
