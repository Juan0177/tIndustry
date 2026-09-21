using System.Text.Json;
using System.Text.Json.Serialization;

namespace TIndustry.Shared;

public sealed record ConveyorDefinition(
    string Id,
    int Tier,
    float RateItemsPerSecond,
    int Capacity,
    float ItemSpacing);

/// <summary>Minimal content.json loader — only conveyors needed for the spike belt.</summary>
public static class ConveyorContent
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private sealed class Root
    {
        public List<ConveyorDefinitionDto> Conveyors { get; set; } = [];
    }

    private sealed class ConveyorDefinitionDto
    {
        public string Id { get; set; } = "";
        public int Tier { get; set; }
        public float RateItemsPerSecond { get; set; }
        public int Capacity { get; set; } = 1;
        public float ItemSpacing { get; set; } = 1f;
    }

    public static IReadOnlyList<ConveyorDefinition> LoadConveyors(string contentJsonPath)
    {
        var json = File.ReadAllText(contentJsonPath);
        var root = JsonSerializer.Deserialize<Root>(json, Options)
            ?? throw new InvalidDataException($"content.json non valido: {contentJsonPath}");

        return root.Conveyors
            .Select(c => new ConveyorDefinition(
                c.Id,
                c.Tier,
                c.RateItemsPerSecond,
                Math.Max(1, c.Capacity),
                c.ItemSpacing <= 0 ? 1f : c.ItemSpacing))
            .ToList();
    }

    public static ConveyorDefinition RequireBelt(string contentJsonPath, string id = "conveyor-basic")
    {
        var belt = LoadConveyors(contentJsonPath).FirstOrDefault(c => c.Id == id)
            ?? throw new InvalidDataException($"Conveyor '{id}' non trovato in {contentJsonPath}");
        return belt;
    }
}
