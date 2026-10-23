using Godot;
using TIndustry.Shared;

namespace TIndustry.Godot;

/// <summary>
/// Everything about construction, in one node above buildings and items (the HUD is a CanvasLayer, so
/// it stays on top): the local player's plans (drawn on their own layer, see <see cref="_Ready"/>), the sites (a sprite each with the
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
    private int? _plansSignature;
    private VarianteColori _variante;
    private Node2D? _plans;

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
            // A bigger Core changes the belts that now touch it (their joins) as well as the Core itself.
            : BuildSpec.IsCoreUpgrade(spec.Kind) ? StructureLayers.Both
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

        // Plans redraw when what they show changed (local plans, which sites stand, which are taken apart), or in new
        // colours when the colour variant changes; checked once per tick, not redrawn every tick.
        if (view.CurrentTick != _lastTick || UiColors.Variante != _variante)
        {
            _lastTick = view.CurrentTick;
            var signature = PlansSignature(view);
            if (signature != _plansSignature || UiColors.Variante != _variante)
            {
                _plansSignature = signature;
                _plans?.QueueRedraw();
            }

            _variante = UiColors.Variante;
        }

        return changed;
    }

    /// <summary>
    /// The plans are drawn on a canvas layer of their own that follows the camera (like the placement ghost): aligned
    /// with the world, above it, and never darkened by the night.
    /// </summary>
    public override void _Ready()
    {
        var layer = new CanvasLayer { Name = "Piani", Layer = World.PlansLayer, FollowViewportEnabled = true };
        _plans = new Node2D { Name = "PianiLocali" };
        _plans.Draw += () => DrawPlans(_plans);
        layer.AddChild(_plans);
        AddChild(layer);
    }

    /// <summary>Sites being taken apart that the last plans redraw hatched (checked by the playtest).</summary>
    public int HatchedSites { get; private set; }

    private void DrawPlans(CanvasItem canvas)
    {
        if (_view is null)
        {
            return;
        }

        // Sites being taken apart (anyone's) carry the removal hatching too, not just its colour.
        var sites = _view.Sites;
        HatchedSites = 0;
        for (var i = 0; i < sites.Count; i++)
        {
            if (sites[i].Deconstructing)
            {
                DrawRemovalHatch(canvas, Bounds(sites[i].Tiles));
                HatchedSites++;
            }
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
            // the site.
            for (var i = 0; i < builder.Plans.Count; i++)
            {
                var plan = builder.Plans[i];
                if ((i == 0 && builder.CurrentSiteId is not null) || _view.SiteOfPlan(plan) is not null)
                {
                    continue;
                }

                DrawPlan(canvas, plan);
            }
        }
    }

    /// <summary>What the plans layer draws, as a hash: the local player's plans in order and every site with its kind.</summary>
    private static int PlansSignature(IGameView view)
    {
        var hash = new HashCode();
        var builders = view.Builders;
        for (var b = 0; b < builders.Count; b++)
        {
            if (builders[b].OwnerPlayerId != LocalPlayer)
            {
                continue;
            }

            var plans = builders[b].Plans;
            hash.Add(plans.Count);
            for (var i = 0; i < plans.Count; i++)
            {
                hash.Add(plans[i]);
            }
        }

        var sites = view.Sites;
        hash.Add(sites.Count);
        for (var i = 0; i < sites.Count; i++)
        {
            hash.Add(sites[i].Id);
            hash.Add(sites[i].Deconstructing);
        }

        return hash.ToHashCode();
    }

    private void DrawPlan(CanvasItem canvas, BuildPlan plan)
    {
        var tiles = _view!.PlanTiles(plan);
        if (plan.Breaking)
        {
            // Mindustry style: a translucent fill over the whole block and its outline, plus a hatching (the X is the
            // refused ghost's sign): the mark reads without its colour.
            var box = Bounds(tiles);
            canvas.DrawRect(box, new Color(UiColors.Smontaggio, BreakFillAlpha));
            DrawRemovalHatch(canvas, box);
            return;
        }

        // The same preview as the placement ghost (a belt drag shows every planned cell like the ghost).
        var spec = plan.Spec;
        var arrow = PlacementPreview.HasDirection(spec.Kind);
        if (spec.Kind is null or BuildingKind.Bridge)
        {
            // Belts: one turned sprite per cell; a bridge: both ends (the exit is where a bridge placed now would end).
            for (var i = 0; i < tiles.Count; i++)
            {
                PlacementPreview.Draw(canvas, spec.StructureId, CellRect(tiles[i]), spec.Direction,
                    turnSprite: spec.Kind is null, arrow, valid: true);
            }

            return;
        }

        PlacementPreview.Draw(canvas, spec.StructureId, Bounds(tiles), spec.Direction, turnSprite: false, arrow, valid: true);
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

            // A site stalled for resources pulses at half frequency; still in playtests (repeatable captures).
            if (!ClientSettings.PlaytestForcesEffectsOff)
            {
                visual.Time += site.Missing is null ? delta : delta * 0.5f;
            }
            visual.Material.SetShaderParameter(ProgressParam, site.Progress);
            visual.Material.SetShaderParameter(ColorParam, ColorOf(site));
            visual.Material.SetShaderParameter(TimeParam, visual.Time);
        }

        if (_sites.Count != _live.Count)
        {
            CollectGone(_sites);
            foreach (var id in _gone)
            {
                Free(_sites[id]);
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
            var angle = spec.Kind is null ? PlacementPreview.Angle(spec.Direction) : 0f;
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

            drone.SetTier(view.CoreTier);
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

    public override void _ExitTree()
    {
        // Engine teardown frees the nodes; the C# material wrappers go now, not to the finalizer.
        foreach (var visual in _sites.Values)
        {
            visual.Material.Dispose();
        }

        _sites.Clear();
    }

    /// <summary>Frees a site's nodes and its own material (no wrapper left for the finalizer).</summary>
    private static void Free(SiteVisual visual)
    {
        visual.Root.QueueFree();
        visual.Material.Dispose();
    }

    /// <summary>Drops every site, drone and cached site rect (a new game is attached: its site ids start over).</summary>
    public void Clear()
    {
        foreach (var visual in _sites.Values)
        {
            Free(visual);
        }

        foreach (var drone in _drones.Values)
        {
            drone.QueueFree();
        }

        _sites.Clear();
        _drones.Clear();
        _lastTick = -1;
        _plansSignature = null;
        _plans?.QueueRedraw();
    }

    /// <summary>The removal sign: diagonal hatching and an outline, over a mark or a site being taken apart.</summary>
    private static void DrawRemovalHatch(CanvasItem canvas, Rect2 box)
    {
        if (UiColors.SottoSegno.A > 0f)
        {
            canvas.DrawRect(box.Grow(-2f), UiColors.SottoSegno, filled: false, width: 5f);
        }

        PlacementPreview.DrawHatch(canvas, box, UiColors.Smontaggio, World.TileSize / 4f, 2f);
        canvas.DrawRect(box.Grow(-1f), UiColors.Smontaggio, filled: false, width: 2f);
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
}
