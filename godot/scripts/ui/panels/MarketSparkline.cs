using Godot;

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
        var size = Size;
        if (size.X < 2 || size.Y < 2)
        {
            return;
        }

        DrawRect(new Rect2(Vector2.Zero, size), UiColors.FondoBloccato);
        // Vertical range: the points and B, with a little room so a flat line is not glued to an edge.
        var low = Math.Min(1000, _points.Length > 0 ? _points.Min() : 1000);
        var high = Math.Max(low + 200, _points.Length > 0 ? _points.Max() : 1000);
        const float pad = 3f;
        float Y(int milli) => pad + (size.Y - 2 * pad) * (1f - (milli - low) / (float)(high - low));

        var baseY = Mathf.Round(Y(1000)) + 0.5f;
        DrawDashedLine(new Vector2(0, baseY), new Vector2(size.X, baseY), UiColors.MercatoBase, 1f, 4f);
        if (_points.Length < 2)
        {
            return;
        }

        var line = new Vector2[_points.Length];
        var step = (size.X - 1) / (Span - 1);
        var x0 = size.X - 1 - step * (_points.Length - 1);
        for (var i = 0; i < _points.Length; i++)
        {
            line[i] = new Vector2(x0 + step * i, Y(_points[i]));
        }

        var color = _trend switch
        {
            MarketTrend.Su => UiColors.MercatoSu,
            MarketTrend.Giu => UiColors.MercatoGiu,
            MarketTrend.Crollo => UiColors.MercatoCrollo,
            _ => UiColors.MercatoGrafico
        };
        DrawPolyline(line, color, 2f);
    }
}

/// <summary>Direction a market row shows: arrow, chart colour and price colour.</summary>
public enum MarketTrend
{
    Piatto,
    Su,
    Giu,
    Crollo
}
