using Godot;
using TIndustry.Shared;

namespace TIndustry.Godot;

/// <summary>
/// Effects probe of the <c>ui</c> playtest (G3), only with <c>TINDUSTRY_PLAYTEST_EFFETTI=1</c> (effects Alti): captures
/// of the effects for review (<c>fx-*.png</c>, never compared: particles are random), the working blocks' emitters
/// against their state, no orphan emitter once a block is taken down, and node and particle counts flat between 10 s
/// and 60 s of the 200 belts + 100 buildings layout, with the average FPS.
/// </summary>
public partial class World
{
    /// <summary>Most a count may move between 10 s and 60 s of the probe (5 %).</summary>
    private const double EffectsDriftLimit = 0.05;

    /// <summary>Most the emitter pool may grow between 10 s and 60 s of the probe, once warmed up (3 %: the ambient bursts peak at
    /// random, measured +1 to +3 on 147; a leak of one emitter every few seconds goes well past it).</summary>
    private const double PoolGrowthLimit = 0.03;

    private async Task<bool> PlaytestEffectsAsync(string dir)
    {
        if (_effects is not { Quality: EffectsQuality.Alti } fx || _view is not Factory factory
            || GetNodeOrNull<WorldCamera>("Camera") is not { } cam)
        {
            return Fail($"effetti: qualità {_effects?.Quality} invece di Alti con {ClientSettings.PlaytestEffectsEnv}=1");
        }

        async Task Shot(string label)
        {
            await PlaytestTerrainReady();
            SaveViewportPng(Path.Combine(dir, $"fx-{label}.png"));
        }

        // The perf layout (200 belts, 100 working buildings) and its framing: laid by the PERF probe already, or here
        // after the site captures (it would fill the free cells near the Core).
        var perfLaid = OS.GetEnvironment(PlaytestPerfEnv) == "1";
        var framing = (cam.Position, cam.Zoom);

        if (!await PlaytestSiteEffectsAsync(factory, cam, Shot))
        {
            return false;
        }

        if (!perfLaid)
        {
            await PlaytestBeltPerfAsync();
            framing = (cam.Position, cam.Zoom);
        }

        await PlaytestWorkShotsAsync(cam, Shot);
        await PlaytestAmbientShotsAsync(factory, cam, Shot);
        (cam.Position, cam.Zoom) = framing;

        if (await PlaytestEffectsStableAsync(factory, fx) is { } drift)
        {
            return Fail(drift);
        }

        return await PlaytestEmittersFollowBlocksAsync(factory, fx)
            && await PlaytestRebuildBurstAsync(factory, fx)
            && await PlaytestEffectsOffFreesPoolAsync(factory, fx)
            && await PlaytestLightsAsync(dir, factory, cam);
    }

    /// <summary>Working blocks of the perf layout (and the rest of the game): one continuous emitter each.</summary>
    private static int WorkingBlocks(Factory factory) => factory.Miners.Count(m => m.Efficiency > 0f)
        + factory.Smelters.Count(s => s.IsCrafting) + factory.Assemblers.Count(a => a.IsCrafting)
        + factory.Generators.Count(g => g.IsGenerating);

    /// <summary>
    /// Five rebuilds of the building visuals within a second (a drone finishing a row of blocks): the running
    /// emitters move to the new visuals, so the nodes in use and the pool stay where they were, no emitter is refused
    /// and every working block keeps exactly one emitter, frame after frame.
    /// </summary>
    private async Task<bool> PlaytestRebuildBurstAsync(Factory factory, EffectManager fx)
    {
        var baseline = 0;
        var end = Time.GetTicksMsec() + 1000;
        while (Time.GetTicksMsec() < end)
        {
            KeepPerfLayoutWorking(factory);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            baseline = Math.Max(baseline, fx.Emitters);
        }

        var pool = fx.PooledNodes;
        var failures = fx.RentFailures;
        var peak = 0;
        for (var rebuild = 0; rebuild < 5; rebuild++)
        {
            _buildingsDirty = true;
            end = Time.GetTicksMsec() + 200;
            while (Time.GetTicksMsec() < end)
            {
                KeepPerfLayoutWorking(factory);
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                peak = Math.Max(peak, fx.Emitters);
                if (fx.Attachments != WorkingBlocks(factory))
                {
                    return Fail($"effetti: ricostruzione {rebuild + 1}: {fx.Attachments} emettitori legati per {WorkingBlocks(factory)} blocchi al lavoro");
                }
            }
        }

        // Margin: ambient, Core dust and extractor bursts come and go while the blocks' emitters stay.
        const int margin = 16;
        GD.Print($"PLAYTEST effetti: 5 ricostruzioni in 1 s, emettitori in uso {baseline} → massimo {peak}, pool {pool} → {fx.PooledNodes}, "
            + $"rifiutati {fx.RentFailures - failures}");
        if (fx.RentFailures != failures || peak > baseline + margin || fx.PooledNodes > pool + margin)
        {
            return Fail($"effetti: le ricostruzioni consumano il pool (in uso {baseline} → {peak}, pool {pool} → {fx.PooledNodes}, rifiutati {fx.RentFailures - failures})");
        }

        return true;
    }

    /// <summary>Effetti Spenti while playing frees every particle node at once; back to Alti, the working blocks emit again.</summary>
    private async Task<bool> PlaytestEffectsOffFreesPoolAsync(Factory factory, EffectManager fx)
    {
        fx.Quality = EffectsQuality.Spenti;
        if (fx.GetChildCount() != 0 || fx.PooledNodes != 0 || fx.ActiveParticles != 0)
        {
            return Fail($"effetti: con Spenti restano {fx.GetChildCount()} nodi ({fx.PooledNodes} nel pool, {fx.ActiveParticles} particelle)");
        }

        fx.Quality = EffectsQuality.Alti;
        KeepPerfLayoutWorking(factory);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        if (fx.Emitters == 0 || fx.Attachments != WorkingBlocks(factory))
        {
            return Fail($"effetti: di nuovo Alti, {fx.Emitters} emettitori e {fx.Attachments} legati per {WorkingBlocks(factory)} blocchi al lavoro");
        }

        GD.Print($"PLAYTEST effetti: Spenti → 0 nodi; di nuovo Alti → {fx.Emitters} emettitori");
        return true;
    }

    /// <summary>A basic miner (unlocked from the start) built by the drone near the Core: the rich beam at work, the flash when it is done, the crumble when it is taken down.</summary>
    private async Task<bool> PlaytestSiteEffectsAsync(Factory factory, WorldCamera cam, Func<string, Task> shot)
    {
        var core = new GridPosition(factory.CoreTiles.Min(t => t.X), factory.CoreTiles.Min(t => t.Y));
        GridPosition? spot = null;
        for (var r = 3; r < 12 && spot is null; r++)
        {
            foreach (var at in new[] { new GridPosition(core.X + r, core.Y - 3), new GridPosition(core.X - r, core.Y - 4), new GridPosition(core.X + r, core.Y + 3) })
            {
                if (factory.CanPlace(new BuildSpec(Miner.BasicId, BuildingKind.Miner, at, Direction.East), out _))
                {
                    spot = at;
                    break;
                }
            }
        }

        if (spot is not { } origin)
        {
            return Fail("effetti: nessun posto libero per il minatore vicino al Core");
        }

        cam.Position = FootprintCenter(origin, Miner.Size);
        cam.Zoom = new Vector2(1.6f, 1.6f);
        var finished = ConstructFinishedCount;
        SubmitCommand(new PlaceBuilding(LocalPlayer, BuildingKind.Miner, origin, Direction.East));
        if (!await PlaytestWaitUntil(() => _view!.Builders.Count > 0 && _view.Builders[0].CurrentSiteId is not null, 20))
        {
            return Fail("effetti: il drone non lavora al cantiere del minatore");
        }

        await PlaytestWait(0.5);
        await shot("cantiere");
        if (!await PlaytestWaitUntil(() => ConstructFinishedCount > finished, 30))
        {
            return Fail("effetti: cantiere del minatore non finito");
        }

        // The flash lasts 0.3 s: two frames after the event.
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await shot("cantiere-finito");

        SubmitCommand(new Remove(LocalPlayer, origin));
        if (!await PlaytestWaitUntil(() => _view!.Miners.Count(m => m.Position.Equals(origin)) == 0 && LocalQueueEmpty(), 30))
        {
            return Fail("effetti: smontaggio del minatore non finito");
        }

        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await shot("smontaggio");
        return true;
    }

    /// <summary>Close-ups of a working smelter, miner, generator and assembler of the perf layout.</summary>
    private async Task PlaytestWorkShotsAsync(WorldCamera cam, Func<string, Task> shot)
    {
        async Task Frame(Vector2 at, string label)
        {
            KeepPerfLayoutWorking((Factory)_view!);
            cam.Position = at;
            cam.Zoom = new Vector2(1.6f, 1.6f);
            await PlaytestWait(1.2);
            await shot(label);
        }

        if (_view!.Smelters.FirstOrDefault(s => s.IsCrafting) is { } smelter)
        {
            await Frame(FootprintCenter(smelter.Position, Crafter.Size), "forno");
        }

        if (_view.Miners.FirstOrDefault(m => m.Efficiency > 0f) is { } miner)
        {
            await Frame(FootprintCenter(miner.Position, Miner.Size), "minatore");
        }

        if (_view.Generators.FirstOrDefault(g => g.IsGenerating) is { } gen)
        {
            await Frame(FootprintCenter(gen.Position, Generator.Size), "generatore");
        }

        if (_view.Assemblers.FirstOrDefault(a => a.IsCrafting) is { } asm)
        {
            await Frame(FootprintCenter(asm.Position, Crafter.Size), "assemblatore");
        }
    }

    /// <summary>For each biome with ambient particles, the first place of the map mostly of that biome, at zoom 1 (<c>fx-ambiente-&lt;bioma&gt;</c>).</summary>
    private async Task PlaytestAmbientShotsAsync(Factory factory, WorldCamera cam, Func<string, Task> shot)
    {
        foreach (var biome in new[] { "neve", "sabbia", "prati" })
        {
            if (BiomeSpot(factory, biome) is not { } at)
            {
                GD.Print($"PLAYTEST effetti: nessuna zona di {biome} per la cattura dell'ambiente");
                continue;
            }

            cam.Position = at;
            cam.Zoom = Vector2.One;
            cam.ClampToMap();
            await PlaytestWait(3.0);
            await shot($"ambiente-{biome}");
        }
    }

    /// <summary>
    /// 60 s on the perf layout with every block kept working: node count and particles at 10 s and at 60 s (particles
    /// as the average of 2-10 s and 50-60 s: the ambient bursts come and go at random) must stay within <see cref="EffectsDriftLimit"/>; prints them with the
    /// average and minimum FPS (VSync off); the emitter pool must not grow past <see cref="PoolGrowthLimit"/> after the
    /// first 10 s. Returns the problem, or null.
    /// </summary>
    private async Task<string?> PlaytestEffectsStableAsync(Factory factory, EffectManager fx)
    {
        var vsync = DisplayServer.WindowGetVsyncMode();
        DisplayServer.WindowSetVsyncMode(DisplayServer.VSyncMode.Disabled);
        var start = Time.GetTicksUsec();
        var nextFeed = 0.0;
        double Seconds() => (Time.GetTicksUsec() - start) / 1e6;
        var particleSum = new double[2];
        var particleFrames = new int[2];
        void PerFrame()
        {
            var t = Seconds();
            if (t >= nextFeed)
            {
                nextFeed = t + 0.5;
                KeepPerfLayoutWorking(factory);
            }

            var window = t is >= 2 and < 10 ? 0 : t >= 50 ? 1 : -1;
            if (window >= 0)
            {
                particleSum[window] += fx.ActiveParticles;
                particleFrames[window]++;
            }
        }

        var first = await MeasureFramesAsync(10.0, PerFrame);
        var nodes10 = Performance.GetMonitor(Performance.Monitor.ObjectNodeCount);
        var pool10 = fx.PooledNodes;
        var second = await MeasureFramesAsync(50.0, PerFrame);
        var nodes60 = Performance.GetMonitor(Performance.Monitor.ObjectNodeCount);
        var pool60 = fx.PooledNodes;
        DisplayServer.WindowSetVsyncMode(vsync);

        var particles10 = particleSum[0] / Math.Max(1, particleFrames[0]);
        var particles60 = particleSum[1] / Math.Max(1, particleFrames[1]);
        var average = (first.Average * 10 + second.Average * 50) / 60;
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        GD.Print(string.Create(inv,
            $"PLAYTEST effetti: 60 s con effetti Alti, fps medio {average:F0}, minimo {Math.Min(first.Minimum, second.Minimum):F0}; "
            + $"nodi {nodes10:F0} a 10 s, {nodes60:F0} a 60 s; particelle {particles10:F0} a 10 s, {particles60:F0} a 60 s; "
            + $"emettitori nel pool {pool10} → {pool60}, legati {fx.Attachments}"));

        static bool Drifts(double a, double b) => Math.Abs(b - a) > EffectsDriftLimit * Math.Max(1, a);
        if (Drifts(nodes10, nodes60))
        {
            return $"effetti: nodi non stabili ({nodes10:F0} → {nodes60:F0})";
        }

        if (particles10 <= 0 || Drifts(particles10, particles60))
        {
            return string.Create(inv, $"effetti: particelle non stabili ({particles10:F0} → {particles60:F0})");
        }

        if (pool60 > pool10 * (1 + PoolGrowthLimit))
        {
            return $"effetti: il pool di emettitori cresce dopo il riscaldamento ({pool10} → {pool60}, oltre il 3 %)";
        }

        return pool60 > EffectManager.MaxEmitters ? $"effetti: {pool60} emettitori oltre il limite" : null;
    }

    /// <summary>Tops up the perf layout so every block keeps working: inputs, fuel and room for products of the crafters, fuel of the generators.</summary>
    private static void KeepPerfLayoutWorking(Factory factory)
    {
        foreach (var gen in factory.Generators)
        {
            gen.RestoreFuel(Generator.FuelBufferCapacity, Generator.SecondsPerFuel);
        }

        foreach (var crafter in factory.Smelters.Concat(factory.Assemblers))
        {
            // The packed grid has nowhere to put the products: empty the output queue, or the block stops.
            if (crafter.OutputQueue.Count > 0)
            {
                crafter.RestoreCraftState(crafter.Progress, crafter.IsCrafting, crafter.InputBuffer, outputs: null,
                    crafter.EjectIndex, crafter.ItemsCrafted, crafter.FuelBuffer, crafter.BurnRemaining);
            }

            crafter.SeedFuel();
            foreach (var input in crafter.Recipe.Inputs)
            {
                for (var i = 0; i < 100 && crafter.TryAccept(input.ItemId); i++)
                {
                }
            }
        }
    }

    /// <summary>
    /// One continuous emitter per working block and none for idle ones; a smelter taken down leaves no emitter
    /// behind once its visual is gone.
    /// </summary>
    private async Task<bool> PlaytestEmittersFollowBlocksAsync(Factory factory, EffectManager fx)
    {
        int Working() => WorkingBlocks(factory);

        KeepPerfLayoutWorking(factory);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        // Checked before the next World._Process: the last sync saw exactly this state.
        if (fx.Attachments != Working())
        {
            return Fail($"effetti: {fx.Attachments} emettitori legati per {Working()} blocchi al lavoro");
        }

        var visual = _buildingsLayer!.GetChildren().OfType<SmelterVisual>()
            .FirstOrDefault(v => factory.Smelters.Any(s => s.IsCrafting && s.Position.Equals(v.Origin)));
        if (visual is null || fx.AttachmentsOf(visual) != 1)
        {
            return Fail("effetti: nessun forno al lavoro col suo emettitore");
        }

        if (!factory.RemoveInstant(visual.Origin, refund: false))
        {
            return Fail("effetti: forno non smontato");
        }

        _buildingsDirty = true;
        for (var i = 0; i < 3; i++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }

        if (fx.AttachmentsOf(visual) != 0 || fx.Attachments != Working())
        {
            return Fail($"effetti: forno smontato con {fx.AttachmentsOf(visual)} emettitori; {fx.Attachments} legati per {Working()} blocchi al lavoro");
        }

        GD.Print($"PLAYTEST effetti: forno smontato, 0 emettitori rimasti; {fx.Attachments} legati per {Working()} blocchi al lavoro");
        return true;
    }
}
