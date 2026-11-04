using Godot;
using TIndustry.Shared;

namespace TIndustry.Godot;

/// <summary>
/// Mini chart of a market item in the Mercato overview: the price of the last points (one per second, oldest first,
/// in thousandths of the base value B) as a line, newest on the right, over a dashed line at B. The line colour follows
/// the trend the row's arrow shows (<see cref="UiColors.MercatoSu"/>, <see cref="UiColors.MercatoGiu"/>,
/// <see cref="UiColors.MercatoCrollo"/>), read at every draw so a new colour variant applies at once.
/// </summary>
public partial class MarketSparkline : Control
{
    /// <summary>Points drawn across the width (the history the simulation keeps: 2 minutes).</summary>
    public const int Span = 120;

    private int[] _points = [];
    private MarketTrend _trend;

    public int Points => _points.Length;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        SizeFlagsVertical = SizeFlags.ShrinkCenter;
    }

    /// <summary>Shows the last <see cref="Span"/> points of <paramref name="historyMilli"/>.</summary>
    public void SetData(IReadOnlyList<int> historyMilli, MarketTrend trend)
    {
        var from = Math.Max(0, historyMilli.Count - Span);
        _points = historyMilli.Skip(from).ToArray();
        _trend = trend;
        QueueRedraw();
    }

    public override void _Draw()
    {
        DrawRect(new Rect2(Vector2.Zero, Size), UiColors.FondoBloccato);
        DrawChart(this, new Rect2(Vector2.Zero, Size), _points, _trend, 2f, 3f);
    }

    /// <summary>
    /// The chart of <paramref name="points"/> (thousandths of B, oldest first, at most <see cref="Span"/>) in
    /// <paramref name="rect"/> of <paramref name="canvas"/>: the dashed line at B and the price line in the colour of
    /// <paramref name="trend"/>, newest on the right edge. The Mercato rows and the market screen in the world draw it.
    /// </summary>
    public static void DrawChart(CanvasItem canvas, Rect2 rect, IReadOnlyList<int> points, MarketTrend trend, float width, float pad)
    {
        var size = rect.Size;
        if (size.X < 2 || size.Y < 2)
        {
            return;
        }

        // Vertical range: the points and B, with a little room so a flat line is not glued to an edge.
        var low = Math.Min(1000, points.Count > 0 ? points.Min() : 1000);
        var high = Math.Max(low + 200, points.Count > 0 ? points.Max() : 1000);
        float Y(int milli) => rect.Position.Y + pad + (size.Y - 2 * pad) * (1f - (milli - low) / (float)(high - low));

        var baseY = Mathf.Round(Y(1000)) + 0.5f;
        canvas.DrawDashedLine(new Vector2(rect.Position.X, baseY), new Vector2(rect.End.X, baseY), UiColors.MercatoBase, 1f, 4f);
        if (points.Count < 2)
        {
            return;
        }

        var line = new Vector2[points.Count];
        var step = (size.X - 1) / (Span - 1);
        var x0 = rect.Position.X + size.X - 1 - step * (points.Count - 1);
        for (var i = 0; i < points.Count; i++)
        {
            line[i] = new Vector2(x0 + step * i, Y(points[i]));
        }

        canvas.DrawPolyline(line, ColorOf(trend), width);
    }

    /// <summary>Colour of the chart line, the price and its arrow for <paramref name="trend"/>.</summary>
    public static Color ColorOf(MarketTrend trend) => trend switch
    {
        MarketTrend.Su => UiColors.MercatoSu,
        MarketTrend.Giu => UiColors.MercatoGiu,
        MarketTrend.Crollo => UiColors.MercatoCrollo,
        _ => UiColors.MercatoGrafico
    };

    /// <summary>▲/▼ of <paramref name="item"/> against the start of its chart (2 minutes ago), as the Mercato rows show it.</summary>
    public static MarketTrend TrendOf(MarketItemView item)
    {
        var start = item.HistoryMilli.Count > 0 ? item.HistoryMilli[0] : item.PriceMilli;
        return item.PriceMilli > start ? MarketTrend.Su : item.PriceMilli < start ? MarketTrend.Giu : MarketTrend.Piatto;
    }

    /// <summary>The price of the moment with its arrow: "$12 ▲", "$12 ▼" or "$12".</summary>
    public static string PriceText(MarketItemView item, MarketTrend trend) => trend switch
    {
        MarketTrend.Su => $"${item.UnitIncome} ▲",
        MarketTrend.Giu => $"${item.UnitIncome} ▼",
        _ => $"${item.UnitIncome}"
    };
}

/// <summary>Direction a market row shows: arrow, chart colour and price colour.</summary>
public enum MarketTrend
{
    Piatto,
    Su,
    Giu,
    Crollo
}
