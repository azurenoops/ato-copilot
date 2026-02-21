using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;
using Ato.Copilot.Core.Extensions;
using Ato.Copilot.State.Extensions;
using Ato.Copilot.Agents.Extensions;
using Ato.Copilot.Mcp.Extensions;
using Ato.Copilot.Mcp.Server;

// ────────────────────────────────────────────────────────────────
//  ATO Copilot — Compliance-Only MCP Server
//  Supports dual-mode: stdio (GitHub Copilot / Claude) and HTTP
// ────────────────────────────────────────────────────────────────

var mode = DetermineRunMode(args);

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .MinimumLevel.Override("System", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Application", "ATO Copilot")
    .WriteTo.File(
        path: "logs/ato-copilot-.log",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 14,
        outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}")
    .CreateLogger();

if (mode != "stdio")
{
    Log.Logger = new LoggerConfiguration()
        .MinimumLevel.Information()
        .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
        .Enrich.FromLogContext()
        .Enrich.WithProperty("Application", "ATO Copilot")
        .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
        .WriteTo.File(
            path: "logs/ato-copilot-.log",
            rollingInterval: RollingInterval.Day,
            retainedFileCountLimit: 14)
        .CreateLogger();
}

try
{
    Log.Information("ATO Copilot starting in {Mode} mode", mode);

    if (mode == "stdio")
        await RunStdioModeAsync(args);
    else
        await RunHttpModeAsync(args);
}
catch (Exception ex)
{
    Log.Fatal(ex, "ATO Copilot terminated unexpectedly");
    Environment.ExitCode = 1;
}
finally
{
    Log.CloseAndFlush();
}

// ────────────────────────────────────────────────────────────────
//  Stdio Mode — for GitHub Copilot, Claude Desktop, etc.
// ────────────────────────────────────────────────────────────────
async Task RunStdioModeAsync(string[] args)
{
    var builder = Host.CreateDefaultBuilder(args)
        .UseSerilog()
        .ConfigureAppConfiguration((ctx, config) =>
        {
            config.SetBasePath(AppContext.BaseDirectory);
            config.AddJsonFile("appsettings.json", optional: true);
            config.AddJsonFile($"appsettings.{ctx.HostingEnvironment.EnvironmentName}.json", optional: true);
            config.AddEnvironmentVariables("ATO_");
        })
        .ConfigureServices((ctx, services) =>
        {
            RegisterCoreServices(services, ctx.Configuration);
            services.AddMcpStdioService();
        });

    using var host = builder.Build();
    await host.RunAsync();
}

// ────────────────────────────────────────────────────────────────
//  HTTP Mode — REST API for web apps, dashboards, etc.
// ────────────────────────────────────────────────────────────────
async Task RunHttpModeAsync(string[] args)
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog();

    builder.Configuration
        .SetBasePath(AppContext.BaseDirectory)
        .AddJsonFile("appsettings.json", optional: true)
        .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true)
        .AddEnvironmentVariables("ATO_");

    // Register services
    RegisterCoreServices(builder.Services, builder.Configuration);

    // Add HTTP-specific services
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddCors(options =>
    {
        options.AddDefaultPolicy(policy =>
        {
            var origins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
                ?? new[] { "http://localhost:3000", "http://localhost:5173" };
            policy.WithOrigins(origins)
                .AllowAnyHeader()
                .AllowAnyMethod();
        });
    });

    var app = builder.Build();

    // Configure pipeline
    app.UseSerilogRequestLogging();
    app.UseCors();

    // Map MCP HTTP endpoints
    var httpBridge = app.Services.GetRequiredService<McpHttpBridge>();
    httpBridge.MapEndpoints(app);

    // Root endpoint
    app.MapGet("/", () => Results.Json(new
    {
        service = "ATO Copilot",
        version = "1.0.0",
        mode = "http",
        endpoints = new
        {
            chat = "POST /mcp/chat",
            mcp = "POST /mcp",
            tools = "GET /mcp/tools",
            health = "GET /health"
        }
    }));

    var port = builder.Configuration.GetValue("Server:Port", 3001);
    var urls = builder.Configuration.GetValue("Server:Urls", $"http://0.0.0.0:{port}");
    app.Urls.Add(urls!);

    Log.Information("ATO Copilot HTTP server listening on {Urls}", urls);

    await app.RunAsync();
}

// ────────────────────────────────────────────────────────────────
//  Service Registration (shared between modes)
// ────────────────────────────────────────────────────────────────
void RegisterCoreServices(IServiceCollection services, IConfiguration configuration)
{
    // Core infrastructure
    services.AddAtoCopilotCore(configuration);

    // State management
    services.AddInMemoryStateManagement();

    // Compliance agent + tools
    services.AddComplianceAgent(configuration);

    // MCP server
    services.AddMcpServer(configuration);
}

// ────────────────────────────────────────────────────────────────
//  Mode Detection
// ────────────────────────────────────────────────────────────────
string DetermineRunMode(string[] args)
{
    // Check command line: --stdio or --http
    if (args.Contains("--stdio")) return "stdio";
    if (args.Contains("--http")) return "http";

    // Check environment variable
    var envMode = Environment.GetEnvironmentVariable("ATO_RUN_MODE");
    if (!string.IsNullOrEmpty(envMode)) return envMode.ToLowerInvariant();

    // Default: if stdin is not a terminal → stdio, otherwise HTTP
    if (!Console.IsInputRedirected) return "http";
    return "stdio";
}
