using Godot;
using TIndustry.Shared;

namespace TIndustry.Godot;

/// <summary>Build tool selection and mouse/keyboard input.</summary>
public partial class SpikeWorld
{
    private void OnHudToolChosen(FactoryHud.ToolKind kind)
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
            && _slice is not null
            && !_slice.IsStructureUnlocked(FactoryHud.StructureIdFor(ToHudTool(tool))))
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

    private static FactoryHud.ToolKind ToHudTool(BuildTool tool) => tool switch
    {
        BuildTool.Cursor => FactoryHud.ToolKind.Cursor,
        BuildTool.Miner => FactoryHud.ToolKind.Miner,
        BuildTool.MinerAdvanced => FactoryHud.ToolKind.MinerAdvanced,
        BuildTool.Smelter => FactoryHud.ToolKind.Smelter,
        BuildTool.Assembler => FactoryHud.ToolKind.Assembler,
        BuildTool.Junction => FactoryHud.ToolKind.Junction,
        BuildTool.Splitter => FactoryHud.ToolKind.Splitter,
        BuildTool.Generator => FactoryHud.ToolKind.Generator,
        BuildTool.Sorter => FactoryHud.ToolKind.Sorter,
        BuildTool.Bridge => FactoryHud.ToolKind.Bridge,
        BuildTool.Belt => FactoryHud.ToolKind.Belt,
        BuildTool.BeltFast => FactoryHud.ToolKind.BeltFast,
        BuildTool.BeltExpress => FactoryHud.ToolKind.BeltExpress,
        BuildTool.Extractor => FactoryHud.ToolKind.Extractor,
        BuildTool.PowerNode => FactoryHud.ToolKind.PowerNode,
        BuildTool.PowerNodeT2 => FactoryHud.ToolKind.PowerNodeT2,
        _ => FactoryHud.ToolKind.Cursor
    };

    private static BuildTool FromHudTool(FactoryHud.ToolKind tool) => tool switch
    {
        FactoryHud.ToolKind.Cursor => BuildTool.Cursor,
        FactoryHud.ToolKind.Miner => BuildTool.Miner,
        FactoryHud.ToolKind.MinerAdvanced => BuildTool.MinerAdvanced,
        FactoryHud.ToolKind.Smelter => BuildTool.Smelter,
        FactoryHud.ToolKind.Assembler => BuildTool.Assembler,
        FactoryHud.ToolKind.Junction => BuildTool.Junction,
        FactoryHud.ToolKind.Splitter => BuildTool.Splitter,
        FactoryHud.ToolKind.Generator => BuildTool.Generator,
        FactoryHud.ToolKind.Sorter => BuildTool.Sorter,
        FactoryHud.ToolKind.Bridge => BuildTool.Bridge,
        FactoryHud.ToolKind.Belt => BuildTool.Belt,
        FactoryHud.ToolKind.BeltFast => BuildTool.BeltFast,
        FactoryHud.ToolKind.BeltExpress => BuildTool.BeltExpress,
        FactoryHud.ToolKind.Extractor => BuildTool.Extractor,
        FactoryHud.ToolKind.PowerNode => BuildTool.PowerNode,
        FactoryHud.ToolKind.PowerNodeT2 => BuildTool.PowerNodeT2,
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

    public override void _UnhandledInput(InputEvent @event)
    {
        if (_tutorial?.IsOpen == true)
        {
            return;
        }

        if (_saves?.IsOpen == true)
        {
            return;
        }

        if (_settingsPanel?.IsOpen == true)
        {
            return;
        }

        if (_home?.IsOpen == true)
        {
            return;
        }

        if (_slice is null)
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

            if (key.Keycode == Key.Key4 || key.Keycode == Key.A)
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

            if (key.Keycode == Key.Key7 || key.Keycode == Key.G)
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
                SaveSlice(FactorySliceSaveStore.ContinueSlotId, "Partita salvata (continua)");
                GetViewport().SetInputAsHandled();
                return;
            }

            if (key.Keycode == Key.F9)
            {
                LoadSlice(FactorySliceSaveStore.ContinueSlotId, "Partita caricata (continua)");
                GetViewport().SetInputAsHandled();
                return;
            }

            if (key.Keycode == Key.F6)
            {
                SaveSlice(FactorySliceSaveStore.QuickSlotId, "Slot-1 salvato");
                GetViewport().SetInputAsHandled();
                return;
            }

            if (key.Keycode == Key.F7)
            {
                LoadSlice(FactorySliceSaveStore.QuickSlotId, "Slot-1 caricato");
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
                        // Cursor mode: no place — leave click for camera pan / look.
                        return;
                    }

                    _draggingPlace = _tool is BuildTool.Belt or BuildTool.BeltFast or BuildTool.BeltExpress
                        or BuildTool.Junction or BuildTool.Splitter or BuildTool.Sorter;
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
                    _draggingRemove = true;
                    TryRemoveAt(cell);
                    GetViewport().SetInputAsHandled();
                }
                else
                {
                    _draggingRemove = false;
                }
            }
        }
        else if (@event is InputEventMouseMotion motion)
        {
            var cell = ScreenToCell(motion.Position);
            if (!_hover.Equals(cell))
            {
                _hover = cell;
                QueueRedraw();
            }

            if (_draggingPlace && _tool is BuildTool.Belt or BuildTool.BeltFast or BuildTool.BeltExpress
                or BuildTool.Junction or BuildTool.Splitter or BuildTool.Sorter)
            {
                TryPlaceAt(cell);
            }
            else if (_draggingRemove)
            {
                TryRemoveAt(cell);
            }
        }
    }
}
