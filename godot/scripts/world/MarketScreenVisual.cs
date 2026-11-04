using Godot;
using TIndustry.Shared;

namespace TIndustry.Godot;

/// <summary>
/// The 2×2 stock-exchange screen in the world (spec 2a «Schermo di borsa»): the frame with its glass and LED strip
/// (<c>market-screen-base</c>) and, drawn on the glass, the followed item's icon, its price with ▲/▼, «Crollo» and the
/// percent over B (module; on a module line when both do not fit), the chart of the last 2 minutes (the Mercato
/// overview's own) and the lines of the researched
/// Finance modules. For <see cref="MarketScreen.ResultTicks"/> after a «Vendi tutto» it shows the income and lights the
/// strip (<c>market-screen-led</c>) in the LED colour; a jackpot writes «JACKPOT» and blinks the corner lamps
/// (<c>market-screen-corners</c>) yellow. Detached from the Core it says «Staccato dal CORE», with no item «Scegli un
/// oggetto». It reads the view a few times a second (every <see cref="RefreshTicks"/> ticks of the game, so a paused
/// game and the playtests draw the same picture) and when the screen, the language or the colour variant changes.
/// </summary>
public partial class MarketScreenVisual : Node2D
{
    /// <summary>Glass of the 64 px sprite (tools/art/blocchi/borsa.py <c>VETRO</c>, inclusive 5..58 × 7..42).</summary>
    private static readonly Rect2 GlassPixels = new(5, 7, 54, 36);

    /// <summary>Side of the sprite in pixels (2 cells of 32).</summary>
    private const float SpritePixels = MarketScreen.Size * Sprites.PixelsPerCell;

    /// <summary>Ticks between two reads of the market (4 a second, as the Mercato panel).</summary>
    public const int RefreshTicks = 15;

    /// <summary>Ticks of one on or off phase of the jackpot's corner lamps.</summary>
    private const int BlinkTicks = 15;

    /// <summary>
    /// Small text on the glass: the text font at half its size, one world pixel per glyph pixel (a 12 px line); the
    /// income of a sale uses the full size when it fits.
    /// </summary>
    private const float SmallText = 0.5f;

    private const float Pad = 3f;

    private Sprite2D? _led;
    private Sprite2D? _corners;
    private Node2D? _display;
    private int _blockSide;

    // What the glass shows, read at the last refresh.
    private bool _active;
    private string? _itemId;
    private MarketItemView? _item;
    private MarketInsight _insight = MarketInsight.None;
    private bool _forecast;
    private ScreenSaleResult? _result;
    private bool _blinkOn;
    private (long Bucket, bool Active, string? Item, ScreenSaleResult? Result, bool Blink, VarianteColori Colors, int Modules, bool Sequence)? _key;

    /// <summary>The jackpot sequence running on this screen (set by every <see cref="Sync"/>), else null.</summary>
    private JackpotSequenceView? _sequence;

    public GridPosition Origin { get; private set; }

    public static MarketScreenVisual Create(MarketScreen screen, int tileSize)
    {
        var root = new MarketScreenVisual
        {
            Name = $"MarketScreen_{screen.Position.X}_{screen.Position.Y}",
            ZIndex = 3,
            Origin = screen.Position,
            _blockSide = MarketScreen.Size * tileSize
        };
        var texture = Sprites.For("market-screen-base");
        BuildingPad.AddShadow(root, texture, MarketScreen.Size, tileSize);
        root.AddChild(BuildingPad.Layer("Base", texture, MarketScreen.Size, tileSize, 0));
        root._led = BuildingPad.Layer("Led", Sprites.For("market-screen-led"), MarketScreen.Size, tileSize, 1);
        root._led.Visible = false;
        root.AddChild(root._led);
        root._corners = BuildingPad.Layer("Angoli", Sprites.For("market-screen-corners"), MarketScreen.Size, tileSize, 1);
        root._corners.Visible = false;
        root.AddChild(root._corners);
        root._display = new Node2D { Name = "Vetro", ZIndex = 2, TextureFilter = TextureFilterEnum.Nearest };
        root._display.Draw += () => root.DrawGlass(root._display);
        root.AddChild(root._display);
        return root;
    }

    /// <summary>
    /// The result of the last «Vendi tutto» while it is on the screen (<see cref="MarketScreen.ResultTicks"/> from its tick,
    /// or from <paramref name="shownFrom"/>: the end of its jackpot sequence), else null.
    /// </summary>
    public static ScreenSaleResult? ShownResult(MarketScreen screen, long tick, long? shownFrom = null) =>
        screen is { Active: true, LastResult: { } result } && tick >= (shownFrom ?? result.Tick)
            && tick < (shownFrom ?? result.Tick) + MarketScreen.ResultTicks
            ? result
            : null;

    /// <summary>
    /// Reads the screen. <paramref name="sequence"/> (running) takes the glass and the lights over for the jackpot
    /// sequence, drawn every frame; <paramref name="shownFrom"/> is the tick its result is shown from once it ended.
    /// </summary>
    public void Sync(MarketScreen screen, IGameView view, JackpotSequenceView? sequence = null, long? shownFrom = null)
    {
        var running = sequence is { Running: true } ? sequence : null;
        var tick = view.CurrentTick;
        var result = running is null ? ShownResult(screen, tick, shownFrom) : null;
        var from = shownFrom ?? result?.Tick ?? 0;
        var blink = result is { Jackpot: true } && (tick - from) / BlinkTicks % 2 == 0;
        var key = (tick / RefreshTicks, screen.Active, screen.WatchedItemId, result, blink, UiColors.Variante,
            MarketModuleTexts.ModulesResearched(view), running is not null);
        _sequence = running;
        if (_key != key)
        {
            _key = key;
            _active = screen.Active;
            _itemId = screen.WatchedItemId;
            _item = _itemId is null ? null : view.MarketItem(_itemId);
            _insight = _itemId is null ? MarketInsight.None : view.MarketInsight(_itemId);
            _forecast = _active && view.IsStructureUnlocked(MarketInsight.ForecastNodeId);
            _result = result;
            _blinkOn = blink;

            var glow = BuildingPad.GlowBoost;
            if (_led is not null)
            {
                _led.Visible = result is not null;
                if (result is not null)
                {
                    var c = LedColor(result.Jackpot ? ScreenLed.Yellow : result.Led);
                    _led.SelfModulate = new Color(c.R * glow, c.G * glow, c.B * glow);
                }
            }

            if (_corners is not null)
            {
                _corners.Visible = blink;
                var y = UiColors.LedGiallo;
                _corners.SelfModulate = new Color(y.R * glow, y.G * glow, y.B * glow);
            }

            _display?.QueueRedraw();
        }

        if (running?.Sequence is { } seq)
        {
            SyncSequenceLights(running, seq);
            if (seq.Step != JackpotStep.Zoom)
            {
                _display?.QueueRedraw(); // the sequence animates the glass: every frame
            }
        }
    }

    public static Color LedColor(ScreenLed led) => led switch
    {
        ScreenLed.Blue => UiColors.LedBlu,
        ScreenLed.Purple => UiColors.LedViola,
        ScreenLed.Yellow => UiColors.LedGiallo,
        _ => UiColors.TestoTenue
    };

    public override void _Notification(int what)
    {
        // New language: the glass is drawn again with the new words.
        if (what == NotificationTranslationChanged)
        {
            _key = null;
        }
    }

    /// <summary>The glass in the node's space (centred on the block), in world pixels.</summary>
    private Rect2 Glass()
    {
        var k = _blockSide / SpritePixels;
        var half = _blockSide / 2f;
        return new Rect2(GlassPixels.Position * k - new Vector2(half, half), GlassPixels.Size * k);
    }

    private void DrawGlass(CanvasItem canvas)
    {
        var glass = Glass();
        var inner = glass.Grow(-Pad);
        if (_sequence?.Sequence is { Step: not JackpotStep.Zoom } seq)
        {
            DrawSequence(canvas, _sequence, seq, glass, inner);
            return;
        }

        if (!_active)
        {
            Centered(canvas, Testi.T("borsa.schermo.staccato"), glass, UiColors.Insufficiente);
            return;
        }

        if (_result is { } result)
        {
            DrawResult(canvas, result, inner);
            return;
        }

        if (_itemId is null || _item is not { } item)
        {
            Centered(canvas, Testi.T("borsa.schermo.scegli"), glass, UiColors.TestoTenue);
            return;
        }

        // Row 1: icon · price ▲▼ · «Crollo» · percent over B (module).
        var line = LineHeight(SmallText);
        var icon = Sprites.Item(item.ItemId);
        var iconSide = line;
        canvas.DrawTextureRect(icon, new Rect2(inner.Position, new Vector2(iconSide, iconSide)), false);
        var crashed = item.Phase == MarketPhase.Crash;
        var trend = MarketSparkline.TrendOf(item);
        var baseline = inner.Position.Y + Ascent(SmallText) + 1;
        var priceX = inner.Position.X + iconSide + 2;
        var priceText = MarketSparkline.PriceText(item, trend);
        Text(canvas, priceText, new Vector2(priceX, baseline), trend == MarketTrend.Piatto ? UiColors.Testo : MarketSparkline.ColorOf(trend));
        // Right of the price: «Crollo» during a crash, then the percent over B (module) when it fits too; a percent
        // that does not fit goes to the module lines.
        var right = new List<(string Text, Color Color)>();
        if (crashed)
        {
            right.Add((Testi.T("borsa.schermo.crollo.stato"), UiColors.MercatoCrollo));
        }

        (string Text, Color Color)? percentSegment = null;
        if (_insight.PercentOverB is { } percent)
        {
            percentSegment = (Testi.F("borsa.schermo.percentuale", MarketModuleTexts.Signed(percent)),
                percent > 0 ? UiColors.MercatoSu : percent < 0 ? UiColors.MercatoGiu : UiColors.Testo);
        }

        var room = inner.End.X - priceX - Width(priceText, SmallText) - 4;
        var percentOnTop = false;
        if (percentSegment is { } ps && Width(Joined(right.Select(r => r.Text).Append(ps.Text)), SmallText) <= room)
        {
            right.Add(ps);
            percentOnTop = true;
        }

        if (right.Count > 0)
        {
            var rx = inner.End.X - Width(Joined(right.Select(r => r.Text)), SmallText);
            for (var i = 0; i < right.Count; i++)
            {
                if (i > 0)
                {
                    rx = DrawSeparator(canvas, rx, baseline);
                }

                var text = Fit(right[i].Text, inner.End.X - rx, SmallText);
                Text(canvas, text, new Vector2(rx, baseline), right[i].Color);
                rx += Width(text, SmallText);
            }
        }

        var top = inner.Position.Y + line + 1;
        var below = new Rect2(inner.Position.X, top, inner.Size.X, inner.End.Y - top);

        // Module lines under the chart, packed two to a line when they fit; the chart takes what is left.
        var segments = ModuleSegments();
        if (!percentOnTop && percentSegment is { } moved)
        {
            segments.Insert(0, moved);
        }

        var lines = Pack(segments, below.Size.X, Math.Max(0, (int)((below.Size.Y - MinChart) / line)));
        var chart = new Rect2(below.Position, new Vector2(below.Size.X, below.Size.Y - lines.Count * line));
        MarketSparkline.DrawChart(canvas, chart, item.HistoryMilli, crashed ? MarketTrend.Crollo : trend, 1.5f, 2f);
        var y = chart.End.Y + Ascent(SmallText);
        foreach (var packed in lines)
        {
            var x = below.Position.X;
            for (var i = 0; i < packed.Count; i++)
            {
                if (i > 0)
                {
                    x = DrawSeparator(canvas, x, y);
                }

                var (text, color) = packed[i];
                text = Fit(text, below.End.X - x, SmallText);
                Text(canvas, text, new Vector2(x, y), color);
                x += Width(text, SmallText);
            }

            y += line;
        }
    }

    /// <summary>Smallest chart height kept when module lines take the bottom of the glass.</summary>
    private const float MinChart = 14f;

    /// <summary>The separator of two texts on one line (<c>formato.separatore</c>: «a · b»), without the texts.</summary>
    private static string Separator => Testi.F("formato.separatore", "", "");

    /// <summary>Texts joined on one line with the separator.</summary>
    private static string Joined(IEnumerable<string> texts) => string.Join(Separator, texts);

    /// <summary>Draws the separator at <paramref name="x"/>; returns the x after it.</summary>
    private static float DrawSeparator(CanvasItem canvas, float x, float baseline)
    {
        Text(canvas, Separator, new Vector2(x, baseline), UiColors.TestoTenue);
        return x + Width(Separator, SmallText);
    }

    /// <summary>The module lines of the followed item, in the screen's short words, each with its colour.</summary>
    private List<(string Text, Color Color)> ModuleSegments()
    {
        var segments = new List<(string, Color)>();
        if (_forecast)
        {
            if (MarketModuleTexts.Forecast(_insight, screen: true) is { } forecast)
            {
                segments.Add((forecast, _insight.SecondsToCrashEnd is not null ? UiColors.MercatoCrollo : UiColors.Testo));
            }

            segments.Add(MarketModuleTexts.Blocked(_insight, screen: true) is { } blocked
                ? (blocked, UiColors.Insufficiente)
                : (MarketModuleTexts.Advice(_insight), _insight.SellNow ? UiColors.MercatoSu : UiColors.TestoTenue));
        }

        if (_insight.JackpotChancePercent is { } odds)
        {
            segments.Add((Testi.F("borsa.schermo.probabilita", odds), UiColors.LedGiallo));
        }

        return segments;
    }

    /// <summary>Greedy packing of the segments into at most <paramref name="maxLines"/> lines of <paramref name="width"/>.</summary>
    private static List<List<(string Text, Color Color)>> Pack(List<(string Text, Color Color)> segments, float width, int maxLines)
    {
        var lines = new List<List<(string, Color)>>();
        var used = 0f;
        foreach (var segment in segments)
        {
            var w = Width(segment.Text, SmallText);
            if (lines.Count > 0 && used + Width(Separator, SmallText) + w <= width)
            {
                lines[^1].Add(segment);
                used += Width(Separator, SmallText) + w;
                continue;
            }

            if (lines.Count == maxLines)
            {
                break;
            }

            lines.Add([segment]);
            used = w;
        }

        return lines;
    }

    /// <summary>
    /// «JACKPOT» (with a jackpot) over the sold item's icon and the income, at full size when they fit, centred in
    /// <paramref name="area"/>.
    /// </summary>
    private static void DrawResult(CanvasItem canvas, ScreenSaleResult result, Rect2 area)
    {
        var income = Testi.F("borsa.incasso", result.Income);
        var incomeScale = Width(income, 1f) + IconSide(1f) <= area.Size.X ? 1f : SmallText;
        var parts = new List<(string Text, float Scale, Color Color)>();
        if (result.Jackpot)
        {
            var jackpot = Testi.T("borsa.jackpot");
            parts.Add((jackpot, Width(jackpot, 1f) <= area.Size.X ? 1f : SmallText, UiColors.LedGiallo));
        }

        parts.Add((income, incomeScale, UiColors.Denaro));
        var height = parts.Sum(p => LineHeight(p.Scale));
        var y = area.Position.Y + Math.Max(0, (area.Size.Y - height) / 2f);
        for (var i = 0; i < parts.Count; i++)
        {
            var (text, scale, color) = parts[i];
            // The income line (the last) starts with the icon of the item sold, standing on the baseline.
            var icon = i == parts.Count - 1 ? IconSide(scale) : 0f;
            var fitted = Fit(text, area.Size.X - icon, scale);
            var x = area.Position.X + (area.Size.X - icon - Width(fitted, scale)) / 2f;
            if (icon > 0)
            {
                var side = Math.Min(icon - 2, Ascent(scale) + 1);
                canvas.DrawTextureRect(Sprites.Item(result.ItemId),
                    new Rect2(x, y + Ascent(scale) - side, side, side), false);
            }

            Text(canvas, fitted, new Vector2(x + icon, y + Ascent(scale)), color, scale);
            y += LineHeight(scale);
        }
    }

    /// <summary>Room for an item icon before a text at <paramref name="scale"/>: the icon and a 2 px gap.</summary>
    private static float IconSide(float scale) => (scale >= 1f ? 16f : LineHeight(SmallText)) + 2f;

    private static void Centered(CanvasItem canvas, string text, Rect2 area, Color color)
    {
        text = Fit(text, area.Size.X - 2 * Pad, SmallText);
        var x = area.Position.X + (area.Size.X - Width(text, SmallText)) / 2f;
        var y = area.Position.Y + (area.Size.Y - LineHeight(SmallText)) / 2f + Ascent(SmallText);
        Text(canvas, text, new Vector2(x, y), color);
    }

    // --- The jackpot sequence on the glass (spec 2b) ------------------------------------------------------------------

    /// <summary>Colours the LED strip cycles through while the sequence's LEDs blink.</summary>
    private static readonly ScreenLed[] LedCycle = [ScreenLed.Blue, ScreenLed.Purple, ScreenLed.Yellow];

    /// <summary>Seconds of one colour of the blinking LEDs, of one phase of the corner lamps.</summary>
    private const double LedCycleSeconds = 0.12;
    private const double CornerBlinkSeconds = 0.25;

    /// <summary>Rolling digits: steps a second, and the share of the result step after which the last digit stops.</summary>
    private const double ReelSpeed = 15;
    private const float ReelFirstStop = 0.15f;
    private const float ReelLastStop = 0.7f;

    /// <summary>
    /// The strip and the corner lamps during the sequence: off until the LEDs' step, then blue, purple and yellow in turn
    /// and the outcome's colour at the end; on a jackpot the corner lamps blink yellow over the result.
    /// </summary>
    private void SyncSequenceLights(JackpotSequenceView view, JackpotSequence seq)
    {
        var glow = BuildingPad.GlowBoost;
        var step = seq.Step;
        if (_led is not null)
        {
            _led.Visible = step is JackpotStep.Led or JackpotStep.Risultato;
            if (_led.Visible)
            {
                var led = step == JackpotStep.Led && seq.StepProgress < 0.7
                    ? LedCycle[(int)(seq.Elapsed / LedCycleSeconds) % LedCycle.Length]
                    : view.OutcomeLed;
                var c = LedColor(led);
                _led.SelfModulate = new Color(c.R * glow, c.G * glow, c.B * glow);
            }
        }

        if (_corners is not null)
        {
            _corners.Visible = view.Jackpot && step == JackpotStep.Risultato
                && (int)(seq.Elapsed / CornerBlinkSeconds) % 2 == 0;
            var y = UiColors.LedGiallo;
            _corners.SelfModulate = new Color(y.R * glow, y.G * glow, y.B * glow);
        }
    }

    /// <summary>
    /// The glass during the sequence: the channel changing to the sold item, its chart (in slow motion on the way back,
    /// stopping on the sale price), «calcolo…» while it computes and the LEDs blink, then the income on rolling digits.
    /// </summary>
    private static void DrawSequence(CanvasItem canvas, JackpotSequenceView view, JackpotSequence seq, Rect2 glass, Rect2 inner)
    {
        var p = (float)seq.StepProgress;
        switch (seq.Step)
        {
            case JackpotStep.CambioCanale when p < 0.25f:
                DrawChannelNoise(canvas, glass, seq.Elapsed);
                break;
            case JackpotStep.CambioCanale:
                DrawChannel(canvas, view, inner);
                break;
            case JackpotStep.CorsaAlCore or JackpotStep.Ritorno:
                DrawSequenceChart(canvas, view, inner);
                break;
            case JackpotStep.Calcolo or JackpotStep.Led:
                Centered(canvas, view.Computing[(int)(seq.Elapsed / 0.33) % view.Computing.Length], glass, UiColors.Testo);
                break;
            case JackpotStep.Risultato:
                DrawRollingResult(canvas, view, seq, inner);
                break;
        }
    }

    /// <summary>The channel switching: a few bright lines that jump across the glass.</summary>
    private static void DrawChannelNoise(CanvasItem canvas, Rect2 glass, double elapsed)
    {
        var frame = (int)(elapsed * 30);
        var height = Math.Max(1, (int)glass.Size.Y - 3);
        for (var i = 0; i < 7; i++)
        {
            var y = glass.Position.Y + (i * 37 + frame * 13 + i * frame * 7) % height;
            var thick = 1 + (i + frame) % 3;
            var colour = i % 2 == 0 ? UiColors.TestoTenue : UiColors.LedBlu;
            canvas.DrawRect(new Rect2(glass.Position.X, y, glass.Size.X, thick), new Color(colour, 0.7f));
        }
    }

    /// <summary>The new channel: the sold item's icon over its name.</summary>
    private static void DrawChannel(CanvasItem canvas, JackpotSequenceView view, Rect2 inner)
    {
        var line = LineHeight(SmallText);
        var side = Math.Min(inner.Size.Y - line - 2, 32f);
        var top = inner.Position.Y + (inner.Size.Y - side - line - 2) / 2f;
        canvas.DrawTextureRect(Sprites.Item(view.ItemId),
            new Rect2(inner.Position.X + (inner.Size.X - side) / 2f, top, side, side), false);
        var name = Fit(view.ItemName, inner.Size.X, SmallText);
        var x = inner.Position.X + (inner.Size.X - Width(name, SmallText)) / 2f;
        Text(canvas, name, new Vector2(x, top + side + 2 + Ascent(SmallText)), UiColors.Testo);
    }

    /// <summary>
    /// The sold item's chart up to the revealed point (<see cref="JackpotSequenceView.ChartReveal"/>) with its price over
    /// it: drawn segment by segment, the head moving smoothly between two points, newest point on the right edge.
    /// </summary>
    private static void DrawSequenceChart(CanvasItem canvas, JackpotSequenceView view, Rect2 inner)
    {
        var count = view.HistoryCount;
        var history = view.History;
        var reveal = Math.Clamp(view.ChartReveal(), 0f, count - 1);
        var whole = (int)reveal;
        var frac = reveal - whole;
        var head = whole + 1 < count
            ? (int)Mathf.Lerp(history[whole], history[whole + 1], frac)
            : history[whole];
        var first = history[0];
        var trend = head > first ? MarketTrend.Su : head < first ? MarketTrend.Giu : MarketTrend.Piatto;

        // Row 1: icon and the price of the head.
        var line = LineHeight(SmallText);
        canvas.DrawTextureRect(Sprites.Item(view.ItemId), new Rect2(inner.Position, new Vector2(line, line)), false);
        Text(canvas, view.PriceText(head), new Vector2(inner.Position.X + line + 2, inner.Position.Y + Ascent(SmallText) + 1),
            trend == MarketTrend.Piatto ? UiColors.Testo : MarketSparkline.ColorOf(trend));

        var chart = new Rect2(inner.Position.X, inner.Position.Y + line + 1, inner.Size.X, inner.Size.Y - line - 1);
        const float pad = 2f;
        var low = view.HistoryLow;
        var high = view.HistoryHigh;
        float Y(float milli) => chart.Position.Y + pad + (chart.Size.Y - 2 * pad) * (1f - (milli - low) / (high - low));

        var baseY = Mathf.Round(Y(1000)) + 0.5f;
        canvas.DrawDashedLine(new Vector2(chart.Position.X, baseY), new Vector2(chart.End.X, baseY), UiColors.MercatoBase, 1f, 4f);
        if (count < 2)
        {
            return;
        }

        var step = (chart.Size.X - 1) / (MarketSparkline.Span - 1);
        var x0 = chart.Position.X + chart.Size.X - 1 - step * (count - 1);
        var colour = MarketSparkline.ColorOf(trend);
        for (var i = 0; i < whole; i++)
        {
            canvas.DrawLine(new Vector2(x0 + step * i, Y(history[i])), new Vector2(x0 + step * (i + 1), Y(history[i + 1])),
                colour, 1.5f);
        }

        var headAt = new Vector2(x0 + step * reveal, Y(head));
        if (frac > 0f)
        {
            canvas.DrawLine(new Vector2(x0 + step * whole, Y(history[whole])), headAt, colour, 1.5f);
        }

        canvas.DrawRect(new Rect2(headAt - new Vector2(1.5f, 1.5f), 3f, 3f), UiColors.Testo);
    }

    /// <summary>
    /// The result of the sequence: «JACKPOT» (with a jackpot) over the item's icon and the income, whose digits roll and
    /// stop one by one from the left on the real value.
    /// </summary>
    private static void DrawRollingResult(CanvasItem canvas, JackpotSequenceView view, JackpotSequence seq, Rect2 area)
    {
        var glyphs = view.IncomeGlyphs;
        var digitWidth = Width(JackpotSequenceView.Digit(0), 1f);
        var scale = GlyphsWidth(glyphs, digitWidth) + IconSide(1f) <= area.Size.X ? 1f : SmallText;
        var jackpotScale = 0f;
        if (view.Jackpot)
        {
            jackpotScale = Width(view.JackpotText, 1f) <= area.Size.X ? 1f : SmallText;
        }

        var height = LineHeight(scale) + (view.Jackpot ? LineHeight(jackpotScale) : 0f);
        var y = area.Position.Y + Math.Max(0, (area.Size.Y - height) / 2f);
        if (view.Jackpot)
        {
            var jackpot = Fit(view.JackpotText, area.Size.X, jackpotScale);
            Text(canvas, jackpot, new Vector2(area.Position.X + (area.Size.X - Width(jackpot, jackpotScale)) / 2f,
                y + Ascent(jackpotScale)), UiColors.LedGiallo, jackpotScale);
            y += LineHeight(jackpotScale);
        }

        var icon = IconSide(scale);
        var slot = digitWidth * scale;
        var width = GlyphsWidth(glyphs, digitWidth) * scale;
        var x = area.Position.X + Math.Max(0, (area.Size.X - icon - width) / 2f);
        var baseline = y + Ascent(scale);
        var side = Math.Min(icon - 2, Ascent(scale) + 1);
        canvas.DrawTextureRect(Sprites.Item(view.ItemId), new Rect2(x, baseline - side, side, side), false);
        x += icon;

        var digits = 0;
        foreach (var g in glyphs)
        {
            digits += IsDigit(g) ? 1 : 0;
        }

        var p = (float)seq.StepProgress;
        var roll = LineHeight(scale) * 0.8f;
        var colour = UiColors.Denaro;
        var k = 0;
        foreach (var g in glyphs)
        {
            if (!IsDigit(g))
            {
                Text(canvas, g, new Vector2(x, baseline), colour, scale);
                x += Width(g, scale);
                continue;
            }

            var stop = ReelFirstStop + (ReelLastStop - ReelFirstStop) * (digits > 1 ? k / (float)(digits - 1) : 0f);
            if (p >= stop)
            {
                DrawDigit(canvas, g, x, slot, baseline, colour, scale);
            }
            else
            {
                // Spinning: the digit leaves upwards while the next comes in from below, fading as they move.
                var v = seq.Elapsed * ReelSpeed + k * 3.3;
                var d = (int)v % 10;
                var f = (float)(v - Math.Floor(v));
                DrawDigit(canvas, JackpotSequenceView.Digit(d), x, slot, baseline - f * roll, new Color(colour, 1f - f), scale);
                DrawDigit(canvas, JackpotSequenceView.Digit((d + 1) % 10), x, slot, baseline + (1f - f) * roll,
                    new Color(colour, f), scale);
            }

            x += slot;
            k++;
        }
    }

    private static bool IsDigit(string glyph) => glyph.Length == 1 && glyph[0] is >= '0' and <= '9';

    /// <summary>Width of the income glyphs at scale 1, every digit in a slot of <paramref name="digitWidth"/>.</summary>
    private static float GlyphsWidth(string[] glyphs, float digitWidth)
    {
        var width = 0f;
        foreach (var g in glyphs)
        {
            width += IsDigit(g) ? digitWidth : Width(g, 1f);
        }

        return width;
    }

    private static void DrawDigit(CanvasItem canvas, string digit, float x, float slot, float baseline, Color colour, float scale) =>
        Text(canvas, digit, new Vector2(x + (slot - Width(digit, scale)) / 2f, baseline), colour, scale);

    private static Font Font => UiTheme.TextFont;

    private static float Width(string text, float scale) =>
        Font.GetStringSize(text, HorizontalAlignment.Left, -1, UiTheme.TextSize).X * scale;

    private static float LineHeight(float scale) => Font.GetHeight(UiTheme.TextSize) * scale;

    private static float Ascent(float scale) => Font.GetAscent(UiTheme.TextSize) * scale;

    /// <summary><paramref name="text"/> cut with «…» until it fits <paramref name="width"/>.</summary>
    private static string Fit(string text, float width, float scale)
    {
        if (Width(text, scale) <= width)
        {
            return text;
        }

        for (var n = text.Length - 1; n > 0; n--)
        {
            var cut = text[..n].TrimEnd() + "…";
            if (Width(cut, scale) <= width)
            {
                return cut;
            }
        }

        return "";
    }

    /// <summary>Text with its baseline at <paramref name="at"/>, drawn at <paramref name="scale"/> of the text font.</summary>
    private static void Text(CanvasItem canvas, string text, Vector2 at, Color color, float scale = SmallText)
    {
        if (text.Length == 0)
        {
            return;
        }

        canvas.DrawSetTransform(at, 0f, new Vector2(scale, scale));
        canvas.DrawString(Font, Vector2.Zero, text, HorizontalAlignment.Left, -1, UiTheme.TextSize, color);
        canvas.DrawSetTransform(Vector2.Zero, 0f, Vector2.One);
    }
}

/// <summary>
/// The texts of the Finance modules (spec 2a «Ramo Finanza della Ricerca») from a <see cref="MarketInsight"/>: the full
/// words of the Mercato overview and of the screen's tooltip, or the short ones that fit on the screen's glass
/// (<c>screen</c>). Seconds become minutes rounded up, «meno di 1 min» under 60 s.
/// </summary>
public static class MarketModuleTexts
{
    /// <summary>Countdown of the forecast: spike, spike arriving or end of the crash; null when there is none.</summary>
    public static string? Forecast(MarketInsight insight, bool screen)
    {
        var prefix = screen ? "borsa.schermo." : "borsa.previsione.";
        if (insight.SecondsToCrashEnd is { } crash)
        {
            return Minutes(prefix + "crollo", crash);
        }

        if (insight.SecondsToSpike is { } spike)
        {
            return spike == 0 ? Testi.T(prefix + "arrivo") : Minutes(prefix + "picco", spike);
        }

        return null;
    }

    /// <summary>
    /// Which Finance modules are researched, as bits (forecast 1, percent 2, odds 4): part of the cache keys of the
    /// screen and its tooltip, so research done while the game is paused shows at once.
    /// </summary>
    public static int ModulesResearched(IGameView view) =>
        (view.IsStructureUnlocked(MarketInsight.ForecastNodeId) ? 1 : 0)
        | (view.IsStructureUnlocked(MarketInsight.PercentNodeId) ? 2 : 0)
        | (view.IsStructureUnlocked(MarketInsight.OddsNodeId) ? 4 : 0);

    /// <summary>The forecast with its advice on one line («picco fra circa 3 min · aspetta»), or the advice alone.</summary>
    public static string ForecastWithAdvice(MarketInsight insight) =>
        Forecast(insight, screen: false) is { } forecast
            ? Testi.F("formato.separatore", forecast, Advice(insight))
            : Advice(insight);

    /// <summary>The forecast's advice: «vendi ora» in the spike, «aspetta» otherwise.</summary>
    public static string Advice(MarketInsight insight) => Testi.T(insight.SellNow ? "borsa.vendi.ora" : "borsa.aspetta");

    /// <summary>The global «Vendi tutto» wait after a missed jackpot attempt; null when free.</summary>
    public static string? Blocked(MarketInsight insight, bool screen) =>
        insight.SecondsSellAllBlocked is { } wait
            ? Minutes(screen ? "borsa.schermo.bloccato" : "borsa.previsione.bloccato", wait)
            : null;

    /// <summary>«+185% su B»; null without the module.</summary>
    public static string? Percent(MarketInsight insight) =>
        insight.PercentOverB is { } percent ? Testi.F("borsa.percentuale", Signed(percent)) : null;

    /// <summary>«probabilità jackpot 12%»; null without the module.</summary>
    public static string? Odds(MarketInsight insight) =>
        insight.JackpotChancePercent is { } odds ? Testi.F("borsa.probabilita", odds) : null;

    /// <summary>A whole number with its sign: «+185», «–1», «0» (the en dash is the font's minus).</summary>
    public static string Signed(int value) => value switch
    {
        > 0 => "+" + value.ToString(System.Globalization.CultureInfo.InvariantCulture),
        < 0 => "–" + (-(long)value).ToString(System.Globalization.CultureInfo.InvariantCulture),
        _ => "0"
    };

    /// <summary><c>key</c> with the minutes rounded up, or <c>key.presto</c> under a minute.</summary>
    private static string Minutes(string key, int seconds) =>
        seconds < 60 ? Testi.T(key + ".presto") : Testi.F(key, (seconds + 59) / 60);
}
