namespace TIndustry.Shared;

/// <summary>
/// Power-node stub: relays live generator adjacency within LinkRange so craft
/// machines near a node can receive the +20% powered bonus (no full PowerNetworks graph).
/// </summary>
public sealed class PowerNodeStub
{
    public const string Tier1Id = "power-node";
    public const string Tier2Id = "power-node-t2";

    public PowerNodeStub(GridPosition position, string definitionId = Tier1Id)
    {
        Position = position;
        DefinitionId = definitionId == Tier2Id ? Tier2Id : Tier1Id;
        Size = DefinitionId == Tier2Id ? 2 : 1;
        LinkRange = DefinitionId == Tier2Id ? 10f : 6f;
    }

    public GridPosition Position { get; private set; }
    public string DefinitionId { get; }
    public int Size { get; }
    public float LinkRange { get; }

    public void Relocate(GridPosition position) => Position = position;

    public IEnumerable<GridPosition> OccupiedTiles()
    {
        for (var y = 0; y < Size; y++)
        {
            for (var x = 0; x < Size; x++)
            {
                yield return new GridPosition(Position.X + x, Position.Y + y);
            }
        }
    }

    public bool Occupies(GridPosition tile)
    {
        foreach (var t in OccupiedTiles())
        {
            if (t.Equals(tile))
            {
                return true;
            }
        }

        return false;
    }

    public GridPosition CenterTile =>
        new(Position.X + (Size - 1) / 2, Position.Y + (Size - 1) / 2);

    public bool IsWithinRange(GridPosition otherOrigin, int otherSize)
    {
        var ax = Position.X + (Size - 1) * 0.5f;
        var ay = Position.Y + (Size - 1) * 0.5f;
        var bx = otherOrigin.X + (otherSize - 1) * 0.5f;
        var by = otherOrigin.Y + (otherSize - 1) * 0.5f;
        var dx = ax - bx;
        var dy = ay - by;
        return dx * dx + dy * dy <= LinkRange * LinkRange;
    }

    /// <summary>True when this node sits next to a craft footprint (edge-adjacent).</summary>
    public bool IsAdjacentTo(GridPosition craftOrigin, int craftSize)
    {
        foreach (var nodeTile in OccupiedTiles())
        {
            for (var y = 0; y < craftSize; y++)
            {
                for (var x = 0; x < craftSize; x++)
                {
                    var craftTile = new GridPosition(craftOrigin.X + x, craftOrigin.Y + y);
                    var dx = Math.Abs(nodeTile.X - craftTile.X);
                    var dy = Math.Abs(nodeTile.Y - craftTile.Y);
                    if (dx + dy == 1)
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }
}
