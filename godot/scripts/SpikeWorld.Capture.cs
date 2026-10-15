using Godot;
using TIndustry.Shared;

namespace TIndustry.Godot;

/// <summary>Dev-only screenshot capture modes (TINDUSTRY_CAPTURE) and belt pixel assertions. Not used in normal play.</summary>
public partial class SpikeWorld
{
    private async Task CaptureItemColorsShotsAsync(string destDir)
    {
        StartNewSandbox(toast: false);
        _home?.Close();
        _settings.ShowResourceOverlay = false;
        SyncFpsOverlay();
        SyncResourceOverlay();
        SyncCameraMapBounds();

        // Seed belt cargo of every ore + plate/wire so cargo tints are visible.
        if (_slice is not null)
        {
            _slice.Research.ForceUnlock("conveyor-basic");
            _slice.Research.ForceUnlock("sorter");
            _slice.EnsureDemoBuildStock(ironPlates: 40, copperWire: 20, copperOre: 8);

            var core = new GridPosition(
                _slice.CoreTiles.Min(t => t.X),
                _slice.CoreTiles.Min(t => t.Y));
            var beltStart = new GridPosition(core.X - 6, core.Y + 4);
            for (var i = 0; i < 8; i++)
            {
                var pos = new GridPosition(beltStart.X + i, beltStart.Y);
                if (i == 3)
                {
                    _slice.TryPlaceSorter(pos, Direction.East, "copper-ore");
                }
                else
                {
                    _slice.TryPlaceBelt(pos, Direction.East);
                }
            }

            _hud?.SetSorterFilterBrush("copper-ore");

            string[] cargo =
            [
                "iron-ore", "copper-ore", "coal", "lead-ore", "titanium-ore",
                "iron-plate", "copper-wire"
            ];
            long nextId = 9001;
            for (var i = 0; i < cargo.Length; i++)
            {
                var cell = new GridPosition(beltStart.X + i, beltStart.Y);
                _slice.Belts.TryInsert(
                    cell,
                    new TransportedItem(nextId++, cargo[i], progress: 0.35f + i * 0.02f));
            }

            RebuildBeltVisual();
            SyncItemSprites();
        }

        // Frame starter deposit ring + cargo belt south of core.
        FocusCameraOnCore();
        if (HasNode("Camera"))
        {
            var cam = GetNode<Camera2D>("Camera");
            cam.Zoom = new Vector2(0.55f, 0.55f);
            cam.Position += new Vector2(-2f * TileSize, 3f * TileSize);
            if (cam is SpikeCamera spike)
            {
                spike.ClampToMap();
            }
        }

        QueueRedraw();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree().CreateTimer(0.55), SceneTreeTimer.SignalName.Timeout);

        void SaveBoth(string name)
        {
            var img = GetViewport().GetTexture().GetImage();
            img.SavePng(Path.Combine(destDir, name));
            GD.Print($"Saved {name}");
        }

        SaveBoth("godot-item-colors-world.png");

        // Zoom in on stock HUD (top-right core panel) — four tinted icons.
        if (HasNode("Camera"))
        {
            var cam = GetNode<Camera2D>("Camera");
            cam.Zoom = new Vector2(0.35f, 0.35f);
            FocusCameraOnCore();
        }

        QueueRedraw();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree().CreateTimer(0.4), SceneTreeTimer.SignalName.Timeout);
        SaveBoth("godot-item-colors-hud.png");

        GD.Print("Item color screenshot set complete.");
        GetTree().Quit();
    }

    private async Task CaptureRaylibWorldParityShotsAsync(string destDir)
    {
        // Ensure sandbox world with terrain + centered core (boot already does this).
        StartNewSandbox(toast: false);
        _home?.Close();
        _settings.ShowResourceOverlay = false;
        SyncFpsOverlay();
        SyncResourceOverlay();
        SyncCameraMapBounds();
        FocusCameraOnCore();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree().CreateTimer(0.6), SceneTreeTimer.SignalName.Timeout);

        void SaveBoth(string name)
        {
            var img = GetViewport().GetTexture().GetImage();
            img.SavePng(Path.Combine(destDir, name));
            GD.Print($"Saved {name} → {destDir}");
        }

        // Core centered + nearby starter minerals.
        SaveBoth("godot-raylib-world-parity-core.png");

        // Pan slightly west to frame starter iron/copper patches + icons.
        if (HasNode("Camera"))
        {
            var cam = GetNode<Camera2D>("Camera");
            cam.Position += new Vector2(-4f * TileSize, 0);
            if (cam is SpikeCamera spike)
            {
                spike.ClampToMap();
            }
        }

        QueueRedraw();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree().CreateTimer(0.35), SceneTreeTimer.SignalName.Timeout);
        SaveBoth("godot-raylib-world-parity-ores.png");

        // Zoom out and pin to NW corner so the world AABB border is in frame.
        if (HasNode("Camera"))
        {
            var cam = GetNode<Camera2D>("Camera");
            cam.Zoom = new Vector2(0.22f, 0.22f);
            cam.Position = new Vector2(14f * TileSize, 10f * TileSize);
            if (cam is SpikeCamera spike)
            {
                spike.ClampToMap();
            }
        }

        QueueRedraw();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree().CreateTimer(0.35), SceneTreeTimer.SignalName.Timeout);
        SaveBoth("godot-raylib-world-parity-border.png");

        GD.Print("Raylib world parity screenshot set complete.");
        GetTree().Quit();
    }

    private async Task CaptureHomeShotsAsync(string destDir)
    {
        _home?.Open();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree().CreateTimer(0.5), SceneTreeTimer.SignalName.Timeout);
        var splash = GetViewport().GetTexture().GetImage();
        splash.SavePng(Path.Combine(destDir, "godot-port-home-splash.png"));
        GD.Print($"Saved home splash → {destDir}");

        _home?.Close();
        _returnHomeAfterCampaign = true;
        if (_campaign is not null && _progress is not null)
        {
            _campaignSelect?.Open(_campaign, _progress);
        }

        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree().CreateTimer(0.45), SceneTreeTimer.SignalName.Timeout);
        var campagna = GetViewport().GetTexture().GetImage();
        campagna.SavePng(Path.Combine(destDir, "godot-port-home-campagna.png"));

        _returnHomeAfterCampaign = false;
        _campaignSelect?.Close();
        StartNewSandbox(toast: false);
        _home?.Close();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree().CreateTimer(0.4), SceneTreeTimer.SignalName.Timeout);
        var sandbox = GetViewport().GetTexture().GetImage();
        sandbox.SavePng(Path.Combine(destDir, "godot-port-home-sandbox.png"));

        GD.Print("Home screenshot set complete.");
        GetTree().Quit();
    }

    private async Task CaptureMapSeedShotsAsync(string destDir)
    {
        _home?.Close();
        if (_campaign?.FirstLevel is { } l01)
        {
            StartCampaignLevel(l01, toast: false);
        }

        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree().CreateTimer(0.5), SceneTreeTimer.SignalName.Timeout);
        var campaign = GetViewport().GetTexture().GetImage();
        campaign.SavePng(Path.Combine(destDir, "godot-port-mapseed-campaign.png"));

        StartNewSandbox(toast: false);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree().CreateTimer(0.45), SceneTreeTimer.SignalName.Timeout);
        var sandbox = GetViewport().GetTexture().GetImage();
        sandbox.SavePng(Path.Combine(destDir, "godot-port-mapseed-sandbox.png"));

        // Second sandbox seed for contrast (swap via CreateSandboxSlice).
        var content = FactoryContent.Load(_contentPath);
        var core = FactorySlice.CenteredCoreOrigin(DefaultMapWidth, DefaultMapHeight);
        _slice = FactorySlice.CreateSandboxSlice(
            content, core, mapWidth: DefaultMapWidth, mapHeight: DefaultMapHeight, seed: 777);
        QueueRedraw();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree().CreateTimer(0.4), SceneTreeTimer.SignalName.Timeout);
        var alt = GetViewport().GetTexture().GetImage();
        alt.SavePng(Path.Combine(destDir, "godot-port-mapseed-alt.png"));

        GD.Print($"Map-seed shots · campaign seed={_campaign?.FirstLevel?.Seed} sandbox=42 alt=777");
        GetTree().Quit();
    }

    private async Task CaptureCampaignShotsAsync(string destDir)
    {
        if (_slice is null || _hud is null || _campaign is null || _progress is null)
        {
            return;
        }

        _campaignSelect?.Open(_campaign, _progress);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree().CreateTimer(0.5), SceneTreeTimer.SignalName.Timeout);
        var selectShot = GetViewport().GetTexture().GetImage();
        selectShot.SavePng(Path.Combine(destDir, "godot-port-campaign-select.png"));
        GD.Print($"Saved campaign select → {destDir}");

        _campaignSelect?.Close();
        if (_campaign.FirstLevel is { } l01)
        {
            StartCampaignLevel(l01, toast: false);
        }

        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree().CreateTimer(0.45), SceneTreeTimer.SignalName.Timeout);
        var objShot = GetViewport().GetTexture().GetImage();
        objShot.SavePng(Path.Combine(destDir, "godot-port-campaign-objectives.png"));

        // Progress: sell toward L01 objectives.
        while (_slice.Wallet.MaterialCount("iron-ore") > 0
               && _activeLevel is not null
               && !CampaignProgress.AreAllObjectivesComplete(
                   _activeLevel, _slice.Wallet, _slice.Session, _slice.Research))
        {
            if (!_slice.TrySellFromWallet("iron-ore", 1))
            {
                break;
            }
        }

        CheckCampaignComplete();
        UpdateHud();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree().CreateTimer(0.4), SceneTreeTimer.SignalName.Timeout);
        var progressShot = GetViewport().GetTexture().GetImage();
        progressShot.SavePng(Path.Combine(destDir, "godot-port-campaign-progress.png"));

        _campaignSelect?.Open(_campaign, _progress);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree().CreateTimer(0.4), SceneTreeTimer.SignalName.Timeout);
        var unlockedShot = GetViewport().GetTexture().GetImage();
        unlockedShot.SavePng(Path.Combine(destDir, "godot-port-campaign-l02-unlocked.png"));

        GD.Print("Campaign screenshot set complete.");
        GetTree().Quit();
    }

    private async Task SavePortScreenshotsAsync()
    {
        var destDir = CaptureDirectory();

        if (destDir is null || _slice is null || _hud is null)
        {
            GD.PushWarning("Nessuna cartella screenshot / hud null.");
            return;
        }

        var cam = HasNode("Camera") ? GetNode<Camera2D>("Camera") : null;
        if (cam is not null)
        {
            cam.Position = new Vector2(8f * TileSize, 8f * TileSize);
            cam.Zoom = new Vector2(0.65f, 0.65f);
        }

        ClearToCursor(toast: false);
        SyncResearchLocks();
        UpdateHud();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree().CreateTimer(0.5), SceneTreeTimer.SignalName.Timeout);

        if (OS.GetEnvironment("TINDUSTRY_CAPTURE_MODE") == "research")
        {
            await CaptureResearchShotsAsync(destDir);
            return;
        }

        if (OS.GetEnvironment("TINDUSTRY_CAPTURE_MODE") == "raylib-world")
        {
            await CaptureRaylibWorldParityShotsAsync(destDir);
            return;
        }

        if (OS.GetEnvironment("TINDUSTRY_CAPTURE_MODE") == "item-colors")
        {
            await CaptureItemColorsShotsAsync(destDir);
            return;
        }

        if (OS.GetEnvironment("TINDUSTRY_CAPTURE_MODE") == "campaign")
        {
            await CaptureCampaignShotsAsync(destDir);
            return;
        }

        if (OS.GetEnvironment("TINDUSTRY_CAPTURE_MODE") == "mapseed")
        {
            await CaptureMapSeedShotsAsync(destDir);
            return;
        }

        if (OS.GetEnvironment("TINDUSTRY_CAPTURE_MODE") == "mindustry-ui")
        {
            await CaptureMindustryUiShotsAsync(destDir);
            return;
        }

        if (OS.GetEnvironment("TINDUSTRY_CAPTURE_MODE") == "home")
        {
            await CaptureHomeShotsAsync(destDir);
            return;
        }

        if (OS.GetEnvironment("TINDUSTRY_CAPTURE_MODE") == "autosell")
        {
            await CaptureAutoSellShotsAsync(destDir);
            return;
        }

        if (OS.GetEnvironment("TINDUSTRY_CAPTURE_MODE") == "t2")
        {
            await CaptureT2ShotsAsync(destDir);
            return;
        }

        if (OS.GetEnvironment("TINDUSTRY_CAPTURE_MODE") == "structures")
        {
            await CaptureStructuresShotsAsync(destDir);
            return;
        }

        if (OS.GetEnvironment("TINDUSTRY_CAPTURE_MODE") == "belt-t2")
        {
            await CaptureBeltT2ChevronShotsAsync(destDir);
            return;
        }

        if (OS.GetEnvironment("TINDUSTRY_CAPTURE_MODE") == "logistics-redesign")
        {
            await CaptureLogisticsRedesignShotsAsync(destDir);
            return;
        }

        if (OS.GetEnvironment("TINDUSTRY_CAPTURE_MODE") is "miner-gears" or "miner-gears-fill"
            or "miner-center-gear" or "miner-real-gear")
        {
            await CaptureMinerGearsShotsAsync(destDir);
            return;
        }

        if (OS.GetEnvironment("TINDUSTRY_CAPTURE_MODE") == "prod-power-core")
        {
            await CaptureProdPowerCoreShotsAsync(destDir);
            return;
        }

        // Default / mercato: Core $ HUD + Mercato sell loop.
        var coreShot = GetViewport().GetTexture().GetImage();
        coreShot.SavePng(Path.Combine(destDir, "godot-port-mercato-core-money.png"));
        GD.Print($"Saved mercato core-money → {destDir}");

        _mercato?.Open(_slice);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree().CreateTimer(0.5), SceneTreeTimer.SignalName.Timeout);
        var panelShot = GetViewport().GetTexture().GetImage();
        panelShot.SavePng(Path.Combine(destDir, "godot-port-mercato-panel.png"));

        var beforeMoney = _slice.Wallet.Money;
        if (!_slice.TrySellFromWallet("iron-ore", 3))
        {
            GD.PushError("Capture sell iron-ore failed");
        }
        else
        {
            _hud.ShowToast($"Venduti 3 ore → +${_slice.Wallet.Money - beforeMoney}");
        }

        _mercato?.Refresh();
        UpdateHud();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree().CreateTimer(0.45), SceneTreeTimer.SignalName.Timeout);
        var sellShot = GetViewport().GetTexture().GetImage();
        sellShot.SavePng(Path.Combine(destDir, "godot-port-mercato-sell.png"));

        SaveSlice(FactorySliceSaveStore.ContinueSlotId, "Partita salvata (continua)");
        LoadSlice(FactorySliceSaveStore.ContinueSlotId, "Partita caricata (continua)");
        if (_slice.Session.SaleIncome <= 0)
        {
            GD.PushError("capture restore sale income");
        }

        _mercato?.Open(_slice);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree().CreateTimer(0.4), SceneTreeTimer.SignalName.Timeout);
        var roundtrip = GetViewport().GetTexture().GetImage();
        roundtrip.SavePng(Path.Combine(destDir, "godot-port-mercato-save-roundtrip.png"));

        GD.Print("Mercato screenshot set complete.");
        GetTree().Quit();
    }

    private async Task CaptureLogisticsRedesignShotsAsync(string destDir)
    {
        if (_slice is null || _hud is null)
        {
            return;
        }

        _home?.Close();
        foreach (var id in ResearchState.GodotSliceStructureIds)
        {
            _slice.Research.ForceUnlock(id);
        }

        _slice.EnsureDemoBuildStock();
        SyncResearchLocks();

        // Clear a quiet row for showcase.
        for (var x = 2; x <= 14; x++)
        {
            for (var y = 2; y <= 5; y++)
            {
                _slice.TryRemoveBelt(new GridPosition(x, y));
            }
        }

        // Splitter + sorter (with iron-plate filter) + bridge span 3.
        _slice.TryPlaceSplitter(new GridPosition(3, 3), Direction.East);
        _slice.TryPlaceSorter(new GridPosition(5, 3), Direction.East, "iron-plate");
        _slice.TryPlaceBridge(new GridPosition(8, 3), Direction.East); // shortest free → span 2 or more
        // Force longer span for animation: block span-2 exit then re-place.
        _slice.TryRemoveBelt(new GridPosition(8, 3));
        _slice.TryPlaceBelt(new GridPosition(10, 3), Direction.South, "conveyor-basic");
        _slice.TryPlaceBelt(new GridPosition(11, 3), Direction.South, "conveyor-basic");
        _slice.TryPlaceBridge(new GridPosition(8, 3), Direction.East); // → (12,3) span 4

        // Side output demo belts near bridge end.
        _slice.TryPlaceBelt(new GridPosition(12, 2), Direction.North, "conveyor-basic");
        _slice.TryPlaceBelt(new GridPosition(7, 3), Direction.East, "conveyor-basic");

        _hud.SetSorterFilterBrush("iron-plate");
        _visualDirty = true;
        RebuildBeltVisual();
        UpdateHud();

        if (HasNode("Camera"))
        {
            var cam = GetNode<Camera2D>("Camera");
            cam.Position = new Vector2(8f * TileSize, 4f * TileSize);
            cam.Zoom = new Vector2(1.05f, 1.05f);
        }

        _hud.SetSelectedTool(FactoryHud.ToolKind.Splitter);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree().CreateTimer(0.55), SceneTreeTimer.SignalName.Timeout);
        var world = GetViewport().GetTexture().GetImage();
        world.SavePng(Path.Combine(destDir, "godot-port-logistics-redesign-world.png"));

        // Hold a few frames so bridge arrows animate through a cycle.
        await ToSignal(GetTree().CreateTimer(1.0), SceneTreeTimer.SignalName.Timeout);
        var anim = GetViewport().GetTexture().GetImage();
        anim.SavePng(Path.Combine(destDir, "godot-port-logistics-redesign-bridge-anim.png"));

        _hud.SetSelectedTool(FactoryHud.ToolKind.Sorter);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree().CreateTimer(0.35), SceneTreeTimer.SignalName.Timeout);
        var palette = GetViewport().GetTexture().GetImage();
        palette.SavePng(Path.Combine(destDir, "godot-port-logistics-redesign-palette.png"));

        GD.Print("Logistics redesign screenshot set complete.");
        GetTree().Quit();
    }

    private async Task CaptureMinerGearsShotsAsync(string destDir)
    {
        if (_slice is null || _hud is null)
        {
            return;
        }

        _home?.Close();
        foreach (var id in ResearchState.GodotSliceStructureIds)
        {
            _slice.Research.ForceUnlock(id);
        }

        _slice.EnsureDemoBuildStock();
        SyncResearchLocks();

        // Quiet showcase row: T1 miner | T2 miner | T2 + generator (boosted).
        for (var x = 2; x <= 16; x++)
        {
            for (var y = 2; y <= 8; y++)
            {
                _slice.TryRemoveBuildingAt(new GridPosition(x, y));
                _slice.TryRemoveBelt(new GridPosition(x, y));
            }
        }

        // T1 on iron deposit area if any; otherwise still shows pad+gears (idle if no deposit).
        _slice.TryPlaceMiner(new GridPosition(3, 4), Direction.East);
        _slice.TryPlaceMiner(new GridPosition(7, 4), Direction.East, definitionId: MinerProducer.AdvancedId);
        _slice.TryPlaceMiner(new GridPosition(12, 4), Direction.East, definitionId: MinerProducer.AdvancedId);
        _slice.TryPlaceGenerator(new GridPosition(12, 2), Direction.East);
        // Fuel the gen so T2 lights perno.
        if (_slice.Generators.Count > 0)
        {
            _slice.Generators[0].TryAcceptFuel("coal");
            _slice.Generators[0].TryAcceptFuel("coal");
            _slice.Generators[0].TryAcceptFuel("coal");
        }

        // Outward belts so miners can eject (keeps Progress cycling while working).
        for (var x = 3; x <= 14; x++)
        {
            _slice.TryPlaceBelt(new GridPosition(x, 6), Direction.East, "conveyor-basic");
        }

        _visualDirty = true;
        _buildingsDirty = true;
        RebuildBeltVisual();
        RebuildBuildingVisuals();
        UpdateHud();

        if (HasNode("Camera"))
        {
            var cam = GetNode<Camera2D>("Camera");
            cam.Position = new Vector2(9f * TileSize, 5f * TileSize);
            cam.Zoom = new Vector2(1.0f, 1.0f);
        }

        // Tick a bit so gen is live and miners progress.
        for (var i = 0; i < 20; i++)
        {
            _slice.Tick(1f / 30f);
        }

        RebuildBuildingVisuals();
        _hud.SetSelectedTool(FactoryHud.ToolKind.Miner);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree().CreateTimer(0.4), SceneTreeTimer.SignalName.Timeout);

        var prefix = OS.GetEnvironment("TINDUSTRY_CAPTURE_MODE") switch
        {
            "miner-real-gear" => "godot-port-miner-real-gear",
            "miner-center-gear" => "godot-port-miner-center-gear",
            "miner-gears-fill" => "godot-port-miner-gears-fill",
            _ => "godot-port-miner-gears"
        };

        var world = GetViewport().GetTexture().GetImage();
        world.SavePng(Path.Combine(destDir, $"{prefix}-world.png"));

        // Close-ups: camera on T1
        if (HasNode("Camera"))
        {
            var cam = GetNode<Camera2D>("Camera");
            cam.Position = new Vector2(4f * TileSize, 5f * TileSize);
            cam.Zoom = new Vector2(1.6f, 1.6f);
        }

        await ToSignal(GetTree().CreateTimer(0.35), SceneTreeTimer.SignalName.Timeout);
        var t1 = GetViewport().GetTexture().GetImage();
        t1.SavePng(Path.Combine(destDir, $"{prefix}-t1.png"));

        // T2 isolated (middle)
        if (HasNode("Camera"))
        {
            var cam = GetNode<Camera2D>("Camera");
            cam.Position = new Vector2(8f * TileSize, 5f * TileSize);
        }

        await ToSignal(GetTree().CreateTimer(0.35), SceneTreeTimer.SignalName.Timeout);
        var t2 = GetViewport().GetTexture().GetImage();
        t2.SavePng(Path.Combine(destDir, $"{prefix}-t2.png"));

        // T2 boosted (lit perno) — right miner + gen
        if (HasNode("Camera"))
        {
            var cam = GetNode<Camera2D>("Camera");
            cam.Position = new Vector2(13f * TileSize, 4.5f * TileSize);
        }

        // Ensure gen still burning.
        if (_slice.Generators.Count > 0)
        {
            _slice.Generators[0].TryAcceptFuel("coal");
        }

        for (var i = 0; i < 10; i++)
        {
            _slice.Tick(1f / 30f);
        }

        RebuildBuildingVisuals();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree().CreateTimer(0.45), SceneTreeTimer.SignalName.Timeout);
        var boosted = GetViewport().GetTexture().GetImage();
        boosted.SavePng(Path.Combine(destDir, $"{prefix}-t2-boosted.png"));

        // Multi-frame spin while working (hold on boosted T2).
        for (var frame = 0; frame < 4; frame++)
        {
            await ToSignal(GetTree().CreateTimer(0.2), SceneTreeTimer.SignalName.Timeout);
            var spin = GetViewport().GetTexture().GetImage();
            var name = $"{prefix}-spin-{frame}.png";
            spin.SavePng(Path.Combine(destDir, name));
        }

        // Palette with miner selected (static composite art).
        if (HasNode("Camera"))
        {
            var cam = GetNode<Camera2D>("Camera");
            cam.Position = new Vector2(9f * TileSize, 5f * TileSize);
            cam.Zoom = new Vector2(0.85f, 0.85f);
        }

        _hud.SetSelectedTool(FactoryHud.ToolKind.MinerAdvanced);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree().CreateTimer(0.35), SceneTreeTimer.SignalName.Timeout);
        var palette = GetViewport().GetTexture().GetImage();
        palette.SavePng(Path.Combine(destDir, $"{prefix}-palette.png"));

        GD.Print($"Miner gears screenshot set complete ({prefix}).");
        GetTree().Quit();
    }

    private async Task CaptureProdPowerCoreShotsAsync(string destDir)
    {
        if (_slice is null || _hud is null)
        {
            return;
        }

        const string prefix = "godot-port-prod-power-core";
        _home?.Close();
        foreach (var id in ResearchState.GodotSliceStructureIds)
        {
            _slice.Research.ForceUnlock(id);
        }

        _slice.EnsureDemoBuildStock();
        SyncResearchLocks();

        // Quiet showcase grid for production / power / core.
        for (var x = 2; x <= 18; x++)
        {
            for (var y = 2; y <= 12; y++)
            {
                _slice.TryRemoveBuildingAt(new GridPosition(x, y));
                _slice.TryRemoveBelt(new GridPosition(x, y));
            }
        }

        // Row 1: extractor idle-grey | extractor copper | forno crafting | assembler crafting
        _slice.TryPlaceExtractor(new GridPosition(3, 3), Direction.East, "iron-ore");
        // Clear filter to show idle grey (SetFilterItem rejects empty — place then override via restore).
        if (_slice.Extractors.Count > 0)
        {
            // Keep one with iron-ore; second with copper for recolor proof.
        }

        _slice.TryPlaceExtractor(new GridPosition(5, 3), Direction.East, "copper-ore");
        _slice.TryPlaceSmelter(new GridPosition(8, 3), Direction.East);
        _slice.TryPlaceAssembler(new GridPosition(12, 3), Direction.East);
        // Row 2: generator burning | core already at demo footprint — place gen + leave core visible
        _slice.TryPlaceGenerator(new GridPosition(3, 7), Direction.East);

        // Force craft states for animation.
        foreach (var sm in _slice.Smelters)
        {
            sm.RestoreCraftState(0.4f, isCrafting: true, null, null, 0);
        }

        foreach (var asm in _slice.Assemblers)
        {
            asm.RestoreCraftState(0.35f, isCrafting: true, null, null, 0);
        }

        if (_slice.Generators.Count > 0)
        {
            _slice.Generators[0].TryAcceptFuel("coal");
            _slice.Generators[0].TryAcceptFuel("coal");
            _slice.Generators[0].TryAcceptFuel("coal");
        }

        _visualDirty = true;
        _buildingsDirty = true;
        RebuildBeltVisual();
        RebuildBuildingVisuals();
        UpdateHud();

        // Kick gen into burning.
        for (var i = 0; i < 8; i++)
        {
            _slice.Tick(1f / 30f);
        }

        // Keep craft flags (Tick may complete without inputs).
        foreach (var sm in _slice.Smelters)
        {
            sm.RestoreCraftState(0.45f, isCrafting: true, null, null, 0);
        }

        foreach (var asm in _slice.Assemblers)
        {
            asm.RestoreCraftState(0.4f, isCrafting: true, null, null, 0);
        }

        if (HasNode("Camera"))
        {
            var cam = GetNode<Camera2D>("Camera");
            cam.Position = new Vector2(10f * TileSize, 7f * TileSize);
            cam.Zoom = new Vector2(0.75f, 0.75f);
        }

        _hud.SetSelectedTool(FactoryHud.ToolKind.Smelter);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree().CreateTimer(0.45), SceneTreeTimer.SignalName.Timeout);

        var map = GetViewport().GetTexture().GetImage();
        map.SavePng(Path.Combine(destDir, $"{prefix}-map.png"));

        // Palette with production tool selected.
        await ToSignal(GetTree().CreateTimer(0.25), SceneTreeTimer.SignalName.Timeout);
        var palette = GetViewport().GetTexture().GetImage();
        palette.SavePng(Path.Combine(destDir, $"{prefix}-palette.png"));

        // Isolated close-ups — zoomed hard, camera on footprint centers.
        await CaptureCloseAsync(destDir, prefix, "extractor", 3.5f, 3.5f, 2.6f);
        await CaptureCloseAsync(destDir, prefix, "extractor-copper", 5.5f, 3.5f, 2.6f);

        // Keep craft alive across waits (Tick would finish Progress).
        await HoldCraftAndCaptureAsync(destDir, prefix, "smelter", 9f, 4f, 2.2f, smelter: true);
        await HoldCraftAndCaptureAsync(destDir, prefix, "assembler", 13f, 4f, 2.2f, smelter: false);

        if (_slice.Generators.Count > 0)
        {
            _slice.Generators[0].TryAcceptFuel("coal");
            _slice.Tick(1f / 30f);
        }

        await CaptureCloseAsync(destDir, prefix, "generator", 4f, 8f, 2.2f);

        // Core (demo footprint ~9,14 size 2)
        if (HasNode("Camera"))
        {
            var cam = GetNode<Camera2D>("Camera");
            cam.Position = new Vector2(10f * TileSize, 15f * TileSize);
            cam.Zoom = new Vector2(1.5f, 1.5f);
        }

        await ToSignal(GetTree().CreateTimer(0.35), SceneTreeTimer.SignalName.Timeout);
        var core = GetViewport().GetTexture().GetImage();
        core.SavePng(Path.Combine(destDir, $"{prefix}-core.png"));

        // Active anim frames — smelter heat pulse
        if (HasNode("Camera"))
        {
            var cam = GetNode<Camera2D>("Camera");
            cam.Position = new Vector2(9f * TileSize, 4f * TileSize);
            cam.Zoom = new Vector2(2.2f, 2.2f);
        }

        for (var frame = 0; frame < 4; frame++)
        {
            for (var i = 0; i < 8; i++)
            {
                foreach (var sm in _slice.Smelters)
                {
                    sm.RestoreCraftState(0.2f, isCrafting: true, null, null, 0);
                }

                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            }

            var shot = GetViewport().GetTexture().GetImage();
            var name = $"{prefix}-smelter-anim-{frame}.png";
            shot.SavePng(Path.Combine(destDir, name));
        }

        // Assembler press
        if (HasNode("Camera"))
        {
            var cam = GetNode<Camera2D>("Camera");
            cam.Position = new Vector2(13f * TileSize, 4f * TileSize);
            cam.Zoom = new Vector2(2.2f, 2.2f);
        }

        foreach (var asm in _slice.Assemblers)
        {
            asm.RestoreCraftState(0f, isCrafting: false, null, null, 0);
        }

        for (var i = 0; i < 20; i++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }

        var asmIdle = GetViewport().GetTexture().GetImage();
        asmIdle.SavePng(Path.Combine(destDir, $"{prefix}-assembler-idle.png"));

        for (var frame = 0; frame < 4; frame++)
        {
            for (var i = 0; i < 6; i++)
            {
                foreach (var asm in _slice.Assemblers)
                {
                    asm.RestoreCraftState(0.15f, isCrafting: true, null, null, 0);
                }

                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            }

            var shot = GetViewport().GetTexture().GetImage();
            var name = $"{prefix}-assembler-anim-{frame}.png";
            shot.SavePng(Path.Combine(destDir, name));
        }

        // Generator orbit while burning
        if (HasNode("Camera"))
        {
            var cam = GetNode<Camera2D>("Camera");
            cam.Position = new Vector2(4f * TileSize, 8f * TileSize);
        }

        if (_slice.Generators.Count > 0)
        {
            _slice.Generators[0].TryAcceptFuel("coal");
            _slice.Generators[0].TryAcceptFuel("coal");
        }

        for (var i = 0; i < 6; i++)
        {
            _slice.Tick(1f / 30f);
        }

        for (var frame = 0; frame < 4; frame++)
        {
            for (var i = 0; i < 4; i++)
            {
                _slice.Tick(1f / 30f);
            }

            await ToSignal(GetTree().CreateTimer(0.2), SceneTreeTimer.SignalName.Timeout);
            var shot = GetViewport().GetTexture().GetImage();
            var name = $"{prefix}-generator-orbit-{frame}.png";
            shot.SavePng(Path.Combine(destDir, name));
        }

        // Burn out so dots freeze (no rebuild — Sync freezes angle).
        if (_slice.Generators.Count > 0)
        {
            var gen = _slice.Generators[0];
            gen.RestoreFuel(0, 0f, gen.FuelConsumed);
        }

        await ToSignal(GetTree().CreateTimer(0.35), SceneTreeTimer.SignalName.Timeout);
        var genStopped = GetViewport().GetTexture().GetImage();
        genStopped.SavePng(Path.Combine(destDir, $"{prefix}-generator-stopped.png"));

        // New fuel → orbit restarts from angle 0
        if (_slice.Generators.Count > 0)
        {
            _slice.Generators[0].TryAcceptFuel("coal");
            _slice.Tick(1f / 30f);
        }

        await ToSignal(GetTree().CreateTimer(0.35), SceneTreeTimer.SignalName.Timeout);
        var genRestart = GetViewport().GetTexture().GetImage();
        genRestart.SavePng(Path.Combine(destDir, $"{prefix}-generator-restart.png"));

        // Extractor recolor is static — already captured. One more anim hold for glow.
        if (HasNode("Camera"))
        {
            var cam = GetNode<Camera2D>("Camera");
            cam.Position = new Vector2(4f, 3.5f) * TileSize;
            cam.Zoom = new Vector2(2.0f, 2.0f);
        }

        await ToSignal(GetTree().CreateTimer(0.3), SceneTreeTimer.SignalName.Timeout);
        var exClose = GetViewport().GetTexture().GetImage();
        exClose.SavePng(Path.Combine(destDir, $"{prefix}-extractor-iron.png"));

        GD.Print("Prod/power/core screenshot set complete.");
        GetTree().Quit();
    }

    private async Task CaptureCloseAsync(
        string destDir,
        string prefix,
        string label,
        float camX,
        float camY,
        float zoom)
    {
        if (HasNode("Camera"))
        {
            var cam = GetNode<Camera2D>("Camera");
            cam.Position = new Vector2(camX * TileSize, camY * TileSize);
            cam.Zoom = new Vector2(zoom, zoom);
        }

        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree().CreateTimer(0.3), SceneTreeTimer.SignalName.Timeout);
        var shot = GetViewport().GetTexture().GetImage();
        var name = $"{prefix}-{label}.png";
        shot.SavePng(Path.Combine(destDir, name));
    }

    private async Task HoldCraftAndCaptureAsync(
        string destDir,
        string prefix,
        string label,
        float camX,
        float camY,
        float zoom,
        bool smelter)
    {
        if (HasNode("Camera"))
        {
            var cam = GetNode<Camera2D>("Camera");
            cam.Position = new Vector2(camX * TileSize, camY * TileSize);
            cam.Zoom = new Vector2(zoom, zoom);
        }

        // Hold craft for ~0.7s so press/glow settle without finishing the recipe.
        for (var i = 0; i < 45; i++)
        {
            if (smelter)
            {
                foreach (var sm in _slice!.Smelters)
                {
                    sm.RestoreCraftState(0.15f, isCrafting: true, null, null, 0);
                }
            }
            else
            {
                foreach (var asm in _slice!.Assemblers)
                {
                    asm.RestoreCraftState(0.15f, isCrafting: true, null, null, 0);
                }
            }

            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }

        var shot = GetViewport().GetTexture().GetImage();
        var name = $"{prefix}-{label}.png";
        shot.SavePng(Path.Combine(destDir, name));
    }

    private async Task CaptureBeltT2ChevronShotsAsync(string destDir)
    {
        if (_slice is null || _hud is null)
        {
            return;
        }

        _home?.Close();
        foreach (var id in ResearchState.GodotSliceStructureIds)
        {
            _slice.Research.ForceUnlock(id);
        }

        _slice.EnsureDemoBuildStock();
        SyncResearchLocks();

        // Clear a showcase row: T1 left, T2 right (same arrow density; T2 has Blu4 bordino).
        for (var x = 3; x <= 10; x++)
        {
            _slice.TryRemoveBelt(new GridPosition(x, 3));
        }

        _slice.TryPlaceBelt(new GridPosition(3, 3), Direction.East, "conveyor-basic");
        _slice.TryPlaceBelt(new GridPosition(4, 3), Direction.East, "conveyor-basic");
        _slice.TryPlaceBelt(new GridPosition(5, 3), Direction.East, "conveyor-basic");
        _slice.TryPlaceBelt(new GridPosition(7, 3), Direction.East, "conveyor-fast");
        _slice.TryPlaceBelt(new GridPosition(8, 3), Direction.East, "conveyor-fast");
        _slice.TryPlaceBelt(new GridPosition(9, 3), Direction.East, "conveyor-fast");

        _visualDirty = true;
        RebuildBeltVisual();
        UpdateHud();

        if (HasNode("Camera"))
        {
            var cam = GetNode<Camera2D>("Camera");
            cam.Position = new Vector2(6.5f * TileSize, 4.5f * TileSize);
            cam.Zoom = new Vector2(1.15f, 1.15f);
        }

        _hud.SetSelectedTool(FactoryHud.ToolKind.Belt);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree().CreateTimer(0.45), SceneTreeTimer.SignalName.Timeout);
        var worldT1 = GetViewport().GetTexture().GetImage();
        worldT1.SavePng(Path.Combine(destDir, "godot-port-belt-t2-single-chevron-world.png"));

        _hud.SetSelectedTool(FactoryHud.ToolKind.BeltFast);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree().CreateTimer(0.35), SceneTreeTimer.SignalName.Timeout);
        var palette = GetViewport().GetTexture().GetImage();
        palette.SavePng(Path.Combine(destDir, "godot-port-belt-t2-single-chevron-palette.png"));

        GD.Print("Belt T2 single-chevron screenshot set complete.");
        GetTree().Quit();
    }

    private async Task CaptureStructuresShotsAsync(string destDir)
    {
        if (_slice is null || _hud is null)
        {
            return;
        }

        _home?.Close();
        foreach (var id in ResearchState.GodotSliceStructureIds)
        {
            _slice.Research.ForceUnlock(id);
        }

        _slice.Research.ForceUnlock(PowerNodeStub.Tier2Id);
        SyncResearchLocks();

        // Demo already has T1 miner/forno/assembler/gen/junction/splitter/sorter/bridge/core.
        // Add missing art showcase pieces on free tiles.
        _slice.TryPlaceBelt(new GridPosition(4, 3), Direction.East, "conveyor-fast");
        _slice.TryPlaceBelt(new GridPosition(5, 3), Direction.East, "conveyor-fast");
        _slice.TryPlaceMiner(new GridPosition(16, 4), Direction.East, definitionId: MinerProducer.AdvancedId);
        _slice.TryPlaceExtractor(new GridPosition(8, 14), Direction.North);
        _slice.TryPlacePowerNode(new GridPosition(8, 4));
        _slice.TryPlacePowerNode(new GridPosition(18, 7), PowerNodeStub.Tier2Id);

        _visualDirty = true;
        _buildingsDirty = true;
        RebuildBeltVisual();
        RebuildBuildingVisuals();
        UpdateHud();

        if (HasNode("Camera"))
        {
            var cam = GetNode<Camera2D>("Camera");
            cam.Position = new Vector2(11f * TileSize, 9f * TileSize);
            cam.Zoom = new Vector2(0.55f, 0.55f);
        }

        _hud.SetSelectedTool(FactoryHud.ToolKind.Belt);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree().CreateTimer(0.5), SceneTreeTimer.SignalName.Timeout);
        var world = GetViewport().GetTexture().GetImage();
        world.SavePng(Path.Combine(destDir, "godot-port-structures-world.png"));

        _hud.SetSelectedTool(FactoryHud.ToolKind.Junction);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree().CreateTimer(0.35), SceneTreeTimer.SignalName.Timeout);
        var logistics = GetViewport().GetTexture().GetImage();
        logistics.SavePng(Path.Combine(destDir, "godot-port-structures-palette-logistics.png"));

        _hud.SetSelectedTool(FactoryHud.ToolKind.MinerAdvanced);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree().CreateTimer(0.35), SceneTreeTimer.SignalName.Timeout);
        var production = GetViewport().GetTexture().GetImage();
        production.SavePng(Path.Combine(destDir, "godot-port-structures-palette-production.png"));

        _hud.SetSelectedTool(FactoryHud.ToolKind.PowerNode);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree().CreateTimer(0.35), SceneTreeTimer.SignalName.Timeout);
        var power = GetViewport().GetTexture().GetImage();
        power.SavePng(Path.Combine(destDir, "godot-port-structures-palette-power.png"));

        GD.Print("Structures screenshot set complete.");
        GetTree().Quit();
    }

    private async Task CaptureT2ShotsAsync(string destDir)
    {
        if (_slice is null || _hud is null)
        {
            return;
        }

        _home?.Close();
        foreach (var id in ResearchState.GodotSliceStructureIds)
        {
            _slice.Research.ForceUnlock(id);
        }

        _slice.EnsureDemoBuildStock();
        SyncResearchLocks();
        _slice.TryPlaceBelt(new GridPosition(4, 8), Direction.East, "conveyor-fast");
        _slice.TryPlaceBelt(new GridPosition(5, 8), Direction.East, "conveyor-fast");
        _slice.TryPlaceMiner(new GridPosition(2, 4), Direction.East, definitionId: MinerProducer.AdvancedId);
        _slice.TryPlaceExtractor(new GridPosition(8, 14), Direction.North);
        _slice.TryPlacePowerNode(new GridPosition(11, 12));
        _visualDirty = true;
        _buildingsDirty = true;
        RebuildBeltVisual();
        RebuildBuildingVisuals();
        UpdateHud();

        // Logistics: show BeltFast in palette.
        _hud.SetSelectedTool(FactoryHud.ToolKind.BeltFast);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree().CreateTimer(0.45), SceneTreeTimer.SignalName.Timeout);
        var logistics = GetViewport().GetTexture().GetImage();
        logistics.SavePng(Path.Combine(destDir, "godot-port-t2-logistics.png"));

        _hud.SetSelectedTool(FactoryHud.ToolKind.MinerAdvanced);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree().CreateTimer(0.4), SceneTreeTimer.SignalName.Timeout);
        var production = GetViewport().GetTexture().GetImage();
        production.SavePng(Path.Combine(destDir, "godot-port-t2-production.png"));

        _hud.SetSelectedTool(FactoryHud.ToolKind.PowerNode);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree().CreateTimer(0.4), SceneTreeTimer.SignalName.Timeout);
        var power = GetViewport().GetTexture().GetImage();
        power.SavePng(Path.Combine(destDir, "godot-port-t2-power.png"));

        GD.Print("T2 screenshot set complete.");
        GetTree().Quit();
    }

    private async Task CaptureAutoSellShotsAsync(string destDir)
    {
        if (_slice is null || _hud is null)
        {
            return;
        }

        // Seed stock so Mercato shows rows; toggle OFF then ON for panel shots.
        _slice.Wallet.AddMaterial("iron-ore", 8);
        _slice.AutoSellAtCore = false;
        _mercato?.Open(_slice);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree().CreateTimer(0.45), SceneTreeTimer.SignalName.Timeout);
        var offShot = GetViewport().GetTexture().GetImage();
        offShot.SavePng(Path.Combine(destDir, "godot-port-autosell-off.png"));

        _slice.AutoSellAtCore = true;
        _mercato?.Close();
        _mercato?.Open(_slice);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree().CreateTimer(0.45), SceneTreeTimer.SignalName.Timeout);
        var onShot = GetViewport().GetTexture().GetImage();
        onShot.SavePng(Path.Combine(destDir, "godot-port-autosell-on.png"));

        // Live liquidate: spike demo with auto-sell until core sales register.
        _mercato?.Close();
        var demo = FactorySlice.CreateSpikeDemo(FactoryContent.Load(_contentPath));
        demo.AutoSellAtCore = true;
        _slice = demo;
        _itemSprites.Clear();
        if (_itemsLayer is not null)
        {
            foreach (var child in _itemsLayer.GetChildren())
            {
                child.QueueFree();
            }
        }

        _visualDirty = true;
        _buildingsDirty = true;
        RebuildBeltVisual();
        RebuildBuildingVisuals();
        SyncResearchLocks();
        UpdateHud();

        var money0 = _slice.Wallet.Money;
        for (var i = 0; i < 30 * 50; i++)
        {
            _slice.Tick(1f / 30f);
            if (_slice.Session.SaleIncome > 0 && _slice.Wallet.Money > money0)
            {
                break;
            }
        }

        _visualDirty = true;
        UpdateHud();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree().CreateTimer(0.45), SceneTreeTimer.SignalName.Timeout);
        var liveShot = GetViewport().GetTexture().GetImage();
        liveShot.SavePng(Path.Combine(destDir, "godot-port-autosell-live.png"));

        GD.Print($"Auto-sell shots · money {_slice.Wallet.Money} · sales ${_slice.Session.SaleIncome}");
        GetTree().Quit();
    }

    private async Task CaptureMindustryUiShotsAsync(string destDir)
    {
        if (_slice is null || _hud is null)
        {
            return;
        }

        async Task SaveNamed(string name)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await ToSignal(GetTree().CreateTimer(0.35), SceneTreeTimer.SignalName.Timeout);
            var img = GetViewport().GetTexture().GetImage();
            img.SavePng(Path.Combine(destDir, name));
            GD.Print($"Screenshot: {name}");
        }

        // 1) Empty reserved info slot (cursor) — palette anchored, slot invisible.
        ClearToCursor(toast: false);
        UpdateHud();
        await SaveNamed("godot-port-mindustry-ui-info-slot-empty.png");

        var fullEmpty = GetViewport().GetTexture().GetImage();
        var dockW = Math.Min(360, fullEmpty.GetWidth());
        var dockH = Math.Min(300, fullEmpty.GetHeight());
        var emptyCrop = fullEmpty.GetRegion(new Rect2I(
            fullEmpty.GetWidth() - dockW, fullEmpty.GetHeight() - dockH, dockW, dockH));
        emptyCrop.SavePng(Path.Combine(destDir, "godot-port-mindustry-ui-info-slot-empty-crop.png"));

        // 2) Filled slot — miner selected → name + barred costs in reserved strip.
        _hud.SetSelectedTool(FactoryHud.ToolKind.Miner);
        OnHudToolChosen(FactoryHud.ToolKind.Miner);
        UpdateHud();
        await SaveNamed("godot-port-mindustry-ui-info-slot-filled.png");

        var fullFilled = GetViewport().GetTexture().GetImage();
        var filledCrop = fullFilled.GetRegion(new Rect2I(
            fullFilled.GetWidth() - dockW, fullFilled.GetHeight() - dockH, dockW, dockH));
        filledCrop.SavePng(Path.Combine(destDir, "godot-port-mindustry-ui-info-slot-filled-crop.png"));

        // 3) ? modal still carries prose / I/O.
        if (!_slice.IsStructureUnlocked("smelter"))
        {
            _slice.Wallet.AddMoney(500);
            _slice.Wallet.AddMaterial("iron-plate", 40);
            _ = _slice.TryUnlockStructure("smelter");
            SyncResearchLocks();
        }

        _hud.SetSelectedTool(FactoryHud.ToolKind.Smelter);
        OnHudToolChosen(FactoryHud.ToolKind.Smelter);
        while (_slice.Wallet.MaterialCount("iron-plate") > 0)
        {
            _slice.Wallet.TrySpend(0, [new ResourceAmount("iron-plate", 1)]);
        }

        UpdateHud();
        _hud.OpenBlockDetailForCapture();
        await SaveNamed("godot-port-mindustry-ui-info-slot-detail-modal.png");
        _hud.CloseBlockDetailForCapture();

        GD.Print("Mindustry info-slot screenshot set complete.");
        GetTree().Quit();
    }

    private async Task CaptureResearchShotsAsync(string destDir)
    {
        if (_slice is null || _hud is null)
        {
            return;
        }

        var lockedFull = GetViewport().GetTexture().GetImage();
        var barH = Math.Min(170, lockedFull.GetHeight());
        var lockedBar = lockedFull.GetRegion(
            new Rect2I(0, lockedFull.GetHeight() - barH, lockedFull.GetWidth(), barH));
        lockedBar.SavePng(Path.Combine(destDir, "godot-port-techtree-graph-locked-toolbar.png"));

        _research?.Open(_slice);
        _research?.SelectStructure("smelter");
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree().CreateTimer(0.55), SceneTreeTimer.SignalName.Timeout);
        var panelShot = GetViewport().GetTexture().GetImage();
        panelShot.SavePng(Path.Combine(destDir, "godot-port-techtree-graph-panel.png"));

        if (!_slice.TryUnlockStructure("smelter"))
        {
            GD.PushError("Capture unlock smelter failed");
        }

        _research?.SelectStructure("assembler");
        _research?.Refresh();
        SyncResearchLocks();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree().CreateTimer(0.45), SceneTreeTimer.SignalName.Timeout);
        var afterUnlock = GetViewport().GetTexture().GetImage();
        afterUnlock.SavePng(Path.Combine(destDir, "godot-port-techtree-graph-path.png"));

        GD.Print("Tech-tree graph screenshot set complete.");
        GetTree().Quit();
    }

    private static Image CropCenterCells(Image src, int cropCells)
    {
        var span = cropCells * TileSize;
        var x0 = Math.Max(0, (src.GetWidth() - span) / 2);
        var y0 = Math.Max(0, (src.GetHeight() - span) / 2);
        var w = Math.Min(span, src.GetWidth() - x0);
        var h = Math.Min(span, src.GetHeight() - y0);
        return src.GetRegion(new Rect2I(x0, y0, w, h));
    }

    // Legacy corner QA capture kept for local debugging (not auto-run in Phase D).
    private async Task SaveCornerShotSetAsync(string destDir)
    {
        if (_slice is null)
        {
            return;
        }

        if (_buildingsLayer is not null)
        {
            _buildingsLayer.Visible = false;
        }

        if (_itemsLayer is not null)
        {
            _itemsLayer.Visible = false;
        }

        if (_hud is not null)
        {
            _hud.Visible = false;
        }

        var beltDef = _slice.BeltDefinition;
        var cam = HasNode("Camera") ? GetNode<Camera2D>("Camera") : null;

        // 1) Corner block alone — crop one cell.
        await CaptureBeltLayout(
            destDir,
            "godot-port-corner-alone.png",
            [
                new GridPosition(9, 8),
                new GridPosition(10, 8),
                new GridPosition(10, 9)
            ],
            beltDef,
            cam,
            focusCellX: 10.5f,
            focusCellY: 8.5f,
            cropCells: 1);

        // 2) Corner with conveyor belts (L).
        await CaptureBeltLayout(
            destDir,
            "godot-port-corner-belts.png",
            [
                new GridPosition(7, 8),
                new GridPosition(8, 8),
                new GridPosition(9, 8),
                new GridPosition(10, 8),
                new GridPosition(10, 9),
                new GridPosition(10, 10),
                new GridPosition(10, 11)
            ],
            beltDef,
            cam,
            focusCellX: 9.5f,
            focusCellY: 9.0f,
            cropCells: 5);

        // 3) U shape (open west).
        await CaptureBeltLayout(
            destDir,
            "godot-port-corner-u.png",
            [
                new GridPosition(8, 8),
                new GridPosition(9, 8),
                new GridPosition(10, 8),
                new GridPosition(10, 9),
                new GridPosition(10, 10),
                new GridPosition(9, 10),
                new GridPosition(8, 10)
            ],
            beltDef,
            cam,
            focusCellX: 9.0f,
            focusCellY: 9.0f,
            cropCells: 5);

        // 4) Z shape.
        await CaptureBeltLayout(
            destDir,
            "godot-port-corner-z.png",
            [
                new GridPosition(7, 8),
                new GridPosition(8, 8),
                new GridPosition(9, 8),
                new GridPosition(9, 9),
                new GridPosition(9, 10),
                new GridPosition(10, 10),
                new GridPosition(11, 10),
                new GridPosition(12, 10)
            ],
            beltDef,
            cam,
            focusCellX: 9.5f,
            focusCellY: 9.0f,
            cropCells: 6);

        // Pixel asserts on the L/belts layout (reload + one more frame).
        await CaptureBeltLayout(
            destDir,
            "godot-port-corner-recipe-close.png",
            [
                new GridPosition(7, 8),
                new GridPosition(8, 8),
                new GridPosition(9, 8),
                new GridPosition(10, 8),
                new GridPosition(10, 9),
                new GridPosition(10, 10),
                new GridPosition(10, 11)
            ],
            beltDef,
            cam,
            focusCellX: 10.5f,
            focusCellY: 8.5f,
            cropCells: 5,
            runAsserts: true);

        GD.Print("Corner shot set complete (alone / belts / U / Z).");
    }

    private void ClearAllBelts()
    {
        if (_slice is null)
        {
            return;
        }

        foreach (var pos in _slice.Belts.Cells.Keys.ToList())
        {
            _slice.TryRemoveBelt(pos);
        }
    }

    private async Task CaptureBeltLayout(
        string destDir,
        string fileName,
        List<GridPosition> path,
        ConveyorDefinition beltDef,
        Camera2D? cam,
        float focusCellX,
        float focusCellY,
        int cropCells,
        bool runAsserts = false)
    {
        ClearAllBelts();
        _slice!.Belts.PlacePath(path, beltDef);
        RebuildBeltVisual();
        if (cam is not null)
        {
            cam.Position = new Vector2(focusCellX * TileSize, focusCellY * TileSize);
            cam.Zoom = new Vector2(1f, 1f);
        }

        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

        var img = GetViewport().GetTexture().GetImage();
        var vpW = img.GetWidth();
        var vpH = img.GetHeight();
        var size = TileSize * cropCells;
        var region = img.GetRegion(new Rect2I(
            (vpW - size) / 2, (vpH - size) / 2, size, size));
        var pathOut = Path.Combine(destDir, fileName);
        var err = region.SavePng(pathOut);
        GD.Print(err == Error.Ok ? $"Screenshot: {pathOut}" : $"Screenshot failed: {err}");

        if (runAsserts)
        {
            var cell = TileSize;
            var cornerX0 = (vpW / 2) - (cell / 2);
            var cornerY0 = (vpH / 2) - (cell / 2);
            AssertCellOpaque(img, cornerX0, cornerY0, cell, "corner(10,8)");
            AssertCellOpaque(img, cornerX0 - cell, cornerY0, cell, "straight(9,8)");
            AssertNorthEdgeFlush(img, cornerX0 - cell, cornerX0 + cell, cornerY0);
            AssertSeamRailContinuous(img, cornerX0, cornerY0, cell);
            AssertCornerRecipe(img, cornerX0, cornerY0, cell);
            img.SavePng(Path.Combine(destDir, "godot-port-corner-recipe-map.png"));
        }
    }

    private static bool IsTerrain(Color c) =>
        c.G > c.R + 5f / 255f && c.G >= c.B && c.B < 60f / 255f && c.R < 55f / 255f;

    private static bool IsRail(Color c) =>
        !IsTerrain(c) && c.R <= 30f / 255f && c.G <= 36f / 255f && c.B <= 42f / 255f;

    private static void AssertCellOpaque(Image img, int x0, int y0, int cell, string label)
    {
        var terrain = 0;
        for (var y = y0; y < y0 + cell; y++)
        {
            for (var x = x0; x < x0 + cell; x++)
            {
                if (x < 0 || y < 0 || x >= img.GetWidth() || y >= img.GetHeight())
                {
                    continue;
                }

                if (IsTerrain(img.GetPixel(x, y)))
                {
                    terrain++;
                }
            }
        }

        var total = cell * cell;
        GD.Print($"PixelCheck {label}: terrain={terrain}/{total} ({100.0 * terrain / total:F2}%)");
        if (terrain > 0)
        {
            GD.PushError($"FULLTILE FAIL {label}: terrain pixels inside cell (want 0).");
        }
    }

    private static void AssertNorthEdgeFlush(Image img, int x0, int x1, int yTop)
    {
        var beltOnEdge = 0;
        var span = 0;
        for (var x = x0; x < x1; x++)
        {
            if (x < 0 || x >= img.GetWidth() || yTop < 1 || yTop >= img.GetHeight())
            {
                continue;
            }

            span++;
            if (!IsTerrain(img.GetPixel(x, yTop)))
            {
                beltOnEdge++;
            }
        }

        GD.Print($"PixelCheck north-edge flush: beltOnEdge={beltOnEdge}/{span}");
        if (span > 0 && beltOnEdge < span * 0.95)
        {
            GD.PushError("FULLTILE FAIL north edge: belt does not sit on tile edge.");
        }
    }

    /// <summary>
    /// Top/bottom rail band thickness must match across the straight↔corner seam
    /// (no rientranza / notch where west rail used to thicken N/S borders).
    /// </summary>
    private static void AssertSeamRailContinuous(Image img, int cornerX0, int cornerY0, int cell)
    {
        static int TopRailEnd(Image image, int x, int y0)
        {
            var end = y0 - 1;
            for (var y = y0; y < y0 + 16; y++)
            {
                if (x < 0 || x >= image.GetWidth() || y < 0 || y >= image.GetHeight())
                {
                    break;
                }

                if (!IsRail(image.GetPixel(x, y)))
                {
                    break;
                }

                end = y;
            }

            return end;
        }

        static int BotRailStart(Image image, int x, int y0, int cellSize)
        {
            var start = y0 + cellSize;
            for (var y = y0 + cellSize - 1; y >= y0 + cellSize - 16; y--)
            {
                if (x < 0 || x >= image.GetWidth() || y < 0 || y >= image.GetHeight())
                {
                    break;
                }

                if (!IsRail(image.GetPixel(x, y)))
                {
                    break;
                }

                start = y;
            }

            return start;
        }

        var straightX = cornerX0 - 8;
        var seamX = cornerX0 + 2;
        var topStraight = TopRailEnd(img, straightX, cornerY0);
        var topSeam = TopRailEnd(img, seamX, cornerY0);
        var botStraight = BotRailStart(img, straightX, cornerY0, cell);
        var botSeam = BotRailStart(img, seamX, cornerY0, cell);
        var topDelta = Math.Abs(topSeam - topStraight);
        var botDelta = Math.Abs(botSeam - botStraight);
        GD.Print(
            $"PixelCheck seam rails: topStraightEnd={topStraight} topSeamEnd={topSeam} Δ={topDelta} " +
            $"botStraightStart={botStraight} botSeamStart={botSeam} Δ={botDelta}");
        if (topDelta > 1 || botDelta > 1)
        {
            GD.PushError("SEAM FAIL: N/S rail band steps across straight↔corner join (rientranza).");
        }

        // Tube (Blu4) meets the straight — colors differ by design; only log.
        var yBody = cornerY0 + cell / 4;
        var straightBody = img.GetPixel(cornerX0 - 8, yBody);
        var cornerTube = img.GetPixel(cornerX0 + cell / 4, yBody);
        GD.Print(
            $"PixelCheck seam tube: straight=({straightBody.R:F3},{straightBody.G:F3},{straightBody.B:F3}) " +
            $"cornerTube=({cornerTube.R:F3},{cornerTube.G:F3},{cornerTube.B:F3})");

        AssertInnerCornerKnuckle(img, cornerX0, cornerY0, cell);
        AssertEastRailFlush(img, cornerX0, cornerY0, cell);
    }

    /// <summary>
    /// Recipe: Blu1 N/E + SW knuckle, Blu4 tube near focus, Blu3 outside arc (NE).
    /// </summary>
    private static void AssertCornerRecipe(Image img, int cornerX0, int cornerY0, int cell)
    {
        static bool Near(Color c, float r, float g, float b, float tol = 0.05f) =>
            Math.Abs(c.R - r) <= tol && Math.Abs(c.G - g) <= tol && Math.Abs(c.B - b) <= tol;

        // Blu1 #191E24 on north edge mid.
        var n = img.GetPixel(cornerX0 + cell / 2, cornerY0 + 2);
        var nOk = IsRail(n) || Near(n, 0.098f, 0.118f, 0.141f);
        GD.Print($"PixelCheck recipe Blu1 N: ({n.R:F3},{n.G:F3},{n.B:F3}) ok={nOk}");
        if (!nOk)
        {
            GD.PushError("RECIPE FAIL: north edge is not Blu1.");
        }

        // Blu4 tube near focus (SW quadrant).
        var tube = img.GetPixel(cornerX0 + cell / 3, cornerY0 + cell * 2 / 3);
        var tubeOk = Near(tube, 0.525f, 0.655f, 0.722f, 0.08f);
        GD.Print($"PixelCheck recipe Blu4 tube: ({tube.R:F3},{tube.G:F3},{tube.B:F3}) ok={tubeOk}");
        if (!tubeOk)
        {
            GD.PushError("RECIPE FAIL: tube interior is not Blu4.");
        }

        // Blu3 exterior opposite focus (NE tip inside borders).
        var ext = img.GetPixel(cornerX0 + cell * 88 / 100, cornerY0 + cell * 12 / 100);
        var extOk = Near(ext, 0.196f, 0.235f, 0.275f, 0.08f);
        GD.Print($"PixelCheck recipe Blu3 exterior: ({ext.R:F3},{ext.G:F3},{ext.B:F3}) ok={extOk}");
        if (!extOk)
        {
            GD.PushError("RECIPE FAIL: NE exterior of arc is not Blu3.");
        }
    }

    /// <summary>
    /// SW ┘ knuckle must be solid rail; bottom edge beside it must NOT extend a
    /// groove/rail stub (that read as the inner-corner notch).
    /// </summary>
    private static void AssertInnerCornerKnuckle(Image img, int cornerX0, int cornerY0, int cell)
    {
        var railBand = Math.Max(3, cell * 8 / 100);
        var knuckleRails = 0;
        var knuckleCells = 0;
        for (var y = cornerY0 + cell - railBand; y < cornerY0 + cell; y++)
        {
            for (var x = cornerX0; x < cornerX0 + railBand; x++)
            {
                knuckleCells++;
                if (IsRail(img.GetPixel(x, y)))
                {
                    knuckleRails++;
                }
            }
        }

        GD.Print($"PixelCheck SW knuckle: rail={knuckleRails}/{knuckleCells}");
        if (knuckleCells > 0 && knuckleRails < knuckleCells * 0.9)
        {
            GD.PushError("CORNER FAIL: SW knuckle is not solid rail (inner notch).");
        }

        // Immediately east of knuckle on the bottom edge: body, not rail/groove stub.
        var stubRails = 0;
        var stubSamples = 0;
        var yBot = cornerY0 + cell - 1;
        for (var x = cornerX0 + railBand; x < cornerX0 + railBand + railBand && x < cornerX0 + cell - railBand; x++)
        {
            stubSamples++;
            if (IsRail(img.GetPixel(x, yBot)))
            {
                stubRails++;
            }
        }

        GD.Print($"PixelCheck SW bottom-beside-knuckle: rail={stubRails}/{stubSamples} (want 0)");
        if (stubRails > 0)
        {
            GD.PushError("CORNER FAIL: south-rail stub east of SW knuckle (inner step).");
        }
    }

    /// <summary>
    /// East outer rail X must stay flush from corner into the vertical strip
    /// (no SE bump / width jump at the join).
    /// </summary>
    private static void AssertEastRailFlush(Image img, int cornerX0, int cornerY0, int cell)
    {
        static int EastRailStart(Image image, int y, int xRight)
        {
            for (var x = xRight; x >= xRight - 16; x--)
            {
                if (x < 0 || y < 0 || x >= image.GetWidth() || y >= image.GetHeight())
                {
                    break;
                }

                if (!IsRail(image.GetPixel(x, y)))
                {
                    return x + 1;
                }
            }

            return xRight - 16;
        }

        var xRight = cornerX0 + cell - 1;
        var midCorner = EastRailStart(img, cornerY0 + cell / 2, xRight);
        var botCorner = EastRailStart(img, cornerY0 + cell - 2, xRight);
        var topVert = EastRailStart(img, cornerY0 + cell + 4, xRight);
        var midVert = EastRailStart(img, cornerY0 + cell + cell / 2, xRight);
        var d1 = Math.Abs(botCorner - midCorner);
        var d2 = Math.Abs(topVert - botCorner);
        var d3 = Math.Abs(midVert - midCorner);
        GD.Print(
            $"PixelCheck east rail: midCorner={midCorner} botCorner={botCorner} " +
            $"topVert={topVert} midVert={midVert} Δ=({d1},{d2},{d3})");
        if (d1 > 1 || d2 > 1 || d3 > 1)
        {
            GD.PushError("CORNER FAIL: east rail X jumps at corner→vertical (SE bump).");
        }

        // SE bottom interior (left of east rail) must not be a south-rail stub.
        var seStub = 0;
        var ySe = cornerY0 + cell - 1;
        for (var x = cornerX0 + cell / 2; x < cornerX0 + cell - 8; x++)
        {
            if (IsRail(img.GetPixel(x, ySe)))
            {
                seStub++;
            }
        }

        GD.Print($"PixelCheck SE south stub: rail={seStub} (want 0)");
        if (seStub > 0)
        {
            GD.PushError("CORNER FAIL: SE south-rail stub present (outer bump).");
        }
    }

    /// <summary>
    /// Output folder for capture modes: <c>TINDUSTRY_CAPTURE_DIR</c>, else <c>godot/artifacts</c> (gitignored).
    /// </summary>
    private static string? CaptureDirectory()
    {
        var dir = OS.GetEnvironment("TINDUSTRY_CAPTURE_DIR");
        if (string.IsNullOrWhiteSpace(dir))
        {
            dir = ProjectSettings.GlobalizePath("res://artifacts");
        }

        try
        {
            Directory.CreateDirectory(dir);
            return dir;
        }
        catch (Exception ex)
        {
            GD.PrintErr($"Capture dir non creabile ({dir}): {ex.Message}");
            return null;
        }
    }
}
