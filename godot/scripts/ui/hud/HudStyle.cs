using Godot;
using TIndustry.Shared;

namespace TIndustry.Godot;

/// <summary>Shared HUD palette and widget factories (panels, slots, icons, cost chips).</summary>
public static class HudStyle
{
    public static readonly Color PanelBg = new(0.08f, 0.09f, 0.10f, 0.92f);
    public static readonly Color PanelEdge = new(0.28f, 0.32f, 0.30f, 1f);
    public static readonly Color SlotBg = new(0.14f, 0.16f, 0.15f, 1f);
    public static readonly Color SlotIdle = new(0.38f, 0.42f, 0.40f, 1f);
    public static readonly Color SlotSelected = new(0.92f, 0.78f, 0.28f, 1f);
    public static readonly Color TextPrimary = new(0.93f, 0.95f, 0.90f, 1f);
    public static readonly Color TextMuted = new(0.68f, 0.74f, 0.68f, 1f);
    public static readonly Color Insufficient = new(0.90f, 0.38f, 0.32f, 1f);
    public static readonly Color MoneyColor = new(0.98f, 0.82f, 0.38f, 1f);
    public static readonly Color PowerColor = new(0.98f, 0.90f, 0.42f, 1f);
    public static readonly Color CatTools = new(0.78f, 0.52f, 0.48f, 1f);
    public static readonly Color CatLogistics = new(0.48f, 0.68f, 0.84f, 1f);
    public static readonly Color CatProduction = new(0.84f, 0.60f, 0.30f, 1f);
    public static readonly Color CatPower = new(0.92f, 0.80f, 0.30f, 1f);

    /// <summary>Item icon in its own colours (32 px; never tinted).</summary>
    public static Texture2D ItemTexture(string itemId) => Sprites.Item(itemId);

    /// <summary>Mindustry-style compact counts: 950 · 1.2k · 340k.</summary>
    public static string FormatCount(int n) => n switch
    {
        >= 100_000 => $"{n / 1000}k",
        >= 1_000 => $"{n / 1000f:0.#}k",
        _ => n.ToString()
    };

    public static TextureRect MakeIcon(Texture2D? tex, float size, Color? tint = null)
    {
        var icon = new TextureRect
        {
            Texture = tex,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            CustomMinimumSize = new Vector2(size, size),
            MouseFilter = Control.MouseFilterEnum.Ignore
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

    /// <summary>Applies the dark HUD panel look to an existing container (used by panel subclasses).</summary>
    public static void StylePanel(PanelContainer panel, string name)
    {
        panel.Name = name;
        panel.MouseFilter = Control.MouseFilterEnum.Stop;
        var style = new StyleBoxFlat
        {
            BgColor = PanelBg,
            BorderColor = PanelEdge,
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            CornerRadiusTopLeft = 4,
            CornerRadiusTopRight = 4,
            CornerRadiusBottomLeft = 4,
            CornerRadiusBottomRight = 4,
            ContentMarginLeft = 0,
            ContentMarginRight = 0,
            ContentMarginTop = 0,
            ContentMarginBottom = 0
        };
        panel.AddThemeStyleboxOverride("panel", style);
    }

    public static StyleBoxFlat MakeSlotStyle(Color border, int width) => new()
    {
        BgColor = SlotBg,
        BorderColor = border,
        BorderWidthLeft = width,
        BorderWidthTop = width,
        BorderWidthRight = width,
        BorderWidthBottom = width,
        CornerRadiusTopLeft = 3,
        CornerRadiusTopRight = 3,
        CornerRadiusBottomLeft = 3,
        CornerRadiusBottomRight = 3,
        ContentMarginLeft = 2,
        ContentMarginRight = 2,
        ContentMarginTop = 2,
        ContentMarginBottom = 2
    };

    public static Control MakeMoneyChip(int amount, bool ok)
    {
        var row = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        row.AddThemeConstantOverride("separation", 2);
        var icon = MakeIcon(ItemTexture("money"), 16);
        icon.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        row.AddChild(icon);
        var label = new Label { Text = $"{amount}", MouseFilter = Control.MouseFilterEnum.Ignore, VerticalAlignment = VerticalAlignment.Center };
        label.AddThemeColorOverride("font_color", ok ? TextPrimary : Insufficient);
        label.AddThemeFontSizeOverride("font_size", 11);
        row.AddChild(label);
        return row;
    }

    /// <summary>Material cost chip; a red slash marks a missing material. Tooltip = item name.</summary>
    public static Control MakeCostChip(string itemId, int amount, bool ok, FactoryContent? content)
    {
        var wrap = new PanelContainer
        {
            CustomMinimumSize = new Vector2(44, 28),
            MouseFilter = Control.MouseFilterEnum.Ignore,
            TooltipText = content?.DisplayName(itemId) ?? itemId
        };
        wrap.AddThemeStyleboxOverride("panel", MakeSlotStyle(ok ? SlotIdle : Insufficient, 1));

        var row = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        row.AddThemeConstantOverride("separation", 2);
        wrap.AddChild(row);

        var iconHost = new Control
        {
            CustomMinimumSize = new Vector2(22, 22),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        row.AddChild(iconHost);

        var icon = new TextureRect
        {
            Texture = ItemTexture(itemId),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            CustomMinimumSize = new Vector2(20, 20),
            Position = new Vector2(1, 1),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        iconHost.AddChild(icon);

        if (!ok)
        {
            // Mindustry-style red slash over missing material.
            var slash = new ColorRect
            {
                Color = Insufficient,
                MouseFilter = Control.MouseFilterEnum.Ignore,
                Rotation = Mathf.DegToRad(-38f),
                PivotOffset = new Vector2(11, 1)
            };
            slash.Position = new Vector2(0, 10);
            slash.Size = new Vector2(24, 3);
            iconHost.AddChild(slash);

            var slash2 = new ColorRect
            {
                Color = new Color(0.15f, 0.05f, 0.05f, 0.85f),
                MouseFilter = Control.MouseFilterEnum.Ignore,
                Rotation = Mathf.DegToRad(-38f),
                PivotOffset = new Vector2(11, 1)
            };
            slash2.Position = new Vector2(0, 12);
            slash2.Size = new Vector2(24, 2);
            iconHost.AddChild(slash2);
        }

        var qty = new Label
        {
            Text = $"×{amount}",
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        qty.AddThemeColorOverride("font_color", ok ? TextPrimary : Insufficient);
        qty.AddThemeFontSizeOverride("font_size", 11);
        row.AddChild(qty);

        return wrap;
    }
}
