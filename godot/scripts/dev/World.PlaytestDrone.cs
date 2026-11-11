using Godot;
using TIndustry.Shared;

namespace TIndustry.Godot;

/// <summary>
/// The «il drone sei tu» controls in the <c>ui</c> playtest: W held flies the drone and sends its input only twice (press
/// and release), an input held by someone else (the agents' <c>muovi</c>) is not overwritten, the camera follows the
/// drone, a middle drag takes the view away and the release brings it back, and the left button held on a deposit with
/// no block selected mines it by hand until it goes up. The drone is flown back to where it was afterwards.
/// </summary>
public partial class World
{
    private async Task<bool> PlaytestDroneAndCameraAsync(Func<string, Task> shot)
    {
        if (_host is not { } host || LocalDrone is null || GetNodeOrNull<WorldCamera>("Camera") is not { } cam)
        {
            return Fail("drone: partita, drone o telecamera assenti");
        }

        DeselectBlock(toast: false);
        var home = new GridPosition((int)MathF.Floor(LocalDrone.X), (int)MathF.Floor(LocalDrone.Y));
        var inputs = 0;
        void Count(SimCommand command)
        {
            if (command is SetDroneInput { PlayerId: LocalPlayer })
            {
                inputs++;
            }
        }

        host.CommandApplied += Count;
        _playtestCameraFollows = true;
        try
        {
            // W held: the drone flies North; the input goes out once on the press and once on the release.
            var before = (LocalDrone.X, LocalDrone.Y);
            await PlaytestHoldKey(Key.W, 0.4);
            await PlaytestWait(0.2);
            var drone = LocalDrone!;
            if (before.Y - drone.Y < 2f || Math.Abs(before.X - drone.X) > 0.01f || (drone.InputX, drone.InputY) != (0, 0)
                || inputs != 2)
            {
                return Fail($"drone W: da {before} a ({drone.X:0.##}, {drone.Y:0.##}), ingresso ({drone.InputX}, {drone.InputY}), "
                    + $"{inputs} SetDroneInput (attesi 2)");
            }

            // The camera has caught up with the drone (soft follow).
            await PlaytestWait(0.6);
            if (DroneWorldPosition() is not { } at || cam.Position.DistanceTo(at) > TileSize)
            {
                return Fail($"visuale: {cam.Position} lontana dal drone {DroneWorldPosition()}");
            }

            await shot("drone-visuale");

            // An input held by someone else (the agents' muovi) is left alone while no key changes.
            SubmitCommand(new SetDroneInput(LocalPlayer, 1, 0));
            await PlaytestWait(0.3);
            if ((LocalDrone!.InputX, LocalDrone.InputY) != (1, 0))
            {
                return Fail($"drone: l'ingresso tenuto da un comando è stato sovrascritto ({LocalDrone.InputX}, {LocalDrone.InputY})");
            }

            SubmitCommand(new SetDroneInput(LocalPlayer, 0, 0));
            await PlaytestWait(0.3);

            // Middle drag: the view leaves the drone while held, and goes back to it after the release.
            var mid = ViewportToWindow(GetViewport().GetVisibleRect().Size / 2);
            var followed = cam.Position;
            await PlaytestPress(mid, MouseButton.Middle);
            await PlaytestMoveHeld(mid, mid + new Vector2(-300, -180), mask: MouseButtonMask.Middle);
            var dragged = cam.Position;
            await PlaytestRelease(mid + new Vector2(-300, -180), MouseButton.Middle);
            await PlaytestWait(0.8);
            if (dragged.DistanceTo(followed) < TileSize || DroneWorldPosition() is not { } back
                || cam.Position.DistanceTo(back) > TileSize)
            {
                return Fail($"visuale col centrale: trascinata a {dragged} (da {followed}), dopo il rilascio {cam.Position}, drone {DroneWorldPosition()}");
            }

            GD.Print($"PLAYTEST step ok: drone col tasto W ({inputs} SetDroneInput), visuale che lo segue, centrale che la sposta e la riporta");

            // Left button held on a free deposit in range (no block selected): the drone mines it until the release.
            if (PlaytestMinableCell() is not { } ore)
            {
                return Fail("scavo: nessun giacimento scavabile nel raggio del drone");
            }

            _playtestCameraFollows = false;
            cam.Position = CellCenter(ore);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await PlaytestPress(CellToScreen(ore));
            await PlaytestWait(0.3);
            var mining = LocalDrone!.MiningTile;
            await shot("drone-scavo");
            await PlaytestRelease(CellToScreen(ore));
            await PlaytestWait(0.1);
            if (mining != ore || LocalDrone!.MiningTile is not null)
            {
                return Fail($"scavo: cella {ore}, scavo tenuto {mining?.ToString() ?? "nessuno"}, dopo il rilascio {LocalDrone!.MiningTile?.ToString() ?? "nessuno"}");
            }

            GD.Print($"PLAYTEST step ok: clic tenuto su un giacimento ({ore}) scava a mano, il rilascio smette");
            return true;
        }
        finally
        {
            host.CommandApplied -= Count;
            _playtestCameraFollows = false;
            PlaytestFlyDrone(home);
            FocusCameraOnCore();
        }
    }

    /// <summary>
    /// Task 5 probe: a plan placed beyond the drone's range is drawn out of range (<see cref="ConstructionLayer.OutOfRangePlans"/>),
    /// and the hold in the HUD (<see cref="Hud.HoldShown"/>) follows the drone's hold: shown while it mines a deposit far
    /// from the Core, hidden again once the drone is back and the hold has gone into the Core. Failures are PushErrors.
    /// </summary>
    private async Task<bool> PlaytestOutOfRangeAndHoldAsync(Func<string, Task> shot)
    {
        if (_view is null || _hud is null || _constructionLayer is null || LocalDrone is not { } start || _view.CoreTiles.Count == 0)
        {
            return Fail("fuori raggio/stiva: partita, HUD o drone assenti");
        }

        DeselectBlock(toast: false);
        var home = new GridPosition((int)MathF.Floor(start.X), (int)MathF.Floor(start.Y));
        var unit = _view.EffectiveUnit;

        // A belt plan 35 cells from the drone (range 27.5): it waits, drawn out of range.
        GridPosition? far = null;
        foreach (var (dx, dy) in new[] { (35, 0), (-35, 0), (0, 35), (0, -35), (25, 25), (-25, -25), (25, -25), (-25, 25) })
        {
            var cell = new GridPosition(home.X + dx, home.Y + dy);
            if (cell.X >= 0 && cell.Y >= 0 && cell.X < ActiveMapWidth && cell.Y < ActiveMapHeight
                && _view.CanOccupy(cell) && !_view.IsPlannedTile(cell))
            {
                far = cell;
                break;
            }
        }

        if (far is not { } planned)
        {
            return Fail("fuori raggio: nessuna cella libera a 35 celle dal drone");
        }

        var outBefore = _constructionLayer.OutOfRangePlans;
        SubmitCommand(new PlaceBelt(LocalPlayer, planned, Direction.East, BuildSpec.DefaultConveyorId));
        if (!await PlaytestWaitUntil(() => _view!.IsPlannedTile(planned) && _constructionLayer.OutOfRangePlans > outBefore,
                StepTimeout))
        {
            return Fail($"fuori raggio: piano in {planned} non disegnato fuori raggio ({_constructionLayer.OutOfRangePlans} fuori raggio)");
        }

        if (GetNodeOrNull<Camera2D>("Camera") is { } cam)
        {
            cam.Position = (CellCenter(planned) + CellCenter(home)) / 2f;
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }

        await shot("piano-fuori-raggio");
        TryRemoveAt(planned);
        if (!await PlaytestWaitUntil(() => !_view!.IsPlannedTile(planned), StepTimeout))
        {
            return Fail($"fuori raggio: piano in {planned} non tolto");
        }

        // The hold: a free deposit the drone digs, more than 8 cells from the Core's edge (no unloading there).
        if (PlaytestDepositAwayFromCore(unit) is not { } ore)
        {
            return Fail("stiva: nessun giacimento libero lontano dal CORE");
        }

        PlaytestFlyDrone(ore);
        SubmitCommand(new SetMining(LocalPlayer, ore));
        if (!await PlaytestWaitUntil(() => LocalDrone is { HoldCount: >= 1 }, StepTimeout))
        {
            return Fail($"stiva: lo scavo di {ore} non riempie la stiva ({LocalDrone?.HoldCount})");
        }

        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        var drone = LocalDrone!;
        var shown = _hud.HoldShown;
        if (shown.ItemId != drone.HoldItemId || shown.Count != drone.HoldCount || shown.Capacity != unit.HoldCapacity)
        {
            return Fail($"stiva nell'HUD: {shown} invece di ({drone.HoldItemId}, {drone.HoldCount}, {unit.HoldCapacity})");
        }

        if (GetNodeOrNull<Camera2D>("Camera") is { } mineCam)
        {
            mineCam.Position = CellCenter(ore);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }

        await shot("drone-stiva-scavo");
        SubmitCommand(new SetMining(LocalPlayer, null));
        PlaytestFlyDrone(home);
        if (!await PlaytestWaitUntil(() => LocalDrone is { HoldCount: 0 } && _hud.HoldShown.ItemId is null, StepTimeout))
        {
            return Fail($"stiva: non svuotata vicino al CORE (stiva {LocalDrone?.HoldCount}, HUD {_hud.HoldShown})");
        }

        FocusCameraOnCore();
        GD.Print($"PLAYTEST step ok: piano fuori raggio disegnato come tale, stiva nell'HUD piena di {shown.Count} e vuota dopo lo scarico");
        return true;
    }

    /// <summary>A free deposit the tier's drone mines, 9 to 20 cells from the Core's bounding box, nearest first; or null.</summary>
    private GridPosition? PlaytestDepositAwayFromCore(UnitDefinition unit)
    {
        if (_view?.Terrain is not { } terrain)
        {
            return null;
        }

        var minX = _view.CoreTiles.Min(t => t.X);
        var minY = _view.CoreTiles.Min(t => t.Y);
        var maxX = _view.CoreTiles.Max(t => t.X);
        var maxY = _view.CoreTiles.Max(t => t.Y);
        for (var r = 9; r <= 20; r++)
        {
            for (var y = minY - r; y <= maxY + r; y++)
            {
                for (var x = minX - r; x <= maxX + r; x++)
                {
                    if (Math.Max(Math.Max(minX - x, x - maxX), Math.Max(minY - y, y - maxY)) != r)
                    {
                        continue;
                    }

                    var cell = new GridPosition(x, y);
                    if (terrain.InBounds(cell) && terrain[cell].Deposit is var deposit and not DepositKind.None
                        && unit.CanMine(TerrainMap.ItemIdForDeposit(deposit)) && _view.CanOccupy(cell) && !_view.IsPlannedTile(cell))
                    {
                        return cell;
                    }
                }
            }
        }

        return null;
    }

    /// <summary>The free deposit cell the drone could start mining now nearest to it, or null.</summary>
    private GridPosition? PlaytestMinableCell()
    {
        if (_view?.Terrain is not { } terrain || LocalDrone is not { } drone)
        {
            return null;
        }

        var centre = new GridPosition((int)MathF.Floor(drone.X), (int)MathF.Floor(drone.Y));
        for (var r = 1; r <= 20; r++)
        {
            for (var dy = -r; dy <= r; dy++)
            {
                for (var dx = -r; dx <= r; dx++)
                {
                    if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != r)
                    {
                        continue;
                    }

                    var cell = new GridPosition(centre.X + dx, centre.Y + dy);
                    if (terrain.InBounds(cell) && terrain[cell].Deposit != DepositKind.None
                        && _view.MiningRefusal(LocalPlayer, cell) is null)
                    {
                        return cell;
                    }
                }
            }
        }

        return null;
    }
}
