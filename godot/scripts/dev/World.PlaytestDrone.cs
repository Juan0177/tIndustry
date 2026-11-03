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
