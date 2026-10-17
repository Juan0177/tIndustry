using Godot;
using TIndustry.Shared;

namespace TIndustry.Godot;

/// <summary>
/// Everything about construction, in one node above buildings and items (the HUD is a CanvasLayer, so
/// it stays on top): the local player's plans (drawn here), the sites (a sprite each with the
/// <c>construct</c> shader) and the drones with their build beam (<see cref="DroneVisual"/>).
/// Reads <see cref="IGameView"/> only; plans are redrawn only when the simulation ticked.
/// </summary>
public partial class ConstructionLayer : Node2D
{
    /// <summary>Mindustry's <c>Pal.accent</c>: plans, beam and site outline.</summary>
    public static readonly Color Accent = new("FFD37F");

    /// <summary>Mindustry's <c>Pal.remove</c>: deconstruction and breaking plans.</summary>
    public static readonly Color Remove = new("E55454");

    private const int LocalPlayer = 0;
    private const float PlanAlpha = 0.35f;
    private const int SitesZ = 1;

    private static readonly Dictionary<string, Texture2D?> Textures = new(StringComparer.Ordinal);
    private static Texture2D? _blank;

    private readonly Shader _shader = GD.Load<Shader>("res://shaders/construct.gdshader");
    private readonly Dictionary<long, SiteVisual> _sites = [];
    private readonly Dictionary<long, DroneVisual> _drones = [];
    private IGameView? _view;
    private long _lastTick = -1;
    private bool _drewPlans;

    private sealed class SiteVisual(Node2D root, ShaderMaterial material)
    {
        public Node2D Root { get; } = root;
        public ShaderMaterial Material { get; } = material;
        public float Time { get; set; }
    }

    /// <summary>
    /// Brings sites, drones and plans up to date with <paramref name="view"/>. Returns true when a site
    /// appeared or vanished this frame: blocks and belts under it changed, so their visuals must be rebuilt.
    /// </summary>
    public bool Sync(IGameView? view, float delta)
    {
        if (!ReferenceEquals(view, _view))
        {
            Clear();
            _view = view;
        }

        if (view is null)
        {
            return false;
        }

        var sitesChanged = SyncSites(view, delta);
        SyncDrones(view, delta);

        if (view.CurrentTick != _lastTick)
        {
            _lastTick = view.CurrentTick;
            if (_drewPlans || view.PlansOf(LocalPlayer).Count > 0)
            {
                QueueRedraw();
            }
        }

        return sitesChanged;
    }

    public override void _Draw()
    {
        _drewPlans = false;
        if (_view is null)
        {
            return;
        }

        foreach (var builder in _view.Builders)
        {
            if (builder.OwnerPlayerId != LocalPlayer)
            {
                continue;
            }

            // The first plan has a site once work began: the site shows it.
            var first = builder.CurrentSiteId is null ? 0 : 1;
            for (var i = first; i < builder.Plans.Count; i++)
            {
                DrawPlan(builder.Plans[i]);
                _drewPlans = true;
            }
        }
    }

    private void DrawPlan(BuildPlan plan)
    {
        var tiles = _view!.PlanTiles(plan);
        if (plan.Breaking)
        {
            var box = Bounds(tiles);
            DrawRect(box, Remove, filled: false, width: -1f);
            DrawLine(box.Position, box.End, Remove, -1f);
            DrawLine(new Vector2(box.End.X, box.Position.Y), new Vector2(box.Position.X, box.End.Y), Remove, -1f);
            return;
        }

        var spec = plan.Spec;
        var tex = TextureFor(spec.StructureId);
        var ghost = new Color(1f, 1f, 1f, PlanAlpha);
        if (spec.Kind is null)
        {
            // Belt: its arrow sprite turned to the belt's direction.
            foreach (var tile in tiles)
            {
                var cell = CellRect(tile);
                DrawSetTransform(cell.GetCenter(), DirectionAngle(spec.Direction), Vector2.One);
                DrawTextureRect(tex, new Rect2(-cell.Size / 2f, cell.Size), false, ghost);
                DrawSetTransform(Vector2.Zero, 0f, Vector2.One);
                DrawRect(cell, Accent, filled: false, width: -1f);
            }

            return;
        }

        if (spec.Kind == BuildingKind.Bridge)
        {
            // Both ends; the exit is where a bridge placed now would end.
            foreach (var tile in tiles)
            {
                DrawTextureRect(tex, CellRect(tile), false, ghost);
                DrawRect(CellRect(tile), Accent, filled: false, width: -1f);
            }

            return;
        }

        var rect = Bounds(tiles);
        DrawTextureRect(tex, rect, false, ghost);
        DrawRect(rect, Accent, filled: false, width: -1f);
    }

    /// <summary>One sprite (two for a bridge) per site with its own shader material; returns true on add/remove.</summary>
    private bool SyncSites(IGameView view, float delta)
    {
        var changed = false;
        var live = new HashSet<long>();
        foreach (var site in view.Sites)
        {
            live.Add(site.Id);
            if (!_sites.TryGetValue(site.Id, out var visual))
            {
                visual = CreateSiteVisual(site);
                _sites[site.Id] = visual;
                AddChild(visual.Root);
                changed = true;
            }

            // A site stalled for resources pulses at half frequency.
            visual.Time += site.Missing is null ? delta : delta * 0.5f;
            visual.Material.SetShaderParameter("progress", site.Progress);
            visual.Material.SetShaderParameter("color", ColorOf(site));
            visual.Material.SetShaderParameter("time", visual.Time);
        }

        foreach (var id in _sites.Keys.Where(id => !live.Contains(id)).ToList())
        {
            _sites[id].Root.QueueFree();
            _sites.Remove(id);
            changed = true;
        }

        return changed;
    }

    private SiteVisual CreateSiteVisual(ConstructSite site)
    {
        var material = new ShaderMaterial { Shader = _shader };
        var root = new Node2D { Name = $"Site_{site.Id}", ZIndex = SitesZ };
        var spec = site.Spec;
        var tex = TextureFor(spec.StructureId);
        if (spec.Kind is null || spec.Kind == BuildingKind.Bridge)
        {
            // One cell per piece (bridge: both ends); belts turn to their direction.
            var angle = spec.Kind is null ? DirectionAngle(spec.Direction) : 0f;
            foreach (var tile in site.Tiles)
            {
                root.AddChild(SiteSprite(tex, material, CellRect(tile), angle));
            }
        }
        else
        {
            root.AddChild(SiteSprite(tex, material, Bounds(site.Tiles), 0f));
        }

        return new SiteVisual(root, material);
    }

    private static Sprite2D SiteSprite(Texture2D tex, ShaderMaterial material, Rect2 rect, float angle) => new()
    {
        Texture = tex,
        Material = material,
        Centered = true,
        Position = rect.GetCenter(),
        Rotation = angle,
        Scale = rect.Size / tex.GetSize(),
        TextureFilter = TextureFilterEnum.Nearest
    };

    private void SyncDrones(IGameView view, float delta)
    {
        var live = new HashSet<long>();
        foreach (var builder in view.Builders)
        {
            live.Add(builder.Id);
            if (!_drones.TryGetValue(builder.Id, out var drone))
            {
                drone = DroneVisual.Create(builder);
                _drones[builder.Id] = drone;
                AddChild(drone);
            }

            Rect2? beam = null;
            var beamColor = Accent;
            if (builder.CurrentSiteId is { } siteId && FindSite(view, siteId) is { } site)
            {
                beam = Bounds(site.Tiles);
                beamColor = ColorOf(site);
            }

            drone.Sync(builder, view.CurrentTick, delta, beam, beamColor);
        }

        foreach (var id in _drones.Keys.Where(id => !live.Contains(id)).ToList())
        {
            _drones[id].QueueFree();
            _drones.Remove(id);
        }
    }

    private static ConstructSite? FindSite(IGameView view, long id)
    {
        foreach (var site in view.Sites)
        {
            if (site.Id == id)
            {
                return site;
            }
        }

        return null;
    }

    private void Clear()
    {
        foreach (var visual in _sites.Values)
        {
            visual.Root.QueueFree();
        }

        foreach (var drone in _drones.Values)
        {
            drone.QueueFree();
        }

        _sites.Clear();
        _drones.Clear();
        _lastTick = -1;
        QueueRedraw();
    }

    private static Color ColorOf(ConstructSite site) => site.Deconstructing ? Remove : Accent;

    private static Rect2 CellRect(GridPosition tile) =>
        new(tile.X * World.TileSize, tile.Y * World.TileSize, World.TileSize, World.TileSize);

    /// <summary>World rect covering <paramref name="tiles"/>.</summary>
    private static Rect2 Bounds(IReadOnlyList<GridPosition> tiles)
    {
        var rect = CellRect(tiles[0]);
        for (var i = 1; i < tiles.Count; i++)
        {
            rect = rect.Merge(CellRect(tiles[i]));
        }

        return rect;
    }

    private static float DirectionAngle(Direction direction)
    {
        var (dx, dy) = DirectionMath.ToOffset(direction);
        return Mathf.Atan2(dy, dx);
    }

    /// <summary>Block sprite of a structure id (flat <c>assets/</c>); a white square when there is none.</summary>
    private static Texture2D TextureFor(string structureId)
    {
        if (!Textures.TryGetValue(structureId, out var tex))
        {
            var name = structureId switch
            {
                "conveyor-express" => "conveyor-fast",
                "conveyor-bridge" => "bridge",
                _ => structureId
            };
            var path = $"res://assets/{name}.png";
            tex = ResourceLoader.Exists(path) ? GD.Load<Texture2D>(path) : null;
            Textures[structureId] = tex;
        }

        return tex ?? (_blank ??= BlankTexture());
    }

    private static Texture2D BlankTexture()
    {
        var image = Image.CreateEmpty(8, 8, false, Image.Format.Rgba8);
        image.Fill(Colors.White);
        return ImageTexture.CreateFromImage(image);
    }
}
