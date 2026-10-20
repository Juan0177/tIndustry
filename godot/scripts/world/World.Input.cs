using Godot;
using TIndustry.Shared;

namespace TIndustry.Godot;

/// <summary>Build tool selection and mouse/keyboard input.</summary>
public partial class World
{
    private void OnHudToolChosen(Hud.ToolKind kind)
    {
        _tool = FromHudTool(kind);
        QueueRedraw();
    }

    private void ClearToCursor(bool toast = true)
    {
        _tool = BuildTool.Cursor;
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
        if (tool != BuildTool.Cursor
            && _view is not null
            && !_view.IsStructureUnlocked(Hud.StructureIdFor(ToHudTool(tool))))
        {
            _hud?.ShowToast(Testi.F("rifiuto.Locked", ToolName(tool)));
            return;
        }

        _tool = tool;
        _hud?.SetSelectedTool(ToHudTool(tool));
        if (toast)
        {
            _hud?.ShowToast(tool == BuildTool.Cursor ? ToolName(tool) : Testi.F("palette.selezionato", ToolName(tool)));
        }

        QueueRedraw();
    }

    private static Hud.ToolKind ToHudTool(BuildTool tool) => tool switch
    {
        BuildTool.Cursor => Hud.ToolKind.Cursor,
        BuildTool.Miner => Hud.ToolKind.Miner,
        BuildTool.MinerAdvanced => Hud.ToolKind.MinerAdvanced,
        BuildTool.Smelter => Hud.ToolKind.Smelter,
        BuildTool.Assembler => Hud.ToolKind.Assembler,
        BuildTool.Junction => Hud.ToolKind.Junction,
        BuildTool.Splitter => Hud.ToolKind.Splitter,
        BuildTool.Generator => Hud.ToolKind.Generator,
        BuildTool.Sorter => Hud.ToolKind.Sorter,
        BuildTool.Bridge => Hud.ToolKind.Bridge,
        BuildTool.Belt => Hud.ToolKind.Belt,
        BuildTool.BeltFast => Hud.ToolKind.BeltFast,
        BuildTool.BeltExpress => Hud.ToolKind.BeltExpress,
        BuildTool.BeltArmored => Hud.ToolKind.BeltArmored,
        BuildTool.Extractor => Hud.ToolKind.Extractor,
        BuildTool.PowerNode => Hud.ToolKind.PowerNode,
        BuildTool.PowerNodeT2 => Hud.ToolKind.PowerNodeT2,
        BuildTool.Lamp => Hud.ToolKind.Lamp,
        _ => Hud.ToolKind.Cursor
    };

    private static BuildTool FromHudTool(Hud.ToolKind tool) => tool switch
    {
        Hud.ToolKind.Cursor => BuildTool.Cursor,
        Hud.ToolKind.Miner => BuildTool.Miner,
        Hud.ToolKind.MinerAdvanced => BuildTool.MinerAdvanced,
        Hud.ToolKind.Smelter => BuildTool.Smelter,
        Hud.ToolKind.Assembler => BuildTool.Assembler,
        Hud.ToolKind.Junction => BuildTool.Junction,
        Hud.ToolKind.Splitter => BuildTool.Splitter,
        Hud.ToolKind.Generator => BuildTool.Generator,
        Hud.ToolKind.Sorter => BuildTool.Sorter,
        Hud.ToolKind.Bridge => BuildTool.Bridge,
        Hud.ToolKind.Belt => BuildTool.Belt,
        Hud.ToolKind.BeltFast => BuildTool.BeltFast,
        Hud.ToolKind.BeltExpress => BuildTool.BeltExpress,
        Hud.ToolKind.BeltArmored => BuildTool.BeltArmored,
        Hud.ToolKind.Extractor => BuildTool.Extractor,
        Hud.ToolKind.PowerNode => BuildTool.PowerNode,
        Hud.ToolKind.PowerNodeT2 => BuildTool.PowerNodeT2,
        Hud.ToolKind.Lamp => BuildTool.Lamp,
        _ => BuildTool.Cursor
    };

    /// <summary>Name of the tool: the cursor's own, else its structure's.</summary>
    private string ToolName(BuildTool tool) => tool == BuildTool.Cursor
        ? Testi.T("strumento.cursore")
        : Testi.Struttura(Hud.StructureIdFor(ToHudTool(tool)), _view?.Content);

    /// <summary>
    /// Runs before every panel and the world: drops all input while the map overlay is up (Esc or a click
    /// must not close a panel or start another game mid-load) and real input during a playtest.
    /// </summary>
    public override void _Input(InputEvent @event)
    {
        if (_mapWorkBusy || IsRealInputDuringPlaytest(@event))
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
                // Raylib parity: H / Home → jump camera to CORE.
                FocusCameraOnCore();
                _hud?.ShowToast(Testi.T("hud.camera.core"));
                GetViewport().SetInputAsHandled();
                return;
            }

            if (key.Keycode == Key.Escape || key.Keycode == Key.Quoteleft)
            {
                if (_tool != BuildTool.Cursor)
                {
                    ClearToCursor();
                }
                else if (key.Keycode == Key.Escape)
                {
                    // Raylib parity: autosave Continua then Esc → Home.
                    AutoSaveContinue(quiet: true);
                    _home?.Open(inGame: true);
                }
                else
                {
                    ClearToCursor();
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
                SelectTool(BuildTool.Splitter, toast: true);
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

            if (key.Keycode == Key.C)
            {
                CycleSorterFilter();
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
                LoadGame(FactorySaveStore.ContinueSlotId, Testi.T("salvataggi.caricata.continua"));
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
                LoadGame(FactorySaveStore.QuickSlotId, Testi.T("salvataggi.rapido.caricato"));
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
                    if (_tool == BuildTool.Cursor)
                    {
                        // Cursor mode: no place — WorldCamera.LeftDragPans pans the view.
                        return;
                    }

                    BeginLine(cell, mouse.CtrlPressed);
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
                    else
                    {
                        TryRemoveAt(_removeStart);
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
}
