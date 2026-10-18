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

    public async Task InitializeAsync()
    {
        Client = await McpClient.CreateAsync(new StdioClientTransport(new StdioClientTransportOptions
        {
            Name = "tindustry",
            Command = "dotnet",
            Arguments = [ServerDll()],
            // The server's session folders land under this test's own temp folder.
            EnvironmentVariables = new Dictionary<string, string?> { ["TMP"] = TempDirectory, ["TEMP"] = TempDirectory, ["TMPDIR"] = TempDirectory }
        }));
    }

    public async Task DisposeAsync()
    {
        await Client.DisposeAsync();
        Directory.Delete(TempDirectory, recursive: true);
    }

    /// <summary>Calls a tool and parses its JSON text answer.</summary>
    public async Task<JsonElement> Call(string tool, object? args = null)
    {
        var arguments = args is null
            ? null
            : JsonSerializer.SerializeToElement(args).EnumerateObject().ToDictionary(p => p.Name, p => (object?)p.Value);
        var result = await Client.CallToolAsync(tool, arguments);
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

    // tests/TIndustry.AgentPlay.Tests/bin/<config>/net8.0/ -> tools/AgentPlay/bin/<config>/net8.0/AgentPlay.dll
    private static string ServerDll()
    {
        var output = new DirectoryInfo(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar));
        var configuration = output.Parent!.Name;
        var root = output.Parent.Parent!.Parent!.Parent!.Parent!.FullName;
        var dll = Path.Combine(root, "tools", "AgentPlay", "bin", configuration, output.Name, "AgentPlay.dll");
        return File.Exists(dll) ? dll : throw new FileNotFoundException("Server AgentPlay non compilato.", dll);
    }
}
