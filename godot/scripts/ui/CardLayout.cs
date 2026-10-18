using Godot;

namespace TIndustry.Godot;

/// <summary>
/// Centred modal cards that keep inside the window at every UI scale (G4): a card has a preferred size in UI units and
/// is shrunk to the window (less <see cref="Margin"/> on each side) when the scaled window is smaller; it follows the
/// window when the UI scale changes. A card whose content may be taller than the window scrolls it
/// (<see cref="CenterToContent"/>).
/// </summary>
public static class CardLayout
{
    /// <summary>Space kept between a card and the window edge, UI units.</summary>
    public const float Margin = 12f;

    /// <summary>
    /// <paramref name="card"/> centred in its parent at <paramref name="preferred"/>, shrunk to fit the parent. Its
    /// content must cope with a smaller card (an expanding ScrollContainer inside).
    /// </summary>
    public static void Center(Control card, Vector2 preferred)
    {
        Prepare(card);
        void Fit()
        {
            if (card.GetParentControl() is not { } parent)
            {
                return;
            }

            var room = parent.Size - new Vector2(2 * Margin, 2 * Margin);
            Place(card, Math.Min(preferred.X, room.X), Math.Min(preferred.Y, room.Y));
        }

        Follow(card, Fit);
    }

    /// <summary>
    /// <paramref name="card"/> centred in its parent, <paramref name="width"/> wide (shrunk to fit) and as tall as
    /// <paramref name="content"/> needs; past the window height the content scrolls in <paramref name="scroll"/>
    /// (the card's only child, holding <paramref name="content"/>). It follows the content when that changes size.
    /// </summary>
    public static void CenterToContent(PanelContainer card, ScrollContainer scroll, Control content, float width)
    {
        Prepare(card);
        scroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
        scroll.VerticalScrollMode = ScrollContainer.ScrollMode.Auto;
        void Fit()
        {
            if (card.GetParentControl() is not { } parent)
            {
                return;
            }

            var room = parent.Size - new Vector2(2 * Margin, 2 * Margin);
            var frame = card.GetThemeStylebox("panel").GetMinimumSize();
            var needed = content.GetCombinedMinimumSize().Y + frame.Y;
            Place(card, Math.Min(width, room.X), Math.Min(needed, room.Y));
        }

        content.MinimumSizeChanged += Fit;
        Follow(card, Fit);
    }

    private static void Prepare(Control card)
    {
        card.SetAnchorsPreset(Control.LayoutPreset.Center);
        card.GrowHorizontal = Control.GrowDirection.Both;
        card.GrowVertical = Control.GrowDirection.Both;
    }

    private static void Place(Control card, float width, float height)
    {
        // Whole UI units, so the card edges sit on the same pixels at 100% and 200%.
        width = MathF.Floor(width / 2) * 2;
        height = MathF.Floor(height / 2) * 2;
        card.OffsetLeft = -width / 2;
        card.OffsetRight = width / 2;
        card.OffsetTop = -height / 2;
        card.OffsetBottom = height / 2;
    }

    /// <summary>Runs <paramref name="fit"/> now (if in the tree), when the card enters the tree and when its parent resizes.</summary>
    private static void Follow(Control card, Action fit)
    {
        Control? hooked = null;
        void Hook()
        {
            if (card.GetParentControl() is { } parent && !ReferenceEquals(parent, hooked))
            {
                hooked = parent;
                parent.Resized += fit;
            }

            fit();
        }

        card.TreeEntered += Hook;
        if (card.IsInsideTree())
        {
            Hook();
        }
    }
}
