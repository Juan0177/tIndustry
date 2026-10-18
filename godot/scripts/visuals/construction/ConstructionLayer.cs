using Godot;
using TIndustry.Shared;

namespace TIndustry.Godot;

/// <summary>
/// Everything about construction, in one node above buildings and items (the HUD is a CanvasLayer, so
/// it stays on top): the local player's plans (drawn here), the sites (a sprite each with the
/// <c>construct</c> shader) and the drones with their build beam (<see cref="DroneVisual"/>).
/// Reads <see cref="IGameView"/> only; plans are redrawn only when the simulation ticked. The per-frame
/// sync walks the view's lists by index (no boxed enumerators) and reuses its buffers: nothing is allocated
/// while the sets of sites and drones stay the same.
/// </summary>
public partial class ConstructionLayer : Node2D
{
    /// <summary>Which structure visuals of the world a construction change touches.</summary>
    [Flags]
    public enum StructureLayers
    {
        None = 0,
        Buildings = 1,
        Belts = 2,
        Both = Buildings | Belts
    }

    private const int LocalPlayer = 0;
    private const float PlanAlpha = 0.35f;
    private const float BreakFillAlpha = 0.3f;
    private const int SitesZ = 1;

    private static readonly StringName ProgressParam = "progress";
    private static readonly StringName ColorParam = "color";
    private static readonly StringName TimeParam = "time";

    private readonly Shader _shader = GD.Load<Shader>("res://shaders/construct.gdshader");
    private readonly Dictionary<long, SiteVisual> _sites = [];
    private readonly Dictionary<long, DroneVisual> _drones = [];
    private readonly HashSet<long> _live = [];
    private readonly List<long> _gone = [];
    private IGameView? _view;
    private long _lastTick = -1;
    private bool _drewPlans;

    private sealed class SiteVisual(Node2D root, ShaderMaterial material, BuildSpec spec, Rect2 bounds)
    {
        public Node2D Root { get; } = root;
        public ShaderMaterial Material { get; } = material;
        public BuildSpec Spec { get; } = spec;
        public Rect2 Bounds { get; } = bounds;
        public float Time { get; set; }
    }

    /// <summary>World layer that draws <paramref name="spec"/> once built: belts and logistics, or buildings.</summary>
    public static StructureLayers LayerOf(BuildSpec spec) =>
        spec.Kind is null or BuildingKind.Junction or BuildingKind.Splitter or BuildingKind.Sorter or BuildingKind.Bridge
            ? StructureLayers.Belts
            : StructureLayers.Buildings;

    /// <summary>
    /// Layer a just finished site changes. Its event arrives before the next <see cref="Sync"/>, so the site
    /// is still known here; one that came and went between two frames was never seen and touches both.
    /// </summary>
    public StructureLayers LayerOfSite(long siteId) =>
        _sites.TryGetValue(siteId, out var visual) ? LayerOf(visual.Spec) : StructureLayers.Both;

    /// <summary>
    /// Particle effects (G3) of the drones' build beam; null = none. The site rects are kept for the effects of a
    /// finished site, whose event carries no position (see <see cref="SiteBounds"/>).
    /// </summary>
    public EffectManager? Effects { get; set; }

    /// <summary>
    /// World rect of a site drawn by the last <see cref="Sync"/>, or null if it was never seen. A finished site's
    /// event arrives before the next Sync, so its rect is still here.
    /// </summary>
    public Rect2? SiteBounds(long siteId) => _sites.TryGetValue(siteId, out var visual) ? visual.Bounds : null;

    /// <summary>True once the site has been drawn: for a deconstruction, its block's removal was seen.</summary>
    public bool HasSeenSite(long siteId) => _sites.ContainsKey(siteId);

    /// <summary>
    /// Brings sites, drones and plans up to date with <paramref name="view"/>. Returns the layers whose
    /// blocks changed under a site that appeared this frame: a deconstruction takes its block away when it
    /// starts. A construction changes the world only when it finishes (see <see cref="LayerOfSite"/>).
    /// </summary>
    public StructureLayers Sync(IGameView? view, float delta)
    {
        if (!ReferenceEquals(view, _view))
        {
            Clear();
            _view = view;
        }

        if (view is null)
        {
            return StructureLayers.None;
        }

        var changed = SyncSites(view, delta);
        SyncDrones(view, delta);

        if (view.CurrentTick != _lastTick)
        {
            _lastTick = view.CurrentTick;
            if (_drewPlans || HasLocalPlans(view))
            {
                QueueRedraw();
            }
        }

        return changed;
    }

    public override void _Draw()
    {
        _drewPlans = false;
        if (_view is null)
        {
            return;
        }

        var builders = _view.Builders;
        for (var b = 0; b < builders.Count; b++)
        {
            var builder = builders[b];
            if (builder.OwnerPlayerId != LocalPlayer)
            {
                continue;
            }

            // A plan whose site stands (the current one, or one sent to the back while stalled) is shown by
            // the site. Plans still count as drawn so the layer keeps redrawing while any exist.
            for (var i = 0; i < builder.Plans.Count; i++)
            {
                _drewPlans = true;
                var plan = builder.Plans[i];
                if ((i == 0 && builder.CurrentSiteId is not null) || _view.SiteOfPlan(plan) is not null)
                {
                    continue;
                }

                DrawPlan(plan);
            }
        }
    }

    private static bool HasLocalPlans(IGameView view)
    {
        var builders = view.Builders;
        for (var i = 0; i < builders.Count; i++)
        {
            var builder = builders[i];
            if (builder.OwnerPlayerId == LocalPlayer && builder.Plans.Count > 0)
            {
                return true;
            }
        }

        return false;
    }

    private void DrawPlan(BuildPlan plan)
    {
        var tiles = _view!.PlanTiles(plan);
        if (plan.Breaking)
        {
            // Mindustry style: a translucent red fill over the whole block, plus the outline and the X.
            var box = Bounds(tiles);
            DrawRect(box, new Color(UiColors.Smontaggio, BreakFillAlpha));
            DrawRect(box, UiColors.Smontaggio, filled: false, width: -1f);
            DrawLine(box.Position, box.End, UiColors.Smontaggio, -1f);
            DrawLine(new Vector2(box.End.X, box.Position.Y), new Vector2(box.Position.X, box.End.Y), UiColors.Smontaggio, -1f);
            return;
        }

        var spec = plan.Spec;
        var tex = Sprites.For(spec.StructureId);
        var ghost = new Color(1f, 1f, 1f, PlanAlpha);
        if (spec.Kind is null)
        {
            // Belt: its arrow sprite turned to the belt's direction.
            for (var i = 0; i < tiles.Count; i++)
            {
                var cell = CellRect(tiles[i]);
                DrawSetTransform(cell.GetCenter(), DirectionAngle(spec.Direction), Vector2.One);
                DrawTextureRect(tex, new Rect2(-cell.Size / 2f, cell.Size), false, ghost);
                DrawSetTransform(Vector2.Zero, 0f, Vector2.One);
                DrawRect(cell, UiColors.Costruzione, filled: false, width: -1f);
            }

            return;
        }

        if (spec.Kind == BuildingKind.Bridge)
        {
            // Both ends; the exit is where a bridge placed now would end.
            for (var i = 0; i < tiles.Count; i++)
            {
                DrawTextureRect(tex, CellRect(tiles[i]), false, ghost);
                DrawRect(CellRect(tiles[i]), UiColors.Costruzione, filled: false, width: -1f);
            }

            return;
        }

        var rect = Bounds(tiles);
        DrawTextureRect(tex, rect, false, ghost);
        DrawRect(rect, UiColors.Costruzione, filled: false, width: -1f);
    }

    /// <summary>
    /// One sprite (two for a bridge) per site with its own shader material. Returns the layers of the new
    /// sites that start as deconstructions: their block is gone now.
    /// </summary>
    private StructureLayers SyncSites(IGameView view, float delta)
    {
        var changed = StructureLayers.None;
        _live.Clear();
        var sites = view.Sites;
        for (var i = 0; i < sites.Count; i++)
        {
            var site = sites[i];
            _live.Add(site.Id);
            if (!_sites.TryGetValue(site.Id, out var visual))
            {
                visual = CreateSiteVisual(site);
                _sites[site.Id] = visual;
                AddChild(visual.Root);
                if (site.Deconstructing)
                {
                    changed |= LayerOf(site.Spec);
                }
            }

            // A site stalled for resources pulses at half frequency.
            visual.Time += site.Missing is null ? delta : delta * 0.5f;
            visual.Material.SetShaderParameter(ProgressParam, site.Progress);
            visual.Material.SetShaderParameter(ColorParam, ColorOf(site));
            visual.Material.SetShaderParameter(TimeParam, visual.Time);
        }

        if (_sites.Count != _live.Count)
        {
            CollectGone(_sites);
            foreach (var id in _gone)
            {
                _sites[id].Root.QueueFree();
                _sites.Remove(id);
            }
        }

        return changed;
    }

    /// <summary>Fills <see cref="_gone"/> with the ids of <paramref name="known"/> missing from <see cref="_live"/>.</summary>
    private void CollectGone<T>(Dictionary<long, T> known)
    {
        _gone.Clear();
        foreach (var id in known.Keys)
        {
            if (!_live.Contains(id))
            {
                _gone.Add(id);
            }
        }
    }

    private SiteVisual CreateSiteVisual(ConstructSite site)
    {
        var material = new ShaderMaterial { Shader = _shader };
        var root = new Node2D { Name = $"Site_{site.Id}", ZIndex = SitesZ };
        var spec = site.Spec;
        var tex = Sprites.For(spec.StructureId);
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

        return new SiteVisual(root, material, spec, Bounds(site.Tiles));
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
        _live.Clear();
        var builders = view.Builders;
        for (var i = 0; i < builders.Count; i++)
        {
            var builder = builders[i];
            _live.Add(builder.Id);
            if (!_drones.TryGetValue(builder.Id, out var drone))
            {
                drone = DroneVisual.Create(builder);
                _drones[builder.Id] = drone;
                AddChild(drone);
            }

            Rect2? beam = null;
            var beamColor = UiColors.Costruzione;
            if (builder.CurrentSiteId is { } siteId && FindSite(view, siteId) is { } site)
            {
                beam = Bounds(site.Tiles);
                beamColor = ColorOf(site);
            }

            drone.Sync(builder, view.CurrentTick, delta, beam, beamColor, Effects);
        }

        if (_drones.Count != _live.Count)
        {
            CollectGone(_drones);
            foreach (var id in _gone)
            {
                _drones[id].QueueFree();
                _drones.Remove(id);
            }
        }
    }

    private static ConstructSite? FindSite(IGameView view, long id)
    {
        var sites = view.Sites;
        for (var i = 0; i < sites.Count; i++)
        {
            var site = sites[i];
            if (site.Id == id)
            {
                return site;
            }
        }

        return null;
    }

    /// <summary>World positions of the drones drawn (their lights at night).</summary>
    public IEnumerable<Vector2> DronePositions => _drones.Values.Select(d => d.Position);

    /// <summary>Drops every site, drone and cached site rect (a new game is attached: its site ids start over).</summary>
    public void Clear()
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

    private static Color ColorOf(ConstructSite site) => site.Deconstructing ? UiColors.Smontaggio : UiColors.Costruzione;

    private static Rect2 CellRect(GridPosition tile) =>
        new(tile.X * World.TileSize, tile.Y * World.TileSize, World.TileSize, World.TileSize);

    /// <summary>World rect covering <paramref name="tiles"/>.</summary>
    public static Rect2 Bounds(IReadOnlyList<GridPosition> tiles)
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
}
