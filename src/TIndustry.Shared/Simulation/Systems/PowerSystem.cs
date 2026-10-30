namespace TIndustry.Shared;

/// <summary>
/// Second half of the power tick: the generators burn their coal, then every network makes its balance in integers
/// (spec «Rete di corrente», «La rete»):
/// <list type="bullet">
/// <item>produced = the output of its burning generators; requested = the power use of the consumers that asked
/// (<see cref="PowerDemandSystem"/>); both in thousandths of a unit per second, and one tick of a rate is exactly that
/// many sixty-thousandths of a unit of battery charge;</item>
/// <item>produced ≥ requested: satisfaction 10 000 and the surplus goes to the batteries up to their capacity;</item>
/// <item>else the batteries give what is missing while they have charge, and if that is still short the satisfaction
/// is floor((produced + taken from the batteries) × 10 000 / requested), the same for every machine;</item>
/// <item>a forno with coal whose share would leave it below its coal speed (1.2 × s &lt; 1) burns coal instead and its
/// request is taken out; the network's balance is then made again without it (once, deterministically);</item>
/// <item>the network's charge is shared by its batteries in proportion to their capacity (remainder one each, by id).</item>
/// </list>
/// </summary>
public sealed class PowerSystem : ISimulationSystem
{
    public const int FullBp = 10_000;

    public string Name => "Power";

    public void Run(GameState state, SystemContext ctx)
    {
        var grid = state.PowerGrid;
        var networks = grid.Networks;
        var count = networks.Count;
        var produced = new long[count];
        var requested = new long[count];
        var coalFornoRequest = new long[count];

        var outputRate = Generator.OutputRateOf(state.Content);
        foreach (var gen in state.Generators)
        {
            if (gen.Tick(ctx.Dt, state.Belts) && grid.NetworkOf(gen.Id) is { } network)
            {
                produced[network.Index] += outputRate;
            }
        }

        var smelterRate = RateOf(state.Content, Crafter.SmelterBuildingId);
        var assemblerRate = RateOf(state.Content, Crafter.AssemblerBuildingId);
        foreach (var craft in state.CraftMachines())
        {
            var rate = craft.IsAssembler ? assemblerRate : smelterRate;
            if (!Ask(grid, craft.Id, craft.PowerRequested, rate, requested, out var index))
            {
                craft.PowerRequested = false;
                continue;
            }

            if (craft.HasCoal)
            {
                coalFornoRequest[index] += rate;
            }
        }

        var minerRate = RateOf(state.Content, Miner.AdvancedId);
        foreach (var miner in state.Miners)
        {
            miner.PowerRequested = Ask(grid, miner.Id, miner.PowerRequested, minerRate, requested, out _);
        }

        // A lamp without a power use in the content stays dark instead of lighting for free.
        var lampRate = RateOf(state.Content, Lamp.BuildingId);
        foreach (var lamp in state.Lamps)
        {
            lamp.PowerRequested = Ask(grid, lamp.Id, lamp.PowerRequested, lampRate, requested, out _);
        }

        // Fornos with coal step out where the share is below their coal speed, then the balance is made again.
        var coalNetworks = new bool[count];
        for (var i = 0; i < count; i++)
        {
            if (coalFornoRequest[i] > 0)
            {
                var (bp, _) = Balance(produced[i], requested[i], networks[i].Stored, networks[i].Capacity);
                if (12L * bp < 10L * FullBp) // 1.2 × s < 1
                {
                    coalNetworks[i] = true;
                    requested[i] -= coalFornoRequest[i];
                }
            }
        }

        foreach (var craft in state.CraftMachines())
        {
            if (craft.PowerRequested && craft.HasCoal && coalNetworks[grid.NetworkOf(craft.Id)!.Index])
            {
                craft.PowerRequested = false;
            }
        }

        for (var i = 0; i < count; i++)
        {
            var network = networks[i];
            var (bp, stored) = Balance(produced[i], requested[i], network.Stored, network.Capacity);
            network.ProducedRate = produced[i];
            network.RequestedRate = requested[i];
            network.SatisfactionBp = bp;
            ShareCharge(network, stored);
        }
    }

    /// <summary>
    /// Satisfaction (basis points) and the batteries' charge after one tick with <paramref name="produced"/> and
    /// <paramref name="requested"/> (thousandths per second = sixty-thousandths per tick).
    /// </summary>
    public static (int SatisfactionBp, long Stored) Balance(long produced, long requested, long stored, long capacity)
    {
        if (produced >= requested)
        {
            return (FullBp, Math.Min(capacity, stored + (produced - requested)));
        }

        var missing = requested - produced;
        if (stored >= missing)
        {
            return (FullBp, stored - missing);
        }

        var served = produced + stored;
        return ((int)Math.Min((long)FullBp, (long)((Int128)served * FullBp / requested)), 0);
    }

    /// <summary>The power use of <paramref name="buildingId"/> in thousandths of a unit per second (0 when it has none).</summary>
    public static long RateOf(FactoryContent content, string buildingId) =>
        content.FindBuilding(buildingId)?.PowerUseRate ?? 0;

    private static bool Ask(PowerGrid grid, long id, bool wants, long rate, long[] requested, out int index)
    {
        index = -1;
        if (!wants || rate <= 0 || grid.NetworkOf(id) is not { } network)
        {
            return false;
        }

        index = network.Index;
        requested[index] += rate;
        return true;
    }

    /// <summary>
    /// Puts <paramref name="stored"/> back in the batteries of <paramref name="network"/> in proportion to their
    /// capacity; the remainder of the division goes one unit each to the batteries in id order.
    /// </summary>
    private static void ShareCharge(PowerNetwork network, long stored)
    {
        var batteries = network.Batteries;
        if (batteries.Count == 0 || network.Capacity <= 0)
        {
            return;
        }

        var each = network.Capacity / batteries.Count;
        var given = 0L;
        foreach (var battery in batteries)
        {
            var share = (long)((Int128)stored * each / network.Capacity);
            battery.Charge = share;
            given += share;
        }

        var left = stored - given;
        for (var i = 0; left > 0 && i < batteries.Count; i++, left--)
        {
            batteries[i].Charge++;
        }
    }
}
