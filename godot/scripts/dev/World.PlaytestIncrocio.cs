using Godot;
using TIndustry.Shared;

namespace TIndustry.Godot;

/// <summary>
/// Belt blending probes of the <c>ui</c> playtest: a T merge (two side feeders, nothing behind) and the auto-junction
/// of a belt drag (Mindustry): a real left-drag across a perpendicular belt, first with the junction locked (the
/// belt stays, one toast says why), then unlocked (the drone takes the belt apart and builds a junction).
/// </summary>
public partial class World
{
    /// <summary>T merge at <paramref name="at"/> (the corners probe's free anchor): belts from West and East into a North line.</summary>
    private async Task PlaytestTMergeShotAsync(Camera2D cam, Func<string, Task> shot, Factory factory, GridPosition at)
    {
        BuildSpec Belt(int dx, int dy, Direction dir) =>
            new(BuildSpec.DefaultConveyorId, null, new GridPosition(at.X + dx, at.Y + dy), dir);
        BuildSpec[] layout =
        [
            Belt(3, 1, Direction.North), Belt(3, 0, Direction.North), Belt(3, -1, Direction.North),
            Belt(1, 1, Direction.East), Belt(2, 1, Direction.East), Belt(4, 1, Direction.West), Belt(5, 1, Direction.West)
        ];
        foreach (var spec in layout)
        {
            factory.PlaceInstant(spec, charge: false);
        }

        var merge = new GridPosition(at.X + 3, at.Y + 1);
        GD.Print($"PLAYTEST unione a T: {factory.GetBeltShape(merge)} in {merge}");
        _visualDirty = true;
        cam.Position = new Vector2((at.X + 3.5f) * TileSize, (at.Y + 0.5f) * TileSize);
        cam.Zoom = new Vector2(1.6f, 1.6f);
        await PlaytestWait(0.3);
        await shot("nastro-a-t");

        foreach (var spec in layout)
        {
            factory.RemoveInstant(spec.Origin, refund: false);
        }

        _visualDirty = true;
        await PlaytestWait(0.1);
    }

    private async Task<bool> PlaytestAutoJunctionAsync(Func<string, Task> shot)
    {
        if (_view is not Factory factory || _view.Terrain is not { } terrain || GetNodeOrNull<Camera2D>("Camera") is not { } cam)
        {
            return Fail("incrocio automatico: partita o camera assente");
        }

        // A free 7×3 patch near the Core: a South line in its middle column, drags along its top and bottom rows.
        bool Free(GridPosition a)
        {
            for (var y = a.Y - 1; y <= a.Y + 1; y++)
            {
                for (var x = a.X; x <= a.X + 6; x++)
                {
                    var cell = new GridPosition(x, y);
                    if (!terrain.InBounds(cell) || terrain[cell].Deposit != DepositKind.None || factory.IsPlannedTile(cell)
                        || !factory.CanPlace(new BuildSpec(BuildSpec.DefaultConveyorId, null, cell, Direction.East), out _))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        var core = new GridPosition(_view.CoreTiles.Min(t => t.X), _view.CoreTiles.Min(t => t.Y));
        GridPosition? anchor = null;
        for (var dy = 4; dy < 40 && anchor is null; dy++)
        {
            foreach (var y in new[] { core.Y + dy, core.Y - dy })
            {
                if (Free(new GridPosition(core.X - 3, y)))
                {
                    anchor = new GridPosition(core.X - 3, y);
                    break;
                }
            }
        }

        if (anchor is not { } at)
        {
            return Fail("incrocio automatico: nessuno spazio libero");
        }

        var column = at.X + 3;
        for (var y = at.Y - 1; y <= at.Y + 1; y++)
        {
            factory.PlaceInstant(new BuildSpec(BuildSpec.DefaultConveyorId, null, new GridPosition(column, y), Direction.South), charge: false);
        }

        _visualDirty = true;
        cam.Position = new Vector2((at.X + 3.5f) * TileSize, (at.Y + 0.5f) * TileSize);
        cam.Zoom = new Vector2(1.6f, 1.6f);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

        var (tool, dir) = (_tool, _placeDir);
        (_tool, _placeDir) = (BuildTool.Belt, Direction.East);
        try
        {
            async Task Drag(int row) =>
                await PlaytestDrag(MouseButton.Left, CellToScreen(new GridPosition(at.X, row)), CellToScreen(new GridPosition(at.X + 6, row)));

            var lockedCell = new GridPosition(column, at.Y - 1);
            if (!factory.IsStructureUnlocked("junction"))
            {
                await Drag(lockedCell.Y);
                if (!await PlaytestWaitUntil(() => PendingCommands.Count == 0, StepTimeout))
                {
                    return Fail("incrocio bloccato: comandi del trascinamento non applicati");
                }

                var expected = Testi.Rifiuto(RefusalCode.CrossingNeedsJunction);
                if (_hud?.ToastText != expected
                    || !factory.Belts.TryGet(lockedCell, out var kept) || kept.Kind != LogisticsKind.Belt || kept.Direction != Direction.South)
                {
                    return Fail($"incrocio bloccato: toast «{_hud?.ToastText}» invece di «{expected}» o nastro cambiato in {lockedCell}");
                }

                GD.Print("PLAYTEST step ok: incrocio bloccato, il nastro resta e il toast lo dice");
                factory.Research.ForceUnlock("junction");
            }

            var crossing = new GridPosition(column, at.Y + 1);
            await Drag(crossing.Y);
            if (!await PlaytestWaitUntil(
                    () => factory.Belts.TryGet(crossing, out var cell) && cell.Kind == LogisticsKind.Junction && LocalQueueEmpty(),
                    BeltDragTimeout))
            {
                return Fail($"incrocio automatico: nessun incrocio costruito in {crossing}");
            }
        }
        finally
        {
            (_tool, _placeDir) = (tool, dir);
            ClearToCursor(toast: false);
        }

        await PlaytestWaitUntil(DroneSettled, DroneSettleTimeout);
        await shot("incrocio-automatico");
        GD.Print("PLAYTEST step ok: trascinamento sopra un nastro perpendicolare = incrocio");
        return true;
    }
}
