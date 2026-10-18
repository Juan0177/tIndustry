using Godot;
using TIndustry.Shared;

namespace TIndustry.Godot;

/// <summary>
/// The night (G3, <see cref="GameRules.Night"/>): a <see cref="CanvasModulate"/> darkens the world's canvas and
/// <see cref="PointLight2D"/>s on the Core, the working furnaces, the burning generators, the lit lamps and the drones
/// cut through it. The HUD is on its own CanvasLayer, which a CanvasModulate never reaches. Lights only for sources
/// in view (plus their radius), at most <see cref="MaxLights"/>, taken from a list that never shrinks. Looks only.
/// </summary>
public partial class NightLayer : Node2D
{
    /// <summary>Most lights drawn at once (the rest of the sources in view stay dark).</summary>
    public const int MaxLights = 128;

    /// <summary>Colour the whole world is multiplied by at night: dark blue, belts and items still readable.</summary>
    public static readonly Color Darkness = new(0.34f, 0.38f, 0.54f);

    private readonly record struct LightKind(float RadiusCells, Color Color, float Energy);

    private static readonly LightKind CoreLight = new(5f, new Color(0.55f, 0.88f, 1f), 1.3f);
    private static readonly LightKind LampLight = new(6.5f, new Color(1f, 0.9f, 0.68f), 1.4f);
    private static readonly LightKind SmelterLight = new(3f, new Color(1f, 0.55f, 0.25f), 1.2f);
    private static readonly LightKind GeneratorLight = new(3f, new Color(1f, 0.84f, 0.5f), 1f);
    private static readonly LightKind DroneLight = new(2f, new Color(1f, 0.95f, 0.82f), 0.9f);

    private readonly List<PointLight2D> _lights = [];
    private CanvasModulate? _darkness;
    private Texture2D? _texture;
    private int _used;
    private Rect2 _view;
    private int _tileSize;
    private IReadOnlySet<GridPosition>? _coreTiles;
    private Vector2 _coreCenterCells;

    /// <summary>True while the night is drawn.</summary>
    public bool Night { get; private set; }

    /// <summary>Lights drawn this frame (≤ <see cref="MaxLights"/>).</summary>
    public int ActiveLights => _used;

    /// <summary>Light nodes created so far (drawn or idle): flat once the busiest view has been seen.</summary>
    public int LightNodes => _lights.Count;

    /// <summary>The darkness node, or null before the first night.</summary>
    public CanvasModulate? DarknessNode => _darkness;

    /// <summary>Lights drawn this frame (playtest checks).</summary>
    public IEnumerable<PointLight2D> VisibleLights => _lights.Take(_used);

    /// <summary>Night on or off: the darkness shows or hides at once, the lights follow at the next <see cref="Sync"/>.</summary>
    public void SetNight(bool on)
    {
        Night = on;
        if (on && _darkness is null)
        {
            _darkness = new CanvasModulate { Name = "Buio", Color = Darkness };
            AddChild(_darkness);
        }

        if (_darkness is not null)
        {
            _darkness.Visible = on;
        }

        if (!on)
        {
            HideFrom(0);
        }
    }

    /// <summary>
    /// Every frame: one light per source of <paramref name="view"/> inside <paramref name="viewRect"/> (world pixels),
    /// Core first, then lamps, furnaces, generators and <paramref name="drones"/> (world positions), until
    /// <see cref="MaxLights"/>.
    /// </summary>
    public void Sync(IGameView? view, Rect2 viewRect, IEnumerable<Vector2> drones, int tileSize)
    {
        if (!Night || view is null)
        {
            HideFrom(0);
            return;
        }

        _texture ??= Sprites.For("luce");
        _view = viewRect;
        _tileSize = tileSize;
        _used = 0;

        if (view.CoreTiles.Count > 0)
        {
            Add(CoreCenterCells(view.CoreTiles) * tileSize, CoreLight);
        }

        foreach (var lamp in view.Lamps)
        {
            if (lamp.IsLit)
            {
                Add(Center(lamp.Position, Lamp.Size), LampLight);
            }
        }

        foreach (var smelter in view.Smelters)
        {
            if (smelter.IsCrafting)
            {
                Add(Center(smelter.Position, Crafter.Size), SmelterLight);
            }
        }

        foreach (var gen in view.Generators)
        {
            if (gen.IsGenerating)
            {
                Add(Center(gen.Position, Generator.Size), GeneratorLight);
            }
        }

        foreach (var drone in drones)
        {
            Add(drone, DroneLight);
        }

        HideFrom(_used);
    }

    /// <summary>Centre of the Core in cells, computed once per game: a new game or a loaded save brings a new set.</summary>
    private Vector2 CoreCenterCells(IReadOnlySet<GridPosition> tiles)
    {
        if (!ReferenceEquals(tiles, _coreTiles))
        {
            var min = new Vector2(tiles.Min(t => t.X), tiles.Min(t => t.Y));
            var max = new Vector2(tiles.Max(t => t.X) + 1, tiles.Max(t => t.Y) + 1);
            _coreCenterCells = (min + max) * 0.5f;
            _coreTiles = tiles;
        }

        return _coreCenterCells;
    }

    private Vector2 Center(GridPosition origin, int size) =>
        new((origin.X + size * 0.5f) * _tileSize, (origin.Y + size * 0.5f) * _tileSize);

    private void Add(Vector2 at, LightKind kind)
    {
        if (_used >= MaxLights)
        {
            return;
        }

        var radius = kind.RadiusCells * _tileSize;
        if (!_view.Grow(radius).HasPoint(at))
        {
            return;
        }

        if (_used == _lights.Count)
        {
            var created = new PointLight2D
            {
                Name = $"Luce{_lights.Count}",
                Texture = _texture,
                BlendMode = Light2D.BlendModeEnum.Add,
                ShadowEnabled = false
            };
            AddChild(created);
            _lights.Add(created);
        }

        var light = _lights[_used++];
        light.Position = at;
        light.TextureScale = radius * 2f / _texture!.GetWidth();
        light.Color = kind.Color;
        light.Energy = kind.Energy;
        light.Visible = true;
    }

    private void HideFrom(int index)
    {
        for (var i = index; i < _lights.Count; i++)
        {
            if (_lights[i].Visible)
            {
                _lights[i].Visible = false;
            }
        }

        if (index == 0)
        {
            _used = 0;
        }
    }
}
