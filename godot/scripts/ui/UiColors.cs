using Godot;

namespace TIndustry.Godot;

/// <summary>Colour variant of the client (setting "Colori"): every meaningful colour follows it.</summary>
public enum VarianteColori
{
    /// <summary>The project's palette.</summary>
    Normale,

    /// <summary>
    /// Safe for deuteranopia and protanopia (Okabe–Ito hues, tuned for dark panels): meanings sit on the blue–yellow
    /// axis and on lightness, never on red against green.
    /// </summary>
    Daltonici,

    /// <summary>Black panels, white text, light edges and brighter meaning colours.</summary>
    AltoContrasto
}

/// <summary>
/// The client's palette (G4): the interface colours of <see cref="UiTheme"/> and the colours that carry a meaning in
/// the world (placement ghost, building / taking apart, power, money, refusals). Every colour of the client UI and of
/// the world overlays comes from here; <see cref="Apply"/> switches the whole table to a <see cref="VarianteColori"/>
/// (the theme is rebuilt, world overlays read the colours every frame). The meaning never rests on colour alone: the
/// refused ghost has an X, taking apart is hatched, a missing cost is slashed.
/// </summary>
public static class UiColors
{
    static UiColors() => SetNormale();

    /// <summary>The variant in use.</summary>
    public static VarianteColori Variante { get; private set; }

    /// <summary>
    /// Switches every colour to <paramref name="variante"/>; true when it changed (the caller rebuilds the theme,
    /// <see cref="UiTheme.Refresh"/>).
    /// </summary>
    public static bool Apply(VarianteColori variante)
    {
        if (variante == Variante)
        {
            return false;
        }

        SetNormale();
        if (variante == VarianteColori.Daltonici)
        {
            SetDaltonici();
        }
        else if (variante == VarianteColori.AltoContrasto)
        {
            SetAltoContrasto();
        }

        Variante = variante;
        return true;
    }

    // Interface (Mindustry style: near-black panels, yellow-orange edge, light text).

    /// <summary>Panel background.</summary>
    public static Color Fondo { get; private set; }

    /// <summary>Full-screen background (Ricerca). Same in every variant (read once by the panel).</summary>
    public static Color FondoSchermo { get; private set; }

    /// <summary>Background of slots, rows and fields inside a panel.</summary>
    public static Color FondoSlot { get; private set; }

    /// <summary>Background of the selected slot or toggled button.</summary>
    public static Color FondoScelto { get; private set; }

    /// <summary>Background of a locked slot.</summary>
    public static Color FondoBloccato { get; private set; }

    /// <summary>Background of a hovered button.</summary>
    public static Color FondoSopra { get; private set; }

    /// <summary>Darkening behind a modal panel. Same in every variant (read once by the panels).</summary>
    public static Color Oscuramento { get; private set; }

    /// <summary>
    /// Accent (the project's "Accento"): panel edges, titles, selection. Same in every variant: the generated check
    /// boxes and logo carry it.
    /// </summary>
    public static Color Accento { get; private set; }

    /// <summary>Neutral edge of slots and buttons.</summary>
    public static Color Bordo { get; private set; }

    /// <summary>Edge of a locked slot.</summary>
    public static Color BordoBloccato { get; private set; }

    public static Color Testo { get; private set; }
    public static Color TestoTenue { get; private set; }
    public static Color TestoDisattivo { get; private set; }

    /// <summary>Text drawn on the accent (hovered primary button).</summary>
    public static Color TestoSuAccento { get; private set; }

    /// <summary>Shadow under text drawn straight on the world (toast, placement hint).</summary>
    public static Color Ombra { get; private set; }

    // Meanings.

    /// <summary>Missing material, refusal, error.</summary>
    public static Color Insufficiente { get; private set; }

    /// <summary>Done, unlocked, available.</summary>
    public static Color Fatto { get; private set; }

    /// <summary>Money.</summary>
    public static Color Denaro { get; private set; }

    /// <summary>Power (stored energy).</summary>
    public static Color Energia { get; private set; }

    /// <summary>A construction site stalled for materials (Mindustry's warning amber).</summary>
    public static Color Fermo { get; private set; }

    /// <summary>The CORE (research tree root).</summary>
    public static Color Core { get; private set; }

    // Research tree (Mindustry: neutral greys for what you have, the accent for what you can research next).

    /// <summary>Path from the selected node to the CORE.</summary>
    public static Color RicercaPercorso { get; private set; }

    public static Color RicercaNodo { get; private set; }
    public static Color RicercaNodoBloccato { get; private set; }

    /// <summary>Edge of an unlocked node (an available one uses the accent, the CORE its own colour).</summary>
    public static Color RicercaSbloccato { get; private set; }

    public static Color RicercaBloccato { get; private set; }
    public static Color RicercaLineaSbloccata { get; private set; }
    public static Color RicercaLineaDisponibile { get; private set; }
    public static Color RicercaLineaBloccata { get; private set; }

    // World overlays.

    /// <summary>Building: plans, sites, the drone's beam and engine.</summary>
    public static Color Costruzione { get; private set; }

    /// <summary>Taking apart: marks (hatched), sites, the drone's beam.</summary>
    public static Color Smontaggio { get; private set; }

    /// <summary>Fill of the right drag's removal rectangle (its outline is <see cref="Smontaggio"/>).</summary>
    public static Color SmontaggioArea { get; private set; }

    /// <summary>Outline (and arrow) of a block preview that can be placed: ghost and plans.</summary>
    public static Color FantasmaValido { get; private set; }

    /// <summary>Outline, arrow, X and sprite tint of a block preview that is refused.</summary>
    public static Color FantasmaNonValido { get; private set; }

    /// <summary>Bridge ghost: ends and span where the bridge fits.</summary>
    public static Color PonteEstremi { get; private set; }

    public static Color PonteCampata { get; private set; }

    /// <summary>Bridge ghost on a cell already planned (refused): ends and span.</summary>
    public static Color PonteRifiutatoEstremi { get; private set; }

    public static Color PonteRifiutatoCampata { get; private set; }

    /// <summary>
    /// Dark stroke drawn under the ghost's outline, X and arrow and under removal outlines, so they read on any
    /// ground (transparent = none; only the high-contrast variant has one).
    /// </summary>
    public static Color SottoSegno { get; private set; }

    // FondoScelto, FondoBloccato, BordoBloccato, TestoDisattivo and Accento of the Normale variant are mirrored in
    // tools/art/palette.py (UI_* and ACCENTO[3]) for the generated check boxes; test_genera.test_spunte_come_ui_colors
    // keeps them equal.

    private static void SetNormale()
    {
        Fondo = new("14161AF2");
        FondoSchermo = new("0E0F12");
        FondoSlot = new("23262C");
        FondoScelto = new("3B3424");
        FondoBloccato = new("101114");
        FondoSopra = new("2E3138");
        Oscuramento = new("000000B8");
        Accento = new("FFD37F");
        Bordo = new("686D75");
        BordoBloccato = new("2C2E33");
        Testo = new("EEEEEA");
        TestoTenue = new("A6AAB0");
        TestoDisattivo = new("6A6E75");
        TestoSuAccento = new("14161A");
        Ombra = new("000000C0");

        Insufficiente = new("EB5E55");
        Fatto = new("84D68F");
        Denaro = new("F9D162");
        Energia = new("FBE36B");
        Fermo = new("F5A623");
        Core = new("59C7E6");

        RicercaPercorso = new("EBC86E");
        RicercaNodo = new("212426");
        RicercaNodoBloccato = new("141617");
        RicercaSbloccato = new("9EA6A8");
        RicercaBloccato = new("3D4245");
        RicercaLineaSbloccata = new("8C9499");
        RicercaLineaDisponibile = new("C7A86B");
        RicercaLineaBloccata = new("383D40");

        Costruzione = Accento;
        Smontaggio = new("E55454");
        SmontaggioArea = Smontaggio with { A = 0.14f };
        FantasmaValido = Accento;
        FantasmaNonValido = Insufficiente;
        PonteEstremi = new(0.7f, 0.85f, 1f, 0.55f);
        PonteCampata = new(0.55f, 0.7f, 0.95f, 0.28f);
        PonteRifiutatoEstremi = new(0.9f, 0.25f, 0.2f, 0.55f);
        PonteRifiutatoCampata = new(0.9f, 0.25f, 0.2f, 0.28f);
        SottoSegno = Colors.Transparent;
    }

    /// <summary>
    /// Okabe–Ito: valid sky blue against refused vermillion, building yellow against taking apart reddish purple, done
    /// bluish green against missing vermillion, money yellow against power sky blue, stalled orange (with its text).
    /// </summary>
    private static void SetDaltonici()
    {
        Insufficiente = new("F07B3F");
        Fatto = new("2DBE96");
        Denaro = new("F0E442");
        Energia = new("56B4E9");
        Fermo = new("E69F00");
        // The CORE outline neutral: sky blue is the valid ghost here.
        Core = new("E0E0E0");

        Smontaggio = new("CC79A7");
        SmontaggioArea = Smontaggio with { A = 0.16f };
        FantasmaValido = new("56B4E9");
        FantasmaNonValido = Insufficiente;
        PonteEstremi = FantasmaValido with { A = 0.55f };
        PonteCampata = FantasmaValido with { A = 0.28f };
        PonteRifiutatoEstremi = Insufficiente with { A = 0.55f };
        PonteRifiutatoCampata = Insufficiente with { A = 0.28f };
    }

    private static void SetAltoContrasto()
    {
        Fondo = new("000000");
        FondoSlot = new("1A1A1A");
        FondoScelto = new("4A3A10");
        FondoBloccato = new("000000");
        FondoSopra = new("383838");
        Bordo = new("B4B4B4");
        BordoBloccato = new("6E6E6E");
        Testo = new("FFFFFF");
        TestoTenue = new("DCDCDC");
        TestoDisattivo = new("A0A0A0");
        TestoSuAccento = new("000000");
        Ombra = new("000000");

        Insufficiente = new("FF5A4F");
        Fatto = new("6CFF8A");
        Denaro = new("FFE066");
        Energia = new("FFFF80");
        Fermo = new("FFA733");
        Core = new("66E0FF");

        RicercaPercorso = new("FFFFFF");
        RicercaNodo = new("1A1A1A");
        RicercaNodoBloccato = new("000000");
        RicercaSbloccato = new("E6E6E6");
        RicercaBloccato = new("8A8A8A");
        RicercaLineaSbloccata = new("D2D2D2");
        RicercaLineaDisponibile = new("FFD37F");
        RicercaLineaBloccata = new("767676");

        // World signs: pure yellow against a saturated magenta-red (3.6:1 between them) over a black under-stroke
        // that keeps them readable on grass, snow or sand. Taking apart reuses the refused red: hatching against X.
        FantasmaValido = new("FFFF00");
        FantasmaNonValido = new("FF0050");
        Costruzione = FantasmaValido;
        Smontaggio = FantasmaNonValido;
        SmontaggioArea = Smontaggio with { A = 0.22f };
        SottoSegno = new("000000");
        PonteEstremi = new(0.75f, 0.9f, 1f, 0.75f);
        PonteCampata = new(0.6f, 0.78f, 1f, 0.45f);
        PonteRifiutatoEstremi = FantasmaNonValido with { A = 0.75f };
        PonteRifiutatoCampata = FantasmaNonValido with { A = 0.45f };
    }
}
