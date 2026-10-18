using System.Text.Json;
using System.Text.Json.Serialization;
using TIndustry.Godot;
using Xunit;

namespace TIndustry.Shared.Tests;

/// <summary>
/// Settings enums read tolerantly (godot/scripts/settings/TolerantEnumConverter.cs, linked file): an unknown name
/// must not throw, or ClientSettings.Load would fall back to defaults and lose every other setting.
/// </summary>
public sealed class TolerantEnumConverterTests
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    // Same shape as ClientSettings (which needs Godot): an enum with a default plus another field.
    private sealed class Sample
    {
        [JsonConverter(typeof(TolerantEnumConverter<EffectsQuality>))]
        public EffectsQuality Effetti { get; set; } = EffectsQuality.Alti;

        public bool TutorialCompleted { get; set; }
    }

    [Theory]
    [InlineData("\"Medi\"")]
    [InlineData("\"alti\"")]
    [InlineData("\"7\"")]
    [InlineData("7")]
    [InlineData("-1")]
    [InlineData("null")]
    [InlineData("[1]")]
    [InlineData("{ \"x\": 1 }")]
    public void UnknownValueKeepsTheOtherFields(string effetti)
    {
        var json = $"{{ \"effetti\": {effetti}, \"tutorialCompleted\": true }}";

        var read = JsonSerializer.Deserialize<Sample>(json, Options)!;

        Assert.True(read.TutorialCompleted);
        Assert.False(Enum.IsDefined(read.Effetti));
        Assert.Equal(TolerantEnumConverter<EffectsQuality>.Unknown, read.Effetti);
    }

    [Theory]
    [InlineData("\"Spenti\"", EffectsQuality.Spenti)]
    [InlineData("\"Bassi\"", EffectsQuality.Bassi)]
    [InlineData("\"Alti\"", EffectsQuality.Alti)]
    [InlineData("1", EffectsQuality.Bassi)]
    public void KnownValuesAreRead(string effetti, EffectsQuality expected)
    {
        var read = JsonSerializer.Deserialize<Sample>($"{{ \"effetti\": {effetti} }}", Options)!;
        Assert.Equal(expected, read.Effetti);
    }

    [Fact]
    public void MissingFieldKeepsTheDefaultAndWritesTheName()
    {
        var read = JsonSerializer.Deserialize<Sample>("{ \"tutorialCompleted\": true }", Options)!;
        Assert.Equal(EffectsQuality.Alti, read.Effetti);

        read.Effetti = EffectsQuality.Bassi;
        Assert.Contains("\"effetti\":\"Bassi\"", JsonSerializer.Serialize(read, Options));
    }
}
