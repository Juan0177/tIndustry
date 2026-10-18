using Godot;
using TIndustry.Shared;

namespace TIndustry.Godot;

/// <summary>HUD widget factories (panels, icons, cost chips) on the looks of <see cref="UiTheme"/>.</summary>
public static class HudStyle
{
    /// <summary>Item icon in its own colours (32 px; never tinted).</summary>
    public static Texture2D ItemTexture(string itemId) => Sprites.Item(itemId);

    /// <summary>Mindustry-style compact counts: 950 · 1.2k · 340k.</summary>
    public static string FormatCount(int n) => n switch
    {
        >= 100_000 => $"{n / 1000}k",
        >= 1_000 => $"{n / 1000f:0.#}k",
        _ => n.ToString()
    };

    /// <summary>
    /// Icon of <paramref name="size"/> UI px. Item and block art (32 px) drawn smaller is filtered smoothly so no
    /// art pixel is dropped; at 32 px or more, and for the white interface icons (<paramref name="pixel"/>), the
    /// crisp Nearest filter.
    /// </summary>
    public static TextureRect MakeIcon(Texture2D? tex, float size, Color? tint = null, bool pixel = false)
    {
        var icon = new TextureRect
        {
            Texture = tex,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            CustomMinimumSize = new Vector2(size, size),
            MouseFilter = Control.MouseFilterEnum.Ignore,
            TextureFilter = pixel || size >= Sprites.PixelsPerCell
                ? CanvasItem.TextureFilterEnum.Nearest
                : CanvasItem.TextureFilterEnum.Linear
        };
        if (tint is { } t)
        {
            icon.Modulate = t;
        }

        return icon;
    }

    public static PanelContainer MakePanel(string name)
    {
        var panel = new PanelContainer();
        StylePanel(panel, name);
        return panel;
    }

    /// <summary>Names an existing HUD panel and makes it take the mouse (its look is the theme's PanelContainer).</summary>
    public static void StylePanel(PanelContainer panel, string name)
    {
        panel.Name = name;
        panel.MouseFilter = Control.MouseFilterEnum.Stop;
    }

    /// <summary>A label with the theme look <paramref name="variation"/> (empty = plain text).</summary>
    public static Label MakeLabel(string text = "", string variation = "")
    {
        return new Label
        {
            Text = text,
            ThemeTypeVariation = variation,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            VerticalAlignment = VerticalAlignment.Center
        };
    }

    public static Control MakeMoneyChip(int amount, bool ok)
    {
        var row = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore, ThemeTypeVariation = UiTheme.RigaStretta };
        var icon = MakeIcon(ItemTexture("money"), 16);
        icon.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        row.AddChild(icon);
        row.AddChild(MakeLabel($"{amount}", ok ? "" : UiTheme.TestoRosso));
        return row;
    }

    /// <summary>Material cost chip; a red slash marks a missing material. Tooltip = item name.</summary>
    public static Control MakeCostChip(string itemId, int amount, bool ok, FactoryContent? content)
    {
        var wrap = new PanelContainer
        {
            CustomMinimumSize = new Vector2(44, 28),
            MouseFilter = Control.MouseFilterEnum.Ignore,
            TooltipText = Testi.Oggetto(itemId, content),
            ThemeTypeVariation = ok ? UiTheme.Slot : UiTheme.SlotMancante
        };

        var row = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore, ThemeTypeVariation = UiTheme.RigaStretta };
        wrap.AddChild(row);

        var iconHost = new Control
        {
            CustomMinimumSize = new Vector2(22, 22),
            MouseFilter = Control.MouseFilterEnum.Ignore,
            SizeFlagsVertical = Control.SizeFlags.ShrinkCenter
        };
        row.AddChild(iconHost);

        var icon = MakeIcon(ItemTexture(itemId), 20);
        icon.Position = new Vector2(1, 1);
        iconHost.AddChild(icon);

        if (!ok)
        {
            // Mindustry-style red slash over missing material.
            var slash = new ColorRect
            {
                Color = UiColors.Insufficiente,
                MouseFilter = Control.MouseFilterEnum.Ignore,
                Rotation = Mathf.DegToRad(-38f),
                PivotOffset = new Vector2(11, 1)
            };
            slash.Position = new Vector2(0, 10);
            slash.Size = new Vector2(24, 3);
            iconHost.AddChild(slash);

            var slash2 = new ColorRect
            {
                Color = UiColors.Ombra,
                MouseFilter = Control.MouseFilterEnum.Ignore,
                Rotation = Mathf.DegToRad(-38f),
                PivotOffset = new Vector2(11, 1)
            };
            slash2.Position = new Vector2(0, 12);
            slash2.Size = new Vector2(24, 2);
            iconHost.AddChild(slash2);
        }

        row.AddChild(MakeLabel($"×{amount}", ok ? "" : UiTheme.TestoRosso));
        return wrap;
    }
}
