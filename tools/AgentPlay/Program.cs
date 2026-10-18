using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Protocol;
using TIndustry.AgentPlay;

// MCP server over stdio: stdout is the protocol channel, so every log line goes to stderr.
var builder = Host.CreateApplicationBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddConsole(o => o.LogToStandardErrorThreshold = LogLevel.Trace);
builder.Logging.SetMinimumLevel(LogLevel.Warning);

builder.Services.AddSingleton(_ => GameData.Load(Path.Combine(AppContext.BaseDirectory, "data")));
builder.Services.AddSingleton<Sessions>();
builder.Services
    .AddMcpServer(o => o.ServerInfo = new Implementation { Name = "tindustry", Version = "1.0" })
    .WithStdioServerTransport()
    .WithTools<Tools>()
    .WithRequestFilters(filters => filters.AddCallToolFilter(next => async (request, cancellationToken) =>
    {
        // Arguments that do not bind (wrong type, missing) come back as an SDK error: answer like the engine does.
        CallToolResult result;
        try
        {
            result = await next(request, cancellationToken);
        }
        catch (Exception ex) when (ex is ArgumentException or System.Text.Json.JsonException)
        {
            return BadArguments(ex.Message);
        }

        return result.IsError == true ? BadArguments(result.Content.OfType<TextContentBlock>().FirstOrDefault()?.Text) : result;
    }));

await builder.Build().RunAsync();

static CallToolResult BadArguments(string? detail) => new() { Content = [new TextContentBlock { Text = Tools.BadArguments(detail) }] };
