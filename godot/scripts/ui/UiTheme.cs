using Godot;
using System.Linq;

namespace TIndustry.Godot;

/// <summary>
/// The client's single UI theme (G4, Mindustry style): near-black panels with a 2 px accent edge, square corners,
/// white pixel icons and light text in the project's pixel font. Built once in code and set on the root window and on
/// the HUD layer (<see cref="ApplyTo"/>), so every Control of the HUD and of the panels inherits it; widgets pick a
/// look with <see cref="Control.ThemeTypeVariation"/> (the constants below) instead of local styles or overrides.
///
/// <para>Text uses only the native sizes of the two bitmap fonts (16 for text, 24 and 48 for titles), drawn without
/// smoothing, so the glyphs stay whole pixels: at 100%, 150% and 200% UI scale (the HUD layer's scale, see
/// <see cref="UiScale"/>) every font pixel is a whole number of screen pixels; at other scales Nearest sampling
/// repeats some rows and columns, still sharp but slightly uneven.</para>
///
/// <para>Every resource here (styles, fonts, icons) stays referenced by the static theme for the whole run, so no UI
/// wrapper is left for the garbage collector to finalize while the engine shuts down. A new colour variant
/// (<see cref="Refresh"/>) rebuilds the same theme in place and disposes the styles it replaced.</para>
/// </summary>
public static class UiTheme
{
    /// <summary>Native size of the text font (glyphs 8 px drawn ×2).</summary>
    public const int TextSize = 16;

    /// <summary>Native size of the title font (glyphs ×3).</summary>
    public const int TitleSize = 24;

    // Label variations.
    public const string TestoTenue = "TestoTenue";
    public const string TestoAccento = "TestoAccento";
    public const string TestoRosso = "TestoRosso";
    public const string TestoVerde = "TestoVerde";
    public const string TestoDenaro = "TestoDenaro";
    public const string TestoEnergia = "TestoEnergia";

    /// <summary>An item at the CORE's maximum (<see cref="UiColors.Pieno"/>).</summary>
    public const string TestoPieno = "TestoPieno";
    public const string Titolo = "Titolo";
    public const string TitoloChiaro = "TitoloChiaro";
    public const string Avviso = "Avviso";
    public const string AvvisoRosso = "AvvisoRosso";

    // Mercato: price up / down and a crash (the text always carries ▲, ▼ or "Crollo" too).
    public const string TestoSu = "TestoSu";
    public const string TestoGiu = "TestoGiu";
    public const string TestoCrollo = "TestoCrollo";

    // PanelContainer variations (the plain PanelContainer is the HUD "Pannello").
    public const string Scheda = "Scheda";
    public const string Slot = "Slot";
    public const string SlotScelto = "SlotScelto";
    public const string SlotBloccato = "SlotBloccato";
    public const string SlotMancante = "SlotMancante";
    public const string Riga = "Riga";
    public const string Voce = "Voce";
    public const string VoceFatta = "VoceFatta";
    public const string VoceBloccata = "VoceBloccata";

    // Button variations (the plain Button is "Pulsante").
    public const string PulsanteScelto = "PulsanteScelto";
    public const string PulsantePrimario = "PulsantePrimario";
    public const string PulsantePiatto = "PulsantePiatto";
    public const string PulsanteRiga = "PulsanteRiga";
    public const string PulsanteRigaScelta = "PulsanteRigaScelta";

    // Box and grid spacing variations (the plain boxes keep 4 px).
    public const string RigaStretta = "RigaStretta";
    public const string RigaLarga = "RigaLarga";
    public const string RigaAmpia = "RigaAmpia";
    public const string ColonnaStretta = "ColonnaStretta";
    public const string ColonnaLarga = "ColonnaLarga";
    public const string GrigliaLarga = "GrigliaLarga";

    // MarginContainer variation: the content of a ScrollContainer, kept clear of its vertical bar.
    public const string MargineBarra = "MargineBarra";

    private static Theme? _theme;
    // Styles and textures made by Build, disposed when Refresh replaces them.
    private static readonly List<Resource> Owned = [];
    private static FontFile? _textFont;
    private static FontFile? _titleFont;

    /// <summary>The theme (built on first use).</summary>
    public static Theme Theme => _theme ??= Build();

    /// <summary>The text font (size <see cref="TextSize"/>), for controls that draw text themselves.</summary>
    public static Font TextFont => _textFont ??= LoadFont("res://assets/ui/carattere-testo.fnt");

    /// <summary>The title font (size <see cref="TitleSize"/>).</summary>
    public static Font TitleFont => _titleFont ??= LoadFont("res://assets/ui/carattere-titoli.fnt");

    /// <summary>
    /// Sets the theme on <paramref name="window"/> (its popups and tooltips) and on the HUD's root Control
    /// (<see cref="UiScale.RootOf"/>): a theme reaches a Control only through Control and Window parents, so the HUD's
    /// CanvasLayer would otherwise cut the panels off from the window's theme.
    /// </summary>
    public static void ApplyTo(Window window, Control hudRoot)
    {
        window.Theme = Theme;
        hudRoot.Theme = Theme;
    }

    /// <summary>
    /// Interface icon <c>ui-&lt;name&gt;</c> generated by tools/art (32 px, or 16 px when <paramref name="small"/>),
    /// cached by <see cref="Sprites"/>; null when missing.
    /// </summary>
    public static Texture2D? Icon(string name, bool small = false) =>
        Sprites.TryFor(small ? $"ui/ui-{name}-16" : $"ui/ui-{name}");

    private static FontFile LoadFont(string path)
    {
        var font = GD.Load<FontFile>(path);
        // Bitmap glyphs only at whole multiples of their native size (never resampled to an odd size).
        font.FixedSizeScaleMode = TextServer.FixedSizeScaleMode.IntegerOnly;
        return font;
    }

    /// <summary>
    /// Rebuilds the theme with the current <see cref="UiColors"/> (a new colour variant): the new items are built in a
    /// scratch theme and merged into the live one in one go, so every Control gets one theme change instead of one
    /// per item; the replaced styles are disposed.
    /// </summary>
    public static void Refresh()
    {
        if (_theme is null)
        {
            return;
        }

        var replaced = Owned.ToArray();
        Owned.Clear();
        using (var scratch = new Theme())
        {
            Populate(scratch);
            // Same item names every time: merging overwrites each one, no Clear (which would notify again).
            _theme.MergeWith(scratch);
        }

        foreach (var resource in replaced)
        {
            resource.Dispose();
        }
    }

    private static T Own<T>(T resource) where T : Resource
    {
        Owned.Add(resource);
        return resource;
    }

    private static Theme Build()
    {
        var t = new Theme { DefaultFont = TextFont, DefaultFontSize = TextSize };
        Populate(t);
        return t;
    }

    private static void Populate(Theme t)
    {
        BuildLabels(t);
        BuildPanels(t);
        BuildButtons(t);
        BuildToggles(t);
        BuildFields(t);
        BuildSliders(t);
        BuildContainers(t);
        BuildScrollAndTooltip(t);
    }

    private static StyleBoxFlat Box(Color bg, Color border, int borderWidth, float marginX, float marginY)
    {
        var box = Own(new StyleBoxFlat { BgColor = bg, BorderColor = border });
        box.SetBorderWidthAll(borderWidth);
        box.SetCornerRadiusAll(0);
        box.ContentMarginLeft = marginX;
        box.ContentMarginRight = marginX;
        box.ContentMarginTop = marginY;
        box.ContentMarginBottom = marginY;
        return box;
    }

    private static void LabelLook(Theme t, string type, Color color, Font? font = null, int size = TextSize)
    {
        if (type != "Label")
        {
            t.SetTypeVariation(type, "Label");
        }

        t.SetColor("font_color", type, color);
        t.SetFontSize("font_size", type, size);
        if (font is not null)
        {
            t.SetFont("font", type, font);
        }
    }

    private static void BuildLabels(Theme t)
    {
        LabelLook(t, "Label", UiColors.Testo);
        t.SetConstant("line_spacing", "Label", 0);
        LabelLook(t, TestoTenue, UiColors.TestoTenue);
        LabelLook(t, TestoAccento, UiColors.Accento);
        LabelLook(t, TestoRosso, UiColors.Insufficiente);
        LabelLook(t, TestoVerde, UiColors.Fatto);
        LabelLook(t, TestoDenaro, UiColors.Denaro);
        LabelLook(t, TestoEnergia, UiColors.Energia);
        LabelLook(t, TestoPieno, UiColors.Pieno);
        LabelLook(t, TestoSu, UiColors.MercatoSu);
        LabelLook(t, TestoGiu, UiColors.MercatoGiu);
        LabelLook(t, TestoCrollo, UiColors.MercatoCrollo);
        LabelLook(t, Titolo, UiColors.Accento, TitleFont, TitleSize);
        LabelLook(t, TitoloChiaro, UiColors.Testo, TitleFont, TitleSize);
        foreach (var (type, color) in new[] { (Avviso, UiColors.Testo), (AvvisoRosso, UiColors.Insufficiente) })
        {
            LabelLook(t, type, color);
            t.SetColor("font_shadow_color", type, UiColors.Ombra);
            t.SetConstant("shadow_offset_x", type, 2);
            t.SetConstant("shadow_offset_y", type, 2);
        }
    }

    private static void BuildPanels(Theme t)
    {
        t.SetStylebox("panel", "PanelContainer", Box(UiColors.Fondo, UiColors.Accento, 2, 8, 8));
        t.SetStylebox("panel", "Panel", Box(UiColors.Fondo, UiColors.Accento, 2, 8, 8));
        void Panel(string type, StyleBoxFlat box)
        {
            t.SetTypeVariation(type, "PanelContainer");
            t.SetStylebox("panel", type, box);
        }

        Panel(Scheda, Box(UiColors.Fondo, UiColors.Accento, 2, 20, 16));
        Panel(Slot, Box(UiColors.FondoSlot, UiColors.Bordo, 2, 2, 2));
        Panel(SlotScelto, Box(UiColors.FondoScelto, UiColors.Accento, 2, 2, 2));
        Panel(SlotBloccato, Box(UiColors.FondoBloccato, UiColors.BordoBloccato, 2, 2, 2));
        Panel(SlotMancante, Box(UiColors.FondoSlot, UiColors.Insufficiente, 2, 2, 2));
        Panel(Riga, Box(UiColors.FondoSlot, UiColors.FondoSlot, 0, 8, 2));
        Panel(Voce, Box(UiColors.FondoSlot, UiColors.Accento, 2, 12, 8));
        Panel(VoceFatta, Box(UiColors.FondoSlot, UiColors.Fatto, 2, 12, 8));
        Panel(VoceBloccata, Box(UiColors.FondoBloccato, UiColors.BordoBloccato, 2, 12, 8));
    }

    /// <summary>Styles and colours of one button look; <paramref name="type"/> "Button" is the plain one.</summary>
    private static void ButtonLook(Theme t, string type, StyleBoxFlat normal, StyleBoxFlat hover, StyleBoxFlat pressed,
        Color text, Color hoverText, Color pressedText)
    {
        if (type != "Button")
        {
            t.SetTypeVariation(type, "Button");
        }

        var disabled = Box(UiColors.FondoBloccato, UiColors.BordoBloccato, 2, normal.ContentMarginLeft, normal.ContentMarginTop);
        t.SetStylebox("normal", type, normal);
        t.SetStylebox("hover", type, hover);
        t.SetStylebox("pressed", type, pressed);
        t.SetStylebox("hover_pressed", type, pressed);
        t.SetStylebox("disabled", type, disabled);
        t.SetStylebox("focus", type, Own(new StyleBoxEmpty()));
        t.SetColor("font_color", type, text);
        t.SetColor("font_focus_color", type, text);
        t.SetColor("font_hover_color", type, hoverText);
        t.SetColor("font_pressed_color", type, pressedText);
        t.SetColor("font_hover_pressed_color", type, pressedText);
        t.SetColor("font_disabled_color", type, UiColors.TestoDisattivo);
        t.SetColor("icon_normal_color", type, text);
        t.SetColor("icon_focus_color", type, text);
        t.SetColor("icon_hover_color", type, hoverText);
        t.SetColor("icon_pressed_color", type, pressedText);
        t.SetColor("icon_hover_pressed_color", type, pressedText);
        t.SetColor("icon_disabled_color", type, UiColors.TestoDisattivo);
        t.SetConstant("h_separation", type, 8);
        t.SetConstant("icon_max_width", type, 16);
        t.SetFontSize("font_size", type, TextSize);
    }

    private static void BuildButtons(Theme t)
    {
        ButtonLook(t, "Button",
            Box(UiColors.FondoSlot, UiColors.Bordo, 2, 12, 6),
            Box(UiColors.FondoSopra, UiColors.Accento, 2, 12, 6),
            Box(UiColors.FondoScelto, UiColors.Accento, 2, 12, 6),
            UiColors.Testo, UiColors.Accento, UiColors.Accento);
        ButtonLook(t, PulsanteScelto,
            Box(UiColors.FondoScelto, UiColors.Accento, 2, 12, 6),
            Box(UiColors.FondoScelto, UiColors.Accento, 2, 12, 6),
            Box(UiColors.FondoScelto, UiColors.Accento, 2, 12, 6),
            UiColors.Accento, UiColors.Accento, UiColors.Accento);
        ButtonLook(t, PulsantePrimario,
            Box(UiColors.FondoScelto, UiColors.Accento, 2, 12, 6),
            Box(UiColors.Accento, UiColors.Accento, 2, 12, 6),
            Box(UiColors.Accento, UiColors.Accento, 2, 12, 6),
            UiColors.Accento, UiColors.TestoSuAccento, UiColors.TestoSuAccento);
        var flat = Box(Colors.Transparent, Colors.Transparent, 0, 0, 2);
        ButtonLook(t, PulsantePiatto, flat, flat, flat, UiColors.Testo, UiColors.Accento, UiColors.Accento);
        t.SetStylebox("disabled", PulsantePiatto, flat);
        // List rows (saves): text on the left, the chosen one with the accent edge.
        ButtonLook(t, PulsanteRiga,
            Box(UiColors.FondoSlot, UiColors.FondoSlot, 2, 12, 6),
            Box(UiColors.FondoSopra, UiColors.Bordo, 2, 12, 6),
            Box(UiColors.FondoScelto, UiColors.Accento, 2, 12, 6),
            UiColors.Testo, UiColors.Testo, UiColors.Accento);
        ButtonLook(t, PulsanteRigaScelta,
            Box(UiColors.FondoScelto, UiColors.Accento, 2, 12, 6),
            Box(UiColors.FondoScelto, UiColors.Accento, 2, 12, 6),
            Box(UiColors.FondoScelto, UiColors.Accento, 2, 12, 6),
            UiColors.Accento, UiColors.Accento, UiColors.Accento);
    }

    private static void BuildToggles(Theme t)
    {
        // Square check boxes generated by tools/art (20 px, cached by Sprites): 2 px edge, an accent square when on.
        var on = Sprites.For("ui/ui-spunta-si");
        var off = Sprites.For("ui/ui-spunta-no");
        var onDisabled = Sprites.For("ui/ui-spunta-si-spenta");
        var offDisabled = Sprites.For("ui/ui-spunta-no-spenta");
        // Check buttons are plain text with the box: no button box around them.
        foreach (var type in new[] { "CheckButton", "CheckBox" })
        {
            foreach (var mirrored in new[] { "", "_mirrored" })
            {
                t.SetIcon("checked" + mirrored, type, on);
                t.SetIcon("unchecked" + mirrored, type, off);
                t.SetIcon("checked_disabled" + mirrored, type, onDisabled);
                t.SetIcon("unchecked_disabled" + mirrored, type, offDisabled);
            }

            t.SetConstant("icon_max_width", type, 0);
            t.SetConstant("h_separation", type, 12);
            var empty = Own(new StyleBoxEmpty { ContentMarginLeft = 0, ContentMarginRight = 0, ContentMarginTop = 4, ContentMarginBottom = 4 });
            foreach (var state in new[] { "normal", "hover", "pressed", "hover_pressed", "disabled", "focus" })
            {
                t.SetStylebox(state, type, empty);
            }

            t.SetColor("font_color", type, UiColors.Testo);
            t.SetColor("font_focus_color", type, UiColors.Testo);
            t.SetColor("font_hover_color", type, UiColors.Accento);
            t.SetColor("font_pressed_color", type, UiColors.Testo);
            t.SetColor("font_hover_pressed_color", type, UiColors.Accento);
            t.SetColor("font_disabled_color", type, UiColors.TestoDisattivo);
            t.SetFontSize("font_size", type, TextSize);
        }
    }

    /// <summary>Slider grabber: an accent bar with a dark edge, made from a small image in code (a plain shape).</summary>
    private static ImageTexture GrabberIcon(Color fill)
    {
        using var image = Image.CreateEmpty(10, 20, false, Image.Format.Rgba8);
        image.Fill(UiColors.Fondo);
        image.FillRect(new Rect2I(2, 2, 6, 16), fill);
        return Own(ImageTexture.CreateFromImage(image));
    }

    private static void BuildSliders(Theme t)
    {
        var track = Box(UiColors.FondoBloccato, UiColors.Bordo, 2, 0, 4);
        var filled = Box(UiColors.FondoScelto, UiColors.Accento, 2, 0, 4);
        t.SetStylebox("slider", "HSlider", track);
        t.SetStylebox("grabber_area", "HSlider", filled);
        t.SetStylebox("grabber_area_highlight", "HSlider", filled);
        t.SetIcon("grabber", "HSlider", GrabberIcon(UiColors.Accento));
        t.SetIcon("grabber_highlight", "HSlider", GrabberIcon(UiColors.Testo));
        t.SetIcon("grabber_disabled", "HSlider", GrabberIcon(UiColors.TestoDisattivo));
        t.SetIcon("tick", "HSlider", GrabberTick());
        t.SetStylebox("focus", "HSlider", Own(new StyleBoxEmpty()));
    }

    private static ImageTexture GrabberTick()
    {
        using var image = Image.CreateEmpty(2, 6, false, Image.Format.Rgba8);
        image.Fill(UiColors.Bordo);
        return Own(ImageTexture.CreateFromImage(image));
    }

    private static void BuildFields(Theme t)
    {
        t.SetStylebox("normal", "LineEdit", Box(UiColors.FondoBloccato, UiColors.Bordo, 2, 8, 4));
        t.SetStylebox("focus", "LineEdit", Box(Colors.Transparent, UiColors.Accento, 2, 8, 4));
        t.SetStylebox("read_only", "LineEdit", Box(UiColors.FondoBloccato, UiColors.BordoBloccato, 2, 8, 4));
        t.SetColor("font_color", "LineEdit", UiColors.Testo);
        t.SetColor("font_placeholder_color", "LineEdit", UiColors.TestoDisattivo);
        t.SetColor("caret_color", "LineEdit", UiColors.Accento);
        t.SetColor("selection_color", "LineEdit", UiColors.Accento with { A = 0.35f });
        t.SetFontSize("font_size", "LineEdit", TextSize);

        var line = Own(new StyleBoxLine { Color = UiColors.Bordo, Thickness = 2 });
        t.SetStylebox("separator", "HSeparator", line);
        t.SetConstant("separation", "HSeparator", 8);
    }

    private static void BuildContainers(Theme t)
    {
        foreach (var (type, baseType, gap) in new[]
                 {
                     (RigaStretta, "HBoxContainer", 2), (RigaLarga, "HBoxContainer", 8), (RigaAmpia, "HBoxContainer", 12),
                     (ColonnaStretta, "VBoxContainer", 2), (ColonnaLarga, "VBoxContainer", 8)
                 })
        {
            t.SetTypeVariation(type, baseType);
            t.SetConstant("separation", type, gap);
        }

        t.SetConstant("separation", "HBoxContainer", 4);
        t.SetConstant("separation", "VBoxContainer", 4);
        t.SetConstant("h_separation", "GridContainer", 4);
        t.SetConstant("v_separation", "GridContainer", 4);
        t.SetConstant("h_separation", "HFlowContainer", 8);
        t.SetConstant("v_separation", "HFlowContainer", 8);
        t.SetTypeVariation(GrigliaLarga, "GridContainer");
        t.SetConstant("h_separation", GrigliaLarga, 12);
        t.SetConstant("v_separation", GrigliaLarga, 4);

        t.SetTypeVariation(MargineBarra, "MarginContainer");
        t.SetConstant("margin_right", MargineBarra, 14);
    }

    private static void BuildScrollAndTooltip(Theme t)
    {
        t.SetStylebox("panel", "ScrollContainer", Own(new StyleBoxEmpty()));
        foreach (var type in new[] { "VScrollBar", "HScrollBar" })
        {
            t.SetStylebox("scroll", type, Box(UiColors.FondoBloccato, UiColors.FondoBloccato, 0, 3, 3));
            t.SetStylebox("scroll_focus", type, Box(UiColors.FondoBloccato, UiColors.FondoBloccato, 0, 3, 3));
            t.SetStylebox("grabber", type, Box(UiColors.Bordo, UiColors.Bordo, 0, 3, 3));
            t.SetStylebox("grabber_highlight", type, Box(UiColors.Accento, UiColors.Accento, 0, 3, 3));
            t.SetStylebox("grabber_pressed", type, Box(UiColors.Accento, UiColors.Accento, 0, 3, 3));
        }

        t.SetStylebox("panel", "TooltipPanel", Box(UiColors.Fondo, UiColors.Accento, 2, 8, 4));
        t.SetColor("font_color", "TooltipLabel", UiColors.Testo);
        t.SetFontSize("font_size", "TooltipLabel", TextSize);
    }
}
