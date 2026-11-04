using System.Globalization;
using Godot;
using TIndustry.Shared;

namespace TIndustry.Godot;

/// <summary>
/// The jackpot-attempt sequence in the world (spec 2b «Sequenza del tentativo di jackpot»), drawn from a
/// <see cref="JackpotSequence"/> that <see cref="World"/> advances in real time: the screen's bot (only graphics) comes
/// out of the screen, changes channel and is surprised, flies to the CORE's nearest edge and fills the briefcase (the
/// pieces over its head), hurries back, throws the briefcase into the glass, then hovers while the screen computes, the
/// LEDs blink and the income rolls in. On a screen-space overlay (on the HUD layer, over the world): the skip hint
/// «clic o Esc per saltare» (not when the jackpot is won) and, on a jackpot, the confetti burst and the big «JACKPOT».
/// The glass itself is drawn by <see cref="MarketScreenVisual"/>, which reads the data prepared here at the start
/// (sold item, history, texts), so a frame of the sequence allocates nothing. One node for the whole run: started
/// and stopped, never freed while the world lives, so no node keeps a reference to an old one.
/// </summary>
public partial class JackpotSequenceView : Node2D
{
    /// <summary>Frames per second of the bot's animations.</summary>
    private const float BotFps = 6f;

    /// <summary>Confetti pieces of a jackpot burst.</summary>
    private const int ConfettiCount = 72;

    /// <summary>Screen pixels per confetti art pixel (8 px pieces drawn 24 px).</summary>
    private const float ConfettiScale = 3f;

    /// <summary>The white confetti frame of <c>market-confetti</c>, tinted with the LED colours of the variant in use.</summary>
    private const int WhiteConfettiFrame = 3;

    /// <summary>Points of the sold item's chart the slow motion of «Ritorno» reveals before it stops on the sale price.</summary>
    private const int ChartTail = 12;

    private static readonly string[] Digits = ["0", "1", "2", "3", "4", "5", "6", "7", "8", "9"];

    private Texture2D[] _idle = [];
    private Texture2D[] _fly = [];
    private Texture2D[] _surprised = [];
    private Texture2D[] _carry = [];
    private Texture2D[] _throw = [];
    private Texture2D? _briefcase;
    private Texture2D? _confettiTexture;

    private Control? _overlay;
    private Label? _hint;
    private Label? _jackpotLabel;

    private int _tileSize;
    private Vector2 _rest;
    private Vector2 _coreStop;
    private Vector2 _glassCentre;

    private readonly Confetto[] _confetti = new Confetto[ConfettiCount];
    private bool _confettiFired;
    private readonly Random _random = new(2468);

    private string _piecesText = "";

    // Last unit price formatted for the glass, kept until it changes (no string per frame).
    private int _priceShown = int.MinValue;
    private string _priceText = "";

    /// <summary>The running sequence; null when none (never started, or stopped).</summary>
    public JackpotSequence? Sequence { get; private set; }

    /// <summary>A sequence is running (not finished, not closed).</summary>
    public bool Running => Sequence is { Finished: false };

    // What the glass shows during the sequence, prepared once at the start.
    public string ItemId { get; private set; } = "";
    public string ItemName { get; private set; } = "";
    public bool Jackpot => Sequence?.Sale.Jackpot == true;

    /// <summary>The LED colour of the outcome (yellow on a jackpot), the strip's last colour.</summary>
    public ScreenLed OutcomeLed { get; private set; }

    /// <summary>The income as the glass writes it («+$98765»), one glyph per entry, digits included.</summary>
    public string IncomeText { get; private set; } = "";
    public string[] IncomeGlyphs { get; private set; } = [];

    /// <summary>«JACKPOT» in the language in use, for the glass.</summary>
    public string JackpotText { get; private set; } = "";

    /// <summary>The three «calcolo» texts with one, two and three dots.</summary>
    public string[] Computing { get; } = ["", "", ""];

    /// <summary>The sold item's chart: the history at the sale then the sale price, in thousandths of B.</summary>
    public int[] History { get; } = new int[MarketSparkline.Span];
    public int HistoryCount { get; private set; }
    public int HistoryLow { get; private set; }
    public int HistoryHigh { get; private set; }
    public int BaseValue { get; private set; }

    /// <summary>The digit glyphs, shared («0»…«9»), for the rolling income.</summary>
    public static string Digit(int d) => Digits[d];

    /// <summary>
    /// The view, with its overlay on <paramref name="uiRoot"/>; drawn at <paramref name="zIndex"/> in the world, which the
    /// world sets above the buildings, the lasers and the construction layer (the bot flies over the screen and the CORE).
    /// </summary>
    public static JackpotSequenceView Create(Control uiRoot, int tileSize, int zIndex)
    {
        var view = new JackpotSequenceView { Name = "SequenzaJackpot", Visible = false, _tileSize = tileSize };
        view.ZIndex = zIndex;
        view.TextureFilter = TextureFilterEnum.Nearest;
        view._idle = Sprites.Frames("market-bot-idle");
        view._fly = Sprites.Frames("market-bot-fly");
        view._surprised = Sprites.Frames("market-bot-surprised");
        view._carry = Sprites.Frames("market-bot-carry");
        view._throw = Sprites.Frames("market-bot-throw");
        view._briefcase = Sprites.For("market-briefcase");
        var confetti = Sprites.Frames("market-confetti");
        view._confettiTexture = confetti[Math.Min(WhiteConfettiFrame, confetti.Length - 1)];

        var overlay = new Control
        {
            Name = "SequenzaJackpot",
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Visible = false,
            TextureFilter = TextureFilterEnum.Nearest
        };
        overlay.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        overlay.Draw += () => view.DrawConfetti(overlay);
        var jackpot = new Label
        {
            Name = "Jackpot",
            Text = "borsa.jackpot",
            MouseFilter = Control.MouseFilterEnum.Ignore,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            ThemeTypeVariation = UiTheme.ScrittaJackpot,
            Visible = false
        };
        jackpot.SetAnchorsPreset(Control.LayoutPreset.Center);
        jackpot.GrowHorizontal = Control.GrowDirection.Both;
        jackpot.GrowVertical = Control.GrowDirection.Both;
        overlay.AddChild(jackpot);
        var hint = new Label
        {
            Name = "Salta",
            Text = "borsa.sequenza.salta",
            MouseFilter = Control.MouseFilterEnum.Ignore,
            HorizontalAlignment = HorizontalAlignment.Center,
            ThemeTypeVariation = UiTheme.Avviso,
            Visible = false
        };
        hint.SetAnchorsPreset(Control.LayoutPreset.CenterBottom);
        hint.GrowHorizontal = Control.GrowDirection.Both;
        hint.GrowVertical = Control.GrowDirection.Begin;
        hint.OffsetTop = -56;
        hint.OffsetBottom = -32;
        overlay.AddChild(hint);
        uiRoot.AddChild(overlay);
        view._overlay = overlay;
        view._hint = hint;
        view._jackpotLabel = jackpot;
        return view;
    }

    /// <summary>
    /// Starts the sequence of <paramref name="sale"/> on the screen centred at <paramref name="screenCentre"/> (world
    /// pixels): the bot rests at <paramref name="rest"/> and flies to <paramref name="coreStop"/>. A running one is
    /// replaced (it starts from the beginning).
    /// </summary>
    public void Start(SaleResolved sale, ScreenSaleResult? result, IGameView view, Vector2 screenCentre, Vector2 rest,
        Vector2 coreStop)
    {
        Sequence = new JackpotSequence(sale);
        var block = MarketScreen.Size * _tileSize;
        _rest = rest;
        _coreStop = coreStop;
        _glassCentre = screenCentre + new Vector2(0, -block * 0.11f);
        ItemId = sale.ItemId;
        ItemName = Testi.Oggetto(sale.ItemId, view.Content);
        OutcomeLed = sale.Jackpot ? ScreenLed.Yellow : result?.Led ?? ScreenLed.Blue;
        IncomeText = Testi.F("borsa.incasso", sale.Income);
        JackpotText = Testi.T("borsa.jackpot");
        IncomeGlyphs = new string[IncomeText.Length];
        for (var i = 0; i < IncomeText.Length; i++)
        {
            var c = IncomeText[i];
            IncomeGlyphs[i] = c is >= '0' and <= '9' ? Digits[c - '0'] : c.ToString();
        }

        for (var i = 0; i < Computing.Length; i++)
        {
            Computing[i] = Testi.F("borsa.sequenza.calcolo", new string('.', i + 1));
        }

        _piecesText = Testi.F("borsa.sequenza.pezzi", sale.Pieces.ToString(CultureInfo.InvariantCulture));
        _priceShown = int.MinValue;

        // The chart: the last points of the history before the sale, then the sale price.
        var item = view.MarketItem(sale.ItemId);
        BaseValue = item?.BaseValue ?? 0;
        var history = item?.HistoryMilli ?? [];
        var take = Math.Min(history.Count, History.Length - 1);
        for (var i = 0; i < take; i++)
        {
            History[i] = history[history.Count - take + i];
        }

        History[take] = sale.PriceMilli;
        HistoryCount = take + 1;
        var low = 1000;
        var high = 1000;
        for (var i = 0; i < HistoryCount; i++)
        {
            low = Math.Min(low, History[i]);
            high = Math.Max(high, History[i]);
        }

        HistoryLow = low;
        HistoryHigh = Math.Max(low + 200, high);

        _confettiFired = false;
        Visible = true;
        if (_overlay is not null)
        {
            _overlay.Visible = true;
            _overlay.MoveToFront();
        }

        if (_hint is not null)
        {
            _hint.Visible = !sale.Jackpot;
        }

        if (_jackpotLabel is not null)
        {
            _jackpotLabel.Visible = false;
        }

        QueueRedraw();
    }

    /// <summary>Hides everything and drops the sequence (end, skip, Home, load, new game, screen gone).</summary>
    public void Stop()
    {
        Sequence?.Close();
        Sequence = null;
        Visible = false;
        if (_overlay is not null)
        {
            _overlay.Visible = false;
        }
    }

    /// <summary>
    /// One frame: the timeline moves by <paramref name="realSeconds"/> (unless paused) and the confetti fly; the bot
    /// and the overlay are drawn again.
    /// </summary>
    public void Advance(double realSeconds)
    {
        if (Sequence is not { } sequence)
        {
            return;
        }

        var paused = sequence.Paused;
        sequence.Advance(realSeconds);
        var result = sequence.Step == JackpotStep.Risultato;
        if (sequence.Sale.Jackpot && result && !_confettiFired)
        {
            FireConfetti();
        }

        if (_jackpotLabel is not null)
        {
            // Hidden while paused: «In pausa» takes the middle of the view.
            _jackpotLabel.Visible = sequence.Sale.Jackpot && result && !paused;
        }

        if (_confettiFired && !paused)
        {
            MoveConfetti((float)realSeconds);
        }

        QueueRedraw();
        _overlay?.QueueRedraw();
    }

    /// <summary>The unit price the glass shows for <paramref name="priceMilli"/> («$12»), formatted only when it changes.</summary>
    public string PriceText(int priceMilli)
    {
        var unit = MarketPrices.UnitIncome(BaseValue, priceMilli);
        if (unit != _priceShown)
        {
            _priceShown = unit;
            _priceText = Testi.F("borsa.sequenza.prezzo", unit);
        }

        return _priceText;
    }

    public override void _Draw()
    {
        if (Sequence is not { Finished: false } sequence)
        {
            return;
        }

        var p = (float)sequence.StepProgress;
        var t = (float)sequence.Elapsed;
        var frame = (int)(t * BotFps);
        Texture2D? bot = null;
        var at = _rest;
        var flip = false;
        var alpha = 1f;
        var showPieces = false;
        switch (sequence.Step)
        {
            case JackpotStep.CambioCanale:
                bot = p < 0.5f ? Frame(_idle, frame) : Frame(_surprised, frame);
                alpha = Math.Min(1f, p / 0.2f);
                break;
            case JackpotStep.CorsaAlCore:
                if (p < 0.55f)
                {
                    at = _rest.Lerp(_coreStop, Smooth(p / 0.55f));
                    bot = Frame(_fly, frame);
                    flip = _coreStop.X < _rest.X;
                }
                else
                {
                    at = _coreStop;
                    bot = Frame(_carry, frame);
                    showPieces = true;
                }

                break;
            case JackpotStep.Ritorno:
                at = _coreStop.Lerp(_rest, Smooth(Math.Min(1f, p / 0.6f)));
                bot = Frame(_carry, frame);
                flip = p < 0.6f && _rest.X < _coreStop.X;
                showPieces = p < 0.5f;
                break;
            case JackpotStep.Calcolo:
                bot = p < 0.15f ? Frame(_throw, 0) : p < 0.5f ? Frame(_throw, 1) : Frame(_idle, frame);
                if (p >= 0.15f && p < 0.5f && _briefcase is not null)
                {
                    // The briefcase flies from over the bot's head into the glass, on an arc, shrinking.
                    var q = (p - 0.15f) / 0.35f;
                    var from = _rest + new Vector2(0, -_tileSize * 0.45f);
                    var pos = from.Lerp(_glassCentre, q) + new Vector2(0, -_tileSize * 0.8f * 4f * q * (1f - q));
                    var side = _briefcase.GetWidth() * (_tileSize / (float)Sprites.PixelsPerCell) * (1f - 0.6f * q);
                    DrawTextureRect(_briefcase, new Rect2(pos - new Vector2(side, side) / 2f, side, side), false);
                }

                break;
            case JackpotStep.Led:
                bot = Frame(_idle, frame);
                break;
            case JackpotStep.Risultato:
                bot = sequence.Sale.Jackpot ? Frame(_surprised, frame) : Frame(_idle, frame);
                break;
        }

        if (bot is not null)
        {
            var side = bot.GetWidth() * (_tileSize / (float)Sprites.PixelsPerCell);
            DrawSetTransform(at, 0f, new Vector2(flip ? -1f : 1f, 1f));
            DrawTextureRect(bot, new Rect2(-side / 2f, -side / 2f, side, side), false, new Color(1, 1, 1, alpha));
            DrawSetTransform(Vector2.Zero, 0f, Vector2.One);
        }

        if (showPieces)
        {
            // The pieces taken, over the bot's head, with the shadow of the HUD notices.
            var font = UiTheme.TextFont;
            var width = font.GetStringSize(_piecesText, HorizontalAlignment.Left, -1, UiTheme.TextSize).X;
            var baseline = at + new Vector2(-width / 2f, -_tileSize * 0.55f);
            DrawString(font, baseline + new Vector2(2, 2), _piecesText, HorizontalAlignment.Left, -1, UiTheme.TextSize, UiColors.Ombra);
            DrawString(font, baseline, _piecesText, HorizontalAlignment.Left, -1, UiTheme.TextSize, UiColors.Testo);
        }
    }

    private static Texture2D? Frame(Texture2D[] frames, int frame) =>
        frames.Length == 0 ? null : frames[frame % frames.Length];

    private static float Smooth(float x)
    {
        x = Math.Clamp(x, 0f, 1f);
        return x * x * (3f - 2f * x);
    }

    // --- Confetti (screen space, on the overlay) ---------------------------------------------------------------------

    private struct Confetto
    {
        public Vector2 Position;
        public Vector2 Velocity;
        public float Angle;
        public float Spin;
        public int Colour;
    }

    /// <summary>The burst: every piece leaves the middle of the game view upwards and outwards, then falls.</summary>
    private void FireConfetti()
    {
        _confettiFired = true;
        var size = _overlay?.Size ?? new Vector2(1280, 720);
        var centre = size / 2f;
        for (var i = 0; i < _confetti.Length; i++)
        {
            var angle = (float)(-Math.PI / 2 + (_random.NextDouble() - 0.5) * Math.PI * 1.4);
            var speed = (float)(260 + _random.NextDouble() * 420);
            _confetti[i] = new Confetto
            {
                Position = centre,
                Velocity = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * speed,
                Angle = (float)(_random.NextDouble() * Math.PI * 2),
                Spin = (float)((_random.NextDouble() - 0.5) * 12),
                Colour = i % 3
            };
        }
    }

    private void MoveConfetti(float dt)
    {
        for (var i = 0; i < _confetti.Length; i++)
        {
            ref var c = ref _confetti[i];
            c.Velocity = new Vector2(c.Velocity.X * (1f - 0.8f * dt), c.Velocity.Y + 520f * dt);
            c.Position += c.Velocity * dt;
            c.Angle += c.Spin * dt;
        }
    }

    private void DrawConfetti(Control canvas)
    {
        if (!_confettiFired || _confettiTexture is null || Sequence is not { Finished: false })
        {
            return;
        }

        var half = _confettiTexture.GetWidth() / 2f;
        for (var i = 0; i < _confetti.Length; i++)
        {
            ref var c = ref _confetti[i];
            // The LED colours of the variant in use, read at every draw.
            var colour = c.Colour switch
            {
                0 => UiColors.LedBlu,
                1 => UiColors.LedViola,
                _ => UiColors.LedGiallo
            };
            canvas.DrawSetTransform(c.Position, c.Angle, new Vector2(ConfettiScale, ConfettiScale));
            canvas.DrawTextureRect(_confettiTexture, new Rect2(-half, -half, half * 2, half * 2), false, colour);
        }

        canvas.DrawSetTransform(Vector2.Zero, 0f, Vector2.One);
    }

    /// <summary>Points of the chart revealed at this moment (fractional: the head moves smoothly between two points).</summary>
    public float ChartReveal()
    {
        var tail = Math.Min(ChartTail, HistoryCount - 1);
        var shown = HistoryCount - 1 - tail;
        if (Sequence is not { } sequence)
        {
            return HistoryCount - 1;
        }

        return sequence.Step switch
        {
            JackpotStep.CambioCanale or JackpotStep.CorsaAlCore => shown,
            JackpotStep.Ritorno => shown + tail * SlowMotion((float)sequence.StepProgress),
            _ => HistoryCount - 1
        };
    }

    /// <summary>Ease-out that slows down to a stop at 90% of the step, so the chart rests on the sale price.</summary>
    private static float SlowMotion(float p)
    {
        var x = Math.Clamp(p / 0.9f, 0f, 1f);
        return 1f - (1f - x) * (1f - x) * (1f - x);
    }
}
