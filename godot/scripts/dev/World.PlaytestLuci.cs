using Godot;
using TIndustry.Shared;

namespace TIndustry.Godot;

/// <summary>
/// Lights probe of the <c>ui</c> playtest (G3 3/4), part of the effects probe (<c>TINDUSTRY_PLAYTEST_EFFETTI=1</c>):
/// shadows follow "Ombre" (no shadow node when off), the glow is on where the world is drawn (also at 50 % render
/// scale), the night darkens the world but never the HUD, lights stay within the view and the cap, lamps light up
/// when powered; captures <c>luci-*.png</c> for review (never compared). Without effects, <see cref="PlaytestLooksOff"/>
/// checks that a playtest draws no shadow, no glow and no night.
/// </summary>
public partial class World
{
    /// <summary>Most the mean brightness of a snow view may grow with the glow on (0..1): bloom must not wash it out.</summary>
    private const double SnowGlowLimit = 0.03;

    /// <summary>Shadow layers under the building visuals.</summary>
    private int ShadowNodes() => _buildingsLayer is null
        ? 0
        : _buildingsLayer.GetChildren().Sum(block => block.GetChildren().Count(c => c.Name == BuildingPad.ShadowName));

    /// <summary>Usual playtest (effects forced off): no shadow, no glow, no HDR, no night, so captures stay as before.</summary>
    private string? PlaytestLooksOff()
    {
        if (ShadowNodes() != 0)
        {
            return $"{ShadowNodes()} ombre in un playtest senza effetti";
        }

        if (GlowActive || GetTree().Root.UseHdr2D)
        {
            return "bagliore o HDR 2D attivi in un playtest senza effetti";
        }

        return _night?.Night == true || _night?.DarknessNode is { Visible: true } ? "notte attiva in un playtest senza effetti" : null;
    }

    private async Task<bool> PlaytestLightsAsync(string dir, Factory factory, WorldCamera cam)
    {
        async Task Shot(string label)
        {
            await PlaytestTerrainReady();
            await PlaytestWait(0.3);
            SaveViewportPng(Path.Combine(dir, $"luci-{label}.png"));
        }

        async Task Frames(int n)
        {
            for (var i = 0; i < n; i++)
            {
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            }
        }

        if (_night is null)
        {
            return Fail("luci: strato della notte mancante");
        }

        var framing = (cam.Position, cam.Zoom);
        var saved = _settings.Clone();
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        try
        {
            // Shadows: one per block with Ombre, none without (the visuals are rebuilt by the setting).
            _settings.Ombre = false;
            ApplyGraphicsSettings();
            await Frames(2);
            if (ShadowNodes() != 0)
            {
                return Fail($"luci: Ombre spente ma {ShadowNodes()} nodi ombra");
            }

            _settings.Ombre = true;
            _settings.Bagliore = true;
            _settings.ScalaRendering = 100;
            ApplyGraphicsSettings();
            await Frames(2);
            var blocks = _buildingsLayer!.GetChildCount();
            if (ShadowNodes() != blocks)
            {
                return Fail($"luci: {ShadowNodes()} ombre per {blocks} blocchi");
            }

            if (!GlowActive || !GetTree().Root.UseHdr2D)
            {
                return Fail("luci: Bagliore attivo ma ambiente o HDR 2D mancanti");
            }

            // Day: shadows and glow on a working smelter and generator of the perf layout.
            Crafter? hot = null;
            for (var i = 0; i < 120 && hot is null; i++)
            {
                KeepPerfLayoutWorking(factory);
                await Frames(1);
                hot = factory.Smelters.FirstOrDefault(s => s.IsCrafting);
            }

            if (hot is null || !factory.Generators.Any(g => g.IsGenerating))
            {
                return Fail("luci: nessun forno al lavoro nel layout");
            }

            cam.Position = FootprintCenter(hot.Position, Crafter.Size);
            cam.Zoom = Vector2.One;
            await PlaytestWait(0.8);
            await Shot("giorno-ombre-bagliore");
            var hudDay = await HudSampleAsync();

            // Night without lamps: the darkness is on the world canvas only, the HUD is untouched.
            factory.State.Rules = factory.State.Rules with { Night = true };
            KeepPerfLayoutWorking(factory);
            await Frames(3);
            if (_night.DarknessNode is not { Visible: true } darkness || !_night.Night)
            {
                return Fail("luci: notte attiva ma CanvasModulate assente");
            }

            if (GetNode<CanvasLayer>("Hud").GetCanvas() == darkness.GetCanvas())
            {
                return Fail("luci: il buio è sulla stessa tela dell'HUD");
            }

            await Shot("notte-senza-lampade");
            var hudNight = await HudSampleAsync();
            // White within the rounding of the sRGB conversion (1/255 or so); the night would make it 0.34-0.54.
            static bool White(Color c) => c.R > 0.98f && c.G > 0.98f && c.B > 0.98f;
            if (!White(hudDay) || !White(hudNight))
            {
                return Fail($"luci: un quadrato bianco dell'HUD si legge {hudDay} di giorno e {hudNight} di notte");
            }

            // Lamps around the working generators (instant, free: playtest only); powered, they light up.
            var lamps = PlaceLampsAroundGenerators(factory, 16);
            _buildingsDirty = true; // placed on the factory directly: no command event rebuilds the visuals
            KeepPerfLayoutWorking(factory);
            await Frames(3);
            var lit = factory.Lamps.Count(l => l.IsLit);
            var litVisuals = _buildingsLayer!.GetChildren().OfType<LampVisual>()
                .Count(v => v.GetNode<Sprite2D>("Glow").Visible);
            if (lamps == 0 || lit == 0 || litVisuals != lit)
            {
                return Fail($"luci: {lamps} lampade posate, {lit} accese, {litVisuals} disegnate accese");
            }

            var lamp = factory.Lamps.First(l => l.IsLit);
            cam.Position = FootprintCenter(lamp.Position, Lamp.Size);
            cam.Zoom = Vector2.One;
            KeepPerfLayoutWorking(factory);
            await PlaytestWait(0.8);
            await Shot("notte-lampade");

            // Cap and view: zoomed out over the whole layout every light is in view (plus its radius), at most 128.
            cam.Zoom = new Vector2(0.25f, 0.25f);
            KeepPerfLayoutWorking(factory);
            await Frames(3);
            var (count, outside) = LightsInView();
            if (count > NightLayer.MaxLights || outside > 0)
            {
                return Fail($"luci: {count} luci accese ({outside} fuori dalla vista), limite {NightLayer.MaxLights}");
            }

            var fps = await NightFpsAsync(factory, 10.0);
            GD.Print(string.Create(inv,
                $"PLAYTEST luci: {blocks} ombre; notte con {lamps} lampade ({lit} accese), {count} luci a zoom 0.25 "
                + $"(nodi luce {_night.LightNodes}), fps medio {fps.Average:F0}, minimo {fps.Minimum:F0}"));
            await Shot("notte-panoramica");

            // Fireflies over the meadows at night.
            if (BiomeSpot(factory, "prati") is { } meadow)
            {
                cam.Position = meadow;
                cam.Zoom = Vector2.One;
                cam.ClampToMap();
                await PlaytestWait(4.0);
                await Shot("notte-lucciole");
            }

            // 50 % render scale: the reduced viewport draws the night and gets the glow (once: the root has none).
            _settings.ScalaRendering = 50;
            ApplyGraphicsSettings();
            cam.Position = FootprintCenter(lamp.Position, Lamp.Size);
            cam.Zoom = Vector2.One;
            KeepPerfLayoutWorking(factory);
            await Frames(3);
            if (!GlowActive || _renderScale?.Target is not { UseHdr2D: true } || _glowNode?.Environment is not null)
            {
                return Fail("luci: alla scala 50% il bagliore non è sulla vista ridotta");
            }

            KeepPerfLayoutWorking(factory);
            await PlaytestWait(0.5);
            await Shot("notte-scala-50");
            _settings.ScalaRendering = 100;
            ApplyGraphicsSettings();

            // Day again: snow with and without the glow, the mean brightness must barely move.
            factory.State.Rules = factory.State.Rules with { Night = false };
            await Frames(2);
            if (_night.DarknessNode is { Visible: true } || _night.ActiveLights != 0)
            {
                return Fail("luci: di giorno restano buio o luci");
            }

            if (BiomeSpot(factory, "neve") is { } snow)
            {
                cam.Position = snow;
                cam.Zoom = Vector2.One;
                cam.ClampToMap();
                await PlaytestWait(0.5);
                await Shot("neve-bagliore");
                var withGlow = MeanBrightness();
                _settings.Bagliore = false;
                ApplyGraphicsSettings();
                await PlaytestWait(0.5);
                await PlaytestTerrainReady();
                var withoutGlow = MeanBrightness();
                GD.Print(string.Create(inv, $"PLAYTEST luci: neve, luminosità media {withoutGlow:F3} senza bagliore, {withGlow:F3} con"));
                if (withGlow - withoutGlow > SnowGlowLimit)
                {
                    return Fail(string.Create(inv, $"luci: il bagliore sbianca la neve ({withoutGlow:F3} → {withGlow:F3})"));
                }
            }

            return true;
        }
        finally
        {
            factory.State.Rules = factory.State.Rules with { Night = false };
            _settings.CopyFrom(saved);
            ApplyGraphicsSettings();
            (cam.Position, cam.Zoom) = framing;
        }
    }

    /// <summary>Up to <paramref name="max"/> lamps on free cells next to the burning generators; returns how many.</summary>
    private static int PlaceLampsAroundGenerators(Factory factory, int max)
    {
        var placed = 0;
        foreach (var gen in factory.Generators.Where(g => g.IsGenerating).ToList())
        {
            foreach (var (cell, _) in BuildingIo.PerimeterSlots(gen.Position, Generator.Size))
            {
                if (placed >= max)
                {
                    return placed;
                }

                if (factory.CanOccupy(cell)
                    && factory.PlaceInstant(new BuildSpec(Lamp.BuildingId, BuildingKind.Lamp, cell, Direction.East), charge: false))
                {
                    placed++;
                    break; // one per generator: the light spreads over the layout
                }
            }
        }

        return placed;
    }

    /// <summary>Lights drawn and how many of them sit outside the view grown by the largest light radius.</summary>
    private (int Count, int Outside) LightsInView()
    {
        GetVisibleTileRange(out var minX, out var minY, out var maxX, out var maxY);
        var view = new Rect2(minX * TileSize, minY * TileSize, (maxX - minX + 1) * TileSize, (maxY - minY + 1) * TileSize)
            .Grow(7 * TileSize);
        var count = 0;
        var outside = 0;
        foreach (var light in _night!.VisibleLights)
        {
            count++;
            outside += view.HasPoint(light.Position) ? 0 : 1;
        }

        return (count, outside);
    }

    /// <summary>FPS over <paramref name="seconds"/> at night with VSync off, the layout kept working.</summary>
    private async Task<(double Average, double Minimum)> NightFpsAsync(Factory factory, double seconds)
    {
        var vsync = DisplayServer.WindowGetVsyncMode();
        DisplayServer.WindowSetVsyncMode(DisplayServer.VSyncMode.Disabled);
        var next = 0UL;
        var result = await MeasureFramesAsync(seconds, () =>
        {
            if (Time.GetTicksMsec() >= next)
            {
                next = Time.GetTicksMsec() + 500;
                KeepPerfLayoutWorking(factory);
            }
        });
        DisplayServer.WindowSetVsyncMode(vsync);
        return (result.Average, result.Minimum);
    }

    /// <summary>
    /// An opaque white square put on the HUD layer for one frame, read back from the drawn frame: white unless something
    /// (the night, the glow) reaches the HUD. Removed before the next capture.
    /// </summary>
    private async Task<Color> HudSampleAsync()
    {
        var probe = new ColorRect
        {
            Name = "ProvaHud",
            Color = Colors.White,
            Position = new Vector2(400, 300),
            Size = new Vector2(24, 24),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        GetNode<CanvasLayer>("Hud").AddChild(probe);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        var at = GetTree().Root.GetFinalTransform() * probe.GetGlobalTransformWithCanvas() * (probe.Size * 0.5f);
        Color colour;
        using (var image = CaptureFrame())
        {
            colour = image.GetPixel(Math.Clamp((int)at.X, 0, image.GetWidth() - 1), Math.Clamp((int)at.Y, 0, image.GetHeight() - 1));
        }

        probe.QueueFree();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        return colour;
    }

    /// <summary>Mean brightness (0..1) of the frame just drawn.</summary>
    private double MeanBrightness()
    {
        using var image = CaptureFrame();
        double sum = 0;
        var n = 0;
        for (var y = 0; y < image.GetHeight(); y += 4)
        {
            for (var x = 0; x < image.GetWidth(); x += 4)
            {
                var c = image.GetPixel(x, y);
                sum += 0.299 * c.R + 0.587 * c.G + 0.114 * c.B;
                n++;
            }
        }

        return sum / Math.Max(1, n);
    }

    /// <summary>
    /// Centre (world pixels) of the first 20×12 window of the map that is mostly <paramref name="biome"/> (50 of 60
    /// samples), or null.
    /// </summary>
    private Vector2? BiomeSpot(Factory factory, string biome)
    {
        if (factory.Terrain is not { } map)
        {
            return null;
        }

        var table = factory.Content.Terrain;
        string? BiomeAt(int x, int y) => map[x, y].Wall == WallKind.None ? table.Floor(map[x, y].Floor)?.Biome : null;
        for (var y0 = 0; y0 + 12 <= map.Height; y0 += 6)
        {
            for (var x0 = 0; x0 + 20 <= map.Width; x0 += 6)
            {
                var hits = 0;
                for (var y = y0; y < y0 + 12; y += 2)
                {
                    for (var x = x0; x < x0 + 20; x += 2)
                    {
                        hits += BiomeAt(x, y) == biome ? 1 : 0;
                    }
                }

                if (hits >= 50)
                {
                    return new Vector2(x0 + 10, y0 + 6) * TileSize;
                }
            }
        }

        return null;
    }
}
