using System.Numerics;
using Raylib_cs;

namespace TIndustry.Logistics;

/// <summary>
/// Procedural Tiny-Industry-inspired world silhouettes (Raylib immediate-mode).
/// Soft shadow + rim + distinct shapes so buildings read at zoom without claiming DLSS/FSR.
/// </summary>
public static class WorldGraphics
{
    private const float BaseTile = 36f;

    public static void DrawSoftShadow(int x, int y, int size, int alpha, float scale)
    {
        var ox = Math.Max(2, (int)(4 * scale));
        var oy = Math.Max(3, (int)(6 * scale));
        var a = Math.Clamp((int)(alpha * 0.55f), 40, 160);
        Raylib.DrawRectangle(x + ox, y + oy, size - ox, size - oy, new Color(6, 8, 8, a));
    }

    /// <summary>Filled body with top/left rim highlight and outer accent outline.</summary>
    public static void DrawRimBody(
        int x,
        int y,
        int size,
        Color fill,
        Color rim,
        Color accent,
        int alpha,
        float scale)
    {
        var pad = Math.Max(2, (int)(2 * scale));
        Raylib.DrawRectangle(x + pad, y + pad, size - pad * 2, size - pad * 2, WithAlpha(fill, alpha));
        var rimW = Math.Max(2, (int)(3 * scale));
        Raylib.DrawRectangle(x + pad, y + pad, size - pad * 2, rimW, WithAlpha(rim, alpha));
        Raylib.DrawRectangle(x + pad, y + pad, rimW, size - pad * 2, WithAlpha(rim, (int)(alpha * 0.85f)));
        UiTheme.DrawAccentRect(x + pad, y + pad, size - pad * 2, size - pad * 2, WithAlpha(accent, alpha),
            Math.Max(1, (int)(2 * scale)));
    }

    public static void DrawMinerSilhouette(
        int x,
        int y,
        int size,
        float progress,
        float efficiency,
        bool preview,
        float tileSize,
        Action<string, int, int, int, Color>? drawLabel)
    {
        var alpha = preview ? 150 : 255;
        var scale = tileSize / BaseTile;
        DrawSoftShadow(x, y, size, alpha, scale);
        DrawRimBody(x, y, size,
            new Color(52, 54, 50, 255),
            new Color(92, 96, 88, 255),
            new Color(240, 180, 80, 255),
            alpha, scale);

        // Chassis deck
        var deckY = y + (int)(size * 0.55f);
        Raylib.DrawRectangle(x + (int)(8 * scale), deckY, size - (int)(16 * scale), (int)(14 * scale),
            new Color(34, 36, 34, alpha));
        Raylib.DrawRectangle(x + (int)(8 * scale), deckY, size - (int)(16 * scale), Math.Max(1, (int)(3 * scale)),
            new Color(120, 110, 70, alpha));

        // Drill mast + rotating bit (TI miner silhouette)
        var mastX = x + size / 2 - Math.Max(2, (int)(4 * scale));
        Raylib.DrawRectangle(mastX, y + (int)(10 * scale), Math.Max(4, (int)(8 * scale)),
            deckY - y - (int)(8 * scale), new Color(70, 74, 68, alpha));
        Raylib.DrawRectangle(mastX - (int)(6 * scale), y + (int)(10 * scale),
            Math.Max(10, (int)(20 * scale)), Math.Max(3, (int)(6 * scale)),
            new Color(88, 92, 84, alpha));

        var center = new Vector2(x + size / 2f, y + size * 0.38f);
        var angle = preview ? 0f : (float)Raylib.GetTime() * 110f;
        var bitR = 14f * scale;
        Raylib.DrawPoly(center, 6, bitR, angle, new Color(108, 116, 110, alpha));
        Raylib.DrawPolyLinesEx(center, 6, bitR, angle, Math.Max(1.5f, 2.5f * scale),
            new Color(230, 210, 150, alpha));
        Raylib.DrawCircleV(center, 5.5f * scale, new Color(210, 143, 68, alpha));
        // Bit tip
        Raylib.DrawTriangle(
            center + new Vector2(0, 16 * scale),
            center + new Vector2(-5 * scale, 6 * scale),
            center + new Vector2(5 * scale, 6 * scale),
            new Color(190, 160, 90, alpha));

        // Side braces
        Raylib.DrawRectangle(x + (int)(6 * scale), y + (int)(22 * scale), (int)(10 * scale), (int)(4 * scale),
            new Color(160, 130, 60, alpha));
        Raylib.DrawRectangle(x + size - (int)(16 * scale), y + (int)(22 * scale), (int)(10 * scale), (int)(4 * scale),
            new Color(160, 130, 60, alpha));

        DrawProgressBar(x, y, size, progress, new Color(231, 166, 66, alpha), alpha, scale);

        if (tileSize >= 12f && drawLabel is not null)
        {
            drawLabel($"{efficiency:P0}", x + size / 2, y + size - (int)(25 * scale), alpha,
                efficiency <= 0f
                    ? new Color(225, 120, 100, alpha)
                    : new Color(233, 190, 96, alpha));
        }

        TryDrawWorldIcon("miner", x, y, size, alpha, scale, tileSize);
    }

    public static void DrawSmelterSilhouette(
        int x,
        int y,
        int size,
        float craftProgress,
        bool isCrafting,
        Direction direction,
        bool preview,
        float tileSize,
        Action<string, int, int, int, Color>? drawLabel,
        Action<Vector2, Direction, int, float> drawDirectionMark)
    {
        var alpha = preview ? 150 : 255;
        var scale = tileSize / BaseTile;
        DrawSoftShadow(x, y, size, alpha, scale);
        DrawRimBody(x, y, size,
            new Color(78, 44, 36, 255),
            new Color(140, 80, 55, 255),
            new Color(240, 130, 80, 255),
            alpha, scale);

        // Chimney stack (TI furnace silhouette)
        var chimW = Math.Max(6, (int)(12 * scale));
        var chimH = Math.Max(14, (int)(28 * scale));
        var chimX = x + size - chimW - (int)(10 * scale);
        var chimY = y + (int)(6 * scale);
        Raylib.DrawRectangle(chimX, chimY, chimW, chimH, new Color(48, 30, 26, alpha));
        Raylib.DrawRectangle(chimX - 2, chimY, chimW + 4, Math.Max(2, (int)(4 * scale)),
            new Color(90, 55, 40, alpha));
        if (!preview)
        {
            var smoke = (float)Raylib.GetTime() * 1.4f;
            var sy = chimY - (int)((6 + MathF.Sin(smoke) * 3) * scale);
            Raylib.DrawCircle(chimX + chimW / 2, sy, Math.Max(2f, 3.5f * scale),
                new Color(70, 60, 55, Math.Clamp(alpha - 80, 40, 180)));
            Raylib.DrawCircle(chimX + chimW / 2 + (int)(4 * scale), sy - (int)(5 * scale),
                Math.Max(1.5f, 2.5f * scale), new Color(90, 80, 70, Math.Clamp(alpha - 100, 30, 140)));
        }

        // Furnace mouth + glow
        var mouthX = x + (int)(12 * scale);
        var mouthY = y + (int)(size * 0.38f);
        var mouthW = size - (int)(36 * scale);
        var mouthH = Math.Max(10, (int)(22 * scale));
        Raylib.DrawRectangle(mouthX, mouthY, mouthW, mouthH, new Color(22, 12, 10, alpha));
        var glow = isCrafting
            ? 11f + MathF.Sin((float)Raylib.GetTime() * 5f) * 4f
            : 7f + MathF.Sin((float)Raylib.GetTime() * 2f) * 1.5f;
        var mouthCenter = new Vector2(mouthX + mouthW / 2f, mouthY + mouthH / 2f);
        Raylib.DrawCircleV(mouthCenter, glow * scale, new Color(200, 70, 30, alpha));
        Raylib.DrawCircleV(mouthCenter, glow * 0.45f * scale, new Color(255, 190, 80, alpha));
        if (isCrafting)
        {
            Raylib.DrawCircle(
                mouthX + (int)(6 * scale),
                mouthY + mouthH - (int)(4 * scale),
                Math.Max(1.5f, 2.2f * scale),
                new Color(255, 140, 40, alpha));
            Raylib.DrawCircle(
                mouthX + mouthW - (int)(8 * scale),
                mouthY + mouthH - (int)(5 * scale),
                Math.Max(1.2f, 1.8f * scale),
                new Color(255, 200, 60, alpha));
        }

        // Brick banding
        for (var i = 0; i < 3; i++)
        {
            var by = y + (int)((14 + i * 10) * scale);
            if (by < mouthY)
            {
                Raylib.DrawLine(x + (int)(8 * scale), by, x + size - (int)(28 * scale), by,
                    new Color(55, 30, 24, alpha));
            }
        }

        var markCenter = new Vector2(x + size / 2f, y + size / 2f);
        drawDirectionMark(markCenter + DirectionVector(direction) * (16f * scale), direction, alpha, tileSize);

        var barProgress = isCrafting ? craftProgress : 0f;
        DrawProgressBar(x, y, size, barProgress, new Color(235, 120, 70, alpha), alpha, scale);

        if (tileSize >= 12f && drawLabel is not null)
        {
            drawLabel("FORNO", x + (int)(14 * scale), y + (int)(8 * scale), alpha,
                new Color(255, 220, 190, alpha));
        }

        TryDrawWorldIcon("smelter", x, y, size, alpha, scale, tileSize);
    }

    public static void DrawAssemblerSilhouette(
        int x,
        int y,
        int size,
        float craftProgress,
        bool isCrafting,
        Direction direction,
        bool preview,
        float tileSize,
        Action<string, int, int, int, Color>? drawLabel,
        Action<Vector2, Direction, int, float> drawDirectionMark)
    {
        var alpha = preview ? 150 : 255;
        var scale = tileSize / BaseTile;
        DrawSoftShadow(x, y, size, alpha, scale);
        DrawRimBody(x, y, size,
            new Color(28, 64, 78, 255),
            new Color(70, 130, 150, 255),
            new Color(90, 210, 220, 255),
            alpha, scale);

        // Work plate
        Raylib.DrawRectangle(x + (int)(12 * scale), y + (int)(18 * scale),
            size - (int)(24 * scale), size - (int)(36 * scale),
            new Color(16, 32, 42, alpha));

        // Mechanical arms (bob when crafting)
        var bob = isCrafting && !preview
            ? MathF.Sin((float)Raylib.GetTime() * 6f) * 3f * scale
            : 0f;
        DrawArm(x + (int)(14 * scale), y + (int)(20 * scale) + (int)bob, scale, alpha, true);
        DrawArm(x + size - (int)(28 * scale), y + (int)(20 * scale) - (int)bob, scale, alpha, false);

        // Center gear
        var center = new Vector2(x + size / 2f, y + size / 2f + 2 * scale);
        var angle = preview ? 15f : (float)Raylib.GetTime() * (isCrafting ? 80f : 25f);
        Raylib.DrawPoly(center, 8, 11 * scale, angle, new Color(50, 110, 125, alpha));
        Raylib.DrawPolyLinesEx(center, 8, 11 * scale, angle, Math.Max(1.5f, 2.2f * scale),
            new Color(160, 230, 240, alpha));
        Raylib.DrawCircleV(center, 4 * scale, new Color(110, 210, 220, alpha));

        drawDirectionMark(center + DirectionVector(direction) * (16f * scale), direction, alpha, tileSize);

        var barProgress = isCrafting ? craftProgress : 0f;
        DrawProgressBar(x, y, size, barProgress, new Color(80, 190, 200, alpha), alpha, scale);

        if (tileSize >= 12f && drawLabel is not null)
        {
            drawLabel("ASSY", x + (int)(14 * scale), y + (int)(8 * scale), alpha,
                new Color(190, 240, 246, alpha));
        }

        TryDrawWorldIcon("assembler", x, y, size, alpha, scale, tileSize);
    }

    public static void DrawGeneratorSilhouette(
        int x,
        int y,
        int size,
        bool preview,
        float tileSize,
        Action<string, int, int, int, Color>? drawLabel)
    {
        var alpha = preview ? 150 : 255;
        var scale = tileSize / BaseTile;
        DrawSoftShadow(x, y, size, alpha, scale);
        DrawRimBody(x, y, size,
            new Color(118, 88, 28, 255),
            new Color(180, 145, 55, 255),
            new Color(245, 205, 80, 255),
            alpha, scale);

        // Twin coils / turbine cylinders
        var coilW = Math.Max(8, (int)(14 * scale));
        var coilH = Math.Max(20, (int)(36 * scale));
        var leftX = x + (int)(14 * scale);
        var rightX = x + size - coilW - (int)(14 * scale);
        var coilY = y + (int)(14 * scale);
        DrawCoil(leftX, coilY, coilW, coilH, alpha, scale, preview);
        DrawCoil(rightX, coilY, coilW, coilH, alpha, scale, preview);

        // Center spark / pulse
        var center = new Vector2(x + size / 2f, y + size * 0.55f);
        var pulse = preview ? 8f : 10f + MathF.Sin((float)Raylib.GetTime() * 5f) * 3.5f;
        Raylib.DrawCircleV(center, (pulse + 4) * scale * 0.55f, new Color(180, 120, 30, alpha));
        Raylib.DrawCircleV(center, pulse * scale * 0.55f, new Color(240, 190, 60, alpha));
        Raylib.DrawCircleV(center, 4 * scale, new Color(255, 235, 150, alpha));

        // Base platform
        Raylib.DrawRectangle(x + (int)(10 * scale), y + size - (int)(18 * scale),
            size - (int)(20 * scale), Math.Max(4, (int)(8 * scale)),
            new Color(70, 52, 18, alpha));

        if (tileSize >= 12f && drawLabel is not null)
        {
            drawLabel("GEN", x + (int)(14 * scale), y + (int)(8 * scale), alpha,
                new Color(255, 235, 170, alpha));
        }

        TryDrawWorldIcon("generator", x, y, size, alpha, scale, tileSize);
    }

    public static void DrawCoreSilhouette(
        int x,
        int y,
        int size,
        float tileSize,
        Action<string, int, int, int, Color>? drawLabel)
    {
        var alpha = 255;
        var scale = tileSize / BaseTile;
        DrawSoftShadow(x, y, size, alpha, scale);

        // Fortified outer wall
        Raylib.DrawRectangle(x + 2, y + 2, size - 4, size - 4, new Color(24, 48, 36, 255));
        UiTheme.DrawAccentRect(x + 3, y + 3, size - 6, size - 6, new Color(110, 210, 140, 255),
            Math.Max(2, (int)(3 * scale)));
        // Rim light
        Raylib.DrawRectangle(x + 4, y + 4, size - 8, Math.Max(2, (int)(4 * scale)),
            new Color(70, 140, 95, 220));
        Raylib.DrawRectangle(x + 4, y + 4, Math.Max(2, (int)(4 * scale)), size - 8,
            new Color(55, 110, 75, 200));

        // Corner posts
        var post = Math.Max(5, (int)(10 * scale));
        var posts = new[]
        {
            (x + 6, y + 6),
            (x + size - 6 - post, y + 6),
            (x + 6, y + size - 6 - post),
            (x + size - 6 - post, y + size - 6 - post)
        };
        foreach (var (px, py) in posts)
        {
            Raylib.DrawRectangle(px, py, post, post, new Color(40, 70, 52, 255));
            Raylib.DrawRectangleLines(px, py, post, post, new Color(130, 210, 150, 255));
        }

        // Inner chamber
        var inset = Math.Max(16, (int)(28 * scale));
        Raylib.DrawRectangle(x + inset, y + inset, size - inset * 2, size - inset * 2,
            new Color(18, 30, 26, 255));
        Raylib.DrawRectangleLines(x + inset, y + inset, size - inset * 2, size - inset * 2,
            new Color(72, 128, 92, 255));

        var pulse = 20f + MathF.Sin((float)Raylib.GetTime() * 3f) * 3.5f;
        var center = new Vector2(x + size / 2f, y + size / 2f);
        Raylib.DrawPoly(center, 6, (pulse + 10) * scale * 0.55f, 0f, new Color(36, 90, 58, 255));
        Raylib.DrawCircleV(center, (pulse + 6) * scale * 0.5f, new Color(44, 104, 68, 255));
        Raylib.DrawCircleV(center, pulse * scale * 0.5f, new Color(103, 225, 139, 255));
        Raylib.DrawCircleV(center, 10 * scale, new Color(210, 251, 218, 255));

        if (tileSize >= 12f && drawLabel is not null)
        {
            drawLabel("CORE", x + size / 2, y + size - (int)(34 * scale), alpha,
                new Color(201, 232, 207, 255));
        }
    }

    public static void DrawBeltTrack(
        int x,
        int y,
        int size,
        int alpha,
        bool fastTier)
    {
        // Soft pad under belt
        Raylib.DrawRectangle(x + size / 6, y + size / 6 + 2, size - size / 3, size - size / 3,
            new Color(12, 14, 14, Math.Clamp(alpha - 60, 50, 160)));
        Raylib.DrawRectangle(x + size / 5, y + size / 5, size - size * 2 / 5, size - size * 2 / 5,
            new Color(38, 43, 42, alpha));
        // Rail lines for TI belt readability
        var inset = Math.Max(2, size / 10);
        Raylib.DrawRectangle(x + size / 5 + inset, y + size / 5 + inset,
            size - size * 2 / 5 - inset * 2, Math.Max(1, size / 16),
            new Color(55, 62, 58, alpha));
        Raylib.DrawRectangle(x + size / 5 + inset, y + size - size / 5 - inset - Math.Max(1, size / 16),
            size - size * 2 / 5 - inset * 2, Math.Max(1, size / 16),
            new Color(55, 62, 58, alpha));
        Raylib.DrawRectangle(x + size / 4, y + size / 4, size / 2, size / 2,
            fastTier
                ? new Color(56, 92, 110, alpha)
                : new Color(70, 77, 74, alpha));
        // Top rim on center plate
        Raylib.DrawRectangle(x + size / 4, y + size / 4, size / 2, Math.Max(1, size / 20),
            fastTier
                ? new Color(90, 140, 160, alpha)
                : new Color(100, 110, 105, alpha));
    }

    /// <summary>
    /// One-way chevrons with dark outline + bright fill for zoom readability.
    /// </summary>
    public static void DrawFlowChevrons(Vector2 center, Direction direction, int alpha, float tileSize)
    {
        if (tileSize < 10f)
        {
            return;
        }

        var vector = DirectionVector(direction);
        var side = new Vector2(-vector.Y, vector.X);
        var scale = tileSize / BaseTile;
        var spacing = 11f;
        var phase = (float)(Raylib.GetTime() * 24.0 % spacing);
        var outline = new Color(28, 26, 16, alpha);
        var fill = new Color(235, 215, 120, alpha);
        var strokeOuter = Math.Max(2.4f, 3.4f * scale);
        var strokeInner = Math.Max(1.4f, 2.0f * scale);
        var wing = 4.8f * scale;
        var depth = 5.5f * scale;

        for (var offset = -18f + phase; offset <= 18f; offset += spacing)
        {
            var tip = center + vector * (offset * scale);
            var back = tip - vector * depth;
            var left = back + side * wing;
            var right = back - side * wing;
            Raylib.DrawLineEx(left, tip, strokeOuter, outline);
            Raylib.DrawLineEx(right, tip, strokeOuter, outline);
            Raylib.DrawLineEx(left, tip, strokeInner, fill);
            Raylib.DrawLineEx(right, tip, strokeInner, fill);
        }
    }

    public static Color TerrainColor(TerrainKind kind, int worldX, int worldY)
    {
        var baseColor = kind switch
        {
            TerrainKind.Grass => new Color(46, 78, 54, 255),
            TerrainKind.Soil => new Color(108, 88, 58, 255),
            TerrainKind.Stone => new Color(92, 98, 96, 255),
            TerrainKind.Water => new Color(42, 78, 98, 255),
            _ => Color.Black
        };

        // Dense micro-variation: two hash octaves so tiles don't look flat at zoom.
        var n1 = Hash01(worldX, worldY, 917);
        var n2 = Hash01(worldX * 3 + 7, worldY * 5 - 3, 421);
        var delta = (int)((n1 - 0.5f) * 22f + (n2 - 0.5f) * 10f);
        // Checker undertone for grass/soil readability at mid zoom
        if (kind is TerrainKind.Grass or TerrainKind.Soil && ((worldX + worldY) & 1) == 0)
        {
            delta -= 4;
        }

        return new Color(
            ClampByte(baseColor.R + delta),
            ClampByte(baseColor.G + delta),
            ClampByte(baseColor.B + (kind == TerrainKind.Water ? delta / 2 : delta)),
            (byte)255);
    }

    public static void DrawTerrainDetail(TerrainKind kind, int ix, int iy, int size, int sizeY, float tileSize, int worldX, int worldY)
    {
        if (tileSize < 12f || kind == TerrainKind.Water)
        {
            return;
        }

        var speckCount = tileSize >= 22f ? 3 : 1;
        for (var i = 0; i < speckCount; i++)
        {
            var hx = Hash01(worldX + i * 13, worldY - i * 7, 55 + i);
            var hy = Hash01(worldX - i * 9, worldY + i * 17, 77 + i);
            var sx = ix + 2 + (int)(hx * Math.Max(1, size - 4));
            var sy = iy + 2 + (int)(hy * Math.Max(1, sizeY - 4));
            var speck = kind switch
            {
                TerrainKind.Grass => new Color(36, 62, 42, 90),
                TerrainKind.Soil => new Color(86, 68, 42, 90),
                _ => new Color(70, 74, 72, 80)
            };
            Raylib.DrawRectangle(sx, sy, Math.Max(1, size / 14), Math.Max(1, size / 14), speck);
        }
    }

    private static void DrawProgressBar(int x, int y, int size, float progress, Color fill, int alpha, float scale)
    {
        var barH = Math.Max(3, (int)(4 * scale));
        var barY = y + size - (int)(10 * scale);
        var barX = x + (int)(9 * scale);
        var barW = size - (int)(18 * scale);
        Raylib.DrawRectangle(barX, barY, barW, barH, new Color(25, 29, 28, alpha));
        Raylib.DrawRectangle(barX, barY, (int)(barW * Math.Clamp(progress, 0f, 1f)), barH, fill);
    }

    private static void DrawArm(int x, int y, float scale, int alpha, bool left)
    {
        var w = Math.Max(8, (int)(14 * scale));
        var h = Math.Max(3, (int)(5 * scale));
        Raylib.DrawRectangle(x, y, w, h, new Color(90, 160, 175, alpha));
        var tipX = left ? x + w : x - Math.Max(3, (int)(5 * scale));
        Raylib.DrawRectangle(tipX, y + h, Math.Max(3, (int)(5 * scale)), Math.Max(6, (int)(10 * scale)),
            new Color(120, 200, 210, alpha));
    }

    private static void DrawCoil(int x, int y, int w, int h, int alpha, float scale, bool preview)
    {
        Raylib.DrawRectangle(x, y, w, h, new Color(70, 52, 18, alpha));
        Raylib.DrawRectangle(x + 1, y + 1, w - 2, Math.Max(2, (int)(3 * scale)),
            new Color(200, 170, 70, alpha));
        var bands = 4;
        for (var i = 0; i < bands; i++)
        {
            var by = y + (int)((8 + i * 7) * scale);
            if (by < y + h - 4)
            {
                Raylib.DrawRectangle(x + 1, by, w - 2, Math.Max(1, (int)(2 * scale)),
                    new Color(160, 120, 40, alpha));
            }
        }

        if (!preview)
        {
            var glowA = (int)(90 + MathF.Sin((float)Raylib.GetTime() * 4f + x * 0.1f) * 40);
            Raylib.DrawRectangle(x + 2, y + 2, w - 4, h - 4, new Color(255, 220, 80, Math.Clamp(glowA, 40, 140)));
        }
    }

    private static void TryDrawWorldIcon(string key, int x, int y, int size, int alpha, float scale, float tileSize)
    {
        if (tileSize < 14f || !GameIcons.Has(key) || alpha < 200)
        {
            return;
        }

        var icon = Math.Max(10, (int)(16 * scale));
        GameIcons.Draw(key, x + size - icon - (int)(6 * scale), y + (int)(6 * scale), icon,
            new Color(255, 255, 255, Math.Clamp(alpha - 40, 120, 220)));
    }

    private static Vector2 DirectionVector(Direction direction) => direction switch
    {
        Direction.North => new Vector2(0, -1),
        Direction.East => new Vector2(1, 0),
        Direction.South => new Vector2(0, 1),
        Direction.West => new Vector2(-1, 0),
        _ => Vector2.Zero
    };

    private static Color WithAlpha(Color color, int alpha) =>
        new(color.R, color.G, color.B, (byte)Math.Clamp(alpha, 0, 255));

    private static byte ClampByte(int value) => (byte)Math.Clamp(value, 0, 255);

    private static float Hash01(int x, int y, int seed)
    {
        var value = unchecked((uint)(x * 374761393 + y * 668265263 + seed * 1442695041));
        value = (value ^ (value >> 13)) * 1274126177u;
        return (value ^ (value >> 16)) / (float)uint.MaxValue;
    }
}
