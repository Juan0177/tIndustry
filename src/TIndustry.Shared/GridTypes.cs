namespace TIndustry.Shared;

/// <summary>Mirror of Logistics Direction — kept independent so Raylib main stays untouched.</summary>
public enum Direction
{
    North,
    East,
    South,
    West
}

public static class DirectionMath
{
    public static Direction Opposite(Direction direction) => direction switch
    {
        Direction.North => Direction.South,
        Direction.East => Direction.West,
        Direction.South => Direction.North,
        Direction.West => Direction.East,
        _ => direction
    };

    public static (int Dx, int Dy) ToOffset(Direction direction) => direction switch
    {
        Direction.North => (0, -1),
        Direction.East => (1, 0),
        Direction.South => (0, 1),
        Direction.West => (-1, 0),
        _ => (0, 0)
    };
}

public readonly record struct GridPosition(int X, int Y)
{
    public GridPosition Step(Direction direction)
    {
        var (dx, dy) = DirectionMath.ToOffset(direction);
        return new GridPosition(X + dx, Y + dy);
    }
}
