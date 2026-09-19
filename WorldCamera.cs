using System.Numerics;

namespace TIndustry.Logistics;

/// <summary>
/// Pan/zoom camera that maps between screen pixels and world/grid coordinates.
/// </summary>
public sealed class WorldCamera
{
    public const float MinZoom = 0.25f;
    public const float MaxZoom = 2.5f;
    public const float DefaultZoom = 1f;
    public const float PanSpeedPixels = 520f;
    // Edge pan removed: camera moves only via WASD / middle-drag / Shift+drag.

    public WorldCamera(float x, float y, float zoom = DefaultZoom)
    {
        X = x;
        Y = y;
        Zoom = Math.Clamp(zoom, MinZoom, MaxZoom);
    }

    /// <summary>World-space X of the top-left of the viewport content area.</summary>
    public float X { get; set; }

    /// <summary>World-space Y of the top-left of the viewport content area.</summary>
    public float Y { get; set; }

    public float Zoom { get; private set; }

    public float TileSize(int baseTileSize) => baseTileSize * Zoom;

    public void SetZoom(float zoom) => Zoom = Math.Clamp(zoom, MinZoom, MaxZoom);

    public void ZoomAt(float screenX, float screenY, float viewportLeft, float viewportTop, float factor)
    {
        var worldBefore = ScreenToWorld(screenX, screenY, viewportLeft, viewportTop);
        SetZoom(Zoom * factor);
        var worldAfter = ScreenToWorld(screenX, screenY, viewportLeft, viewportTop);
        X += worldBefore.X - worldAfter.X;
        Y += worldBefore.Y - worldAfter.Y;
    }

    public void Pan(float deltaWorldX, float deltaWorldY)
    {
        X += deltaWorldX;
        Y += deltaWorldY;
    }

    public void ClampToMap(int mapWidth, int mapHeight, int baseTileSize, float viewWidth, float viewHeight)
    {
        var worldWidth = mapWidth * baseTileSize;
        var worldHeight = mapHeight * baseTileSize;
        var visibleWidth = viewWidth / Zoom;
        var visibleHeight = viewHeight / Zoom;

        if (visibleWidth >= worldWidth)
        {
            X = (worldWidth - visibleWidth) * 0.5f;
        }
        else
        {
            X = Math.Clamp(X, 0f, worldWidth - visibleWidth);
        }

        if (visibleHeight >= worldHeight)
        {
            Y = (worldHeight - visibleHeight) * 0.5f;
        }
        else
        {
            Y = Math.Clamp(Y, 0f, worldHeight - visibleHeight);
        }
    }

    public void CenterOnTile(GridPosition tile, int baseTileSize, float viewWidth, float viewHeight)
    {
        var centerWorldX = (tile.X + 0.5f) * baseTileSize;
        var centerWorldY = (tile.Y + 0.5f) * baseTileSize;
        X = centerWorldX - viewWidth / (2f * Zoom);
        Y = centerWorldY - viewHeight / (2f * Zoom);
    }

    public Vector2 ScreenToWorld(float screenX, float screenY, float viewportLeft, float viewportTop) =>
        new(
            X + (screenX - viewportLeft) / Zoom,
            Y + (screenY - viewportTop) / Zoom);

    public Vector2 WorldToScreen(float worldX, float worldY, float viewportLeft, float viewportTop) =>
        new(
            viewportLeft + (worldX - X) * Zoom,
            viewportTop + (worldY - Y) * Zoom);

    public GridPosition? ScreenToCell(
        float screenX,
        float screenY,
        float viewportLeft,
        float viewportTop,
        float viewportRight,
        float viewportBottom,
        int mapWidth,
        int mapHeight,
        int baseTileSize)
    {
        if (screenX < viewportLeft
            || screenX >= viewportRight
            || screenY < viewportTop
            || screenY >= viewportBottom)
        {
            return null;
        }

        var world = ScreenToWorld(screenX, screenY, viewportLeft, viewportTop);
        var cellX = (int)MathF.Floor(world.X / baseTileSize);
        var cellY = (int)MathF.Floor(world.Y / baseTileSize);
        if (cellX < 0 || cellY < 0 || cellX >= mapWidth || cellY >= mapHeight)
        {
            return null;
        }

        return new GridPosition(cellX, cellY);
    }

    public void GetVisibleTileRange(
        float viewWidth,
        float viewHeight,
        int mapWidth,
        int mapHeight,
        int baseTileSize,
        out int minX,
        out int minY,
        out int maxX,
        out int maxY)
    {
        var visibleWidth = viewWidth / Zoom;
        var visibleHeight = viewHeight / Zoom;
        minX = Math.Max(0, (int)MathF.Floor(X / baseTileSize) - 1);
        minY = Math.Max(0, (int)MathF.Floor(Y / baseTileSize) - 1);
        maxX = Math.Min(mapWidth - 1, (int)MathF.Floor((X + visibleWidth) / baseTileSize) + 1);
        maxY = Math.Min(mapHeight - 1, (int)MathF.Floor((Y + visibleHeight) / baseTileSize) + 1);
    }
}
