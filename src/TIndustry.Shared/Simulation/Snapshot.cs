using System.Text.Json;
using System.Text.Json.Serialization;

namespace TIndustry.Shared;

/// <summary>
/// Canonical snapshot of a <see cref="GameState"/>: compact UTF-8 JSON built on the save DTO
/// (buildings in id order, belt cells by (Y, X), keyed collections by ordinal key), with no
/// metadata that varies between runs. Equal states give equal bytes, so the FNV-1a 64
/// <see cref="Checksum"/> can detect co-op desyncs.
/// </summary>
public static class Snapshot
{
    private const ulong FnvOffset = 14695981039346656037UL;
    private const ulong FnvPrime = 1099511628211UL;

    // System.Text.Json writes numbers with the invariant culture; compact output keeps bytes minimal.
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public static byte[] Write(GameState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        return JsonSerializer.SerializeToUtf8Bytes(new Factory(state).Capture(), Options);
    }

    /// <summary>
    /// State of a snapshot of version <see cref="FactorySaveData.MinReadableVersion"/> to
    /// <see cref="FactorySaveData.CurrentVersion"/>; <paramref name="mapsDirectory"/> is needed by campaign maps.
    /// </summary>
    /// <exception cref="SaveMapException">The terrain cannot be rebuilt (code, argument and Italian detail).</exception>
    public static GameState Read(FactoryContent content, byte[] data, string? mapsDirectory = null)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(data);
        var dto = JsonSerializer.Deserialize<FactorySaveData>(data, Options)
            ?? throw new InvalidDataException("Istantanea vuota o non valida.");
        if (dto.Version < FactorySaveData.MinReadableVersion || dto.Version > FactorySaveData.CurrentVersion)
        {
            throw new InvalidDataException($"Versione istantanea non supportata: {dto.Version}");
        }

        return Factory.Restore(content, dto, mapsDirectory).State;
    }

    /// <summary>FNV-1a 64 bit over the snapshot bytes.</summary>
    public static ulong Checksum(byte[] snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var hash = FnvOffset;
        foreach (var b in snapshot)
        {
            hash ^= b;
            hash *= FnvPrime;
        }

        return hash;
    }
}
