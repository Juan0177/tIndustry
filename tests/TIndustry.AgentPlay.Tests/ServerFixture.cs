using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using Xunit;

namespace TIndustry.AgentPlay.Tests;

/// <summary>One AgentPlay server started as a process over stdio, as Claude Code starts it, with its own temp folder.</summary>
public sealed class ServerFixture : IAsyncLifetime
{
    public string TempDirectory { get; } = Directory.CreateTempSubdirectory("tindustry-agentplay-tests-").FullName;

    public McpClient Client { get; private set; } = null!;

    public async Task InitializeAsync() => Client = await Start(TempDirectory);

    /// <summary>A new server process whose temp folder (session folders included) is <paramref name="tempDirectory"/>.</summary>
    public static Task<McpClient> Start(string tempDirectory, Dictionary<string, string?>? environment = null)
    {
        var variables = TempEnvironment(tempDirectory);
        foreach (var (key, value) in environment ?? [])
        {
            variables[key] = value;
        }

        return McpClient.CreateAsync(new StdioClientTransport(new StdioClientTransportOptions
        {
            Name = "tindustry",
            Command = "dotnet",
            Arguments = [ServerDll()],
            EnvironmentVariables = variables
        }));
    }

    public static Dictionary<string, string?> TempEnvironment(string tempDirectory) =>
        new() { ["TMP"] = tempDirectory, ["TEMP"] = tempDirectory, ["TMPDIR"] = tempDirectory };

    public async Task DisposeAsync()
    {
        await Client.DisposeAsync();
        Directory.Delete(TempDirectory, recursive: true);
    }

    /// <summary>Calls a tool and gives its whole result (text and image contents).</summary>
    public async Task<CallToolResult> CallResult(string tool, object? args = null)
    {
        var arguments = args is null
            ? null
            : JsonSerializer.SerializeToElement(args).EnumerateObject().ToDictionary(p => p.Name, p => (object?)p.Value);
        return await Client.CallToolAsync(tool, arguments);
    }

    /// <summary>A count of a compact answer, where 0 is left out.</summary>
    public static int Count(JsonElement answer, string name) => answer.TryGetProperty(name, out var n) ? n.GetInt32() : 0;

    /// <summary>Calls a tool and parses its JSON text answer.</summary>
    public async Task<JsonElement> Call(string tool, object? args = null)
    {
        var result = await CallResult(tool, args);
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.True(text.StartsWith('{'), $"{tool}: {text}");
        return JsonDocument.Parse(text).RootElement.Clone();
    }

    public async Task<JsonElement> Ok(string tool, object? args = null)
    {
        var answer = await Call(tool, args);
        Assert.True(answer.GetProperty("ok").GetBoolean(), $"{tool}: {answer}");
        return answer;
    }

    public async Task<string> Refused(string tool, object? args = null)
    {
        var answer = await Call(tool, args);
        Assert.False(answer.GetProperty("ok").GetBoolean(), $"{tool}: {answer}");
        Assert.False(string.IsNullOrEmpty(answer.GetProperty("messaggio").GetString()));
        return answer.GetProperty("codice").GetString()!;
    }

    private static DirectoryInfo Output => new(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar));

    // tests/TIndustry.AgentPlay.Tests/bin/<config>/net8.0/
    public static string RepoRoot => Output.Parent!.Parent!.Parent!.Parent!.Parent!.FullName;

    // -> tools/AgentPlay/bin/<config>/net8.0/AgentPlay.dll
    public static string ServerDll()
    {
        var dll = Path.Combine(RepoRoot, "tools", "AgentPlay", "bin", Output.Parent!.Name, Output.Name, "AgentPlay.dll");
        return File.Exists(dll) ? dll : throw new FileNotFoundException("Server AgentPlay non compilato.", dll);
    }
}
