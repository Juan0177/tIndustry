namespace TIndustry.Shared;

/// <summary>
/// Second half of the power tick: the generators burn their fuel, then every network makes its balance in integers
/// (spec «Rete di corrente», «La rete»):
/// <list type="bullet">
/// <item>produced = the output of its burning generators and of its solar panels (output × the world's light / 1000,
/// floored); requested = the power use of the consumers that asked
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

        // Each burning generator gives its own definition's output.
        var generatorRate = Generator.OutputRateOf(state.Content, Generator.BuildingId);
        var combustionRate = Generator.OutputRateOf(state.Content, Generator.CombustionId);
        foreach (var gen in state.Generators)
        {
            if (gen.Tick(ctx.Dt, state.Belts) && grid.NetworkOf(gen.Id) is { } network)
            {
                produced[network.Index] += gen.DefinitionId == Generator.CombustionId ? combustionRate : generatorRate;
            }
        }

        // Solar panels: their output at full light × this tick's light, floored to the thousandth (0 by night).
        RefreshSolarRates(state);
        foreach (var panel in state.SolarPanels)
        {
            if (grid.NetworkOf(panel.Id) is { } network)
            {
                produced[network.Index] += panel.CurrentRate;
            }
        }

        foreach (var craft in state.CraftMachines())
        {
            var rate = RateOf(state.Content, craft.DefinitionId);
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

        foreach (var miner in state.Miners)
        {
            var minerRate = RateOf(state.Content, miner.DefinitionId);
            miner.PowerRequested = Ask(grid, miner.Id, miner.PowerRequested, minerRate, requested, out _);
        }

        // A lamp without a power use in the content stays dark instead of lighting for free.
        var lampRate = RateOf(state.Content, Lamp.BuildingId);
        foreach (var lamp in state.Lamps)
        {
            lamp.PowerRequested = Ask(grid, lamp.Id, lamp.PowerRequested, lampRate, requested, out _);
        }

        // A powered bridge's rate is its conveyor's power use; a bridge without one (or gone from the content) asks nothing.
        foreach (var bridge in state.BridgePowers)
        {
            var bridgeRate = state.Belts.TryGet(bridge.Entry, out var cell) ? cell.Definition.PowerUseRate : 0;
            bridge.PowerRequested = Ask(grid, bridge.Id, bridge.PowerRequested, bridgeRate, requested, out _);
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

        // The powered bridges read their share now: the belts of the next tick (and the settle pass of this one) ask it.
        foreach (var bridge in state.BridgePowers)
        {
            bridge.SatisfactionBp = bridge.PowerRequested ? state.SatisfactionOf(bridge.Id) : 0;
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

    /// <summary>
    /// Sets every solar panel's <see cref="SolarPanel.CurrentRate"/> from the light of the current tick (a panel gone
    /// from the content makes nothing). Shared by the power balance and by a restore, so a loaded panel is right at once.
    /// </summary>
    internal static void RefreshSolarRates(GameState state)
    {
        var light = DayNightClock.LightAt(state.Rules.DayNight, state.FirstBuildTick, state.CurrentTick);
        var smallRate = SolarPanel.RateAt(OutputRateOf(state.Content, SolarPanel.SmallId), light);
        var largeRate = SolarPanel.RateAt(OutputRateOf(state.Content, SolarPanel.LargeId), light);
        foreach (var panel in state.SolarPanels)
        {
            panel.CurrentRate = panel.DefinitionId == SolarPanel.LargeId ? largeRate : smallRate;
        }
    }

    /// <summary>The power output of <paramref name="buildingId"/> in thousandths of a unit per second (0 when it has none).</summary>
    public static long OutputRateOf(FactoryContent content, string buildingId) =>
        content.FindBuilding(buildingId)?.PowerOutputRate ?? 0;

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
    /// Puts <paramref name="stored"/> back in the batteries of <paramref name="network"/> in proportion to each one's own
    /// capacity (<see cref="PowerNetwork.BatteryCapacities"/>, never an average: a second battery type with another
    /// capacity must not break the sharing); the remainder of the divisions goes one unit each to the batteries in id
    /// order. No battery ends above its capacity: a floored share is below it whenever something is left over.
    /// </summary>
    internal static void ShareCharge(PowerNetwork network, long stored)
    {
        var batteries = network.Batteries;
        var capacities = network.BatteryCapacities;
        if (batteries.Count == 0 || network.Capacity <= 0)
        {
            return;
        }

        stored = Math.Clamp(stored, 0, network.Capacity);
        var given = 0L;
        for (var i = 0; i < batteries.Count; i++)
        {
            var share = (long)((Int128)stored * capacities[i] / network.Capacity);
            batteries[i].Charge = share;
            given += share;
        }

        var left = stored - given;
        for (var i = 0; left > 0 && i < batteries.Count; i++)
        {
            if (batteries[i].Charge < capacities[i])
            {
                batteries[i].Charge++;
                left--;
            }
        }
    }
}
