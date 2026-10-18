using Godot;

namespace TIndustry.Godot;

/// <summary>
/// The one place particle effects come from (G3). Visuals ask for an effect by name: a burst at a point
/// (<see cref="Play"/>) or a continuous emitter that follows a node (<see cref="Attach"/>). Effects are data
/// (<c>godot/effetti/effetti.json</c>, rules in <see cref="EffectRules"/>), drawn with <see cref="CpuParticles2D"/>
/// (same look on every GPU) taken from a pool that never shrinks, so the node count stays flat once warm.
/// Respects the quality setting, the per-screen particle cap and the view (plus a margin): nothing starts off screen.
/// Looks only: nothing here reaches the simulation.
/// </summary>
public partial class EffectManager : Node2D
{
    private const string DataPath = "res://effetti/effetti.json";

    /// <summary>Particle sprites are pixel art drawn x2 in the world, like every other sprite.</summary>
    private const float PixelScale = 2f;

    /// <summary>Most emitter nodes alive at once (pooled ones included): bounds the node count whatever is asked.</summary>
    public const int MaxEmitters = 256;

    // Instance, not static: a static Godot object would outlive the engine at quit.
    private readonly Gradient _fadeOut = new()
    {
        Offsets = [0f, 0.6f, 1f],
        Colors = [Colors.White, Colors.White, new Color(1f, 1f, 1f, 0f)]
    };

    private IReadOnlyDictionary<string, EffectDef> _defs = new Dictionary<string, EffectDef>();
    private readonly Dictionary<string, (Texture2D Texture, Material? Material)> _looks = [];
    private readonly HashSet<string> _unknownWarned = [];
    private readonly Stack<CpuParticles2D> _free = new();
    private readonly List<Burst> _bursts = [];
    private readonly Dictionary<int, Attachment> _attached = [];
    private int _nextId = 1;
    private int _nodes;
    private double _clock;
    private EffectsQuality _quality = EffectsQuality.Alti;

    /// <summary>World pixels per cell (view margin of <see cref="EffectRules.ViewMarginCells"/> cells).</summary>
    public int TileSize { get; set; } = 64;

    /// <summary>Effects loaded from the data file, by name.</summary>
    public IReadOnlyDictionary<string, EffectDef> Definitions => _defs;

    /// <summary>Particles alive or reserved by running emitters (bursts, attached and fading ones).</summary>
    public int ActiveParticles { get; private set; }

    /// <summary>Emitter nodes in use (out of the pool).</summary>
    public int Emitters => _nodes - _free.Count;

    /// <summary>Emitter nodes created so far (in use plus pooled): flat once the pool is warm.</summary>
    public int PooledNodes => _nodes;

    /// <summary>Continuous emitters attached (with or without particles right now).</summary>
    public int Attachments => _attached.Count;

    /// <summary>Seconds since the manager started (real time): visuals rate-limit their bursts on it.</summary>
    public double Clock => _clock;

    /// <summary>Continuous emitters attached to <paramref name="owner"/> (0 once it is gone: no orphans).</summary>
    public int AttachmentsOf(Node2D owner)
    {
        var n = 0;
        foreach (var a in _attached.Values)
        {
            if (ReferenceEquals(a.Owner, owner))
            {
                n++;
            }
        }

        return n;
    }

    /// <summary>
    /// Quality drawn (the setting, or Spenti in a playtest). Lowering it stops what it no longer allows at once;
    /// attached emitters restart with the new particle count.
    /// </summary>
    public EffectsQuality Quality
    {
        get => _quality;
        set
        {
            if (_quality == value)
            {
                return;
            }

            _quality = value;
            if (value == EffectsQuality.Spenti)
            {
                for (var i = _bursts.Count - 1; i >= 0; i--)
                {
                    Release(_bursts[i].Node, _bursts[i].Particles);
                }

                _bursts.Clear();
            }

            foreach (var a in _attached.Values)
            {
                StopAttached(a, fade: false);
            }
        }
    }

    public override void _Ready()
    {
        // Above buildings and their child layers (≤ BuildingsZ + 6 = 14), below construction (20).
        ZIndex = 15;
        _defs = LoadDefinitions();
    }

    private static IReadOnlyDictionary<string, EffectDef> LoadDefinitions()
    {
        try
        {
            var text = global::Godot.FileAccess.GetFileAsString(DataPath);
            if (string.IsNullOrEmpty(text))
            {
                var error = global::Godot.FileAccess.GetOpenError();
                GD.PushError($"Effetti: {DataPath} mancante o vuoto ({error}): nessun effetto.");
                return new Dictionary<string, EffectDef>();
            }

            return EffectRules.Parse(text);
        }
        catch (InvalidDataException ex)
        {
            GD.PushError($"Effetti: {ex.Message} Nessun effetto.");
            return new Dictionary<string, EffectDef>();
        }
    }

    /// <summary>
    /// One burst of <paramref name="name"/> at <paramref name="worldPos"/>, emitted East turned by
    /// <paramref name="rotation"/>; with <paramref name="halfArea"/> (world pixels) the particles start anywhere in
    /// that rectangle around the point (a whole block crumbling, snow over a few cells) instead of at the point.
    /// </summary>
    public void Play(string name, Vector2 worldPos, float rotation = 0, Vector2 halfArea = default)
    {
        if (Lookup(name) is not { } def)
        {
            return;
        }

        var wanted = EffectRules.ParticlesFor(def, _quality);
        if (wanted == 0 || !ViewRect().HasPoint(worldPos))
        {
            return;
        }

        var count = EffectRules.Grant(wanted, ActiveParticles, _quality);
        if (count == 0 || Rent() is not { } node)
        {
            return;
        }

        Configure(node, def, count, oneShot: true, halfArea);
        node.GlobalPosition = worldPos;
        node.Rotation = rotation;
        node.Restart();
        ActiveParticles += count;
        _bursts.Add(new Burst(node, count, _clock + Fade(def)));
    }

    /// <summary>
    /// Continuous emitter of <paramref name="name"/> at <paramref name="owner"/> plus <paramref name="offset"/> (world
    /// pixels). It emits only while the quality allows it and the point is in view, and goes away by itself when the
    /// owner leaves the tree. Returns the id for <see cref="Detach"/> (an unknown name gives an id that never emits).
    /// </summary>
    public int Attach(string name, Node2D owner, Vector2 offset)
    {
        var id = _nextId++;
        _attached[id] = new Attachment(Lookup(name), owner, offset);
        return id;
    }

    /// <summary>
    /// A block's working emitter: attached (if <paramref name="id"/> is not live) while <paramref name="on"/>,
    /// detached as soon as it is not. Returns the id to keep (0 = none); called every frame with the block's state.
    /// </summary>
    public int Keep(int id, bool on, string name, Node2D owner, Vector2 offset)
    {
        if (on)
        {
            return id != 0 && _attached.ContainsKey(id) ? id : Attach(name, owner, offset);
        }

        Detach(id);
        return 0;
    }

    /// <summary>
    /// Detaches <paramref name="id"/> from <paramref name="fx"/> if the manager is still alive (a visual leaving the
    /// tree: removed block or rebuilt visuals) and clears it.
    /// </summary>
    public static void DetachFrom(EffectManager? fx, ref int id)
    {
        if (id != 0 && fx is not null && IsInstanceValid(fx) && fx.IsInsideTree())
        {
            fx.Detach(id);
        }

        id = 0;
    }

    /// <summary>Stops the emitter <paramref name="id"/>: its particles fade, then the node goes back to the pool.</summary>
    public void Detach(int id)
    {
        if (_attached.Remove(id, out var a))
        {
            StopAttached(a, fade: true);
        }
    }

    /// <summary>Drops every effect at once (new game, back to Home): all nodes back to the pool.</summary>
    public void Clear()
    {
        foreach (var b in _bursts)
        {
            Release(b.Node, b.Particles);
        }

        _bursts.Clear();
        foreach (var a in _attached.Values)
        {
            StopAttached(a, fade: false);
        }

        _attached.Clear();
    }

    public override void _Process(double delta)
    {
        _clock += delta;
        for (var i = _bursts.Count - 1; i >= 0; i--)
        {
            if (_clock >= _bursts[i].EndsAt)
            {
                Release(_bursts[i].Node, _bursts[i].Particles);
                _bursts.RemoveAt(i);
            }
        }

        if (_attached.Count == 0)
        {
            return;
        }

        var view = ViewRect();
        List<int>? orphans = null;
        foreach (var (id, a) in _attached)
        {
            if (!IsInstanceValid(a.Owner) || !a.Owner.IsInsideTree())
            {
                (orphans ??= []).Add(id);
                continue;
            }

            var at = a.Owner.GlobalPosition + a.Offset;
            var wanted = a.Def is null ? 0 : EffectRules.ParticlesFor(a.Def, _quality);
            if (wanted == 0 || !view.HasPoint(at))
            {
                StopAttached(a, fade: true);
                continue;
            }

            if (a.Node is null)
            {
                var count = EffectRules.Grant(wanted, ActiveParticles, _quality);
                if (count == 0 || Rent() is not { } node)
                {
                    continue;
                }

                Configure(node, a.Def!, count, oneShot: false, default);
                node.GlobalPosition = at;
                node.Rotation = 0;
                node.Restart();
                a.Node = node;
                a.Particles = count;
                ActiveParticles += count;
            }

            a.Node.GlobalPosition = at;
        }

        if (orphans is not null)
        {
            foreach (var id in orphans)
            {
                Detach(id);
            }
        }
    }

    private EffectDef? Lookup(string name)
    {
        if (_defs.TryGetValue(name, out var def))
        {
            return def;
        }

        if (_unknownWarned.Add(name))
        {
            GD.PushWarning($"Effetti: effetto sconosciuto «{name}» (non è in {DataPath}).");
        }

        return null;
    }

    /// <summary>The camera's view in world pixels, grown by the margin.</summary>
    private Rect2 ViewRect()
    {
        var vp = GetViewport();
        var view = vp.GetCanvasTransform().AffineInverse() * vp.GetVisibleRect();
        return view.Grow(EffectRules.ViewMarginCells * TileSize);
    }

    /// <summary>Seconds a node keeps drawing after its last emission (longest particle life).</summary>
    private static double Fade(EffectDef def) => def.Lifetime + 0.1;

    /// <summary>Stops an attached emitter; with <paramref name="fade"/> its particles finish as a burst would.</summary>
    private void StopAttached(Attachment a, bool fade)
    {
        if (a.Node is null)
        {
            return;
        }

        if (fade && a.Def is not null)
        {
            a.Node.Emitting = false;
            _bursts.Add(new Burst(a.Node, a.Particles, _clock + Fade(a.Def)));
        }
        else
        {
            Release(a.Node, a.Particles);
        }

        a.Node = null;
        a.Particles = 0;
    }

    private CpuParticles2D? Rent()
    {
        if (_free.TryPop(out var node))
        {
            node.Visible = true;
            return node;
        }

        if (_nodes >= MaxEmitters)
        {
            return null;
        }

        node = new CpuParticles2D
        {
            Name = $"Emettitore{_nodes}",
            LocalCoords = false,
            Emitting = false,
            Direction = Vector2.Right,
            ColorRamp = _fadeOut,
            ScaleAmountMin = PixelScale,
            ScaleAmountMax = PixelScale,
            LifetimeRandomness = 0.2,
            AnimOffsetMax = 1f
        };
        AddChild(node);
        _nodes++;
        return node;
    }

    private void Release(CpuParticles2D node, int particles)
    {
        node.Emitting = false;
        node.Visible = false;
        _free.Push(node);
        ActiveParticles -= particles;
    }

    private void Configure(CpuParticles2D node, EffectDef def, int count, bool oneShot, Vector2 halfArea)
    {
        if (halfArea == Vector2.Zero)
        {
            node.EmissionShape = CpuParticles2D.EmissionShapeEnum.Point;
        }
        else
        {
            node.EmissionShape = CpuParticles2D.EmissionShapeEnum.Rectangle;
            node.EmissionRectExtents = halfArea;
        }

        var look = Look(def.Sprite);
        node.Texture = look.Texture;
        node.Material = look.Material;
        node.Amount = count;
        node.Lifetime = def.Lifetime;
        node.OneShot = oneShot;
        node.Explosiveness = oneShot ? 1f : 0f;
        node.Spread = def.Spread;
        node.InitialVelocityMin = def.SpeedMin;
        node.InitialVelocityMax = def.SpeedMax;
        node.Gravity = new Vector2(def.GravityX, def.GravityY);
        node.Color = new Color(def.R, def.G, def.B, def.A);
    }

    /// <summary>Texture of a particle sprite; a strip of square variants gets a material that picks one per particle.</summary>
    private (Texture2D Texture, Material? Material) Look(string sprite)
    {
        if (_looks.TryGetValue(sprite, out var look))
        {
            return look;
        }

        var texture = Sprites.For(sprite);
        var frames = texture.GetHeight() > 0 ? texture.GetWidth() / texture.GetHeight() : 1;
        Material? material = frames > 1
            ? new CanvasItemMaterial
            {
                ParticlesAnimation = true,
                ParticlesAnimHFrames = frames,
                ParticlesAnimVFrames = 1,
                ParticlesAnimLoop = false
            }
            : null;
        look = (texture, material);
        _looks[sprite] = look;
        return look;
    }

    private sealed record Burst(CpuParticles2D Node, int Particles, double EndsAt);

    private sealed class Attachment(EffectDef? def, Node2D owner, Vector2 offset)
    {
        public EffectDef? Def { get; } = def;
        public Node2D Owner { get; } = owner;
        public Vector2 Offset { get; } = offset;
        public CpuParticles2D? Node { get; set; }
        public int Particles { get; set; }
    }
}
