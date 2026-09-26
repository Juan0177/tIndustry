namespace TIndustry.Shared;

/// <summary>
/// Mindustry-style building I/O helpers (Raylib <c>BuildingIo</c> port).
/// Perimeter slots drive adjacent building→building item transfer.
/// </summary>
public static class BuildingIo
{
    public static bool Occupies(GridPosition origin, int size, GridPosition tile) =>
        tile.X >= origin.X && tile.X < origin.X + size
        && tile.Y >= origin.Y && tile.Y < origin.Y + size;

    /// <summary>
    /// Perimeter neighbor tiles (N/E/S/W × size) with the outward travel direction.
    /// </summary>
    public static IEnumerable<(GridPosition Position, Direction TravelOut)> PerimeterSlots(
        GridPosition origin,
        int size)
    {
        var count = size * 4;
        for (var index = 0; index < count; index++)
        {
            var edge = DirectionMath.All[index / size];
            var offset = index % size;
            var position = edge switch
            {
                Direction.North => new GridPosition(origin.X + offset, origin.Y - 1),
                Direction.East => new GridPosition(origin.X + size, origin.Y + offset),
                Direction.South => new GridPosition(origin.X + offset, origin.Y + size),
                Direction.West => new GridPosition(origin.X - 1, origin.Y + offset),
                _ => origin
            };
            yield return (position, edge);
        }
    }

    /// <summary>True when two axis-aligned footprints share a 4-connected edge.</summary>
    public static bool FootprintsAdjacent(GridPosition a, int sizeA, GridPosition b, int sizeB) =>
        GeneratorStub.FootprintsAdjacent(a, sizeA, b, sizeB);
}
