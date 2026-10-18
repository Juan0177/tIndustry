using System.Text.Json;
using System.Text.Json.Serialization;

namespace TIndustry.Shared;

/// <summary>One request of the agents' channel: <c>{id, metodo, parametri}</c> on one line.</summary>
public sealed record AgentRequest(
    long Id,
    [property: JsonPropertyName("metodo")] string Method,
    [property: JsonPropertyName("parametri")] JsonElement Params);

/// <summary>
/// One response: <c>{id, ok, risultato}</c> or <c>{id, ok:false, codice, messaggio}</c> on one line. The codes are the
/// game's (<see cref="RefusalCode"/>, <see cref="SaveLoadError"/>) or the channel's (<see cref="AgentCodes"/>).
/// </summary>
public sealed record AgentResponse(
    long Id,
    bool Ok,
    [property: JsonPropertyName("risultato")] JsonElement? Result,
    [property: JsonPropertyName("codice")] string? Code,
    [property: JsonPropertyName("messaggio")] string? Message)
{
    public static AgentResponse Success(long id, JsonElement result) => new(id, true, result, null, null);

    public static AgentResponse Failure(long id, AgentException error) => new(id, false, null, error.Code, error.Message);
}

/// <summary>Error codes of the channel itself (the game's refusals use their <see cref="RefusalCode"/> name).</summary>
public static class AgentCodes
{
    public const string SessioneSconosciuta = nameof(SessioneSconosciuta);
    public const string MotoreNonDisponibile = nameof(MotoreNonDisponibile);
    public const string Timeout = nameof(Timeout);
    public const string ParametriNonValidi = nameof(ParametriNonValidi);
    public const string GiocoTerminato = nameof(GiocoTerminato);

    /// <summary>An unexpected exception inside the engine: the message says which; the session stays usable.</summary>
    public const string ErroreInterno = nameof(ErroreInterno);
}

/// <summary>A request that ends with <c>ok:false</c>: <see cref="Code"/> is what agents read, the message is for people.</summary>
public sealed class AgentException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;

    /// <summary>For <see cref="AgentCodes.ErroreInterno"/>: the full exception text (type, message, stack) for logs, never sent to the agent.</summary>
    public string? Detail { get; init; }
}

/// <summary>JSON lines of the channel (same format for the game and, later, co-op).</summary>
public static class AgentProtocol
{
    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    internal static readonly JsonElement EmptyObject = JsonDocument.Parse("{}").RootElement.Clone();

    /// <summary>Parses one request line; a missing <c>parametri</c> becomes <c>{}</c>.</summary>
    public static AgentRequest ReadLine(string line)
    {
        AgentRequest? request;
        try
        {
            request = JsonSerializer.Deserialize<AgentRequest>(line, JsonOptions);
        }
        catch (JsonException ex)
        {
            throw new AgentException(AgentCodes.ParametriNonValidi, $"Richiesta non valida: {ex.Message}");
        }

        if (request is null || string.IsNullOrWhiteSpace(request.Method))
        {
            throw new AgentException(AgentCodes.ParametriNonValidi, "Richiesta senza metodo.");
        }

        return request.Params.ValueKind == JsonValueKind.Undefined ? request with { Params = EmptyObject } : request;
    }

    public static string WriteLine(AgentResponse response) => JsonSerializer.Serialize(response, JsonOptions);

    public static string WriteLine(AgentRequest request) => JsonSerializer.Serialize(request, JsonOptions);
}
