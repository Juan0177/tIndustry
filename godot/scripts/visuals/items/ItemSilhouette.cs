using Godot;

namespace TIndustry.Godot;

/// <summary>
/// Solid version of an item icon: the game-icons glyphs have see-through facets, so belts showed
/// through the items. The silhouette is the icon's convex hull (cached per texture).
/// </summary>
public static class ItemSilhouette
{
    private static readonly Dictionary<Texture2D, ImageTexture> Cache = [];

    public static Texture2D? Solid(Texture2D? source)
    {
        if (source is null)
        {
            return null;
        }

        if (Cache.TryGetValue(source, out var cached))
        {
            return cached;
        }

        var src = source.GetImage();
        if (src.IsCompressed())
        {
            src.Decompress();
        }

        // Convex-hull fill: closes every crack and notch, even ones open to the edge.
        var w = src.GetWidth();
        var h = src.GetHeight();
        var points = new List<Vector2>();
        for (var y = 0; y < h; y++)
        {
            for (var x = 0; x < w; x++)
            {
                if (src.GetPixel(x, y).A >= 0.5f)
                {
                    points.Add(new Vector2(x + 0.5f, y + 0.5f));
                }
            }
        }

        var hull = points.Count >= 3 ? Geometry2D.ConvexHull(points.ToArray()) : [];
        var solid = Image.CreateEmpty(w, h, false, Image.Format.Rgba8);
        for (var y = 0; y < h; y++)
        {
            for (var x = 0; x < w; x++)
            {
                var inside = hull.Length >= 3
                    && Geometry2D.IsPointInPolygon(new Vector2(x + 0.5f, y + 0.5f), hull);
                solid.SetPixel(x, y, inside ? Colors.White : Colors.Transparent);
            }
        }

        var tex = ImageTexture.CreateFromImage(solid);
        Cache[source] = tex;
        return tex;
    }
}
