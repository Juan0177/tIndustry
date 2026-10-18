using Godot;
using TIndustry.Shared;

namespace TIndustry.Godot;

/// <summary>
/// Full-screen Ricerca: tech-tree graph (nodes + elbow edges + path highlight),
/// pan/zoom, detail panel — Raylib-parity feel. Not a checklist.
/// </summary>
public partial class ResearchPanel : Control
{
    /// <summary>Left edge of the title, hint and funds lines.</summary>
    private const float Margin = 28f;

    /// <summary>Top of the hint under the title row.</summary>
    private const float HintTop = 58f;

    /// <summary>Height of the bottom row (Indietro and the status line) under the tree.</summary>
    private const float BottomRow = 56f;

    private IGameView? _view;
    private Action<SimCommand>? _submit;
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
    private Label? _hintLabel;
    private Label? _legendLabel;
    private HBoxContainer? _header;
    private Label? _statusLabel;
    private Button? _unlockButton;
    private Button? _backButton;
    private string? _selectedId;
    // The status line is kept as a function so a new language writes it again.
    private Func<string>? _statusMessage;

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

    public void Open(IGameView factory, Action<SimCommand> submit)
    {
        _view = factory;
        _submit = submit;
        _statusMessage = null;
        _graph = TechTreeLayout.Build(factory.Content);
        // Default focus: the first thing you can research now (Mindustry draws the eye there too).
        _selectedId ??= _graph.Nodes
            .Where(n => !n.IsRoot)
            .FirstOrDefault(n => factory.Research.GetNodeState(n.Structure) == ResearchNodeState.Available)
            ?.Structure.Id ?? TechTreeLayout.RootId;
        if (_canvas is not null)
        {
            _canvas.Graph = _graph;
            _canvas.SelectedId = _selectedId;
            _canvas.Research = factory.Research;
            _canvas.ResetView();
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

    /// <summary>True when the tree view shows every node (playtests).</summary>
    internal bool WholeTreeInView => _canvas?.WholeTreeInView() ?? false;

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
                _canvas?.ResetView(wholeTree: true);
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
            Color = UiColors.FondoSchermo,
            MouseFilter = MouseFilterEnum.Stop
        };
        bg.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(bg);

        // Title row: icon, "Ricerca", the funds on the right.
        _header = new HBoxContainer { Position = new Vector2(Margin, 18), ThemeTypeVariation = UiTheme.RigaLarga };
        AddChild(_header);
        var icon = HudStyle.MakeIcon(UiTheme.Icon("ricerca"), 32, pixel: true);
        icon.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        _header.AddChild(icon);
        _header.AddChild(new Label { Text = "menu.ricerca", ThemeTypeVariation = UiTheme.Titolo, SizeFlagsHorizontal = SizeFlags.ExpandFill });
        _walletLabel = new Label { Text = "", ThemeTypeVariation = UiTheme.TestoDenaro, VerticalAlignment = VerticalAlignment.Center };
        _header.AddChild(_walletLabel);

        _hintLabel = new Label
        {
            Text = "ricerca.suggerimento",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            ThemeTypeVariation = UiTheme.TestoTenue
        };
        AddChild(_hintLabel);

        _legendLabel = new Label
        {
            Text = "ricerca.legenda",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            ThemeTypeVariation = UiTheme.TestoTenue
        };
        AddChild(_legendLabel);

        _canvas = new TechTreeCanvas { Name = "TechTreeCanvas" };
        _canvas.NodeSelected += SelectId;
        _canvas.TooltipFor = NodeTooltip;
        _canvas.ViewChanged += RefreshDetailChrome;
        AddChild(_canvas);

        _detailPanel = new PanelContainer { Name = "Detail", ThemeTypeVariation = UiTheme.Scheda };
        AddChild(_detailPanel);

        // The text scrolls inside the card; the unlock button and the zoom line stay at its bottom.
        var detailRoot = new VBoxContainer { ThemeTypeVariation = UiTheme.ColonnaLarga };
        _detailPanel.AddChild(detailRoot);
        var detailScroll = new ScrollContainer
        {
            Name = "DetailScroll",
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled
        };
        detailRoot.AddChild(detailScroll);
        var clear = new MarginContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, ThemeTypeVariation = UiTheme.MargineBarra };
        detailScroll.AddChild(clear);
        var lines = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        clear.AddChild(lines);

        _detailTitle = new Label { Text = "—", ThemeTypeVariation = UiTheme.TitoloChiaro, AutowrapMode = TextServer.AutowrapMode.WordSmart };
        lines.AddChild(_detailTitle);

        _detailUsage = DetailLine(lines);
        _detailState = new Label { Text = "" };
        lines.AddChild(_detailState);
        _detailCost = DetailLine(lines);
        _detailPrereq = DetailLine(lines);
        _detailManca = DetailLine(lines);
        _detailManca.ThemeTypeVariation = UiTheme.TestoRosso;

        _unlockButton = new Button
        {
            Text = "ricerca.sblocca",
            CustomMinimumSize = new Vector2(0, 48),
            ThemeTypeVariation = UiTheme.PulsantePrimario
        };
        _unlockButton.Pressed += OnUnlockPressed;
        detailRoot.AddChild(_unlockButton);

        _detailZoom = new Label
        {
            Text = "",
            ClipText = true,
            TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis,
            ThemeTypeVariation = UiTheme.TestoTenue
        };
        detailRoot.AddChild(_detailZoom);

        _backButton = new Button
        {
            Text = "comune.indietro",
            CustomMinimumSize = new Vector2(180, 40)
        };
        _backButton.Pressed += Close;
        AddChild(_backButton);

        _statusLabel = new Label { Text = "", ThemeTypeVariation = UiTheme.TestoAccento };
        AddChild(_statusLabel);

        Resized += LayoutChrome;
        LayoutChrome();
    }

    public override void _Notification(int what)
    {
        // New language: the detail, the status and the tree labels (drawn, not translated by Godot) follow.
        if (what == NotificationTranslationChanged && Visible)
        {
            Refresh();
            CallDeferred(MethodName.LayoutChrome);
        }
    }

    /// <summary>A wrapping secondary line of the detail panel.</summary>
    private static Label DetailLine(VBoxContainer parent)
    {
        var label = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart, ThemeTypeVariation = UiTheme.TestoTenue };
        parent.AddChild(label);
        return label;
    }

    private void LayoutChrome()
    {
        var size = Size;
        if (size.X < 10 || size.Y < 10)
        {
            return;
        }

        if (_header is not null)
        {
            _header.Size = new Vector2(size.X - Margin * 2f, 0f);
        }

        // Hint and legend wrap to the window width; the tree and the detail start under them.
        var top = HintTop;
        foreach (var line in new[] { _hintLabel, _legendLabel })
        {
            if (line is null)
            {
                continue;
            }

            line.Position = new Vector2(Margin, top);
            line.Size = new Vector2(size.X - Margin * 2f, 0f);
            top += line.GetLineCount() * line.GetLineHeight() + 2f;
        }

        var contentTop = top + 8f;
        // The tree, the detail card and the bottom row (Indietro, status) share the height left.
        var contentBottom = size.Y - BottomRow;
        var detailW = Math.Clamp(size.X * 0.3f, 300f, 400f);
        var detailX = size.X - detailW - 28f;
        var detailY = contentTop;
        var detailH = Math.Max(200f, contentBottom - detailY);

        if (_detailPanel is not null)
        {
            _detailPanel.Position = new Vector2(detailX, detailY);
            _detailPanel.Size = new Vector2(detailW, detailH);
        }

        if (_canvas is not null)
        {
            var canvasX = 24f;
            var canvasY = contentTop;
            var canvasW = Math.Max(280f, detailX - canvasX - 16f);
            var canvasH = Math.Max(200f, contentBottom - canvasY);
            _canvas.Position = new Vector2(canvasX, canvasY);
            _canvas.Size = new Vector2(canvasW, canvasH);
            _canvas.QueueRedraw();
        }

        if (_backButton is not null)
        {
            _backButton.Position = new Vector2(Margin, size.Y - BottomRow + 8f);
            _backButton.Size = new Vector2(180, 40);
        }

        if (_statusLabel is not null)
        {
            _statusLabel.Position = new Vector2(230, size.Y - BottomRow + 14f);
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
        if (_view is null)
        {
            return;
        }

        _graph = TechTreeLayout.Build(_view.Content);
        if (_canvas is not null)
        {
            _canvas.Graph = _graph;
            _canvas.Research = _view.Research;
            _canvas.SelectedId = _selectedId;
            _canvas.QueueRedraw();
        }

        if (_walletLabel is not null)
        {
            _walletLabel.Text = Testi.F("ricerca.fondi", _view.Wallet.Money);
        }

        RefreshDetail();
        if (_statusLabel is not null)
        {
            _statusLabel.Text = _statusMessage?.Invoke() ?? "";
        }
    }

    private void RefreshDetailChrome()
    {
        if (_detailZoom is not null && _canvas is not null)
        {
            _detailZoom.Text = Testi.F("ricerca.zoom", _canvas.Zoom.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture));
        }
    }

    private void RefreshDetail()
    {
        RefreshDetailChrome();
        if (_view is null || _selectedId is null
            || _detailTitle is null || _detailUsage is null || _detailState is null
            || _detailCost is null || _detailPrereq is null
            || _detailManca is null || _unlockButton is null)
        {
            return;
        }

        if (_selectedId == TechTreeLayout.RootId)
        {
            _detailTitle.Text = "CORE";
            _detailUsage.Text = Testi.T("ricerca.core.descrizione");
            _detailState.Text = "";
            _detailCost.Text = "";
            _detailPrereq.Text = "";
            _detailManca.Text = "";
            _unlockButton.Disabled = true;
            _unlockButton.Text = Testi.T("ricerca.seleziona");
            return;
        }

        var structure = _view.Content.FindStructure(_selectedId);
        if (structure is null)
        {
            return;
        }

        var state = _view.Research.GetNodeState(structure);
        _detailTitle.Text = Testi.Struttura(structure.Id, _view.Content);
        _detailUsage.Text = FormatStructureUsage(structure.Id, _view.Content);
        _detailState.Text = state switch
        {
            ResearchNodeState.Unlocked => Testi.T(structure.IsStub ? "ricerca.stato.segnaposto" : "ricerca.stato.sbloccato"),
            ResearchNodeState.Available => Testi.T("ricerca.stato.disponibile"),
            _ => Testi.T("ricerca.stato.bloccato")
        };
        _detailState.ThemeTypeVariation = state switch
        {
            ResearchNodeState.Unlocked => UiTheme.TestoVerde,
            ResearchNodeState.Available => UiTheme.TestoAccento,
            _ => UiTheme.TestoTenue
        };

        _detailCost.Text = Testi.F("ricerca.costi",
            FormatUnlockRequirement(structure.Unlock, _view.Content), FormatBuildCost(structure.Id, _view.Content));
        _detailPrereq.Text = FormatPrereqs(structure, _view);

        var manca = FormatMissingUnlockResources(structure, _view);
        if (!string.IsNullOrEmpty(manca))
        {
            _detailManca.Text = manca;
        }
        else if (structure.IsStub)
        {
            _detailManca.Text = Testi.T("ricerca.segnaposto");
        }
        else
        {
            _detailManca.Text = "";
        }

        var canUnlock = _view.Research.CanUnlock(structure, _view.Wallet);
        _unlockButton.Disabled = !canUnlock;
        _unlockButton.Text = Testi.T(_view.Research.IsUnlocked(structure.Id)
            ? "ricerca.gia.sbloccato"
            : !_view.Research.MeetsPrerequisites(structure)
                ? "ricerca.prerequisiti.mancanti"
                : canUnlock ? "ricerca.sblocca" : "rifiuto.InsufficientResources");
    }

    private void OnUnlockPressed()
    {
        if (_view is null || _selectedId is null || _submit is null)
        {
            return;
        }

        if (_view.Content.FindStructure(_selectedId) is null)
        {
            return;
        }

        // Outcome (and the refusal code) comes back through the host events.
        _submit(new UnlockStructure(0, _selectedId));
    }

    /// <summary>Host applied a command: refresh the tree and report an unlock.</summary>
    public void OnCommandApplied(SimCommand command)
    {
        if (command is not UnlockStructure unlock || _view is null)
        {
            return;
        }

        var structure = _view.Content.FindStructure(unlock.StructureId);
        if (structure is not null)
        {
            var content = _view.Content;
            _statusMessage = () => Testi.F(structure.IsStub ? "ricerca.sbloccato.segnaposto" : "ricerca.sbloccato.nome",
                Testi.Struttura(structure.Id, content));
        }

        UnlockedChanged?.Invoke();
        Refresh();
    }

    /// <summary>Host refused a command: show its reason in the status line.</summary>
    public void OnCommandRejected(SimCommand command, Refusal reason)
    {
        if (command is not UnlockStructure)
        {
            return;
        }

        var view = _view;
        _statusMessage = () => Testi.Rifiuto(reason, view);
        Refresh();
    }

    /// <summary>Hover text on a tree node: name, state and what it costs / what is missing.</summary>
    private string NodeTooltip(string id)
    {
        if (_view is null)
        {
            return "";
        }

        if (id == TechTreeLayout.RootId)
        {
            return "CORE";
        }

        var structure = _view.Content.FindStructure(id);
        if (structure is null)
        {
            return "";
        }

        return _view.Research.GetNodeState(structure) switch
        {
            ResearchNodeState.Unlocked => $"{Testi.Struttura(structure.Id, _view.Content)}\n{Testi.T("ricerca.nodo.sbloccato")}",
            ResearchNodeState.Available =>
                $"{Testi.Struttura(structure.Id, _view.Content)}\n"
                + Testi.F("ricerca.nodo.sblocco", FormatUnlockRequirement(structure.Unlock, _view.Content))
                + (FormatMissingUnlockResources(structure, _view) is { Length: > 0 } manca ? $"\n{manca}" : ""),
            _ => $"{Testi.Struttura(structure.Id, _view.Content)}\n{FormatPrereqs(structure, _view)}"
        };
    }

    private static string FormatStructureUsage(string structureId, FactoryContent content)
    {
        // Miners name the ores they dig, from the data (terrain.deposits and the miner tiers).
        var key = $"struttura.{structureId}.descrizione";
        var hint = Testi.T(key) == key
            ? Testi.T("ricerca.uso.generico")
            : Testi.F(key, Testi.Scava(content, structureId));

        var recipeId = structureId switch
        {
            "smelter" => "smelt-iron",
            "assembler" => "craft-copper-wire",
            _ => null
        };
        var recipe = recipeId is null ? null : content.FindRecipe(recipeId);
        if (recipe is null)
        {
            return Testi.F("ricerca.uso", hint);
        }

        string Fmt(IReadOnlyList<ResourceAmount> list) =>
            string.Join(" + ", list.Select(a => $"{a.Amount}× {Testi.Oggetto(a.ItemId, content)}"));

        return Testi.F("ricerca.uso", hint)
            + $"\nI/O: {Fmt(recipe.Inputs)} → {Fmt(recipe.Outputs)} ({recipe.DurationSeconds.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture)}s)";
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
                .Select(m => $"{m.Amount}× {Testi.Oggetto(m.ItemId, content)}"));
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

    private static string FormatUnlockRequirement(UnlockRequirement? unlock, FactoryContent content)
    {
        if (unlock is null)
        {
            return Testi.T("ricerca.gratis");
        }

        var materials = string.Join(" + ",
            unlock.Materials.Select(entry => $"{entry.Amount} {Testi.Oggetto(entry.ItemId, content)}"));
        return materials.Length == 0
            ? $"${unlock.Money}"
            : $"${unlock.Money} + {materials}";
    }

    private static string FormatMissingUnlockResources(StructureDefinition structure, IGameView factory)
    {
        if (factory.Research.IsUnlocked(structure.Id) || !factory.Research.MeetsPrerequisites(structure))
        {
            return string.Empty;
        }

        if (structure.Unlock is null || factory.Research.CanUnlock(structure, factory.Wallet))
        {
            return string.Empty;
        }

        var missing = new List<string>();
        if (factory.Wallet.Money < structure.Unlock.Money)
        {
            missing.Add($"${structure.Unlock.Money - factory.Wallet.Money}");
        }

        foreach (var entry in structure.Unlock.Materials)
        {
            var have = factory.Wallet.MaterialCount(entry.ItemId);
            if (have < entry.Amount)
            {
                missing.Add($"{entry.Amount - have} {Testi.Oggetto(entry.ItemId, factory.Content)}");
            }
        }

        return missing.Count == 0 ? string.Empty : Testi.F("coda.manca", string.Join(" + ", missing));
    }

    private static string FormatPrereqs(StructureDefinition structure, IGameView factory)
    {
        if (structure.Prerequisites.Count == 0)
        {
            return Testi.F("ricerca.prerequisiti", Testi.T("ricerca.prerequisiti.nessuno"));
        }

        var bits = structure.Prerequisites.Select(id =>
        {
            var name = Testi.Struttura(id, factory.Content);
            return factory.Research.IsUnlocked(id) ? name : Testi.F("ricerca.prerequisito.manca", name);
        });
        return Testi.F("ricerca.prerequisiti", string.Join(", ", bits));
    }
}
