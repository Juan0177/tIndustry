using Godot;
using TIndustry.Shared;

namespace TIndustry.Godot;

/// <summary>
/// Belt blending probes of the <c>ui</c> playtest: a T merge (two side feeders, nothing behind), side joins (fed from
/// behind and from the sides) and the auto-junction
/// of a belt drag (Mindustry): a real left-drag across a perpendicular belt, first with the junction locked (the
/// crossed belt turns in place and its line merges into the new one, no toast), then unlocked (the drone takes the
/// belt apart and builds a junction). Everything
/// the probe built, the money and plates it spent and the research it unlocked are put back afterwards, so the later
/// captures see the same game.
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

        // Side joins on the middle row: fed from behind and both sides (the T opened behind), then from behind and West.
        BuildSpec[] joins =
        [
            Belt(3, 1, Direction.North), Belt(3, 0, Direction.North), Belt(3, -1, Direction.North),
            Belt(1, 0, Direction.East), Belt(2, 0, Direction.East), Belt(4, 0, Direction.West), Belt(5, 0, Direction.West)
        ];
        foreach (var spec in joins)
        {
            factory.PlaceInstant(spec, charge: false);
        }

        var join = new GridPosition(at.X + 3, at.Y);
        GD.Print($"PLAYTEST unione dai tre lati: {factory.GetBeltShape(join)} in {join}");
        _visualDirty = true;
        await PlaytestWait(0.3);
        await shot("nastro-tre-lati");

        factory.RemoveInstant(joins[5].Origin, refund: false);
        factory.RemoveInstant(joins[6].Origin, refund: false);
        GD.Print($"PLAYTEST unione laterale: {factory.GetBeltShape(join)} in {join}");
        _visualDirty = true;
        await PlaytestWait(0.3);
        await shot("nastro-unione-laterale");

        foreach (var spec in joins[..5])
        {
            factory.RemoveInstant(spec.Origin, refund: false);
        }

        _visualDirty = true;
        await PlaytestWait(0.1);
    }

    /// <summary>
    /// Belts blend with every building that exchanges items with them (Mindustry's blend by capability): a belt line
    /// running East under an assembler, a generator, an extractor, a smelter and a miner shows the dot block beside
    /// each, and a belt heading North beside the miner with nothing behind is a curve from it. Taken away afterwards.
    /// </summary>
    private async Task PlaytestBuildingsBesideBeltAsync(
        Camera2D cam, Func<string, Task> shot, Factory factory, TerrainMap terrain, GridPosition core)
    {
        // Cells x -1..10, y 0..2 from the anchor: buildings on rows 0-1, the belt line on row 2.
        bool Free(GridPosition a)
        {
            for (var y = a.Y; y <= a.Y + 2; y++)
            {
                for (var x = a.X - 1; x <= a.X + 10; x++)
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

        GridPosition? anchor = null;
        for (var dy = 4; dy < 40 && anchor is null; dy++)
        {
            foreach (var y in new[] { core.Y + dy, core.Y - dy })
            {
                if (Free(new GridPosition(core.X - 5, y)))
                {
                    anchor = new GridPosition(core.X - 5, y);
                    break;
                }
            }
        }

        if (anchor is not { } at)
        {
            GD.Print("PLAYTEST nastro accanto agli edifici: nessuno spazio libero");
            return;
        }

        GridPosition At(int dx, int dy) => new(at.X + dx, at.Y + dy);
        var layout = new List<BuildSpec>
        {
            new(Crafter.AssemblerBuildingId, BuildingKind.Assembler, At(0, 0), Direction.East),
            new(Generator.BuildingId, BuildingKind.Generator, At(2, 0), Direction.East),
            new(Extractor.BuildingId, BuildingKind.Extractor, At(4, 1), Direction.North),
            new(Crafter.SmelterBuildingId, BuildingKind.Smelter, At(5, 0), Direction.East),
            new(Miner.BasicId, BuildingKind.Miner, At(7, 0), Direction.East),
            new(BuildSpec.DefaultConveyorId, null, At(9, 1), Direction.North)
        };
        for (var dx = -1; dx <= 9; dx++)
        {
            layout.Add(new BuildSpec(BuildSpec.DefaultConveyorId, null, At(dx, 2), Direction.East));
        }

        foreach (var spec in layout)
        {
            factory.PlaceInstant(spec, charge: false);
        }

        GD.Print($"PLAYTEST nastro accanto agli edifici: {string.Join(", ", Enumerable.Range(0, 9).Select(dx => factory.GetBeltShape(At(dx, 2))))}; "
            + $"accanto al minatore {factory.GetBeltShape(At(9, 1))}");
        _visualDirty = true;
        _buildingsDirty = true;
        cam.Position = new Vector2((at.X + 4.5f) * TileSize, (at.Y + 1.5f) * TileSize);
        cam.Zoom = new Vector2(1.6f, 1.6f);
        await PlaytestWait(0.3);
        await shot("nastro-accanto-edifici");

        foreach (var spec in layout)
        {
            factory.RemoveInstant(spec.Origin, refund: false);
        }

        _visualDirty = true;
        _buildingsDirty = true;
        await PlaytestWait(0.1);
    }

    private async Task<bool> PlaytestAutoJunctionAsync(Func<string, Task> shot)
    {
        if (_view is not Factory factory || _view.Terrain is not { } terrain || GetNodeOrNull<Camera2D>("Camera") is not { } cam)
        {
            return Fail("incrocio automatico: partita o camera assente");
        }

        // A free 7×5 patch near the Core: a South line in its middle column, drags along its second and fourth rows
        // (the crossed belt is fed from behind and hands over ahead, as the auto-junction requires).
        bool Free(GridPosition a)
        {
            for (var y = a.Y - 2; y <= a.Y + 2; y++)
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

        var money = factory.Wallet.Money;
        var materials = factory.Wallet.MaterialsSnapshot();
        var wasUnlocked = factory.IsStructureUnlocked("junction");
        var column = at.X + 3;
        for (var y = at.Y - 2; y <= at.Y + 2; y++)
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
                var toast = _hud?.ToastText;
                await Drag(lockedCell.Y);
                if (!await PlaytestWaitUntil(
                        () => PendingCommands.Count == 0 && LocalQueueEmpty()
                            && factory.GetBeltShape(lockedCell) == BeltShape.JoinLeft
                            && factory.Belts.TryGet(lockedCell, out var turned) && turned.Direction == Direction.East,
                        BeltDragTimeout))
                {
                    return Fail($"incrocio bloccato: il nastro in {lockedCell} non è diventato un'unione verso Est");
                }

                if (_hud?.ToastText != toast)
                {
                    return Fail($"incrocio bloccato: toast inatteso «{_hud?.ToastText}»");
                }

                await PlaytestWaitUntil(DroneSettled, DroneSettleTimeout);
                await shot("unione-senza-incrocio");
                GD.Print("PLAYTEST step ok: incrocio bloccato, il nastro attraversato gira e diventa un'unione");
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

        for (var y = at.Y - 2; y <= at.Y + 2; y++)
        {
            for (var x = at.X; x <= at.X + 6; x++)
            {
                factory.RemoveInstant(new GridPosition(x, y), refund: false);
            }
        }

        PlaytestRestoreWallet(factory, money, materials);

        if (!wasUnlocked)
        {
            factory.Research.ForceLock("junction");
        }

        _visualDirty = true;
        _buildingsDirty = true;
        await PlaytestWait(0.1);
        return true;
    }

    /// <summary>Puts the money and the materials a probe spent or received back to <paramref name="money"/> and <paramref name="materials"/>.</summary>
    private static void PlaytestRestoreWallet(Factory factory, int money, IReadOnlyDictionary<string, int> materials)
    {
        factory.Wallet.AddMoney(money - factory.Wallet.Money);
        foreach (var id in materials.Keys.Union(factory.Wallet.MaterialsSnapshot().Keys).Order(StringComparer.Ordinal).ToList())
        {
            var delta = materials.GetValueOrDefault(id) - factory.Wallet.MaterialCount(id);
            if (delta > 0)
            {
                factory.Wallet.AddMaterial(id, delta);
            }
            else if (delta < 0)
            {
                factory.Wallet.TryRemoveMaterial(id, -delta);
            }
        }
    }
}
