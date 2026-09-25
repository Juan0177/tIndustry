using Godot;
using TIndustry.Shared;

namespace TIndustry.Godot;

/// <summary>
/// Full-screen Ricerca: tech-tree graph (nodes + elbow edges + path highlight),
/// pan/zoom, detail panel — Raylib-parity feel. Not a checklist.
/// </summary>
public partial class ResearchPanel : Control
{
    private static readonly Color ScreenBg = new(14 / 255f, 18 / 255f, 18 / 255f, 1f);
    private static readonly Color TitleColor = new(239 / 255f, 238 / 255f, 224 / 255f, 1f);
    private static readonly Color HintMuted = new(112 / 255f, 124 / 255f, 119 / 255f, 1f);
    private static readonly Color HintSoft = new(164 / 255f, 173 / 255f, 168 / 255f, 1f);
    private static readonly Color DetailFill = new(24 / 255f, 30 / 255f, 28 / 255f, 1f);
    private static readonly Color DetailBorder = new(60 / 255f, 70 / 255f, 64 / 255f, 1f);
    private static readonly Color StatusCoral = new(225 / 255f, 140 / 255f, 110 / 255f, 1f);
    private static readonly Color MancaCoral = new(220 / 255f, 140 / 255f, 120 / 255f, 1f);
    private static readonly Color Gold = new(211 / 255f, 164 / 255f, 76 / 255f, 1f);

    private FactorySlice? _slice;
    private TechTreeLayout.Graph? _graph;
    private TechTreeCanvas? _canvas;
    private PanelContainer? _detailPanel;
    private Label? _detailTitle;
    private Label? _detailUsage;
    private Label? _detailState;
    private Label? _detailCost;
    private Label? _detailPrereq;
    private Label? _detailManca;
    private Label? _detailZoom;
    private Label? _walletLabel;
    private Label? _statusLabel;
    private Button? _unlockButton;
    private Button? _backButton;
    private string? _selectedId;
    private string? _statusMessage;

    public event Action? Closed;
    public event Action? UnlockedChanged;

    public override void _Ready()
    {
        Name = "ResearchPanel";
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;
        Visible = false;
        BuildUi();
    }

    public void Open(FactorySlice slice)
    {
        _slice = slice;
        _statusMessage = null;
        _graph = TechTreeLayout.Build(slice.Content);
        _selectedId ??= _graph.Nodes.FirstOrDefault()?.Structure.Id;
        if (_canvas is not null)
        {
            _canvas.ResetView();
            _canvas.Graph = _graph;
            _canvas.SelectedId = _selectedId;
            _canvas.Research = slice.Research;
        }

        Visible = true;
        LayoutChrome();
        Refresh();
    }

    public void Close()
    {
        Visible = false;
        Closed?.Invoke();
    }

    public bool IsOpen => Visible;

    /// <summary>Select a node by id (capture / tests).</summary>
    public void SelectStructure(string id) => SelectId(id);

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!Visible)
        {
            return;
        }

        if (@event is InputEventKey key && key.Pressed && !key.Echo)
        {
            if (key.Keycode is Key.Escape or Key.T)
            {
                Close();
                GetViewport().SetInputAsHandled();
                return;
            }

            if (key.Keycode is Key.H or Key.Home)
            {
                _canvas?.ResetView();
                RefreshDetailChrome();
                GetViewport().SetInputAsHandled();
                return;
            }

            if (_graph is not null && _graph.Nodes.Count > 0
                && key.Keycode is Key.Up or Key.Left or Key.Down or Key.Right)
            {
                var ids = _graph.Nodes.Select(n => n.Structure.Id).ToList();
                var idx = Math.Max(0, ids.IndexOf(_selectedId ?? ids[0]));
                idx = key.Keycode is Key.Up or Key.Left
                    ? (idx - 1 + ids.Count) % ids.Count
                    : (idx + 1) % ids.Count;
                SelectId(ids[idx]);
                GetViewport().SetInputAsHandled();
            }
        }
    }

    private void BuildUi()
    {
        var bg = new ColorRect
        {
            Color = ScreenBg,
            MouseFilter = MouseFilterEnum.Stop
        };
        bg.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(bg);

        var title = new Label
        {
            Text = "Ricerca",
            Position = new Vector2(28, 22)
        };
        title.AddThemeColorOverride("font_color", TitleColor);
        title.AddThemeFontSizeOverride("font_size", 28);
        AddChild(title);

        var hint = new Label
        {
            Text = "Nodi e prerequisiti · T apre / Esc chiude · Shift+trascina · Ctrl+rotella zoom · H reset",
            Position = new Vector2(28, 58)
        };
        hint.AddThemeColorOverride("font_color", HintMuted);
        hint.AddThemeFontSizeOverride("font_size", 14);
        AddChild(hint);

        _walletLabel = new Label
        {
            Text = "Magazzino: $0",
            Position = new Vector2(28, 80)
        };
        _walletLabel.AddThemeColorOverride("font_color", HintSoft);
        _walletLabel.AddThemeFontSizeOverride("font_size", 14);
        AddChild(_walletLabel);

        _canvas = new TechTreeCanvas { Name = "TechTreeCanvas" };
        _canvas.NodeSelected += SelectId;
        _canvas.ViewChanged += RefreshDetailChrome;
        AddChild(_canvas);

        _detailPanel = new PanelContainer { Name = "Detail" };
        _detailPanel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = DetailFill,
            BorderColor = DetailBorder,
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            ContentMarginLeft = 16,
            ContentMarginRight = 16,
            ContentMarginTop = 14,
            ContentMarginBottom = 14
        });
        AddChild(_detailPanel);

        var detailRoot = new VBoxContainer();
        detailRoot.AddThemeConstantOverride("separation", 8);
        _detailPanel.AddChild(detailRoot);

        _detailTitle = new Label { Text = "—" };
        _detailTitle.AddThemeColorOverride("font_color", TitleColor);
        _detailTitle.AddThemeFontSizeOverride("font_size", 20);
        detailRoot.AddChild(_detailTitle);

        _detailUsage = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        _detailUsage.AddThemeColorOverride("font_color", HintSoft);
        _detailUsage.AddThemeFontSizeOverride("font_size", 13);
        detailRoot.AddChild(_detailUsage);

        _detailState = new Label { Text = "" };
        _detailState.AddThemeFontSizeOverride("font_size", 14);
        detailRoot.AddChild(_detailState);

        _detailCost = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        _detailCost.AddThemeColorOverride("font_color", HintSoft);
        _detailCost.AddThemeFontSizeOverride("font_size", 14);
        detailRoot.AddChild(_detailCost);

        _detailPrereq = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        _detailPrereq.AddThemeColorOverride("font_color", HintSoft);
        _detailPrereq.AddThemeFontSizeOverride("font_size", 13);
        detailRoot.AddChild(_detailPrereq);

        _detailManca = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        _detailManca.AddThemeColorOverride("font_color", MancaCoral);
        _detailManca.AddThemeFontSizeOverride("font_size", 13);
        detailRoot.AddChild(_detailManca);

        detailRoot.AddChild(new Control { CustomMinimumSize = new Vector2(0, 8) });

        _unlockButton = new Button
        {
            Text = "Conferma sblocco",
            CustomMinimumSize = new Vector2(0, 48)
        };
        StyleMenuButton(_unlockButton);
        _unlockButton.Pressed += OnUnlockPressed;
        detailRoot.AddChild(_unlockButton);

        _detailZoom = new Label { Text = "Zoom 1.00× · percorso evidenziato sul grafo" };
        _detailZoom.AddThemeColorOverride("font_color", new Color(126 / 255f, 137 / 255f, 132 / 255f));
        _detailZoom.AddThemeFontSizeOverride("font_size", 12);
        detailRoot.AddChild(_detailZoom);

        _backButton = new Button
        {
            Text = "Indietro",
            CustomMinimumSize = new Vector2(180, 40)
        };
        StyleMenuButton(_backButton);
        _backButton.Pressed += Close;
        AddChild(_backButton);

        _statusLabel = new Label { Text = "" };
        _statusLabel.AddThemeColorOverride("font_color", StatusCoral);
        _statusLabel.AddThemeFontSizeOverride("font_size", 16);
        AddChild(_statusLabel);

        Resized += LayoutChrome;
        LayoutChrome();
    }

    private static void StyleMenuButton(Button button)
    {
        button.AddThemeStyleboxOverride("normal", new StyleBoxFlat
        {
            BgColor = new Color(45 / 255f, 52 / 255f, 50 / 255f),
            ContentMarginLeft = 12,
            ContentMarginRight = 12,
            ContentMarginTop = 8,
            ContentMarginBottom = 8
        });
        button.AddThemeStyleboxOverride("hover", new StyleBoxFlat
        {
            BgColor = Gold,
            ContentMarginLeft = 12,
            ContentMarginRight = 12,
            ContentMarginTop = 8,
            ContentMarginBottom = 8
        });
        button.AddThemeStyleboxOverride("disabled", new StyleBoxFlat
        {
            BgColor = new Color(32 / 255f, 36 / 255f, 34 / 255f),
            ContentMarginLeft = 12,
            ContentMarginRight = 12,
            ContentMarginTop = 8,
            ContentMarginBottom = 8
        });
        button.AddThemeColorOverride("font_color", new Color(215 / 255f, 219 / 255f, 210 / 255f));
        button.AddThemeColorOverride("font_hover_color", new Color(25 / 255f, 28 / 255f, 26 / 255f));
        button.AddThemeColorOverride("font_disabled_color", new Color(120 / 255f, 128 / 255f, 122 / 255f));
    }

    private void LayoutChrome()
    {
        var size = Size;
        if (size.X < 10 || size.Y < 10)
        {
            return;
        }

        var detailW = Math.Clamp(size.X / 3f, 280f, 360f);
        var detailX = size.X - detailW - 28f;
        var detailY = 118f;
        var detailH = Math.Max(220f, size.Y - detailY - 90f);

        if (_detailPanel is not null)
        {
            _detailPanel.Position = new Vector2(detailX, detailY);
            _detailPanel.Size = new Vector2(detailW, detailH);
        }

        if (_canvas is not null)
        {
            var canvasX = 24f;
            var canvasY = 110f;
            var canvasW = Math.Max(280f, detailX - canvasX - 16f);
            var canvasH = Math.Max(200f, size.Y - canvasY - 90f);
            _canvas.Position = new Vector2(canvasX, canvasY);
            _canvas.Size = new Vector2(canvasW, canvasH);
            _canvas.QueueRedraw();
        }

        if (_backButton is not null)
        {
            _backButton.Position = new Vector2(28, size.Y - 70);
            _backButton.Size = new Vector2(180, 40);
        }

        if (_statusLabel is not null)
        {
            _statusLabel.Position = new Vector2(230, size.Y - 58);
            _statusLabel.Size = new Vector2(Math.Max(200, size.X - 260), 28);
        }
    }

    private void SelectId(string id)
    {
        _selectedId = id;
        if (_canvas is not null)
        {
            _canvas.SelectedId = id;
            _canvas.QueueRedraw();
        }

        RefreshDetail();
    }

    public void Refresh()
    {
        if (_slice is null)
        {
            return;
        }

        _graph = TechTreeLayout.Build(_slice.Content);
        if (_canvas is not null)
        {
            _canvas.Graph = _graph;
            _canvas.Research = _slice.Research;
            _canvas.SelectedId = _selectedId;
            _canvas.QueueRedraw();
        }

        if (_walletLabel is not null)
        {
            _walletLabel.Text =
                $"Magazzino: $ {_slice.Wallet.Money}   ·   verde = sbloccato · ambra = disponibile · grigio = bloccato";
        }

        RefreshDetail();
        if (_statusLabel is not null)
        {
            _statusLabel.Text = _statusMessage ?? "";
        }
    }

    private void RefreshDetailChrome()
    {
        if (_detailZoom is not null && _canvas is not null)
        {
            _detailZoom.Text = $"Zoom {_canvas.Zoom:0.00}× · percorso evidenziato sul grafo";
        }
    }

    private void RefreshDetail()
    {
        RefreshDetailChrome();
        if (_slice is null || _selectedId is null
            || _detailTitle is null || _detailUsage is null || _detailState is null
            || _detailCost is null || _detailPrereq is null
            || _detailManca is null || _unlockButton is null)
        {
            return;
        }

        var structure = _slice.Content.FindStructure(_selectedId);
        if (structure is null)
        {
            return;
        }

        var state = _slice.Research.GetNodeState(structure);
        _detailTitle.Text = structure.DisplayName;
        _detailUsage.Text = FormatStructureUsage(structure.Id, _slice.Content);
        _detailState.Text = state switch
        {
            ResearchNodeState.Unlocked => structure.IsStub
                ? "Stato: sbloccato (segnaposto)"
                : "Stato: sbloccato",
            ResearchNodeState.Available => "Stato: disponibile",
            _ => "Stato: bloccato"
        };
        _detailState.AddThemeColorOverride("font_color", state switch
        {
            ResearchNodeState.Unlocked => new Color(112 / 255f, 218 / 255f, 145 / 255f),
            ResearchNodeState.Available => new Color(220 / 255f, 170 / 255f, 110 / 255f),
            _ => new Color(140 / 255f, 148 / 255f, 142 / 255f)
        });

        _detailCost.Text = "Sblocco: " + FormatUnlockRequirement(structure.Unlock)
            + "\nCostruzione: " + FormatBuildCost(structure.Id, _slice.Content);
        _detailPrereq.Text = FormatPrereqs(structure, _slice);

        var manca = FormatMissingUnlockResources(structure, _slice);
        if (!string.IsNullOrEmpty(manca))
        {
            _detailManca.Text = manca;
            _detailManca.AddThemeColorOverride("font_color", MancaCoral);
        }
        else if (structure.IsStub)
        {
            _detailManca.Text = "Segnaposto: non costruibile ancora.";
            _detailManca.AddThemeColorOverride("font_color", new Color(180 / 255f, 120 / 255f, 100 / 255f));
        }
        else
        {
            _detailManca.Text = "";
        }

        var canUnlock = _slice.Research.CanUnlock(structure, _slice.Wallet);
        _unlockButton.Disabled = !canUnlock;
        _unlockButton.Text = _slice.Research.IsUnlocked(structure.Id)
            ? "Già sbloccato"
            : !_slice.Research.MeetsPrerequisites(structure)
                ? "Prerequisiti mancanti"
                : canUnlock ? "Conferma sblocco" : "Risorse insufficienti";
    }

    private void OnUnlockPressed()
    {
        if (_slice is null || _selectedId is null)
        {
            return;
        }

        var structure = _slice.Content.FindStructure(_selectedId);
        if (structure is null)
        {
            return;
        }

        if (_slice.Research.IsUnlocked(structure.Id))
        {
            _statusMessage = "Già sbloccato.";
        }
        else if (!_slice.Research.MeetsPrerequisites(structure))
        {
            _statusMessage = "Prerequisiti mancanti.";
        }
        else if (_slice.TryUnlockStructure(_selectedId))
        {
            _statusMessage = structure.IsStub
                ? $"{structure.DisplayName} sbloccato (segnaposto — non costruibile ancora)."
                : $"{structure.DisplayName} sbloccato!";
            UnlockedChanged?.Invoke();
        }
        else
        {
            _statusMessage = "Risorse insufficienti per sbloccare.";
        }

        Refresh();
    }

    private static string FormatStructureUsage(string structureId, FactoryContent content)
    {
        var hint = structureId switch
        {
            "miner" => "Estrae minerali dal deposito sotto · uscita su tutti i lati · 2×2",
            "miner-advanced" => "T2: 2× velocità · +25% efficienza · uscita multi-lato",
            "smelter" => "Fondi ore (carbone o corrente) · +20% craft se alimentato · 2×2",
            "assembler" => "Assembla prodotti · R ruota uscita · 2×2",
            "extractor" => "Tira 1 item da CORE/edificio · F filtro · R uscita",
            "conveyor-basic" => "Nastro T1 · flusso unidirezionale · R/rotella",
            "conveyor-fast" => "Nastro T2 · più veloce · R/rotella",
            "conveyor-express" => "Nastro T3 · max velocità · R/rotella",
            "junction" => "Incrocio a croce per nastri",
            "splitter" => "Nastro a T · alterna sinistra/destra",
            "sorter" => "Filtro item · match avanti, altri ai lati · C cicla",
            "conveyor-bridge" => "Ponte span 2–4 · estremi 1×1",
            "generator" => "Brucia carbone per energia · 2×2",
            "power-node" => "Nodo T1 · 1×1 · raggio 6 · auto-link gen",
            "power-node-t2" => "Nodo T2 · 2×2 · raggio 10 · auto-link gen",
            _ => "Struttura di fabbrica."
        };

        var recipeId = structureId switch
        {
            "smelter" => "smelt-iron",
            "assembler" => "craft-copper-wire",
            _ => null
        };
        if (recipeId is null)
        {
            return "Uso: " + hint;
        }

        var recipe = content.FindRecipe(recipeId);
        if (recipe is null)
        {
            return "Uso: " + hint;
        }

        string Fmt(IReadOnlyList<ResourceAmount> list) =>
            string.Join(" + ", list.Select(a => $"{a.Amount}× {content.DisplayName(a.ItemId)}"));

        return $"Uso: {hint}\nI/O: {Fmt(recipe.Inputs)} → {Fmt(recipe.Outputs)} ({recipe.DurationSeconds:0.#}s)";
    }

    private static string FormatBuildCost(string structureId, FactoryContent content)
    {
        var building = content.FindBuilding(structureId);
        if (building is not null)
        {
            return FormatMoneyAndMats(building.MoneyCost, building.BuildCost, content);
        }

        var conveyor = content.FindConveyor(structureId);
        if (conveyor is not null)
        {
            return FormatMoneyAndMats(conveyor.MoneyCost, conveyor.EffectiveBuildCost, content);
        }

        return "—";
    }

    private static string FormatMoneyAndMats(
        int money,
        IReadOnlyList<ResourceAmount> materials,
        FactoryContent content)
    {
        var mats = string.Join(" + ",
            materials.Where(m => m.Amount > 0)
                .Select(m => $"{m.Amount}× {content.DisplayName(m.ItemId)}"));
        if (money <= 0 && string.IsNullOrEmpty(mats))
        {
            return "—";
        }

        if (money <= 0)
        {
            return mats;
        }

        return string.IsNullOrEmpty(mats) ? $"${money}" : $"${money} + {mats}";
    }

    private static string FormatUnlockRequirement(UnlockRequirement? unlock)
    {
        if (unlock is null)
        {
            return "gratis";
        }

        var materials = string.Join(" + ",
            unlock.Materials.Select(entry => $"{entry.Amount} {ItemShortLabel(entry.ItemId)}"));
        return materials.Length == 0
            ? $"${unlock.Money}"
            : $"${unlock.Money} + {materials}";
    }

    private static string FormatMissingUnlockResources(StructureDefinition structure, FactorySlice slice)
    {
        if (slice.Research.IsUnlocked(structure.Id) || !slice.Research.MeetsPrerequisites(structure))
        {
            return string.Empty;
        }

        if (structure.Unlock is null || slice.Research.CanUnlock(structure, slice.Wallet))
        {
            return string.Empty;
        }

        var missing = new List<string>();
        if (slice.Wallet.Money < structure.Unlock.Money)
        {
            missing.Add($"${structure.Unlock.Money - slice.Wallet.Money}");
        }

        foreach (var entry in structure.Unlock.Materials)
        {
            var have = slice.Wallet.MaterialCount(entry.ItemId);
            if (have < entry.Amount)
            {
                missing.Add($"{entry.Amount - have} {ItemShortLabel(entry.ItemId)}");
            }
        }

        return missing.Count == 0 ? string.Empty : "Manca: " + string.Join(" + ", missing);
    }

    private static string FormatPrereqs(StructureDefinition structure, FactorySlice slice)
    {
        if (structure.Requires.Count == 0)
        {
            return "Prerequisiti: nessuno";
        }

        var bits = structure.Requires.Select(id =>
        {
            var name = slice.Content.FindStructure(id)?.DisplayName ?? id;
            return slice.Research.IsUnlocked(id) ? name : $"{name} (manca)";
        });
        return "Prerequisiti: " + string.Join(", ", bits);
    }

    private static string ItemShortLabel(string itemId) => itemId switch
    {
        "iron-ore" => "Ferro",
        "copper-ore" => "Rame",
        "coal" => "Carb.",
        "iron-plate" => "Lastre",
        "copper-wire" => "Fili",
        "lead-ore" => "Piombo",
        "lead-plate" => "Lastre Pb",
        "silicon" => "Silicio",
        _ => itemId
    };
}
