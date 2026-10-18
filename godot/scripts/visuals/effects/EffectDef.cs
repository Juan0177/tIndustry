namespace TIndustry.Godot;

// No Godot types in this file: it is also compiled into the xUnit project (linked file, see EffectRules).

/// <summary>Graphics setting "Effetti": how many particles the client draws (never touches the simulation).</summary>
public enum EffectsQuality
{
    /// <summary>No particles at all, still water.</summary>
    Spenti,
    /// <summary>Fewer particles, no ambient effects.</summary>
    Bassi,
    /// <summary>Everything.</summary>
    Alti
}

/// <summary>
/// One particle effect of <c>godot/effetti/effetti.json</c>: a burst (<c>EffectManager.Play</c>) of
/// <see cref="Amount"/> particles, or a continuous emitter (<c>EffectManager.Attach</c>) keeping about
/// that many alive. Speeds and gravity in world pixels per second, spread in degrees around the emission direction
/// (East, turned by the rotation passed to the manager), colour in 0..1 with alpha. <see cref="Intensity"/> (optional
/// <c>intensita</c>, default 1) multiplies the colour past 1: with the glow on such particles bloom, and at night they
/// still shine through the darkness (fireflies).
/// </summary>
public sealed record EffectDef(
    string Name,
    string Sprite,
    int Amount,
    float Lifetime,
    float SpeedMin,
    float SpeedMax,
    float R,
    float G,
    float B,
    float A,
    float Spread,
    float GravityX,
    float GravityY,
    EffectsQuality MinQuality,
    float Intensity = 1f);
