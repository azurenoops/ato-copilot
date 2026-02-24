using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;
using Azure.Identity;
using Ato.Copilot.Core.Data.Context;
using Ato.Copilot.Core.Extensions;
using Ato.Copilot.Core.Observability;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Text.Json;
using Ato.Copilot.State.Extensions;
using Ato.Copilot.Agents.Extensions;
using Ato.Copilot.Mcp.Extensions;
using Ato.Copilot.Mcp.Middleware;
using Ato.Copilot.Mcp.Server;
using Microsoft.EntityFrameworkCore;

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
    .Destructure.With<SensitiveDataDestructuringPolicy>()
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
        .Destructure.With<SensitiveDataDestructuringPolicy>()
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

            // Azure Key Vault configuration provider (non-Development only, per FR-038)
            if (!ctx.HostingEnvironment.IsDevelopment())
            {
                var builtConfig = config.Build();
                var vaultUri = builtConfig["KeyVault:VaultUri"];
                if (!string.IsNullOrEmpty(vaultUri))
                {
                    config.AddAzureKeyVault(
                        new Uri(vaultUri),
                        new DefaultAzureCredential(new DefaultAzureCredentialOptions
                        {
                            AuthorityHost = AzureAuthorityHosts.AzureGovernment
                        }));
                }
            }
        })
        .ConfigureServices((ctx, services) =>
        {
            RegisterCoreServices(services, ctx.Configuration);
            services.AddMcpStdioService();
        });

    using var host = builder.Build();

    // Auto-migrate database at startup (fail = exit code 1)
    await MigrateDatabaseAsync(host.Services);

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

    // Azure Key Vault configuration provider (non-Development only, per FR-038)
    if (!builder.Environment.IsDevelopment())
    {
        var vaultUri = builder.Configuration["KeyVault:VaultUri"];
        if (!string.IsNullOrEmpty(vaultUri))
        {
            builder.Configuration.AddAzureKeyVault(
                new Uri(vaultUri),
                new DefaultAzureCredential(new DefaultAzureCredentialOptions
                {
                    AuthorityHost = AzureAuthorityHosts.AzureGovernment
                }));
            Log.Information("Azure Key Vault configuration provider added: {VaultUri}", vaultUri);
        }
    }

    // Register services
    RegisterCoreServices(builder.Services, builder.Configuration);

    // Health checks (per FR-045, FR-033)
    builder.Services.AddHealthChecks()
        .AddCheck<AgentHealthCheck>("compliance-agent")
        .AddCheck<Ato.Copilot.Agents.Observability.NistControlsHealthCheck>("nist-controls");

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

    // Auto-migrate database at startup (fail = exit code 1)
    await MigrateDatabaseAsync(app.Services);

    // Configure pipeline — middleware ordering per R-012:
    // 1. Correlation ID (MUST be first — before Serilog request logging)
    // 2. Request logging (Serilog)
    // 3. CORS
    // 4. CAC authentication (JWT validation, amr claim check for CAC/PIV)
    // 5. Authorization (role-based access checks, Tier 2 CAC gate, PIM tier enforcement)
    // 6. Audit logging (captures all requests with user/role/action)
    app.UseMiddleware<CorrelationIdMiddleware>();
    app.UseSerilogRequestLogging();
    app.UseCors();
    app.UseMiddleware<CacAuthenticationMiddleware>();
    app.UseMiddleware<ComplianceAuthorizationMiddleware>();
    app.UseMiddleware<AuditLoggingMiddleware>();

    // Map MCP HTTP endpoints
    var httpBridge = app.Services.GetRequiredService<McpHttpBridge>();
    httpBridge.MapEndpoints(app);

    // Health check endpoint with custom JSON writer (per FR-045 / SC-015)
    app.MapHealthChecks("/health", new HealthCheckOptions
    {
        ResponseWriter = WriteHealthCheckResponseAsync
    });

    // Root endpoint
    app.MapGet("/", () => Results.Json(new
    {
        service = "ATO Copilot MCP Server",
        version = "1.0.0",
        mode = "http",
        tools = "Use MCP protocol to list tools",
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
//  Database Migration
// ────────────────────────────────────────────────────────────────
/// <summary>
/// Applies pending EF Core migrations at startup. Fails fast (exit code 1)
/// if migration cannot complete within 30 seconds.
/// </summary>
async Task MigrateDatabaseAsync(IServiceProvider services)
{
    using var scope = services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AtoCopilotContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<AtoCopilotContext>>();

    try
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        logger.LogInformation("Applying database migrations...");
        await db.Database.MigrateAsync(cts.Token);
        logger.LogInformation("Database migrations applied successfully");
    }
    catch (Exception ex)
    {
        logger.LogCritical(ex, "Database migration failed — shutting down");
        Environment.ExitCode = 1;
        throw;
    }
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

    // Configuration agent + tools
    services.AddConfigurationAgent();

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

// ────────────────────────────────────────────────────────────────
//  Health Check Response Writer (per FR-045 / SC-015)
// ────────────────────────────────────────────────────────────────
/// <summary>
/// Custom JSON response writer for the /health endpoint.
/// Output format: { "status": "Healthy|Degraded|Unhealthy",
///   "agents": [{ "name": "...", "status": "...", "description": "..." }],
///   "totalDurationMs": 45 }
/// </summary>
async Task WriteHealthCheckResponseAsync(HttpContext context, HealthReport report)
{
    context.Response.ContentType = "application/json; charset=utf-8";

    var agents = report.Entries.Select(e => new
    {
        name = e.Key,
        status = e.Value.Status.ToString(),
        description = e.Value.Description ?? string.Empty
    });

    var response = new
    {
        status = report.Status.ToString(),
        agents,
        totalDurationMs = report.TotalDuration.TotalMilliseconds
    };

    var json = JsonSerializer.Serialize(response, new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    });

    await context.Response.WriteAsync(json);
}
