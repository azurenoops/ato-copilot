using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Ato.Copilot.Agents.Common;
using Ato.Copilot.Mcp.Models;
using System.Text.Json;

namespace Ato.Copilot.Mcp.Server;

/// <summary>
/// HTTP bridge for MCP protocol — exposes compliance tools as REST endpoints
/// </summary>
public class McpHttpBridge
{
    private readonly McpServer _mcpServer;
    private readonly IEnumerable<BaseTool> _tools;
    private readonly ILogger<McpHttpBridge> _logger;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly JsonSerializerOptions _sseJsonOptions;

    /// <summary>Initializes a new instance of the <see cref="McpHttpBridge"/> class.</summary>
    /// <param name="mcpServer">The MCP server to delegate requests to.</param>
    /// <param name="tools">All registered BaseTool instances for dynamic tool listing.</param>
    /// <param name="logger">Logger instance.</param>
    public McpHttpBridge(McpServer mcpServer, IEnumerable<BaseTool> tools, ILogger<McpHttpBridge> logger)
    {
        _mcpServer = mcpServer;
        _tools = tools;
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };
        // SSE events MUST be single-line — no indentation
        _sseJsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };
    }

    /// <summary>
    /// Map all MCP HTTP endpoints
    /// </summary>
    public void MapEndpoints(WebApplication app)
    {
        // Cast to Delegate so minimal API correctly handles Task<IResult> return
        // Without the cast, the framework treats these as RequestDelegate and discards the IResult (ASP0016)

        // MCP JSON-RPC endpoint
        app.MapPost("/mcp", (Delegate)HandleMcpRequestAsync)
            .WithName("McpJsonRpc")
            .WithTags("MCP")
            .WithDescription("MCP JSON-RPC endpoint for tool invocations");

        // Compliance chat endpoint
        app.MapPost("/mcp/chat", (Delegate)HandleChatRequestAsync)
            .WithName("McpChat")
            .WithTags("MCP")
            .WithDescription("Process compliance requests via AI agent");

        // Streaming chat endpoint with SSE progress events
        app.MapPost("/mcp/chat/stream", (Delegate)HandleChatStreamRequestAsync)
            .WithName("McpChatStream")
            .WithTags("MCP")
            .WithDescription("Process compliance requests with real-time progress via SSE");

        // Health endpoint
        app.MapGet("/health", (Delegate)HandleHealthAsync)
            .WithName("Health")
            .WithTags("Health")
            .WithDescription("Health check");

        // Tools listing endpoint
        app.MapGet("/mcp/tools", (Delegate)HandleToolsListAsync)
            .WithName("McpToolsList")
            .WithTags("MCP")
            .WithDescription("List available compliance tools");

        _logger.LogInformation("MCP HTTP endpoints mapped");
    }

    /// <summary>Handles MCP JSON-RPC requests (tools/list, tools/call).</summary>
    private async Task<IResult> HandleMcpRequestAsync(HttpContext context)
    {
        try
        {
            var request = await JsonSerializer.DeserializeAsync<McpRequest>(
                context.Request.Body, _jsonOptions);

            if (request == null)
                return Results.BadRequest(new { error = "Invalid request" });

            _logger.LogInformation("HTTP MCP request: {Method}", request.Method);

            // Use reflection to call HandleRequestAsync via the server's StartAsync pipeline
            // For HTTP, we directly process through the chat endpoint or tool calls
            if (request.Method == "tools/call")
            {
                var toolCall = JsonSerializer.Deserialize<McpToolCall>(
                    JsonSerializer.Serialize(request.Params, _jsonOptions), _jsonOptions);

                if (toolCall?.Name == "compliance_chat")
                {
                    var message = toolCall.Arguments?.GetValueOrDefault("message")?.ToString() ?? "";
                    var convId = toolCall.Arguments?.GetValueOrDefault("conversation_id")?.ToString();
                    var result = await _mcpServer.ProcessChatRequestAsync(message, convId);
                    return Results.Json(result, _jsonOptions);
                }
            }

            // For other methods, wrap in chat
            var chatResult = await _mcpServer.ProcessChatRequestAsync(
                JsonSerializer.Serialize(request.Params, _jsonOptions));
            return Results.Json(chatResult, _jsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing HTTP MCP request");
            return Results.Problem(ex.Message);
        }
    }

    /// <summary>Handles natural language chat requests routed to the appropriate agent.</summary>
    private async Task<IResult> HandleChatRequestAsync(HttpContext context)
    {
        try
        {
            var chatRequest = await JsonSerializer.DeserializeAsync<ChatRequest>(
                context.Request.Body, _jsonOptions);

            if (chatRequest == null || string.IsNullOrEmpty(chatRequest.Message))
                return Results.BadRequest(new { error = "Message is required" });

            _logger.LogInformation("Chat request | ConvId: {ConvId}", chatRequest.ConversationId);

            var result = await _mcpServer.ProcessChatRequestAsync(
                chatRequest.Message,
                chatRequest.ConversationId,
                chatRequest.Context,
                chatRequest.ConversationHistory?.Select(m => (m.Role, m.Content)).ToList(),
                action: chatRequest.Action,
                actionContext: chatRequest.ActionContext);

            return Results.Json(result, _jsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing chat request");
            return Results.Problem(ex.Message);
        }
    }

    /// <summary>Handles chat requests with SSE streaming for real-time progress.</summary>
    private async Task HandleChatStreamRequestAsync(HttpContext context)
    {
        try
        {
            var chatRequest = await JsonSerializer.DeserializeAsync<ChatRequest>(
                context.Request.Body, _jsonOptions);

            if (chatRequest == null || string.IsNullOrEmpty(chatRequest.Message))
            {
                context.Response.StatusCode = 400;
                await context.Response.WriteAsJsonAsync(new { error = "Message is required" });
                return;
            }

            _logger.LogInformation("Streaming chat request | ConvId: {ConvId}", chatRequest.ConversationId);

            // Set up SSE headers
            context.Response.ContentType = "text/event-stream";
            context.Response.Headers["Cache-Control"] = "no-cache";
            context.Response.Headers["Connection"] = "keep-alive";
            await context.Response.Body.FlushAsync();

            // Create progress reporter that writes SSE events (T018: typed events)
            var progress = new Progress<string>(async step =>
            {
                try
                {
                    // Try to parse typed SSE event JSON; fallback to plain progress
                    string eventData;
                    if (step.StartsWith("{") && step.Contains("\"type\""))
                    {
                        eventData = step; // Already a typed JSON event from McpServer
                    }
                    else
                    {
                        eventData = JsonSerializer.Serialize(new { type = "progress", step }, _sseJsonOptions);
                    }
                    await context.Response.WriteAsync($"data: {eventData}\n\n");
                    await context.Response.Body.FlushAsync();
                }
                catch { /* client disconnected */ }
            });

            var result = await _mcpServer.ProcessChatRequestAsync(
                chatRequest.Message,
                chatRequest.ConversationId,
                chatRequest.Context,
                chatRequest.ConversationHistory?.Select(m => (m.Role, m.Content)).ToList(),
                context.RequestAborted,
                progress,
                chatRequest.Action,
                chatRequest.ActionContext);

            // Write final result as SSE event
            var resultData = JsonSerializer.Serialize(new { type = "result", data = result }, _sseJsonOptions);
            await context.Response.WriteAsync($"data: {resultData}\n\n");
            await context.Response.Body.FlushAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing streaming chat request");
            try
            {
                var errorData = JsonSerializer.Serialize(new { type = "error", error = ex.Message }, _sseJsonOptions);
                await context.Response.WriteAsync($"data: {errorData}\n\n");
                await context.Response.Body.FlushAsync();
            }
            catch { /* client disconnected */ }
        }
    }

    /// <summary>Returns server health status, capabilities, and agent health check results.</summary>
    private async Task<IResult> HandleHealthAsync(HttpContext context)
    {
        // Run ASP.NET Core health checks if registered
        var healthCheckService = context.RequestServices.GetService<Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckService>();
        var agentHealthEntries = new List<object>();
        var overallStatus = "healthy";

        if (healthCheckService is not null)
        {
            var report = await healthCheckService.CheckHealthAsync(context.RequestAborted);
            overallStatus = report.Status switch
            {
                Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Healthy => "healthy",
                Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Degraded => "degraded",
                _ => "unhealthy"
            };

            foreach (var entry in report.Entries)
            {
                agentHealthEntries.Add(new
                {
                    name = entry.Key,
                    status = entry.Value.Status.ToString(),
                    description = entry.Value.Description ?? string.Empty
                });
            }
        }

        var health = new
        {
            status = overallStatus,
            service = "ATO Copilot MCP",
            version = "1.16.0",
            timestamp = DateTime.UtcNow,
            capabilities = new[] { "compliance-assessment", "nist-800-53", "fedramp", "remediation", "evidence-collection" },
            agents = agentHealthEntries,
            totalDurationMs = healthCheckService is not null ? 0 : -1
        };

        return Results.Json(health, _jsonOptions);
    }

    /// <summary>Returns the list of available compliance tools (dynamically generated from registered BaseTool instances).</summary>
    private Task<IResult> HandleToolsListAsync(HttpContext context)
    {
        var tools = _tools
            .Select(t => new
            {
                name = t.Name,
                description = t.Description,
                inputSchema = new
                {
                    type = "object",
                    properties = t.Parameters.ToDictionary(
                        p => p.Key,
                        p => new { type = p.Value.Type, description = p.Value.Description }),
                    required = t.Parameters
                        .Where(p => p.Value.Required)
                        .Select(p => p.Key)
                        .ToArray()
                }
            })
            .OrderBy(t => t.name)
            .ToArray();

        return Task.FromResult(Results.Json(new { tools, count = tools.Length }, _jsonOptions));
    }
}

/// <summary>
/// Chat request model for HTTP endpoint
/// </summary>
public class ChatRequest
{
    public string Message { get; set; } = string.Empty;
    public string? ConversationId { get; set; }
    public Dictionary<string, object>? Context { get; set; }
    public List<ChatMessage>? ConversationHistory { get; set; }

    /// <summary>
    /// Optional action identifier for drill-down and tool invocation routing.
    /// When present, the server routes to the corresponding MCP tool instead of normal agent processing.
    /// Examples: "remediate", "drillDown", "collectEvidence", "showKanban" (FR-014a, R-006).
    /// </summary>
    public string? Action { get; set; }

    /// <summary>
    /// Contextual data for the specified <see cref="Action"/>.
    /// Contains action-specific parameters (e.g., controlId for drillDown, findingId for remediate).
    /// </summary>
    public Dictionary<string, object>? ActionContext { get; set; }
}

/// <summary>
/// Chat message model for conversation history.
/// </summary>
public class ChatMessage
{
    /// <summary>The role of the message sender (user or assistant).</summary>
    public string Role { get; set; } = "user";
    /// <summary>The message content text.</summary>
    public string Content { get; set; } = string.Empty;
}
