using System.Text.Json;
using Godot;
using TIndustry.Shared;

namespace TIndustry.Godot;

/// <summary>
/// Belt probes of the <c>ui</c> playtest. <c>oggetti-fluidi</c>: an item on a straight basic belt line, drawn at up to
/// 144 frames per second between the last two ticks, must move on screen every frame, never back, at an even speed,
/// across at least two cell handoffs (where it keeps its movement: it never stands still); it must stand exactly still while the
/// simulation is held, and the belt strip must change frame 32 times per cell travelled. The line is taken away
/// afterwards and the belts go back to their first frame. The left-drag probes (<see cref="PlaytestLineDragAsync"/>)
/// drive the Mindustry line with real mouse events.
/// </summary>
public partial class World
{
    // At 144 fps on a basic belt (2.1 cells/s) about 5.8 cells: from the first cell it crosses at least four handoffs before the last cell.
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

        _visualDirty = true;
        cam.Position = CellCenter(new GridPosition(start.X + SmoothItemsLineLength / 2, start.Y));
        cam.Zoom = new Vector2(1.6f, 1.6f);
        var (maxFps, vsync) = (Engine.MaxFps, DisplayServer.WindowGetVsyncMode());
        Engine.MaxFps = 144;
        DisplayServer.WindowSetVsyncMode(DisplayServer.VSyncMode.Disabled);
        MindustryBeltVisual.PlaytestScrolls = true;
        try
        {
            return await MeasureSmoothItemsAsync(itemId, beltId, cells[^1], shot);
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
        long itemId, string beltId, GridPosition last, Func<string, Task> shot)
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
        // Item drawn at Lerp(center(cell), center(next), progress): its cell is floor(x / tile - 0.5) on this East line,
        // and it is handed to the next cell at that cell's center, keeping the rest of its tick's movement (BeltCell's
        // carry), so it moves every frame there too. The 0.01 px puts a position an ulp short of a center on the next cell.
        static int CellOf(float x) => Mathf.FloorToInt((x + 0.01f) / TileSize - 0.5f);
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
            // 0.01 px: float rounding of world coordinates near 32768 px (a cell's end and the next cell's start differ by
            // an ulp): a step within it counts as standing still, which fails like going back.
            const float rounding = 0.01f;
            if (step <= rounding || Math.Abs(at.Y - before.Y) > rounding)
            {
                return Fail($"oggetti-fluidi: frame {i}: spostamento {at - before} (da {before} a {at})");
            }

            travelled += step;
            if (CellOf(at.X) != CellOf(before.X))
            {
                handoffs++;
            }

            speeds.Add(step / previous.Delta);

            previous = now;
        }

        await shot("oggetti-fluidi");
        var cellsTravelled = travelled / TileSize;
        var ratio = speeds.Max() / speeds.Min();
        GD.Print($"PLAYTEST oggetti fluidi: {speeds.Count} frame, {cellsTravelled:0.00} celle, velocità {speeds.Min():0.0}–{speeds.Max():0.0} px/s "
                 + $"(rapporto {ratio:0.00}), {handoffs} passaggi di cella, striscia cambiata {stripChanges} volte, {Engine.GetFramesPerSecond():0} fps");
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
    // Free patch of the drag probes (top-left corner and size, in cells) and what reached the simulation meanwhile,
    // without its tick: the client's drag and the agent's piazza compare equal.
    private GridPosition _linePatch;
    private const int LinePatchWidth = 10;
    private const int LinePatchHeight = 8;
    private readonly List<SimCommand> _lineSent = [];

    private GridPosition LineCell(int x, int y) => new(_linePatch.X + x, _linePatch.Y + y);

    private Vector2 LineScreen(int x, int y) => CellToScreen(LineCell(x, y));

    private void RecordLineCommand(SimCommand command) => _lineSent.Add(command with { Tick = 0 });

    private void RecordLineRefusal(SimCommand command, Refusal _) => RecordLineCommand(command);

    private bool LineBeltAt(int x, int y, Direction way, LogisticsKind kind = LogisticsKind.Belt) =>
        _view!.Belts.TryGetValue(LineCell(x, y), out var cell) && cell.Kind == kind && cell.Direction == way;

    private void ClearLinePatch(Factory factory)
    {
        for (var y = 0; y < LinePatchHeight; y++)
        {
            for (var x = 0; x < LinePatchWidth; x++)
            {
                factory.RemoveInstant(LineCell(x, y), refund: false);
            }
        }

        _visualDirty = true;
        _buildingsDirty = true;
    }

    /// <summary>A South belt column on <paramref name="x"/>, rows 2–4: a line along row 3 crosses its middle.</summary>
    private void LineColumn(Factory factory, int x)
    {
        for (var y = 2; y <= 4; y++)
        {
            factory.PlaceInstant(new BuildSpec(BuildSpec.DefaultConveyorId, null, LineCell(x, y), Direction.South), charge: false);
        }

        _visualDirty = true;
    }

    private async Task SettleLine()
    {
        await PlaytestWaitUntil(() => PendingCommands.Count == 0, StepTimeout);
        await PlaytestWait(0.1);
    }

    /// <summary>A whole left drag from cell <paramref name="from"/> to <paramref name="to"/> of the patch, then settled.</summary>
    private async Task LineDrag((int X, int Y) from, (int X, int Y) to, bool ctrl = false)
    {
        _lineSent.Clear();
        await PlaytestPress(LineScreen(from.X, from.Y), ctrl: ctrl);
        await PlaytestMoveHeld(LineScreen(from.X, from.Y), LineScreen(to.X, to.Y), ctrl);
        await PlaytestRelease(LineScreen(to.X, to.Y), ctrl: ctrl);
        await SettleLine();
    }

    /// <summary>
    /// The left drag with real mouse events, instant build on a free patch: the line faces the drag
    /// (<c>nastri-verso-nord</c>), a block faces it too and the brush stays (<c>assemblatore-verso-sud</c>), nothing is
    /// sent while the button is down and Esc drops it (<c>nastri-anteprima</c>), R during the drag fixes the new way
    /// (<c>nastri-r</c>), T, the right button, focus lost and a release outside the window send nothing
    /// (<c>nastri-interrotto</c>), a line the Core cannot pay toasts once (<c>nastri-un-toast</c>), two crossings in a row
    /// send what the agents' <c>piazza</c> sends (<c>nastri-due-incroci</c>), Ctrl lays a path (<c>nastri-ctrl</c>) and a
    /// building in the way gets a bridge (<c>nastri-ponte</c>). Rules, research, money and blocks are put back.
    /// </summary>
    private async Task<bool> PlaytestLineDragAsync(Func<string, Task> shot)
    {
        if (_view is not Factory factory || _view.Terrain is not { } terrain || _host is not { } host
            || GetNodeOrNull<Camera2D>("Camera") is not { } cam)
        {
            return Fail("trascinamento: partita o camera assente");
        }

        bool Free(GridPosition corner)
        {
            for (var y = corner.Y; y < corner.Y + LinePatchHeight; y++)
            {
                for (var x = corner.X; x < corner.X + LinePatchWidth; x++)
                {
                    var cell = new GridPosition(x, y);
                    if (!terrain.InBounds(cell) || terrain[cell].Deposit != DepositKind.None || factory.IsPlannedTile(cell)
                        || CoreStockSink.PreferDirectionIntoCore(cell, factory.CoreTiles) is not null
                        || !factory.CanPlace(new BuildSpec(BuildSpec.DefaultConveyorId, null, cell, Direction.East), out _))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        var (left, top, bottom) = (factory.CoreTiles.Min(t => t.X), factory.CoreTiles.Min(t => t.Y), factory.CoreTiles.Max(t => t.Y));
        GridPosition? found = null;
        for (var k = 0; k < 40 && found is null; k++)
        {
            foreach (var corner in new[] { new GridPosition(left - 3, bottom + 3 + k), new GridPosition(left - 3, top - 2 - LinePatchHeight - k) })
            {
                if (found is null && Free(corner))
                {
                    found = corner;
                }
            }
        }

        if (found is not { } patch)
        {
            return Fail("trascinamento: nessuno spazio libero vicino al Core");
        }

        _linePatch = patch;
        var money = factory.Wallet.Money;
        var materials = factory.Wallet.MaterialsSnapshot();
        var unlocked = factory.Research.UnlockedIds.ToHashSet(StringComparer.Ordinal);
        var rules = factory.State.Rules;
        var (tool, dir) = (_tool, _placeDir);
        foreach (var id in ResearchState.DemoStructureIds)
        {
            factory.Research.ForceUnlock(id);
        }

        factory.EnsureDemoBuildStock();
        factory.State.Rules = rules with { InstantBuild = true };
        host.CommandApplied += RecordLineCommand;
        host.CommandRejected += RecordLineRefusal;
        cam.Position = (CellCenter(LineCell(0, 0)) + CellCenter(LineCell(LinePatchWidth - 1, LinePatchHeight - 1))) / 2;
        cam.Zoom = new Vector2(0.8f, 0.8f);
        await PlaytestWait(0.2);
        try
        {
            return await PlaytestLineProbesAsync(shot, factory, host);
        }
        finally
        {
            host.CommandApplied -= RecordLineCommand;
            host.CommandRejected -= RecordLineRefusal;
            CancelLine();
            ClearLinePatch(factory);
            factory.State.Rules = rules;
            foreach (var id in factory.Research.UnlockedIds.Where(id => !unlocked.Contains(id)).Order(StringComparer.Ordinal).ToList())
            {
                factory.Research.ForceLock(id);
            }

            PlaytestRestoreWallet(factory, money, materials);
            SyncResearchLocks();
            (_tool, _placeDir) = (tool, dir);
            _hud?.SetDirectionLabel(_placeDir);
            ClearToCursor(toast: false);
            FocusCameraOnCore();
            await PlaytestWait(0.1);
        }
    }

    private async Task<bool> PlaytestLineProbesAsync(Func<string, Task> shot, Factory factory, SimHost host)
    {
        var view = _view!;

        // nastri-verso-nord: brush East, a drag North lays belts facing North.
        (_tool, _placeDir) = (BuildTool.Belt, Direction.East);
        // A frame for _Process to see the tool: until then the camera pans on a left press (WorldCamera.LeftDragPans).
        await PlaytestWait(0.1);
        await LineDrag((1, 6), (1, 2));
        if (Enumerable.Range(2, 5).Where(y => !LineBeltAt(1, y, Direction.North)).ToList() is [var wrongNorth, ..])
        {
            return Fail($"nastri-verso-nord: nessun nastro verso Nord in {LineCell(1, wrongNorth)} ({_lineSent.Count} comandi)");
        }

        await shot("nastri-verso-nord");
        ClearLinePatch(factory);

        // assemblatore-verso-sud: one block on the anchor facing the drag; the brush stays East.
        _tool = BuildTool.Assembler;
        await PlaytestWait(0.1);
        await LineDrag((4, 1), (4, 6));
        var assembler = factory.Assemblers.SingleOrDefault(a => a.Position == LineCell(4, 1));
        if (assembler is not { Direction: Direction.South } || _placeDir != Direction.East || _lineSent.Count != 1)
        {
            return Fail($"assemblatore-verso-sud: assemblatore {assembler?.Direction.ToString() ?? "assente"}, pennello {_placeDir}, {_lineSent.Count} comandi");
        }

        await shot("assemblatore-verso-sud");
        ClearLinePatch(factory);

        // nastri-anteprima: while the button is down only the preview; Esc drops the line, the tool stays.
        _tool = BuildTool.Belt;
        await PlaytestWait(0.1);
        var before = (view.Belts.Count, view.PlansOf(LocalPlayer).Count);
        _lineSent.Clear();
        await PlaytestPress(LineScreen(1, 3));
        await PlaytestMoveHeld(LineScreen(1, 3), LineScreen(8, 3));
        await PlaytestWait(0.2);
        if (!LineDragActive || PendingCommands.Count > 0 || _lineSent.Count > 0
            || before != (view.Belts.Count, view.PlansOf(LocalPlayer).Count))
        {
            return Fail($"nastri-anteprima: trascinamento {LineDragActive}, {PendingCommands.Count} in attesa, {_lineSent.Count} inviati prima del rilascio");
        }

        await shot("nastri-anteprima");
        await PlaytestKey(Key.Escape);
        var escDropped = !LineDragActive && _tool == BuildTool.Belt;
        await PlaytestRelease(LineScreen(8, 3));
        await SettleLine();
        if (!escDropped || _lineSent.Count > 0 || before != (view.Belts.Count, view.PlansOf(LocalPlayer).Count))
        {
            return Fail($"nastri-anteprima: Esc non annulla (strumento {_tool}, {_lineSent.Count} comandi inviati)");
        }

        GD.Print("PLAYTEST step ok: nastri-anteprima, nulla parte prima del rilascio; Esc annulla e lo strumento resta");

        // nastri-r: R during the drag turns the brush and fixes it for the whole line.
        _lineSent.Clear();
        await PlaytestPress(LineScreen(1, 4));
        await PlaytestMoveHeld(LineScreen(1, 4), LineScreen(6, 4));
        await PlaytestKey(Key.R);
        var turned = _placeDir;
        await PlaytestRelease(LineScreen(6, 4));
        await SettleLine();
        if (turned == Direction.East || Enumerable.Range(1, 6).Any(x => !LineBeltAt(x, 4, turned)))
        {
            return Fail($"nastri-r: i nastri della riga non sono tutti verso {turned}");
        }

        await shot("nastri-r");
        ClearLinePatch(factory);
        _placeDir = Direction.East;
        _hud?.SetDirectionLabel(_placeDir);

        // nastri-interrotto (review focus 1): a panel, the right button, focus lost or a release outside the window drop
        // the drag, and nothing is sent.
        var outside = new Vector2(-40, -40);
        (string Name, Func<Task> During, Vector2 Release, Func<Task> After)[] interruptions =
        [
            ("T", () => PlaytestKey(Key.T), LineScreen(6, 4), () => PlaytestKey(Key.T)),
            ("destro", async () =>
            {
                await PlaytestPress(LineScreen(6, 4), MouseButton.Right);
                await PlaytestRelease(LineScreen(6, 4), MouseButton.Right);
            }, LineScreen(6, 4), () => Task.CompletedTask),
            ("fuoco perso", () =>
            {
                // As the OS focus loss arrives: on the SceneTree, which propagates it from the root to every node.
                GetTree().Notification((int)MainLoop.NotificationApplicationFocusOut);
                return Task.CompletedTask;
            }, LineScreen(6, 4), () => Task.CompletedTask),
            ("rilascio fuori dalla finestra", () =>
            {
                PlaytestMove(outside, MouseButtonMask.Left);
                return Task.CompletedTask;
            }, outside, () => Task.CompletedTask)
        ];
        foreach (var (name, during, release, after) in interruptions)
        {
            _tool = BuildTool.Belt; // a panel puts the cursor back in hand
            await PlaytestWait(0.1);
            _lineSent.Clear();
            await PlaytestPress(LineScreen(1, 4));
            await PlaytestMoveHeld(LineScreen(1, 4), LineScreen(6, 4));
            await during();
            // A release outside the window is the interruption itself: the drag is still on until then.
            var dropped = !LineDragActive || release == outside;
            await PlaytestRelease(release);
            await after();
            await SettleLine();
            if (!dropped || LineDragActive || _lineSent.Count > 0 || (name != "T" && _tool != BuildTool.Belt)
                || Enumerable.Range(1, 6).Any(x => view.Belts.ContainsKey(LineCell(x, 4))))
            {
                return Fail($"nastri-interrotto ({name}): trascinamento {(dropped ? "annullato" : "ancora attivo")}, {_lineSent.Count} comandi inviati, strumento {_tool}");
            }
        }

        if (_research?.IsOpen == true)
        {
            return Fail("nastri-interrotto: la Ricerca è rimasta aperta");
        }

        // A release over a HUD panel still ends the drag (sent or dropped): never a preview left stuck to the mouse.
        var hudPanels = PlaytestHudPanelRects();
        if (hudPanels.Count == 0)
        {
            return Fail("nastri-interrotto: nessun pannello dell'HUD su cui rilasciare");
        }

        var (panelName, panel) = hudPanels.MaxBy(r => r.Rect.End.Y);

        _tool = BuildTool.Belt;
        await PlaytestWait(0.1);
        await PlaytestPress(LineScreen(1, 4));
        await PlaytestMoveHeld(LineScreen(1, 4), panel.GetCenter());
        await PlaytestRelease(panel.GetCenter());
        await SettleLine();
        if (LineDragActive)
        {
            return Fail($"nastri-interrotto: rilasciato sopra {panelName}, il trascinamento resta attivo");
        }

        GD.Print($"PLAYTEST rilascio sopra {panelName}: trascinamento finito, {_lineSent.Count} comandi");
        ClearLinePatch(factory);
        await shot("nastri-interrotto");
        GD.Print("PLAYTEST step ok: nastri-interrotto, T, destro, fuoco perso e rilascio fuori dalla finestra non inviano nulla");

        // nastri-un-toast: no raw iron, a line across three perpendicular belts: one toast for the whole line.
        LineColumn(factory, 3);
        LineColumn(factory, 4);
        LineColumn(factory, 5);
        var ore = factory.Wallet.MaterialCount("iron-ore");
        factory.Wallet.TryRemoveMaterial("iron-ore", ore);
        var toasts = _hud?.ToastsShown ?? 0;
        await LineDrag((1, 3), (8, 3));
        await PlaytestWait(0.2);
        factory.Wallet.AddMaterial("iron-ore", ore);
        var crossings = _lineSent.Count(c => c is PlaceBelt { Crossing: true });
        if (crossings != 3 || _hud?.ToastsShown != toasts + 1)
        {
            return Fail($"nastri-un-toast: {crossings} incroci inviati, {(_hud?.ToastsShown ?? 0) - toasts} toast invece di 1");
        }

        await shot("nastri-un-toast");
        ClearLinePatch(factory);

        // nastri-due-incroci: two crossings in a row; the agents' piazza on the same game sends the same commands.
        LineColumn(factory, 3);
        LineColumn(factory, 4);
        await LineDrag((1, 3), (7, 3));
        var client = _lineSent.ToList();
        if (client.Count(c => c is PlaceBelt { Crossing: true }) != 2
            || !LineBeltAt(3, 3, Direction.East, LogisticsKind.Junction) && !view.Belts.ContainsKey(LineCell(3, 3)))
        {
            return Fail($"nastri-due-incroci: {client.Count(c => c is PlaceBelt { Crossing: true })} incroci inviati invece di 2");
        }

        await shot("nastri-due-incroci");
        ClearLinePatch(factory);
        LineColumn(factory, 3);
        LineColumn(factory, 4);
        _lineSent.Clear();
        using (var agent = new AgentGame(host, factory.Content))
        {
            var (from, to) = (LineCell(1, 3), LineCell(7, 3));
            agent.Call("piazza", JsonDocument.Parse(
                $$$"""{"tier":1,"da":{"x":{{{from.X}}},"y":{{{from.Y}}}},"a":{"x":{{{to.X}}},"y":{{{to.Y}}}}}""").RootElement);
        }

        await SettleLine();
        if (!_lineSent.SequenceEqual(client))
        {
            return Fail($"nastri-due-incroci: il client ha inviato [{string.Join("; ", client)}], piazza [{string.Join("; ", _lineSent)}]");
        }

        GD.Print($"PLAYTEST step ok: nastri-due-incroci, client e piazza inviano gli stessi {client.Count} comandi");
        ClearLinePatch(factory);

        // nastri-ctrl: Ctrl lays a four-connected path from the anchor to the cursor instead of the straight line.
        _lineSent.Clear();
        await PlaytestPress(LineScreen(1, 1), ctrl: true);
        await PlaytestMoveHeld(LineScreen(1, 1), LineScreen(6, 5), ctrl: true);
        await PlaytestWait(0.2);
        await shot("nastri-ctrl");
        await PlaytestRelease(LineScreen(6, 5), ctrl: true);
        await SettleLine();
        var path = _lineSent.OfType<PlaceBelt>().Select(c => c.Position).ToList();
        if (path.Count != 10 || path[0] != LineCell(1, 1) || path[^1] != LineCell(6, 5)
            || path.Zip(path.Skip(1)).Any(s => Math.Abs(s.First.X - s.Second.X) + Math.Abs(s.First.Y - s.Second.Y) != 1)
            || path.Any(c => !view.Belts.ContainsKey(c)))
        {
            return Fail($"nastri-ctrl: percorso [{string.Join(" ", path)}]");
        }

        GD.Print("PLAYTEST step ok: nastri-ctrl, percorso a scalinata dall'ancora al cursore");
        ClearLinePatch(factory);

        // nastri-ponte: a smelter across a straight belt line: the line bridges over it.
        if (!factory.PlaceInstant(new BuildSpec(Crafter.SmelterBuildingId, BuildingKind.Smelter, LineCell(4, 3), Direction.East), charge: false))
        {
            return Fail($"nastri-ponte: forno non piazzato in {LineCell(4, 3)}");
        }

        _buildingsDirty = true;
        _lineSent.Clear();
        await PlaytestPress(LineScreen(1, 3));
        await PlaytestMoveHeld(LineScreen(1, 3), LineScreen(8, 3));
        await PlaytestWait(0.2);
        await shot("nastri-ponte-anteprima");
        await PlaytestRelease(LineScreen(8, 3));
        await SettleLine();
        if (!view.Belts.TryGetValue(LineCell(3, 3), out var bridge) || bridge.Kind != LogisticsKind.Bridge
            || !view.Belts.ContainsKey(LineCell(6, 3)) || !LineBeltAt(8, 3, Direction.East))
        {
            return Fail($"nastri-ponte: nessun ponte da {LineCell(3, 3)} a {LineCell(6, 3)} ({string.Join("; ", _lineSent)})");
        }

        await shot("nastri-ponte");
        GD.Print("PLAYTEST step ok: nastri-ponte, la linea scavalca il forno col ponte");
        ClearLinePatch(factory);
        return await PlaytestPickAndWheelAsync(shot, factory);
    }

    /// <summary>
    /// <c>nastri-prendi</c>: a middle click on a fast belt facing North takes it in hand (tool, brush), a locked one only
    /// toasts, a middle drag pans and keeps the tool. <c>rotellina-ruota</c>: the wheel turns every orientable tool like R
    /// (up clockwise, down back) without zooming, also during a drag; with Ctrl, the cursor or a block without a way it zooms.
    /// </summary>
    private async Task<bool> PlaytestPickAndWheelAsync(Func<string, Task> shot, Factory factory)
    {
        if (GetNodeOrNull<WorldCamera>("Camera") is not { } cam)
        {
            return Fail("nastri-prendi: camera assente");
        }

        var view = _view!;

        // smontaggio-interrotto (review focus 1): Esc, a panel, focus lost or a release outside the window drop the removal
        // rectangle like the left line: nothing is sent and no rectangle stays drawn.
        var outside = new Vector2(-40, -40);
        (string Name, Func<Task> During, Vector2 Release, Func<Task> After)[] interruptions =
        [
            ("Esc", () => PlaytestKey(Key.Escape), LineScreen(6, 4), () => Task.CompletedTask),
            ("T", () => PlaytestKey(Key.T), LineScreen(6, 4), () => PlaytestKey(Key.T)),
            ("fuoco perso", () =>
            {
                GetTree().Notification((int)MainLoop.NotificationApplicationFocusOut);
                return Task.CompletedTask;
            }, LineScreen(6, 4), () => Task.CompletedTask),
            ("rilascio fuori dalla finestra", () =>
            {
                PlaytestMove(outside, MouseButtonMask.Right);
                return Task.CompletedTask;
            }, outside, () => Task.CompletedTask)
        ];
        foreach (var (name, during, release, after) in interruptions)
        {
            for (var x = 1; x <= 6; x++)
            {
                factory.PlaceInstant(new BuildSpec(BuildSpec.DefaultConveyorId, null, LineCell(x, 4), Direction.East), charge: false);
            }

            _visualDirty = true;
            _tool = BuildTool.Belt; // a panel puts the cursor back in hand
            await PlaytestWait(0.1);
            await PlaytestPress(LineScreen(1, 4), MouseButton.Right);
            await PlaytestMoveHeld(LineScreen(1, 4), LineScreen(6, 4), mask: MouseButtonMask.Right);
            var drawn = _draggingRemove && _removeMoved;
            await during();
            var dropped = !_draggingRemove || release == outside;
            await PlaytestRelease(release, MouseButton.Right);
            await after();
            await SettleLine();
            if (!drawn || !dropped || _draggingRemove || _removeMoved
                || Enumerable.Range(1, 6).Any(x => !view.Belts.ContainsKey(LineCell(x, 4))))
            {
                return Fail($"smontaggio-interrotto ({name}): rettangolo {(drawn ? "disegnato" : "mai disegnato")}, "
                    + $"{(dropped ? "annullato" : "ancora attivo")}, nastri {Enumerable.Range(1, 6).Count(x => view.Belts.ContainsKey(LineCell(x, 4)))}/6");
            }
        }

        if (_research?.IsOpen == true)
        {
            return Fail("smontaggio-interrotto: la Ricerca è rimasta aperta");
        }

        ClearLinePatch(factory);
        GD.Print("PLAYTEST step ok: smontaggio-interrotto, Esc, T, fuoco perso e rilascio fuori dalla finestra non smontano nulla");

        if (!factory.PlaceInstant(new BuildSpec("conveyor-fast", null, LineCell(2, 2), Direction.North), charge: false))
        {
            return Fail($"nastri-prendi: nastro veloce non piazzato in {LineCell(2, 2)}");
        }

        _visualDirty = true;
        SyncResearchLocks(); // the palette learns the probe's unlocks (ForceUnlock tells no one)
        (_tool, _placeDir) = (BuildTool.Belt, Direction.East);
        await PlaytestWait(0.1);
        await PlaytestPress(LineScreen(2, 2), MouseButton.Middle);
        await PlaytestRelease(LineScreen(2, 2), MouseButton.Middle);
        if (_tool != BuildTool.BeltFast || _placeDir != Direction.North)
        {
            return Fail($"nastri-prendi: strumento {_tool}, pennello {_placeDir} invece di Nastro T2 verso Nord");
        }

        await shot("nastri-prendi");

        // Locked: a toast and the tool stays.
        factory.Research.ForceLock("conveyor-fast");
        SyncResearchLocks();
        _tool = BuildTool.Belt;
        var toasts = _hud?.ToastsShown ?? 0;
        await PlaytestPress(LineScreen(2, 2), MouseButton.Middle);
        await PlaytestRelease(LineScreen(2, 2), MouseButton.Middle);
        factory.Research.ForceUnlock("conveyor-fast");
        SyncResearchLocks();
        if (_tool != BuildTool.Belt || _hud?.ToastsShown != toasts + 1)
        {
            return Fail($"nastri-prendi bloccato: strumento {_tool}, {(_hud?.ToastsShown ?? 0) - toasts} toast");
        }

        // A middle drag pans the view and picks nothing.
        var position = cam.Position;
        await PlaytestDrag(MouseButton.Middle, LineScreen(2, 2), LineScreen(7, 6));
        if (_tool != BuildTool.Belt || cam.Position.IsEqualApprox(position))
        {
            return Fail($"nastri-prendi trascinato: strumento {_tool}, vista {(cam.Position.IsEqualApprox(position) ? "ferma" : "spostata")}");
        }

        cam.Position = position;
        await PlaytestWait(0.1);
        GD.Print("PLAYTEST step ok: nastri-prendi, il centrale prende il nastro (tipo e direzione), trascinato sposta la vista");

        var at = LineScreen(4, 4);
        var zoom = cam.Zoom;
        foreach (var tool in new[] { BuildTool.Belt, BuildTool.Splitter, BuildTool.Sorter, BuildTool.Bridge, BuildTool.Extractor, BuildTool.Assembler })
        {
            (_tool, _placeDir) = (tool, Direction.East);
            await PlaytestWheel(at, MouseButton.WheelUp);
            var up = _placeDir;
            await PlaytestWheel(at, MouseButton.WheelDown);
            if (up != Direction.South || _placeDir != Direction.East || cam.Zoom != zoom)
            {
                return Fail($"rotellina-ruota {tool}: su {up}, giù {_placeDir}, zoom {zoom.X} -> {cam.Zoom.X}");
            }
        }

        // A touchpad's fractions of a notch: four quarter steps turn the block once, not four times.
        (_tool, _placeDir) = (BuildTool.Belt, Direction.East);
        var turns = new List<Direction>();
        for (var i = 0; i < 4; i++)
        {
            await PlaytestWheel(at, MouseButton.WheelUp, factor: 0.25f);
            turns.Add(_placeDir);
        }

        if (!turns.SequenceEqual([Direction.East, Direction.East, Direction.East, Direction.South]) || cam.Zoom != zoom)
        {
            return Fail($"rotellina-ruota touchpad: quarti di scatto [{string.Join(" ", turns)}], zoom {cam.Zoom.X}");
        }

        (string Name, BuildTool Tool, bool Ctrl)[] zooms =
            [("Minatore", BuildTool.Miner, false), ("Lampada", BuildTool.Lamp, false), ("Ctrl", BuildTool.Belt, true), ("cursore", BuildTool.Cursor, false)];
        foreach (var (name, tool, ctrl) in zooms)
        {
            (_tool, _placeDir) = (tool, Direction.East);
            await PlaytestWheel(at, MouseButton.WheelUp, ctrl);
            if (_placeDir != Direction.East || cam.Zoom == zoom)
            {
                return Fail($"rotellina-ruota {name}: pennello {_placeDir}, zoom {zoom.X} -> {cam.Zoom.X} (deve zoomare)");
            }

            cam.Zoom = zoom;
        }

        // During a drag the wheel fixes the way, like R.
        (_tool, _placeDir) = (BuildTool.Belt, Direction.East);
        await PlaytestWait(0.1);
        _lineSent.Clear();
        await PlaytestPress(LineScreen(1, 4));
        await PlaytestMoveHeld(LineScreen(1, 4), LineScreen(6, 4));
        await PlaytestWheel(LineScreen(6, 4), MouseButton.WheelUp);
        await PlaytestWait(0.2);
        await shot("rotellina-ruota");
        await PlaytestRelease(LineScreen(6, 4));
        await SettleLine();
        if (Enumerable.Range(1, 6).Any(x => !LineBeltAt(x, 4, Direction.South)) || cam.Zoom != zoom)
        {
            return Fail($"rotellina-ruota nel trascinamento: nastri non tutti verso Sud ({_lineSent.Count} comandi), zoom {cam.Zoom.X}");
        }

        GD.Print("PLAYTEST step ok: rotellina-ruota, gira ogni blocco orientabile; Ctrl, cursore, minatore e lampada zoomano");
        return true;
    }

    private const int ArmoredProbeWidth = 10;

    /// <summary>
    /// <c>nastro-rinforzato</c>: three miners on a deposit and a smelter in a row, a fast line along their North side and
    /// an armored line along their South side, both heading East. The fast line blends with the buildings and fills; the
    /// armored line stays straight and gets nothing (it takes from buildings only from behind). Everything is taken
    /// away afterwards.
    /// </summary>
    private async Task<bool> PlaytestArmoredBeltAsync(Func<string, Task> shot)
    {
        if (_view is not Factory factory || _view.Terrain is not { } terrain || GetNodeOrNull<Camera2D>("Camera") is not { } cam)
        {
            return Fail("nastro-rinforzato: partita assente");
        }

        // Rows from the origin: 0 fast line, 1-2 miners (x 1, 3, 5) and the smelter (x 7), 3 armored line.
        BuildSpec[] Layout(GridPosition a) =>
        [
            .. Enumerable.Range(0, ArmoredProbeWidth).Select(x =>
                new BuildSpec("conveyor-fast", null, new GridPosition(a.X + x, a.Y), Direction.East)),
            .. Enumerable.Range(0, ArmoredProbeWidth).Select(x =>
                new BuildSpec("conveyor-armored", null, new GridPosition(a.X + x, a.Y + 3), Direction.East)),
            .. new[] { 1, 3, 5 }.Select(x =>
                new BuildSpec(Miner.BasicId, BuildingKind.Miner, new GridPosition(a.X + x, a.Y + 1), Direction.East)),
            new BuildSpec(Crafter.SmelterBuildingId, BuildingKind.Smelter, new GridPosition(a.X + 7, a.Y + 1), Direction.East),
        ];

        // Miners digging as fast as the deposit allows: the spot with the most ore under them.
        var core = new GridPosition(_view.CoreTiles.Min(t => t.X), _view.CoreTiles.Min(t => t.Y));
        (GridPosition At, int Ore)? best = null;
        for (var dy = -30; dy <= 30; dy++)
        {
            for (var dx = -30; dx <= 30; dx++)
            {
                // Away from the Core: a miner touching it would deliver there instead of onto the belts.
                var at = new GridPosition(core.X + dx, core.Y + dy);
                var layout = Layout(at);
                if (Math.Max(Math.Abs(dx), Math.Abs(dy)) < 8 || !layout.All(spec => factory.CanPlace(spec, out _)))
                {
                    continue;
                }

                var ores = layout.Where(spec => spec.Kind == BuildingKind.Miner)
                    .Select(spec => factory.Footprint(spec).Count(t => terrain[t].Deposit != DepositKind.None)).ToArray();
                var ore = ores.Sum();
                if (ores.Min() > 0 && (best is null || ore > best.Value.Ore))
                {
                    best = (at, ore);
                }
            }
        }

        if (best is not { } found)
        {
            return Fail("nastro-rinforzato: nessuno spazio per tre minatori su un giacimento");
        }

        var specs = Layout(found.At);
        foreach (var spec in specs)
        {
            if (!factory.PlaceInstant(spec, charge: false))
            {
                return Fail($"nastro-rinforzato: {spec.StructureId} su {spec.Origin} rifiutato");
            }
        }

        _visualDirty = true;
        _buildingsDirty = true;
        int Items(int row) => Enumerable.Range(0, ArmoredProbeWidth)
            .Sum(x => factory.Belts.Cells[new GridPosition(found.At.X + x, found.At.Y + row)].Items.Count);
        try
        {
            // The four rows whole, clear of the HUD corners.
            cam.Position = CellCenter(new GridPosition(found.At.X + ArmoredProbeWidth / 2, found.At.Y + 1))
                + new Vector2(-TileSize / 2f, TileSize / 2f);
            cam.Zoom = new Vector2(1.2f, 1.2f);
            await PlaytestWait(10);
            var shapes = Enumerable.Range(0, ArmoredProbeWidth)
                .Select(x => factory.GetBeltShape(new GridPosition(found.At.X + x, found.At.Y + 3))).ToArray();
            await shot("nastro-rinforzato");
            GD.Print($"PLAYTEST nastro rinforzato: veloce {Items(0)} oggetti, rinforzato {Items(3)}, forme {string.Join(", ", shapes.Distinct())}");
            if (Items(0) == 0 || Items(3) != 0 || shapes.Any(shape => shape != BeltShape.Straight))
            {
                return Fail("nastro-rinforzato: il veloce deve riempirsi, il rinforzato restare vuoto e dritto");
            }

            GD.Print("PLAYTEST step ok: nastro-rinforzato, passa accanto a minatori e forno senza raccogliere niente");
            return true;
        }
        finally
        {
            foreach (var spec in specs)
            {
                factory.RemoveInstant(spec.Origin, refund: false);
            }

            _visualDirty = true;
            _buildingsDirty = true;
            FocusCameraOnCore();
        }
    }
}
