using Godot;
using TIndustry.Shared;

namespace TIndustry.Godot;

/// <summary>
/// Block selection and mouse/keyboard input, as in Mindustry (no tools): WASD flies the drone, the left click with no
/// block selected selects or configures the block under it (held on a deposit it mines by hand), a block selected in the
/// palette is placed by click or drag, the right click or Esc deselects it, the right drag dismantles, Q picks the block
/// under the cursor; the camera (<see cref="WorldCamera"/>) follows the drone and the middle button drags the view.
/// </summary>
public partial class World
{
    private void OnHudToolChosen(Hud.ToolKind kind)
    {
        _tool = FromHudTool(kind);
        if (_tool != BuildTool.None)
        {
            _selectedNode = null;
        }

        QueueRedraw();
    }

    /// <summary>No block selected any more (Esc, right click, a lock, a new game): the click selects in the world again.</summary>
    private void DeselectBlock(bool toast = true)
    {
        _tool = BuildTool.None;
        CancelLine();
        _hud?.ClearToolSelection(toast);
        QueueRedraw();
    }

    private void OnHudRotate() => Rotate(clockwise: true);

    /// <summary>R (clockwise) and the wheel (up clockwise, down counterclockwise): the brush turns a quarter.</summary>
    private void Rotate(bool clockwise)
    {
        _placeDir = clockwise ? DirectionMath.Right(_placeDir) : DirectionMath.Left(_placeDir);
        // R during a drag fixes the new way for the whole line, until the release.
        _lineFixed |= LineDragActive && IsOrientable(_tool);
        _hud?.SetDirectionLabel(_placeDir);
        _hud?.ShowToast(Testi.F("hud.direzione.toast", Testi.Direzione(_placeDir)));
        QueueRedraw();
    }

    private void SelectTool(BuildTool tool, bool toast = false)
    {
        if (tool != BuildTool.None
            && _view is not null
            && !_view.IsStructureUnlocked(Hud.StructureIdFor(ToHudTool(tool))))
        {
            _hud?.ShowToast(Testi.F("rifiuto.Locked", ToolName(tool)));
            return;
        }

        _tool = tool;
        if (tool != BuildTool.None)
        {
            _selectedNode = null;
        }

        _hud?.SetSelectedTool(ToHudTool(tool));
        if (toast)
        {
            _hud?.ShowToast(tool == BuildTool.None ? ToolName(tool) : Testi.F("palette.selezionato", ToolName(tool)));
        }

        QueueRedraw();
    }

    private static Hud.ToolKind ToHudTool(BuildTool tool) => tool switch
    {
        BuildTool.None => Hud.ToolKind.None,
        BuildTool.Miner => Hud.ToolKind.Miner,
        BuildTool.MinerAdvanced => Hud.ToolKind.MinerAdvanced,
        BuildTool.Smelter => Hud.ToolKind.Smelter,
        BuildTool.Assembler => Hud.ToolKind.Assembler,
        BuildTool.Junction => Hud.ToolKind.Junction,
        BuildTool.Router => Hud.ToolKind.Router,
        BuildTool.Generator => Hud.ToolKind.Generator,
        BuildTool.Sorter => Hud.ToolKind.Sorter,
        BuildTool.SorterInverted => Hud.ToolKind.SorterInverted,
        BuildTool.Bridge => Hud.ToolKind.Bridge,
        BuildTool.Belt => Hud.ToolKind.Belt,
        BuildTool.BeltFast => Hud.ToolKind.BeltFast,
        BuildTool.BeltExpress => Hud.ToolKind.BeltExpress,
        BuildTool.BeltArmored => Hud.ToolKind.BeltArmored,
        BuildTool.Extractor => Hud.ToolKind.Extractor,
        BuildTool.PowerNode => Hud.ToolKind.PowerNode,
        BuildTool.PowerNodeT2 => Hud.ToolKind.PowerNodeT2,
        BuildTool.Lamp => Hud.ToolKind.Lamp,
        BuildTool.Battery => Hud.ToolKind.Battery,
        BuildTool.Container => Hud.ToolKind.Container,
        BuildTool.Vault => Hud.ToolKind.Vault,
        BuildTool.CoreFoundation => Hud.ToolKind.CoreFoundation,
        BuildTool.CoreNucleus => Hud.ToolKind.CoreNucleus,
        _ => Hud.ToolKind.None
    };

    private static BuildTool FromHudTool(Hud.ToolKind tool) => tool switch
    {
        Hud.ToolKind.None => BuildTool.None,
        Hud.ToolKind.Miner => BuildTool.Miner,
        Hud.ToolKind.MinerAdvanced => BuildTool.MinerAdvanced,
        Hud.ToolKind.Smelter => BuildTool.Smelter,
        Hud.ToolKind.Assembler => BuildTool.Assembler,
        Hud.ToolKind.Junction => BuildTool.Junction,
        Hud.ToolKind.Router => BuildTool.Router,
        Hud.ToolKind.Generator => BuildTool.Generator,
        Hud.ToolKind.Sorter => BuildTool.Sorter,
        Hud.ToolKind.SorterInverted => BuildTool.SorterInverted,
        Hud.ToolKind.Bridge => BuildTool.Bridge,
        Hud.ToolKind.Belt => BuildTool.Belt,
        Hud.ToolKind.BeltFast => BuildTool.BeltFast,
        Hud.ToolKind.BeltExpress => BuildTool.BeltExpress,
        Hud.ToolKind.BeltArmored => BuildTool.BeltArmored,
        Hud.ToolKind.Extractor => BuildTool.Extractor,
        Hud.ToolKind.PowerNode => BuildTool.PowerNode,
        Hud.ToolKind.PowerNodeT2 => BuildTool.PowerNodeT2,
        Hud.ToolKind.Lamp => BuildTool.Lamp,
        Hud.ToolKind.Battery => BuildTool.Battery,
        Hud.ToolKind.Container => BuildTool.Container,
        Hud.ToolKind.Vault => BuildTool.Vault,
        Hud.ToolKind.CoreFoundation => BuildTool.CoreFoundation,
        Hud.ToolKind.CoreNucleus => BuildTool.CoreNucleus,
        _ => BuildTool.None
    };

    /// <summary>Name of the selected block (its structure's); "no block selected" for <see cref="BuildTool.None"/>.</summary>
    private string ToolName(BuildTool tool) => tool == BuildTool.None
        ? Testi.T("palette.deselezionato")
        : Testi.Struttura(Hud.StructureIdFor(ToHudTool(tool)), _view?.Content);

    /// <summary>F9 / F7 during a game: the slot loads behind the loading screen; a failure is the toast.</summary>
    private void QuickLoad(string slotId, string toast) =>
        RunMapTask(() => LoadGameWithScreenAsync(slotId, toast, showOnPanel: null), m => _hud?.ShowToast(m));

    /// <summary>
    /// Runs before every panel and the world: drops all input while the loading screen is shown, fade included (Esc
    /// or a click must not close a panel or start another game mid-load), and real input during a playtest, and takes
    /// Space (pause) before a focused button could take it as a press.
    /// </summary>
    public override void _Input(InputEvent @event)
    {
        // WASD is tracked here, before any panel or control could take the release (a key held while a panel opens).
        if (!IsRealInputDuringPlaytest(@event))
        {
            TrackDroneKeys(@event);
        }

        if (LoadingShown || IsRealInputDuringPlaytest(@event) || HandlePauseKey(@event))
        {
            GetViewport().SetInputAsHandled();
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (_saves?.IsOpen == true)
        {
            return;
        }

        if (_settingsPanel?.IsOpen == true)
        {
            return;
        }

        if (_home?.IsOpen == true || _newGame?.IsOpen == true)
        {
            return;
        }

        if (_view is null)
        {
            return;
        }

        if (LineDragActive && @event is InputEventWithModifiers modifiers && modifiers.CtrlPressed != _lineCtrl)
        {
            // Ctrl pressed or let go during the drag: the preview switches between the straight line and the path.
            _lineCtrl = modifiers.CtrlPressed;
            QueueRedraw();
        }

        if (@event is InputEventKey key && key.Pressed && !key.Echo)
        {
            if (HandleDevSpeedKey(key))
            {
                GetViewport().SetInputAsHandled();
                return;
            }

            if (key.Keycode is Key.I or Key.T or Key.M or Key.G)
            {
                CancelDrags(); // a panel opens: the drag is dropped
            }
            else if ((LineDragActive || _draggingRemove) && key.Keycode == Key.Escape)
            {
                CancelDrags();
                GetViewport().SetInputAsHandled();
                return;
            }

            if (key.Keycode == Key.I)
            {
                OpenSettings();
                GetViewport().SetInputAsHandled();
                return;
            }

            if (key.Keycode == Key.T)
            {
                ToggleResearch();
                GetViewport().SetInputAsHandled();
                return;
            }

            if (key.Keycode == Key.M)
            {
                ToggleMercato();
                GetViewport().SetInputAsHandled();
                return;
            }

            if (key.Keycode == Key.G)
            {
                ToggleCampaign();
                GetViewport().SetInputAsHandled();
                return;
            }

            if (_research?.IsOpen == true || _mercato?.IsOpen == true || _campaignSelect?.IsOpen == true)
            {
                return;
            }

            if (key.Keycode is Key.H or Key.Home)
            {
                // H / Home: the view back on the drone at once (it follows the drone anyway; this ends a free look).
                CentreCameraOnDrone();
                _hud?.ShowToast(Testi.T("hud.camera.drone"));
                GetViewport().SetInputAsHandled();
                return;
            }

            if (key.Keycode == Key.Q)
            {
                // Q (Mindustry): the block under the cursor becomes the selected block.
                if (_hover is { } under)
                {
                    PickAt(under);
                }

                GetViewport().SetInputAsHandled();
                return;
            }

            if (key.Keycode == Key.Escape || key.Keycode == Key.Quoteleft)
            {
                if (_tool != BuildTool.None)
                {
                    DeselectBlock();
                }
                else if (_filterPanel?.IsOpen == true)
                {
                    _filterPanel.Close(); // Esc first closes the filter card, then drops a node, then goes Home
                }
                else if (_selectedNode is not null)
                {
                    _selectedNode = null; // Esc first drops the selected node, then goes Home
                }
                else if (key.Keycode == Key.Escape)
                {
                    // Raylib parity: autosave Continua then Esc → Home.
                    AutoSaveContinue(quiet: true);
                    _home?.Open(inGame: true);
                }
                else
                {
                    DeselectBlock();
                }

                GetViewport().SetInputAsHandled();
                return;
            }

            if (key.Keycode == Key.Key1 || key.Keycode == Key.N)
            {
                SelectTool(BuildTool.Belt, toast: true);
                GetViewport().SetInputAsHandled();
                return;
            }

            if (key.Keycode == Key.Key2)
            {
                SelectTool(BuildTool.Miner, toast: true);
                GetViewport().SetInputAsHandled();
                return;
            }

            if (key.Keycode == Key.Key3 || key.Keycode == Key.F)
            {
                SelectTool(BuildTool.Smelter, toast: true);
                GetViewport().SetInputAsHandled();
                return;
            }

            if (key.Keycode == Key.Key4)
            {
                SelectTool(BuildTool.Assembler, toast: true);
                GetViewport().SetInputAsHandled();
                return;
            }

            if (key.Keycode == Key.Key5 || key.Keycode == Key.J)
            {
                SelectTool(BuildTool.Junction, toast: true);
                GetViewport().SetInputAsHandled();
                return;
            }

            if (key.Keycode == Key.Key6)
            {
                SelectTool(BuildTool.Router, toast: true);
                GetViewport().SetInputAsHandled();
                return;
            }

            if (key.Keycode == Key.Key7)
            {
                SelectTool(BuildTool.Generator, toast: true);
                GetViewport().SetInputAsHandled();
                return;
            }

            if (key.Keycode == Key.Key8)
            {
                SelectTool(BuildTool.Sorter, toast: true);
                GetViewport().SetInputAsHandled();
                return;
            }

            if (key.Keycode == Key.Key9)
            {
                SelectTool(BuildTool.Bridge, toast: true);
                GetViewport().SetInputAsHandled();
                return;
            }

            if (key.Keycode == Key.F5)
            {
                SaveGame(FactorySaveStore.ContinueSlotId, Testi.T("salvataggi.salvata.continua"));
                GetViewport().SetInputAsHandled();
                return;
            }

            if (key.Keycode == Key.F9)
            {
                QuickLoad(FactorySaveStore.ContinueSlotId, Testi.T("salvataggi.caricata.continua"));
                GetViewport().SetInputAsHandled();
                return;
            }

            if (key.Keycode == Key.F6)
            {
                SaveGame(FactorySaveStore.QuickSlotId, Testi.T("salvataggi.rapido.salvato"));
                GetViewport().SetInputAsHandled();
                return;
            }

            if (key.Keycode == Key.F7)
            {
                QuickLoad(FactorySaveStore.QuickSlotId, Testi.T("salvataggi.rapido.caricato"));
                GetViewport().SetInputAsHandled();
                return;
            }

            if (key.Keycode == Key.R)
            {
                OnHudRotate();
                GetViewport().SetInputAsHandled();
                return;
            }
        }

        if (_research?.IsOpen == true || _mercato?.IsOpen == true || _campaignSelect?.IsOpen == true)
        {
            return;
        }

        if (@event is InputEventMouseButton mouse)
        {
            var cell = ScreenToCell(mouse.Position);
            if (mouse.ButtonIndex == MouseButton.Left)
            {
                if (mouse.Pressed)
                {
                    if (_tool == BuildTool.None)
                    {
                        // No block selected: held on a free deposit the drone mines it, else a click selects on release.
                        BeginWorldClick(cell, mouse.Position);
                        GetViewport().SetInputAsHandled();
                        return;
                    }

                    BeginLine(cell, mouse.CtrlPressed);
                    GetViewport().SetInputAsHandled();
                }
                else if (_handMining || _clickDown is not null)
                {
                    EndWorldClick(mouse.Position);
                    GetViewport().SetInputAsHandled();
                }
                else if (LineDragActive)
                {
                    // Let go outside the window: dropped, like Esc.
                    if (GetViewport().GetVisibleRect().HasPoint(mouse.Position))
                    {
                        SendLine(cell);
                    }
                    else
                    {
                        CancelLine();
                    }

                    GetViewport().SetInputAsHandled();
                }
            }
            else if (mouse.ButtonIndex == MouseButton.Right)
            {
                if (mouse.Pressed && LineDragActive)
                {
                    // The right button during a left drag only drops the line.
                    CancelLine();
                    GetViewport().SetInputAsHandled();
                }
                else if (mouse.Pressed)
                {
                    // With a block selected the press deselects it (Mindustry); a drag from it still draws the rectangle.
                    _removeDeselected = _tool != BuildTool.None;
                    if (_removeDeselected)
                    {
                        DeselectBlock();
                    }

                    // Nothing is sent yet: a click toggles on release, a drag draws the rectangle marked on release.
                    _draggingRemove = true;
                    _removeMoved = false;
                    _removeStart = cell;
                    GetViewport().SetInputAsHandled();
                }
                else if (_draggingRemove && !GetViewport().GetVisibleRect().HasPoint(mouse.Position))
                {
                    CancelRemoveDrag(); // let go outside the window: dropped, like Esc
                }
                else if (_draggingRemove)
                {
                    if (_removeMoved)
                    {
                        RemoveRectangle(cell);
                    }
                    else if (!_removeDeselected)
                    {
                        TryRemoveAt(_removeStart); // the click that deselected a block removes nothing
                    }

                    CancelRemoveDrag();
                }
            }
            else if (mouse.Pressed && mouse.ButtonIndex is (MouseButton.WheelUp or MouseButton.WheelDown)
                && !mouse.CtrlPressed && IsOrientable(_tool))
            {
                // WorldCamera let the step through (WheelZooms): it turns the block like R, up clockwise. A touchpad sends
                // fractions of a notch (Factor; 0 = unknown, a whole notch): one turn each time they add up to one.
                var notch = mouse.Factor > 0f ? mouse.Factor : 1f;
                _wheelTurn += mouse.ButtonIndex == MouseButton.WheelUp ? notch : -notch;
                if (Math.Abs(_wheelTurn) >= 1f)
                {
                    Rotate(clockwise: _wheelTurn > 0f);
                    _wheelTurn = 0f;
                }

                GetViewport().SetInputAsHandled();
            }
        }
        else if (@event is InputEventMouseMotion motion)
        {
            var cell = ScreenToCell(motion.Position);
            _hoverScreen = motion.Position;
            if (!_hover.Equals(cell))
            {
                _hover = cell;
                QueueRedraw();
            }

            if (_draggingRemove && !_removeMoved && !cell.Equals(_removeStart))
            {
                _removeMoved = true;
                QueueRedraw();
            }
        }
    }

    /// <summary>Screen pixels the mouse may move between press and release for a click to still count as one.</summary>
    private const float ClickSlop = 4f;

    // Left press with no block selected: where it went down (a click on release), or true while it mines by hand.
    private (GridPosition Cell, Vector2 Screen)? _clickDown;
    private bool _handMining;

    /// <summary>
    /// Left press with no block selected: on a free deposit the drone mines it while the button is held
    /// (<see cref="SetMining"/>; a refusal toasts, as any command); anywhere else the click selects on release.
    /// </summary>
    private void BeginWorldClick(GridPosition cell, Vector2 screen)
    {
        if (_view is null || PanelOpen || !GameInPlay)
        {
            return;
        }

        // Something standing on the cell (a block, a belt, a site, a plan, the Core) is selected, deposit or not.
        var occupied = _view.CoreTiles.Contains(cell) || _view.TryGetSpecAt(cell, out _, out _) || _view.TryGetSiteAt(cell, out _)
            || _view.IsPlannedTile(cell) || _view.PowerBlockAt(cell) is not null;
        if (!occupied && _view.MiningRefusal(LocalPlayer, cell) is not (RefusalCode.MiningNoDeposit or RefusalCode.MiningCovered))
        {
            _filterPanel?.Close();
            _selectedNode = null;
            _handMining = true;
            SubmitCommand(new SetMining(LocalPlayer, cell));
            return;
        }

        _clickDown = (cell, screen);
    }

    /// <summary>Left release with no block selected: the mining stops, or a still click selects (<see cref="OnBlockClick"/>).</summary>
    private void EndWorldClick(Vector2 screen)
    {
        StopHandMining();
        if (_clickDown is { } down)
        {
            _clickDown = null;
            if (down.Screen.DistanceTo(screen) < ClickSlop)
            {
                OnBlockClick(down.Cell);
            }
        }
    }

    /// <summary>The held left button let go (release, focus lost, a panel): the drone stops mining by hand.</summary>
    private void StopHandMining()
    {
        _clickDown = null;
        if (!_handMining)
        {
            return;
        }

        _handMining = false;
        SubmitCommand(new SetMining(LocalPlayer, null));
    }

    /// <summary>
    /// A still left click with no block selected on <paramref name="cell"/>: power nodes are selected and linked like
    /// in Mindustry. With a node selected, a power block it reaches (or any power block that is not a node) gets its
    /// laser toggled (<see cref="TogglePowerLink"/>; a refusal is the usual toast), another node out of reach is selected
    /// instead, the node itself or anything else drops the selection. Without one, a click on a node selects it.
    /// A click on a sorter, an inverted sorter or an extractor opens its filter card instead (Mindustry's configuration;
    /// the same block again closes it); a click anywhere else closes the card. The block's information is the hover
    /// tooltip (sites, power networks).
    /// </summary>
    private void OnBlockClick(GridPosition cell)
    {
        if (_view is null || PanelOpen || !GameInPlay || _tool != BuildTool.None)
        {
            return;
        }
        if (TryGetFilterBlock(cell, out var structureId, out var filter))
        {
            _selectedNode = null;
            ToggleFilterPanel(cell, structureId, filter);
            return;
        }

        _filterPanel?.Close();

        var block = _view.PowerBlockAt(cell);
        if (_selectedNode is { } node && _view.PowerNodeLinksOf(node) is not null && block is { } target && target.Id != node
            && (target.Kind != PowerBlockKind.Node || _view.PowerLinkInRange(node, target.Id)))
        {
            SubmitCommand(new TogglePowerLink(LocalPlayer, node, target.Id));
            return;
        }

        _selectedNode = block is { Kind: PowerBlockKind.Node } picked && picked.Id != _selectedNode ? picked.Id : null;
    }

    // --- The drone (WASD) and the camera that follows it -----------------------------------------------------------

    // WASD (the pan_* actions) as the last key events said: tracked in _Input, so a release is never lost.
    private bool _keyUp, _keyDown, _keyLeft, _keyRight;

    // The input this client last sent its drone, and the game it was sent in (a new game starts from its drone's).
    private (int Dx, int Dy) _droneInputSent;
    private SimHost? _droneInputHost;

    // The drone's held input seen last frame: a change to a moving input ends the camera's free look.
    private (int Dx, int Dy) _droneInputSeen;

    /// <summary>Set by a playtest step that checks the follow; otherwise playtests frame their own captures.</summary>
    private bool _playtestCameraFollows;

    private void TrackDroneKeys(InputEvent @event)
    {
        if (@event is not InputEventKey key || key.Echo)
        {
            return;
        }

        if (key.IsAction("pan_up"))
        {
            _keyUp = key.Pressed;
        }
        else if (key.IsAction("pan_down"))
        {
            _keyDown = key.Pressed;
        }
        else if (key.IsAction("pan_left"))
        {
            _keyLeft = key.Pressed;
        }
        else if (key.IsAction("pan_right"))
        {
            _keyRight = key.Pressed;
        }
    }

    /// <summary>The window lost the focus: the releases will not arrive, so no key is held any more.</summary>
    private void ReleaseDroneKeys() => _keyUp = _keyDown = _keyLeft = _keyRight = false;

    /// <summary>The local player's drone (null without a game or a drone).</summary>
    private BuilderUnit? LocalDrone => _view?.Builders.FirstOrDefault(b => b.OwnerPlayerId == LocalPlayer);

    /// <summary>
    /// Every frame: the keys held (none while a panel covers the game) become the drone's input, sent with
    /// <see cref="SetDroneInput"/> only when it changes, so an input held by someone else (the agents' <c>muovi</c>) is
    /// not overwritten every frame. A new game starts from the input its drone holds (a save's is let go at once when no
    /// key is down).
    /// </summary>
    private void SyncDroneInput()
    {
        if (_host != _droneInputHost)
        {
            _droneInputHost = _host;
            _droneInputSent = LocalDrone is { } loaded ? (loaded.InputX, loaded.InputY) : (0, 0);
        }

        if (LocalDrone is null)
        {
            return;
        }

        var keys = GameInPlay && !PanelOpen && !HomeBackdrop && !LoadingShown;
        var wanted = keys ? ((_keyRight ? 1 : 0) - (_keyLeft ? 1 : 0), (_keyDown ? 1 : 0) - (_keyUp ? 1 : 0)) : (0, 0);
        if (wanted != _droneInputSent)
        {
            _droneInputSent = wanted;
            SubmitCommand(new SetDroneInput(LocalPlayer, wanted.Item1, wanted.Item2));
        }
    }

    /// <summary>Centre of the local drone in world pixels: where it is drawn (between ticks), else its simulated centre.</summary>
    private Vector2? DroneWorldPosition()
    {
        if (LocalDrone is not { } drone)
        {
            return null;
        }

        return _constructionLayer?.DroneFlightPosition(drone.Id) ?? new Vector2(drone.X * TileSize, drone.Y * TileSize);
    }

    /// <summary>
    /// Every frame: the camera follows the drone during a game (not behind the Home, not in playtests unless a step asks);
    /// the drone starting to move (any source) ends a free look.
    /// </summary>
    private void SyncCameraFollow(WorldCamera cam)
    {
        var follow = GameInPlay && !HomeBackdrop && (!PlaytestRequested || _playtestCameraFollows);
        cam.FollowTarget = follow ? DroneWorldPosition() : null;
        var seen = LocalDrone is { } drone ? (drone.InputX, drone.InputY) : (0, 0);
        if (seen != _droneInputSeen && seen != (0, 0))
        {
            cam.FreeLook = false;
        }

        _droneInputSeen = seen;
    }

    /// <summary>H: the view jumps onto the drone (and follows it again).</summary>
    private void CentreCameraOnDrone()
    {
        if (GetNodeOrNull<WorldCamera>("Camera") is { } cam && DroneWorldPosition() is { } at)
        {
            cam.CentreOn(at);
        }
    }
}
