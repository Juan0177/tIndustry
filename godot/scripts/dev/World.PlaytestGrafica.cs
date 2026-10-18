using Godot;
using TIndustry.Shared;

namespace TIndustry.Godot;

/// <summary>
/// Graphics settings and effect manager probe of the <c>ui</c> playtest (G3): every option is set through the real
/// Impostazioni panel (buttons and Applica) and its effect on the engine and the world is checked, then everything
/// goes back to the defaults; the effect manager is exercised directly (quality, view, cap, attached emitters).
/// </summary>
public partial class World
{
    private async Task<bool> PlaytestGraphicsSettingsAsync(string dir, Func<string, Task> shot)
    {
        if (_settingsPanel is null || _effects is null || _renderScale is null || _terrainChunks is null)
        {
            return Fail("impostazioni: pannello, effetti o scala di rendering mancanti");
        }

        // An old settings.json (before G3) stays valid: the new fields take their defaults.
        var old = ClientSettings.FromJson("{ \"vSync\": false, \"uiScalePercent\": 150, \"showFps\": true }");
        if (old.VSync || old.UiScalePercent != 150 || !old.ShowFps || old.Effetti != EffectsQuality.Alti || !old.Ombre
            || !old.Bagliore || !old.AnimazioniBlocchi || old.LimiteFps != 0 || old.ScalaRendering != 100)
        {
            return Fail("impostazioni: un file vecchio non dà i valori predefiniti ai campi nuovi");
        }

        if (ClientSettings.FromJson("{ \"effetti\": \"Bassi\", \"limiteFps\": 100, \"scalaRendering\": 60 }") is not
            { Effetti: EffectsQuality.Bassi, LimiteFps: 120, ScalaRendering: 50 })
        {
            return Fail("impostazioni: valori fuori elenco non riportati al più vicino");
        }

        // An unknown effects name (a future version, a typo) resets that field only, not the whole file.
        if (ClientSettings.FromJson("{ \"effetti\": \"Medi\", \"tutorialCompleted\": true, \"ombre\": false }") is not
            { Effetti: EffectsQuality.Alti, TutorialCompleted: true, Ombre: false })
        {
            return Fail("impostazioni: un nome di Effetti sconosciuto ha cancellato le altre impostazioni");
        }

        var forcedOff = ClientSettings.PlaytestForcesEffectsOff;
        var expectedQuality = forcedOff ? EffectsQuality.Spenti : _settings.Effetti;
        if (_effects.Quality != expectedQuality || _terrainChunks.AnimateWater != (expectedQuality != EffectsQuality.Spenti))
        {
            return Fail($"impostazioni: qualità effetti {_effects.Quality}, attesa {expectedQuality} (playtest)");
        }

        OpenSettings();
        await shot("impostazioni");

        // Limite FPS, VSync, Effetti, Ombre, Bagliore, Animazioni dei blocchi in one Applica.
        if (!PressSetting("LimiteFps_60") || !ToggleSetting("VSync", false) || !PressSetting("Effetti_Bassi")
            || !ToggleSetting("Ombre", false) || !ToggleSetting("Bagliore", false) || !ToggleSetting("AnimazioniBlocchi", false)
            || !PressSetting("Applica"))
        {
            return Fail("impostazioni: controllo del pannello non trovato");
        }

        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        var saved = ClientSettings.Load();
        if (Engine.MaxFps != 60
            || DisplayServer.WindowGetVsyncMode() != DisplayServer.VSyncMode.Disabled
            || _settings is not { Effetti: EffectsQuality.Bassi, Ombre: false, Bagliore: false, AnimazioniBlocchi: false }
            || saved is not { LimiteFps: 60, VSync: false, Effetti: EffectsQuality.Bassi, Ombre: false, Bagliore: false, AnimazioniBlocchi: false }
            || _blockAnimations
            || _effects.Quality != (forcedOff ? EffectsQuality.Spenti : EffectsQuality.Bassi)
            || _settings.EffectiveGlow)
        {
            return Fail($"impostazioni: non applicate (MaxFps {Engine.MaxFps}, VSync {DisplayServer.WindowGetVsyncMode()}, "
                + $"effetti {_effects.Quality}, animazioni {_blockAnimations})");
        }

        // Render scale: the world is drawn smaller and stretched; the cell under the cursor stays right.
        foreach (var percent in new[] { 75, 50 })
        {
            OpenSettings();
            if (!PressSetting($"ScalaRendering_{percent}") || !PressSetting("Applica"))
            {
                return Fail("impostazioni: scala di rendering non trovata");
            }

            _settingsPanel.Close();
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            if (await PlaytestRenderScaleProblem(percent) is { } problem)
            {
                return Fail($"scala di rendering {percent}%: {problem}");
            }

            if (percent == 50)
            {
                // After the "Impostazioni" toast faded (1.6 s + 0.45 s), so the capture does not depend on timing.
                await PlaytestWait(2.2);
                await shot("impostazioni-scala-50");
            }
        }

        // Back to the defaults: same engine state as before the probe.
        OpenSettings();
        if (!PressSetting("LimiteFps_Nessuno") || !ToggleSetting("VSync", true) || !PressSetting("Effetti_Alti")
            || !ToggleSetting("Ombre", true) || !ToggleSetting("Bagliore", true) || !ToggleSetting("AnimazioniBlocchi", true)
            || !PressSetting("ScalaRendering_100") || !PressSetting("Applica"))
        {
            return Fail("impostazioni: controllo del pannello non trovato (ripristino)");
        }

        _settingsPanel.Close();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        if (Engine.MaxFps != 0 || DisplayServer.WindowGetVsyncMode() != DisplayServer.VSyncMode.Enabled
            || _renderScale.Target is not null || GetTree().Root.CanvasCullMask != uint.MaxValue || VisibilityLayer != 1
            || !_blockAnimations || _effects.Quality != expectedQuality)
        {
            return Fail("impostazioni: ripristino dei predefiniti non riuscito");
        }

        return PlaytestEffectManagerProblem() is { } effectProblem ? Fail($"effetti: {effectProblem}") : true;
    }

    /// <summary>Null when the render scale target has the right size, the root culls the world and picking is right.</summary>
    private async Task<string?> PlaytestRenderScaleProblem(int percent)
    {
        var root = GetTree().Root;
        if (_renderScale?.Target is not { } target)
        {
            return "nessuna destinazione ridotta";
        }

        var window = (Vector2)DisplayServer.WindowGetSize();
        var expected = (window * percent / 100f).Round();
        if (Math.Abs(target.Size.X - expected.X) > 1 || Math.Abs(target.Size.Y - expected.Y) > 1)
        {
            return $"destinazione {target.Size}, attesa {expected} (finestra {window})";
        }

        if ((root.CanvasCullMask & VisibilityLayer) != 0)
        {
            return "la finestra disegna ancora il mondo a piena risoluzione";
        }

        if (target.CanvasItemDefaultTextureFilter != root.CanvasItemDefaultTextureFilter)
        {
            return $"filtro del mondo ridotto {target.CanvasItemDefaultTextureFilter}, atteso {root.CanvasItemDefaultTextureFilter}";
        }

        var stretch = percent == 50 ? CanvasItem.TextureFilterEnum.Nearest : CanvasItem.TextureFilterEnum.Linear;
        if (_renderScale.StretchFilter != stretch)
        {
            return $"ingrandimento {_renderScale.StretchFilter}, atteso {stretch}";
        }

        if (_view is null || _view.CoreTiles.Count == 0)
        {
            return "nessun Core per la prova del cursore";
        }

        // Two cells right of the Core (clear of the HUD column on the left): the hovered cell follows the mouse exactly.
        var coreX = _view.CoreTiles.Min(t => t.X);
        var coreY = _view.CoreTiles.Min(t => t.Y);
        foreach (var cell in new[] { new GridPosition(coreX + 4, coreY - 1), new GridPosition(coreX + 3, coreY + 1) })
        {
            PlaytestMove(CellToScreen(cell), 0);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            if (!_hover.Equals(cell))
            {
                return $"cella sotto il cursore {_hover}, attesa {cell}";
            }
        }

        var probeCell = new GridPosition(coreX + 3, coreY + 1);
        return await PlaytestReducedWorldUnderCursorProblem(target, probeCell, window)
            ?? await PlaytestGhostUnderCursorProblem(probeCell);
    }

    /// <summary>
    /// The reduced world is drawn under the cursor: a magenta square put on the world canvas over the hovered cell for
    /// one frame must appear in the reduced image centred on the cursor (window pixels mapped to target pixels) within
    /// a quarter of a cell, about one cell wide. Checks <see cref="RenderScale"/>'s transform, which the full-resolution
    /// ghost no longer goes through.
    /// </summary>
    private async Task<string?> PlaytestReducedWorldUnderCursorProblem(SubViewport target, GridPosition cell, Vector2 window)
    {
        var mouse = CellToScreen(cell);
        PlaytestMove(mouse, 0);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        if (!_hover.Equals(cell))
        {
            return $"cella sotto il cursore {_hover}, attesa {cell}";
        }

        var probe = new ColorRect
        {
            Name = "ProvaMondoRidotto",
            Color = Colors.Magenta,
            Position = new Vector2(cell.X * TileSize, cell.Y * TileSize),
            Size = new Vector2(TileSize, TileSize),
            ZIndex = 100,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        AddChild(probe);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        using var image = target.GetTexture().GetImage();
        probe.QueueFree();

        int minX = int.MaxValue, minY = int.MaxValue, maxX = -1, maxY = -1;
        for (var y = 0; y < image.GetHeight(); y++)
        {
            for (var x = 0; x < image.GetWidth(); x++)
            {
                var c = image.GetPixel(x, y);
                if (c.R < 0.8f || c.G > 0.25f || c.B < 0.8f)
                {
                    continue;
                }

                minX = Math.Min(minX, x);
                minY = Math.Min(minY, y);
                maxX = Math.Max(maxX, x);
                maxY = Math.Max(maxY, y);
            }
        }

        if (maxX < 0)
        {
            return "nessuna cella di prova nell'immagine ridotta";
        }

        var scale = (Vector2)target.Size / window;
        var cellPixels = TileSize * GetNode<Camera2D>("Camera").Zoom.X * GetTree().Root.GetFinalTransform().Scale.X * scale.X;
        var centre = new Vector2(minX + maxX + 1, minY + maxY + 1) / 2f;
        var expected = mouse * scale;
        var box = new Vector2(maxX - minX + 1, maxY - minY + 1);
        GD.Print($"SCALA {Mathf.RoundToInt(scale.X * 100)}%: cella di prova {box} px al centro {centre}, cursore {expected}, cella {cellPixels:0.0} px");
        if (centre.DistanceTo(expected) > cellPixels / 4f || box.X > cellPixels * 1.5f || box.Y > cellPixels * 1.5f)
        {
            return $"mondo ridotto: cella di prova {box} px centrata in {centre}, cursore in {expected} (cella {cellPixels:0.0} px)";
        }

        return null;
    }

    /// <summary>
    /// What the player sees: with the belt tool, the placement ghost must sit under the mouse over the reduced world.
    /// The ghost is drawn at full resolution on its own layer (<see cref="EnsurePlacementOverlay"/>), so it is looked
    /// for in the window image with the HUD hidden: the bright pixels that change near the cursor when the tool is
    /// picked (the faint build grid stays below the threshold) must be centred on it within a quarter of a cell.
    /// </summary>
    private async Task<string?> PlaytestGhostUnderCursorProblem(GridPosition cell)
    {
        var mouse = CellToScreen(cell);
        var root = GetTree().Root;
        var hud = GetNode<CanvasLayer>("Hud");
        hud.Visible = false;
        PlaytestMove(mouse, 0);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        using var before = root.GetTexture().GetImage();
        await PlaytestKey(Key.Key1);
        PlaytestMove(mouse, 0);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        using var after = root.GetTexture().GetImage();
        hud.Visible = true;
        await PlaytestKey(Key.Escape);

        var cellPixels = TileSize * GetNode<Camera2D>("Camera").Zoom.X * root.GetFinalTransform().Scale.X;
        var reach = Mathf.CeilToInt(cellPixels * 2f);
        int minX = int.MaxValue, minY = int.MaxValue, maxX = -1, maxY = -1;
        for (var y = Math.Max(0, (int)mouse.Y - reach); y < Math.Min(after.GetHeight(), (int)mouse.Y + reach); y++)
        {
            for (var x = Math.Max(0, (int)mouse.X - reach); x < Math.Min(after.GetWidth(), (int)mouse.X + reach); x++)
            {
                var a = after.GetPixel(x, y);
                var b = before.GetPixel(x, y);
                if (Math.Max(Math.Abs(a.R - b.R), Math.Max(Math.Abs(a.G - b.G), Math.Abs(a.B - b.B))) < 0.12f)
                {
                    continue;
                }

                minX = Math.Min(minX, x);
                minY = Math.Min(minY, y);
                maxX = Math.Max(maxX, x);
                maxY = Math.Max(maxY, y);
            }
        }

        if (maxX < 0)
        {
            return "nessuna sagoma di piazzamento sotto il cursore";
        }

        var centre = new Vector2(minX + maxX + 1, minY + maxY + 1) / 2f;
        var expected = mouse;
        var box = new Vector2(maxX - minX + 1, maxY - minY + 1);
        GD.Print($"SCALA {_renderScale?.Percent}%: sagoma {box} px al centro {centre}, cursore {expected}, cella {cellPixels:0.0} px");
        if (centre.DistanceTo(expected) > cellPixels / 4f || box.X > cellPixels * 1.5f || box.Y > cellPixels * 1.5f)
        {
            return $"sagoma {box} px centrata in {centre}, cursore in {expected} (cella {cellPixels:0.0} px)";
        }

        return null;
    }

    /// <summary>
    /// The manager on its own, at a forced quality (playtests force Spenti): bursts only in view, the cap holds,
    /// attached emitters follow their owner and vanish with it, Spenti draws nothing, Clear empties everything.
    /// </summary>
    private string? PlaytestEffectManagerProblem()
    {
        var fx = _effects!;
        var before = fx.Quality;
        try
        {
            fx.Clear();
            fx.Quality = EffectsQuality.Alti;
            var center = GetNode<Camera2D>("Camera").GetScreenCenterPosition();
            var prova = fx.Definitions["prova"];

            fx.Play("prova", center);
            if (fx.Emitters != 1 || fx.ActiveParticles != prova.Amount)
            {
                return $"un'esplosione: {fx.Emitters} emettitori, {fx.ActiveParticles} particelle";
            }

            fx.Play("prova", center + new Vector2(ActiveMapWidth * TileSize * 4, 0));
            if (fx.Emitters != 1)
            {
                return "un effetto fuori dalla vista è partito";
            }

            var owner = new Node2D { Name = "ProvaEffetti", Position = center };
            AddChild(owner);
            var id = fx.Attach("prova", owner, new Vector2(0, -8));
            fx._Process(0);
            if (fx.Emitters != 2 || fx.Attachments != 1)
            {
                return $"emettitore legato: {fx.Emitters} emettitori";
            }

            // The owner leaves the tree (a block taken down): its emitter goes away by itself.
            RemoveChild(owner);
            owner.Free();
            fx._Process(0);
            if (fx.Attachments != 0)
            {
                return "emettitore orfano dopo la rimozione del proprietario";
            }

            fx.Detach(id);
            fx.Clear();
            fx.Quality = EffectsQuality.Bassi;
            for (var i = 0; i < 200; i++)
            {
                fx.Play("prova", center);
            }

            if (fx.ActiveParticles > EffectRules.ParticleCap(EffectsQuality.Bassi) || fx.ActiveParticles == 0)
            {
                return $"limite Bassi: {fx.ActiveParticles} particelle";
            }

            var pool = fx.PooledNodes;
            fx.Clear();
            for (var i = 0; i < 200; i++)
            {
                fx.Play("prova", center);
            }

            if (fx.PooledNodes != pool)
            {
                return $"il pool cresce: {pool} → {fx.PooledNodes} nodi";
            }

            fx.Quality = EffectsQuality.Spenti;
            fx.Play("prova", center);
            if (fx.Emitters != 0 || fx.ActiveParticles != 0)
            {
                return $"Spenti: {fx.Emitters} emettitori, {fx.ActiveParticles} particelle";
            }

            GD.Print($"EFFETTI: {fx.Definitions.Count} definizioni, pool {fx.PooledNodes} nodi");
            return null;
        }
        finally
        {
            fx.Clear();
            fx.Quality = before;
        }
    }

    private bool PressSetting(string name)
    {
        if (_settingsPanel?.FindChild(name, recursive: true, owned: false) is not BaseButton button)
        {
            return false;
        }

        button.EmitSignal(BaseButton.SignalName.Pressed);
        return true;
    }

    private bool ToggleSetting(string name, bool on)
    {
        if (_settingsPanel?.FindChild(name, recursive: true, owned: false) is not CheckButton check)
        {
            return false;
        }

        check.ButtonPressed = on;
        return true;
    }
}
