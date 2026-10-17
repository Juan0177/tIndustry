using TIndustry.Shared;
using Xunit;

namespace TIndustry.Shared.Tests;

/// <summary>The majority deposit under a footprint never depends on scan or insertion order.</summary>
public sealed class TerrainMajorityTests
{
    private static TerrainMap Map(DepositKind[,] deposits)
    {
        var tiles = new TerrainTile[deposits.GetLength(0), deposits.GetLength(1)];
        for (var x = 0; x < tiles.GetLength(0); x++)
        {
            for (var y = 0; y < tiles.GetLength(1); y++)
            {
                tiles[x, y] = new TerrainTile(TerrainKind.Stone, deposits[x, y]);
            }
        }

        return TerrainMap.FromTiles(tiles);
    }

    [Fact]
    public void TieGoesToLowestDepositIdWhateverTheOrder()
    {
        // 2x2 with 2 iron + 2 copper; iron is met first in one map, copper first in the other.
        var ironFirst = Map(new[,]
        {
            { DepositKind.Iron, DepositKind.Copper },
            { DepositKind.Iron, DepositKind.Copper }
        });
        var copperFirst = Map(new[,]
        {
            { DepositKind.Copper, DepositKind.Iron },
            { DepositKind.Copper, DepositKind.Iron }
        });

        Assert.Equal(DepositKind.Copper, ironFirst.MajorityDeposit(new GridPosition(0, 0), 2));
        Assert.Equal(DepositKind.Copper, copperFirst.MajorityDeposit(new GridPosition(0, 0), 2));
    }

    [Fact]
    public void ThreeWayTieAndClearMajority()
    {
        var map = Map(new[,]
        {
            { DepositKind.Titanium, DepositKind.Lead, DepositKind.Coal },
            { DepositKind.None, DepositKind.None, DepositKind.None },
            { DepositKind.None, DepositKind.None, DepositKind.None }
        });
        // x is the first index: tiles (0,0) titanium, (1,0) lead, (2,0) coal -> 1 each.
        Assert.Equal(DepositKind.Coal, map.MajorityDeposit(new GridPosition(0, 0), 3));

        var clear = Map(new[,]
        {
            { DepositKind.Iron, DepositKind.Coal },
            { DepositKind.Coal, DepositKind.Coal }
        });
        Assert.Equal(DepositKind.Coal, clear.MajorityDeposit(new GridPosition(0, 0), 2));
        Assert.Equal(DepositKind.None, Map(new DepositKind[2, 2]).MajorityDeposit(new GridPosition(0, 0), 2));
    }
}
