namespace TIndustry.Shared;

/// <summary>
/// The Core's storage: the single way items enter the Core stock (<see cref="EconomyWallet"/> materials).
/// Each item has a maximum (<see cref="Capacity"/>): the tier's <see cref="CoreTierDefinition.ItemCapacity"/>
/// plus the space of linked storage blocks. An item at its maximum does not enter: the caller keeps it (a belt
/// holds it on its last cell, a building in its output buffer) and the line stops. Money has no maximum.
/// All counts are integers; nothing here iterates a hash collection.
/// </summary>
public sealed class CoreStorage
{
    private readonly GameState state;

    public CoreStorage(GameState state)
    {
        this.state = state ?? throw new ArgumentNullException(nameof(state));
    }

    /// <summary>Space added to every item's maximum by the storage blocks touching the Core.</summary>
    public int LinkedCapacity { get; private set; }

    /// <summary>Maximum stock of <paramref name="itemId"/>: the Core tier's capacity plus <see cref="LinkedCapacity"/>.</summary>
    public int Capacity(string itemId) => state.Content.CoreTiers[state.CoreTier].ItemCapacity + LinkedCapacity;

    /// <summary>True when the stock of <paramref name="itemId"/> is at (or above) its maximum.</summary>
    public bool IsFull(string itemId) => state.Wallet.MaterialCount(itemId) >= Capacity(itemId);

    /// <summary>Adds one <paramref name="itemId"/> when it is below its maximum; false (nothing changes) when full.</summary>
    public bool TryAccept(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId) || IsFull(itemId))
        {
            return false;
        }

        state.Wallet.AddMaterial(itemId, 1);
        return true;
    }

    /// <summary>
    /// Refunds: adds up to <paramref name="amount"/> of <paramref name="itemId"/> without going over the maximum and
    /// returns how many entered; the rest is lost, as in Mindustry. Stock already above the maximum is kept.
    /// </summary>
    public int AddClamped(string itemId, int amount)
    {
        if (amount <= 0 || string.IsNullOrWhiteSpace(itemId))
        {
            return 0;
        }

        var room = Math.Max(0, Capacity(itemId) - state.Wallet.MaterialCount(itemId));
        var added = Math.Min(amount, room);
        state.Wallet.AddMaterial(itemId, added);
        return added;
    }

    /// <summary>
    /// Puts back pieces taken from the Core and not delivered (an extractor whose belt is blocked, a payment taken
    /// back): ignores the maximum, so nothing is ever lost or duplicated.
    /// </summary>
    public void Return(string itemId, int amount) => state.Wallet.AddMaterial(itemId, amount);

    /// <summary>Takes one <paramref name="itemId"/> out of the Core stock; false when there is none.</summary>
    internal bool TryTake(string itemId) => state.Wallet.TryRemoveMaterial(itemId, 1);

    /// <summary>
    /// Recomputes which storage blocks are linked (at least one cell of their perimeter is Core) and
    /// <see cref="LinkedCapacity"/> (sum of the linked blocks' capacity). A block that has just become linked pours
    /// its contents into the Core with <see cref="AddClamped"/> (the rest is lost) and is emptied; pouring happens
    /// after the new maximum is known. Blocks in ascending id order, contents in ordinal item order. Called after
    /// every finished placement or removal of a storage block, after <see cref="GameState.SetCore"/> and on restore.
    /// </summary>
    public void RecomputeLinks()
    {
        var linkedCapacity = 0;
        List<StorageBlock>? newlyLinked = null;
        foreach (var storage in state.Storages)
        {
            var linked = TouchesCore(storage);
            if (linked && !storage.IsLinked)
            {
                (newlyLinked ??= []).Add(storage);
            }

            storage.IsLinked = linked;
            if (linked)
            {
                linkedCapacity += storage.Capacity;
            }
        }

        LinkedCapacity = linkedCapacity;
        if (newlyLinked is null)
        {
            return;
        }

        foreach (var storage in newlyLinked)
        {
            foreach (var (itemId, count) in storage.ContentsInOrder())
            {
                AddClamped(itemId, count);
            }

            storage.Clear();
        }
    }

    private bool TouchesCore(StorageBlock storage)
    {
        foreach (var (neighbor, _) in BuildingIo.PerimeterSlots(storage.Origin, storage.Size))
        {
            if (state.CoreTiles.Contains(neighbor))
            {
                return true;
            }
        }

        return false;
    }
}
