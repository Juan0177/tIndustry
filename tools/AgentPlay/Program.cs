using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
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
        // Unknown tools stay protocol errors (no matched tool); the others get their arguments checked first.
        if (request.MatchedPrimitive is not McpServerTool tool)
        {
            return await next(request, cancellationToken);
        }

        var name = tool.ProtocolTool.Name;
        var schema = tool.ProtocolTool.InputSchema;
        if (Arguments.Problem(schema, request.Params?.Arguments) is { } problem)
        {
            return BadArguments(problem);
        }

        // A binding failure the check did not foresee (for example a number out of range): the SDK text goes to
        // stderr only, the agent gets the tool's parameters.
        CallToolResult result;
        try
        {
            result = await next(request, cancellationToken);
        }
        catch (Exception ex) when (ex is ArgumentException or System.Text.Json.JsonException)
        {
            Console.Error.WriteLine($"{name}: {ex}");
            return BadArguments(Arguments.Summary(name, schema));
        }

        if (result.IsError == true)
        {
            Console.Error.WriteLine($"{name}: {string.Join(" ", result.Content.OfType<TextContentBlock>().Select(t => t.Text))}");
            return BadArguments(Arguments.Summary(name, schema));
        }

        return result;
    }));

var app = builder.Build();
SimEngine.SweepStale(DateTime.UtcNow);
await app.RunAsync();

static CallToolResult BadArguments(string message) => new() { Content = [new TextContentBlock { Text = Tools.BadArguments(message) }] };
