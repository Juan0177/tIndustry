using Godot;
using TIndustry.Shared;

namespace TIndustry.Godot;

/// <summary>
/// Belt probes of the <c>ui</c> playtest. <c>oggetti-fluidi</c>: an item on a straight basic belt line, drawn at up to
/// 144 frames per second between the last two ticks, must move on screen every frame, never back, at an even speed,
/// across at least two cell handoffs (where the simulation holds it one tick); it must stand exactly still while the
/// simulation is held, and the belt strip must change frame 32 times per cell travelled. The line is taken away
/// afterwards and the belts go back to their first frame.
/// </summary>
public partial class World
{
    // At 144 fps on a basic belt (0.5 cells/s) about 1.4 cells: from 0.85 of the first cell (0.7 plus the settling wait) it crosses two handoffs.
    private const int SmoothItemsFrames = 400;
    private const int SmoothItemsHeldFrames = 30;
    private const int SmoothItemsLineLength = 8;

    private async Task<bool> PlaytestSmoothItemsAsync(Func<string, Task> shot)
    {
        if (_view is not Factory factory || _view.Terrain is not { } terrain || _beltVisual is null
            || GetNodeOrNull<Camera2D>("Camera") is not { } cam)
        {
            return Fail("oggetti-fluidi: partita o nastri assenti");
        }

        const string beltId = BuildSpec.DefaultConveyorId;
        bool Free(GridPosition at)
        {
            for (var x = at.X; x < at.X + SmoothItemsLineLength; x++)
            {
                var cell = new GridPosition(x, at.Y);
                if (!terrain.InBounds(cell) || terrain[cell].Deposit != DepositKind.None || factory.IsPlannedTile(cell)
                    || !factory.CanPlace(new BuildSpec(beltId, null, cell, Direction.East), out _))
                {
                    return false;
                }
            }

            return true;
        }

        var core = new GridPosition(_view.CoreTiles.Min(t => t.X), _view.CoreTiles.Min(t => t.Y));
        GridPosition? found = null;
        for (var dy = 4; dy < 40 && found is null; dy++)
        {
            foreach (var y in new[] { core.Y - dy, core.Y + dy })
            {
                if (Free(new GridPosition(core.X - 3, y)))
                {
                    found = new GridPosition(core.X - 3, y);
                    break;
                }
            }
        }

        if (found is not { } start)
        {
            return Fail("oggetti-fluidi: nessuna riga libera vicino al Core");
        }

        var cells = Enumerable.Range(0, SmoothItemsLineLength).Select(i => new GridPosition(start.X + i, start.Y)).ToArray();
        foreach (var cell in cells)
        {
            factory.PlaceInstant(new BuildSpec(beltId, null, cell, Direction.East), charge: false);
        }

        const long itemId = 910_000L;
        var probe = new TransportedItem(itemId, "iron-ore");
        if (!factory.Belts.TryInsert(start, probe))
        {
            return Fail($"oggetti-fluidi: oggetto non inserito in {start}");
        }

        probe.Progress = 0.7f; // an insert starts at 0: begin near the cell edge so the run crosses two handoffs

        _visualDirty = true;
        cam.Position = CellCenter(new GridPosition(start.X + SmoothItemsLineLength / 2, start.Y));
        cam.Zoom = new Vector2(1.6f, 1.6f);
        var (maxFps, vsync) = (Engine.MaxFps, DisplayServer.WindowGetVsyncMode());
        Engine.MaxFps = 144;
        DisplayServer.WindowSetVsyncMode(DisplayServer.VSyncMode.Disabled);
        MindustryBeltVisual.PlaytestScrolls = true;
        try
        {
            var tickPixels = _view.Content.RequireConveyor(beltId).RateItemsPerSecond * TileSize / SimulationClock.TickRate;
            return await MeasureSmoothItemsAsync(itemId, beltId, cells[^1], tickPixels, shot);
        }
        finally
        {
            MindustryBeltVisual.PlaytestScrolls = false;
            Engine.MaxFps = maxFps;
            DisplayServer.WindowSetVsyncMode(vsync);
            PlaytestHeld = false;
            foreach (var cell in cells)
            {
                factory.RemoveInstant(cell, refund: false);
            }

            _beltVisual?.PlaytestRewind();
            _visualDirty = true;
            FocusCameraOnCore();
            await PlaytestWait(0.1);
        }
    }

    private async Task<bool> MeasureSmoothItemsAsync(
        long itemId, string beltId, GridPosition last, float tickPixels, Func<string, Task> shot)
    {
        // process_frame comes before the nodes' _Process: a sample shows the previous frame, whose delta we kept.
        async Task<(Vector2? Position, double Delta, int Strip)> Sample()
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            var pos = _itemSprites.TryGetValue(itemId, out var sprite) && _view!.Belts.TryGetValue(last, out var end)
                      && !end.Items.Any(i => i.Id == itemId)
                ? sprite.Position
                : (Vector2?)null;
            return (pos, GetProcessDeltaTime(), _beltVisual!.FrameOf(beltId));
        }

        if (_beltVisual!.FramesPerCell(beltId) is var perCell and not 32)
        {
            return Fail($"oggetti-fluidi: {perCell} fotogrammi per cella nella striscia di {beltId} invece di 32");
        }

        await PlaytestWait(0.3); // the sprite exists and has a tick behind it
        var previous = await Sample();
        if (previous.Position is null)
        {
            return Fail("oggetti-fluidi: sprite dell'oggetto assente");
        }

        var speeds = new List<double>();
        var travelled = 0f;
        var stripChanges = 0;
        var handoffs = 0;
        var handoffFrames = 0;
        // Item drawn at Lerp(center(cell), center(next), progress): its cell is floor(x / tile - 0.5) on this East line,
        // and it is handed to the next cell at that cell's center. The 0.01 px (rounding, below) puts a position an ulp
        // short of a center on the next cell, so a -0.002 px step at a handoff counts as standing still there.
        static int CellOf(float x) => Mathf.FloorToInt((x + 0.01f) / TileSize - 0.5f);
        // The simulation holds an item one tick at each handoff (progress 1 on one cell, then 0 on the next: BeltLane
        // drops the overflow), drawn faithfully as one tick standing still. Frames within that tick after a handoff center
        // may stand still and are left out of the speed ratio; everywhere else the item must move every frame.
        bool NearHandoff(float from, float to)
        {
            var center = (CellOf(to) + 0.5f) * TileSize;
            return from <= center + tickPixels + 0.01f && to >= center - 0.01f;
        }
        for (var i = 0; i < SmoothItemsFrames; i++)
        {
            var now = await Sample();
            if (now.Strip != previous.Strip)
            {
                stripChanges++;
            }

            if (now.Position is not { } at || previous.Position is not { } before)
            {
                break; // left the straight run (the last cell, where it stops)
            }

            var step = at.X - before.X;
            var nearHandoff = NearHandoff(before.X, at.X);
            // 0.01 px: float rounding of world coordinates near 32768 px (a cell's end and the next cell's start differ by
            // an ulp), so a step within it counts as standing still, never as going back.
            const float rounding = 0.01f;
            if (step < -rounding || (step <= rounding && !nearHandoff) || Math.Abs(at.Y - before.Y) > rounding)
            {
                return Fail($"oggetti-fluidi: frame {i}: spostamento {at - before} (da {before} a {at})");
            }

            travelled += step;
            if (CellOf(at.X) != CellOf(before.X))
            {
                handoffs++;
            }

            if (nearHandoff)
            {
                handoffFrames++;
            }
            else
            {
                speeds.Add(step / previous.Delta);
            }

            previous = now;
        }

        await shot("oggetti-fluidi");
        var cellsTravelled = travelled / TileSize;
        var ratio = speeds.Max() / speeds.Min();
        GD.Print($"PLAYTEST oggetti fluidi: {speeds.Count} frame, {cellsTravelled:0.00} celle, velocità {speeds.Min():0.0}–{speeds.Max():0.0} px/s "
                 + $"(rapporto {ratio:0.00}), {handoffs} passaggi di cella ({handoffFrames} frame attorno), striscia cambiata {stripChanges} volte, {Engine.GetFramesPerSecond():0} fps");
        if (speeds.Count < 120 || ratio >= 1.5 || handoffs < 2)
        {
            return Fail($"oggetti-fluidi: {speeds.Count} frame misurati, rapporto fra spostamenti {ratio:0.00}, {handoffs} passaggi di cella");
        }

        if (Math.Abs(stripChanges - 32 * cellsTravelled) > 2)
        {
            return Fail($"oggetti-fluidi: la striscia è cambiata {stripChanges} volte su {cellsTravelled:0.00} celle (attese {32 * cellsTravelled:0})");
        }

        // Held simulation: the clock does not run, so the sub-tick fraction and the item stay put.
        PlaytestHeld = true;
        var held = await Sample();
        for (var i = 0; i < SmoothItemsHeldFrames; i++)
        {
            if ((await Sample()).Position != held.Position)
            {
                return Fail($"oggetti-fluidi: l'oggetto si muove in pausa (frame {i})");
            }
        }

        return true;
    }
}
