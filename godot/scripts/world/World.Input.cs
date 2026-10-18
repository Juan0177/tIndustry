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
        _draggingPlace = false;
        _hud?.ClearToolSelection(toast);
        QueueRedraw();
    }

    private void OnHudRotate()
    {
        _placeDir = DirectionMath.Right(_placeDir);
        _hud?.SetDirectionLabel(DirectionIt(_placeDir));
        _hud?.ShowToast($"Direzione: {DirectionIt(_placeDir)}");
        QueueRedraw();
    }

    private void SelectTool(BuildTool tool, bool toast = false)
    {
        if (tool != BuildTool.Cursor
            && _view is not null
            && !_view.IsStructureUnlocked(Hud.StructureIdFor(ToHudTool(tool))))
        {
            _hud?.ShowToast($"{ToolIt(tool)} bloccato: sbloccalo in Ricerca (T).");
            return;
        }

        _tool = tool;
        _hud?.SetSelectedTool(ToHudTool(tool));
        if (toast)
        {
            _hud?.ShowToast(tool == BuildTool.Cursor ? "Cursore" : $"{ToolIt(tool)} selezionato");
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
        Hud.ToolKind.Extractor => BuildTool.Extractor,
        Hud.ToolKind.PowerNode => BuildTool.PowerNode,
        Hud.ToolKind.PowerNodeT2 => BuildTool.PowerNodeT2,
        Hud.ToolKind.Lamp => BuildTool.Lamp,
        _ => BuildTool.Cursor
    };

    private static string ToolIt(BuildTool tool) => tool switch
    {
        BuildTool.Cursor => "Cursore",
        BuildTool.Belt => "Nastro",
        BuildTool.BeltFast => "Nastro T2",
        BuildTool.BeltExpress => "Nastro T3",
        BuildTool.Miner => "Minatore",
        BuildTool.MinerAdvanced => "Minatore T2",
        BuildTool.Smelter => "Forno",
        BuildTool.Assembler => "Assemblatore",
        BuildTool.Junction => "Giunzione",
        BuildTool.Splitter => "Splitter",
        BuildTool.Generator => "Generatore",
        BuildTool.Sorter => "Selezionatore",
        BuildTool.Bridge => "Ponte",
        BuildTool.Extractor => "Estrattore",
        BuildTool.PowerNode => "Nodo potenza",
        BuildTool.PowerNodeT2 => "Nodo T2",
        BuildTool.Lamp => "Lampada",
        _ => "?"
    };

    private static string DirectionIt(Direction dir) => dir switch
    {
        Direction.North => "Nord",
        Direction.East => "Est",
        Direction.South => "Sud",
        Direction.West => "Ovest",
        _ => "?"
    };

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

        if (@event is InputEventKey key && key.Pressed && !key.Echo)
        {
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
                _hud?.ShowToast("Camera sul CORE (H)");
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
                    _home?.Open();
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
                SaveGame(FactorySaveStore.ContinueSlotId, "Partita salvata (continua)");
                GetViewport().SetInputAsHandled();
                return;
            }

            if (key.Keycode == Key.F9)
            {
                LoadGame(FactorySaveStore.ContinueSlotId, "Partita caricata (continua)");
                GetViewport().SetInputAsHandled();
                return;
            }

            if (key.Keycode == Key.F6)
            {
                SaveGame(FactorySaveStore.QuickSlotId, "Slot-1 salvato");
                GetViewport().SetInputAsHandled();
                return;
            }

            if (key.Keycode == Key.F7)
            {
                LoadGame(FactorySaveStore.QuickSlotId, "Slot-1 caricato");
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

                    _draggingPlace = _tool is BuildTool.Belt or BuildTool.BeltFast or BuildTool.BeltExpress
                        or BuildTool.Junction or BuildTool.Splitter or BuildTool.Sorter;
                    _dragCell = cell;
                    TryPlaceAt(cell);
                    GetViewport().SetInputAsHandled();
                }
                else
                {
                    _draggingPlace = false;
                }
            }
            else if (mouse.ButtonIndex == MouseButton.Right)
            {
                if (mouse.Pressed)
                {
                    // Nothing is sent yet: a click toggles on release, a drag marks once it moves.
                    _draggingRemove = true;
                    _removeMoved = false;
                    _removeStart = cell;
                    _dragCell = cell;
                    _removedThisDrag.Clear();
                    GetViewport().SetInputAsHandled();
                }
                else
                {
                    if (_draggingRemove && !_removeMoved)
                    {
                        TryRemoveAt(_removeStart, onlyMark: false);
                    }

                    _draggingRemove = false;
                    _removedThisDrag.Clear();
                }
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

            if (_draggingPlace && _tool is BuildTool.Belt or BuildTool.BeltFast or BuildTool.BeltExpress
                or BuildTool.Junction or BuildTool.Splitter or BuildTool.Sorter)
            {
                // A fast drag skips cells between two motion events: fill them in order.
                foreach (var step in DragCells(_dragCell, cell))
                {
                    TryPlaceAt(step);
                }

                _dragCell = cell;
            }
            else if (_draggingRemove && !cell.Equals(_dragCell))
            {
                if (!_removeMoved)
                {
                    _removeMoved = true;
                    RemoveOnceInDrag(_removeStart);
                }

                foreach (var step in DragCells(_dragCell, cell))
                {
                    RemoveOnceInDrag(step);
                }

                _dragCell = cell;
            }
        }
    }
}
