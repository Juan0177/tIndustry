using Godot;
using TIndustry.Shared;

namespace TIndustry.Godot;

/// <summary>
/// Game speed for every player: speed bar (pause · ×1 · ×10 · ×60 · +1h · +6h), Space = pause / resume,
/// placements made while paused kept as see-through ghosts and built after resume once affordable,
/// and time jumps run over several frames behind a progress bar.
/// </summary>
public partial class World
{
    /// <summary>Simulation time per frame spent on a time jump; the rest keeps the window responsive.</summary>
    private static readonly TimeSpan JumpFrameBudget = TimeSpan.FromMilliseconds(12);

    private static readonly Color PlanPausedEdge = new(0.55f, 0.78f, 1f, 0.95f);
    private static readonly Color PlanWaitingEdge = new(0.95f, 0.62f, 0.25f, 0.95f);

    private readonly GameSpeed _speed = new();
    private readonly BuildQueue _buildQueue = new();
    private readonly Dictionary<string, Texture2D?> _planTextures = [];
    private TimeJump? _jump;
    private bool _pausedBeforeJump;
    private SpeedBar? _speedBar;
    private Label? _speedStatus;
    private TimeJumpPanel? _jumpPanel;

    private bool Jumping => _jump is not null;

    /// <summary>Right after the HUD so full-screen panels (Ricerca, Mercato, Home…) draw over the bar.</summary>
    private void EnsureSpeedBar()
    {
        var layer = GetNode<CanvasLayer>("Hud");
        _speedBar = new SpeedBar { ShowConsoleButton = DevModeRequested };
        // Zero-size anchor point 12 px from the right edge; the bar grows leftwards from it.
        _speedBar.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopRight);
        _speedBar.OffsetLeft = _speedBar.OffsetRight = -12;
        _speedBar.OffsetTop = 8;
        _speedBar.SpeedChosen += speed =>
        {
            _speed.SetSpeed(speed);
            RefreshSpeedUi();
        };
        _speedBar.PauseToggled += () => TogglePause(toast: false);
        _speedBar.FastForwardRequested += StartTimeJump;
        _speedBar.ConsoleRequested += () => _devPanel?.Toggle();
        layer.AddChild(_speedBar);

        _speedStatus = new Label
        {
            Name = "SpeedStatus",
            MouseFilter = Control.MouseFilterEnum.Ignore,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        _speedStatus.SetAnchorsPreset(Control.LayoutPreset.TopRight);
        _speedStatus.OffsetLeft = -620;
        _speedStatus.OffsetRight = -14;
        _speedStatus.OffsetTop = 44;
        _speedStatus.OffsetBottom = 64;
        _speedStatus.AddThemeColorOverride("font_color", HudStyle.SlotSelected);
        _speedStatus.AddThemeColorOverride("font_outline_color", Colors.Black);
        _speedStatus.AddThemeConstantOverride("outline_size", 4);
        _speedStatus.AddThemeFontSizeOverride("font_size", 14);
        layer.AddChild(_speedStatus);
        RefreshSpeedUi();
    }

    private void EnsureTimeJumpPanel()
    {
        _jumpPanel = new TimeJumpPanel();
        GetNode<CanvasLayer>("Hud").AddChild(_jumpPanel);
    }

    private void TogglePause(bool toast = true)
    {
        _speed.TogglePause();
        RefreshSpeedUi();
        if (toast)
        {
            _hud?.ShowToast(_speed.Paused ? "Pausa (Spazio)" : "Ripresa");
        }
    }

    /// <summary>Pauses the game and runs <paramref name="seconds"/> of simulation over the next frames.</summary>
    private void StartTimeJump(int seconds)
    {
        if (_slice is null || Jumping || seconds <= 0)
        {
            return;
        }

        _jump = TimeJump.FromSeconds(seconds);
        _pausedBeforeJump = _speed.Paused;
        _speed.SetPaused(true);
        _devPanel?.Close();
        _draggingPlace = _draggingRemove = false;
        _jumpPanel?.Open(_jump);
        RefreshSpeedUi();
    }

    private void FinishTimeJump()
    {
        if (_jump is null)
        {
            return;
        }

        var total = _jump.TotalTicks;
        _jump = null;
        _jumpPanel?.Close();
        _speed.SetPaused(_pausedBeforeJump);
        RefreshSpeedUi();
        UpdateHud();
        CheckCampaignComplete();
        _hud?.ShowToast($"Avanti di {TimeJump.FormatGameTime(total)}");
    }

    /// <summary>Runs this frame's share of the simulation: a slice of the time jump, or the normal ticks.</summary>
    private void AdvanceSimulation(double delta)
    {
        if (_slice is null)
        {
            return;
        }

        if (_jump is not null)
        {
            _jump.Advance(_slice, JumpFrameBudget);
            _jumpPanel?.Refresh(_jump);
            if (_jump.IsComplete)
            {
                FinishTimeJump();
            }

            return;
        }

        var ticks = _speed.TicksForFrame(_clock, delta);
        for (var i = 0; i < ticks; i++)
        {
            _slice.Step();
        }

        if (!_speed.Paused && _buildQueue.Count > 0)
        {
            ProcessBuildQueue();
        }
    }

    /// <summary>After resume: build the planned ghosts the wallet covers, in the order they were placed.</summary>
    private void ProcessBuildQueue()
    {
        if (_slice is null)
        {
            return;
        }

        var result = _buildQueue.Process(
            _slice,
            plan => PlaceStructure((BuildTool)plan.Tool, plan.Origin, plan.Direction, plan.FilterItemId));
        if (result.Built > 0 || result.Dropped > 0)
        {
            UpdateHud();
            QueueRedraw();
        }

        if (result.Dropped > 0)
        {
            _hud?.ShowToast(result.Dropped == 1
                ? "1 costruzione annullata: cella occupata o non valida"
                : $"{result.Dropped} costruzioni annullate: celle occupate o non valide");
        }
    }

    /// <summary>While paused: remember the placement as a ghost instead of building it.</summary>
    private void PlanAt(GridPosition cell, string structureId, int costMultiplier)
    {
        if (_slice is null)
        {
            return;
        }

        var plan = new PlannedBuild(
            (int)_tool,
            structureId,
            cell,
            ToolSize(_tool),
            _placeDir,
            costMultiplier,
            _tool == BuildTool.Sorter ? _sorterFilterId : null);
        if (_buildQueue.TryAdd(plan, _slice))
        {
            QueueRedraw();
        }
        else if (!_draggingPlace)
        {
            _hud?.ShowToast("Cella occupata o già pianificata");
        }
    }

    /// <summary>New game, campaign level or load: normal speed, no ghosts, no jump in progress.</summary>
    private void OnSessionStarted()
    {
        _buildQueue.Clear();
        _jump = null;
        _jumpPanel?.Close();
        _speed.SetSpeed(1f);
        RefreshSpeedUi();
        QueueDevStartupCommands();
    }

    private void RefreshSpeedUi()
    {
        _speedBar?.Refresh(_speed.Speed, _speed.Paused && !Jumping);
        if (_speedStatus is null || _slice is null)
        {
            return;
        }

        if (Jumping)
        {
            // The progress card says it all; "Spazio per riprendere" would be wrong mid-jump.
            _speedStatus.Text = "";
            return;
        }

        var queue = _buildQueue.Count switch
        {
            0 => "",
            1 => " · 1 costruzione in coda",
            var n => $" · {n} costruzioni in coda"
        };
        _speedStatus.Text = DevStatusText() is { } dev
            ? dev + queue
            : _speed.Paused
                ? "In pausa · Spazio per riprendere" + queue
                : _buildQueue.Count > 0 ? $"In attesa di risorse{queue}" : "";
    }

    /// <summary>Speed bar and its status line only while a game is on screen (not behind Home).</summary>
    private void SyncSpeedBarVisibility()
    {
        var visible = _home?.IsOpen != true;
        if (_speedBar is not null)
        {
            _speedBar.Visible = visible;
        }

        if (_speedStatus is not null)
        {
            _speedStatus.Visible = visible;
        }
    }

    private static int ToolSize(BuildTool tool) => tool switch
    {
        BuildTool.Miner or BuildTool.MinerAdvanced => Miner.Size,
        BuildTool.Smelter or BuildTool.Assembler => Crafter.Size,
        BuildTool.Generator => Generator.Size,
        BuildTool.PowerNodeT2 => 2,
        BuildTool.Extractor => Extractor.Size,
        _ => 1
    };

    private static bool IsDirectional(BuildTool tool) =>
        tool is BuildTool.Belt or BuildTool.BeltFast or BuildTool.BeltExpress or BuildTool.Junction
            or BuildTool.Splitter or BuildTool.Sorter or BuildTool.Bridge or BuildTool.Extractor;

    /// <summary>
    /// Mindustry-style plans: the block's sprite at half opacity with a blue edge while paused,
    /// orange once running but still waiting for resources.
    /// </summary>
    private void DrawPlannedBuilds()
    {
        if (_slice is null)
        {
            return;
        }

        foreach (var plan in _buildQueue.Plans)
        {
            var rect = new Rect2(
                plan.Origin.X * TileSize, plan.Origin.Y * TileSize, plan.Size * TileSize, plan.Size * TileSize);
            DrawRect(rect, new Color(0.35f, 0.55f, 0.85f, 0.18f));
            if (PlanTexture(plan.StructureId) is { } tex)
            {
                DrawTextureRect(tex, rect.Grow(-TileSize * 0.08f), false, new Color(1, 1, 1, 0.5f));
            }

            var waiting = !_speed.Paused && !_slice.CanAffordStructure(plan.StructureId, plan.CostMultiplier);
            DrawRect(rect, waiting ? PlanWaitingEdge : PlanPausedEdge, false, 2f);
            if (IsDirectional((BuildTool)plan.Tool))
            {
                DrawDirectionHint(plan.Origin, plan.Direction);
            }
        }
    }

    private Texture2D? PlanTexture(string structureId)
    {
        if (_planTextures.TryGetValue(structureId, out var cached))
        {
            return cached;
        }

        var file = structureId switch
        {
            "conveyor-bridge" => "bridge",
            "conveyor-express" => "conveyor-fast",
            _ => structureId
        };
        var path = $"res://assets/{file}.png";
        var tex = ResourceLoader.Exists(path) ? GD.Load<Texture2D>(path) : null;
        _planTextures[structureId] = tex;
        return tex;
    }
}
