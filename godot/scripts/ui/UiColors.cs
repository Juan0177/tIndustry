using Godot;

namespace TIndustry.Godot;

/// <summary>
/// The client's palette (G4): the interface colours of <see cref="UiTheme"/> and the colours that carry a meaning in
/// the world (placement ghost, building / taking apart, power, money, refusals). Every colour of the client UI and of
/// the world overlays comes from here, so the colour-blind and high-contrast variants (G4 5/6) replace one table.
/// </summary>
public static class UiColors
{
    // Interface (Mindustry style: near-black panels, yellow-orange edge, light text).

    /// <summary>Panel background.</summary>
    public static readonly Color Fondo = new("14161AF2");

    /// <summary>Full-screen background (Ricerca).</summary>
    public static readonly Color FondoSchermo = new("0E0F12");

    /// <summary>Background of slots, rows and fields inside a panel.</summary>
    public static readonly Color FondoSlot = new("23262C");

    /// <summary>Background of the selected slot or toggled button.</summary>
    public static readonly Color FondoScelto = new("3B3424");

    /// <summary>Background of a locked slot.</summary>
    public static readonly Color FondoBloccato = new("101114");

    /// <summary>Background of a hovered button.</summary>
    public static readonly Color FondoSopra = new("2E3138");

    /// <summary>Darkening behind a modal panel.</summary>
    public static readonly Color Oscuramento = new("000000B8");

    /// <summary>Accent (the project's "Accento"): panel edges, titles, selection.</summary>
    public static readonly Color Accento = new("FFD37F");

    /// <summary>Neutral edge of slots and buttons.</summary>
    public static readonly Color Bordo = new("4A4F57");

    /// <summary>Edge of a locked slot.</summary>
    public static readonly Color BordoBloccato = new("2C2E33");

    public static readonly Color Testo = new("EEEEEA");
    public static readonly Color TestoTenue = new("A6AAB0");
    public static readonly Color TestoDisattivo = new("6A6E75");

    /// <summary>Text drawn on the accent (hovered primary button).</summary>
    public static readonly Color TestoSuAccento = new("14161A");

    /// <summary>Shadow under text drawn straight on the world (toast, placement hint).</summary>
    public static readonly Color Ombra = new("000000C0");

    // Meanings.

    /// <summary>Missing material, refusal, error.</summary>
    public static readonly Color Insufficiente = new("EB5E55");

    /// <summary>Done, unlocked, available.</summary>
    public static readonly Color Fatto = new("84D68F");

    /// <summary>Money.</summary>
    public static readonly Color Denaro = new("F9D162");

    /// <summary>Power (stored energy).</summary>
    public static readonly Color Energia = new("FBE36B");

    /// <summary>A construction site stalled for materials (Mindustry's warning amber).</summary>
    public static readonly Color Fermo = new("F5A623");

    /// <summary>The CORE (research tree root).</summary>
    public static readonly Color Core = new("59C7E6");

    // Research tree (Mindustry: neutral greys for what you have, the accent for what you can research next).

    /// <summary>Path from the selected node to the CORE.</summary>
    public static readonly Color RicercaPercorso = new("EBC86E");

    public static readonly Color RicercaNodo = new("212426");
    public static readonly Color RicercaNodoBloccato = new("141617");

    /// <summary>Edge of an unlocked node (an available one uses the accent, the CORE its own colour).</summary>
    public static readonly Color RicercaSbloccato = new("9EA6A8");

    public static readonly Color RicercaBloccato = new("3D4245");
    public static readonly Color RicercaLineaSbloccata = new("8C9499");
    public static readonly Color RicercaLineaDisponibile = new("C7A86B");
    public static readonly Color RicercaLineaBloccata = new("383D40");

    // World overlays.

    /// <summary>Building: plans, sites, the drone's beam and engine.</summary>
    public static readonly Color Costruzione = Accento;

    /// <summary>Taking apart: marks, sites, the drone's beam.</summary>
    public static readonly Color Smontaggio = new("E55454");

    /// <summary>Outline (and arrow) of a block preview that can be placed: ghost and plans.</summary>
    public static readonly Color FantasmaValido = Accento;

    /// <summary>Outline, arrow and sprite tint of a block preview that is refused.</summary>
    public static readonly Color FantasmaNonValido = Insufficiente;

    /// <summary>Bridge ghost: ends and span where the bridge fits.</summary>
    public static readonly Color PonteEstremi = new(0.7f, 0.85f, 1f, 0.55f);

    public static readonly Color PonteCampata = new(0.55f, 0.7f, 0.95f, 0.28f);

    /// <summary>Bridge ghost on a cell already planned (refused): ends and span.</summary>
    public static readonly Color PonteRifiutatoEstremi = new(0.9f, 0.25f, 0.2f, 0.55f);

    public static readonly Color PonteRifiutatoCampata = new(0.9f, 0.25f, 0.2f, 0.28f);
}
