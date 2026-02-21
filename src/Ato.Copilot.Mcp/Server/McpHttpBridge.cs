using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Ato.Copilot.Mcp.Models;
using System.Text.Json;

namespace Ato.Copilot.Mcp.Server;

/// <summary>
/// HTTP bridge for MCP protocol — exposes compliance tools as REST endpoints
/// </summary>
public class McpHttpBridge
{
    private readonly McpServer _mcpServer;
    private readonly ILogger<McpHttpBridge> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public McpHttpBridge(McpServer mcpServer, ILogger<McpHttpBridge> logger)
    {
        _mcpServer = mcpServer;
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };
    }

    /// <summary>
    /// Map all MCP HTTP endpoints
    /// </summary>
    public void MapEndpoints(WebApplication app)
    {
        // MCP JSON-RPC endpoint
        app.MapPost("/mcp", HandleMcpRequestAsync)
            .WithName("McpJsonRpc")
            .WithTags("MCP")
            .WithDescription("MCP JSON-RPC endpoint for tool invocations");

        // Compliance chat endpoint
        app.MapPost("/mcp/chat", HandleChatRequestAsync)
            .WithName("McpChat")
            .WithTags("MCP")
            .WithDescription("Process compliance requests via AI agent");

        // Health endpoint
        app.MapGet("/health", HandleHealthAsync)
            .WithName("Health")
            .WithTags("Health")
            .WithDescription("Health check");

        // Tools listing endpoint
        app.MapGet("/mcp/tools", HandleToolsListAsync)
            .WithName("McpToolsList")
            .WithTags("MCP")
            .WithDescription("List available compliance tools");

        _logger.LogInformation("MCP HTTP endpoints mapped");
    }

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
                chatRequest.ConversationHistory?.Select(m => (m.Role, m.Content)).ToList());

            return Results.Json(result, _jsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing chat request");
            return Results.Problem(ex.Message);
        }
    }

    private Task<IResult> HandleHealthAsync(HttpContext context)
    {
        var health = new
        {
            status = "healthy",
            service = "ATO Copilot MCP",
            version = "1.0.0",
            timestamp = DateTime.UtcNow,
            capabilities = new[] { "compliance-assessment", "nist-800-53", "fedramp", "remediation", "evidence-collection" }
        };

        return Task.FromResult(Results.Json(health, _jsonOptions));
    }

    private Task<IResult> HandleToolsListAsync(HttpContext context)
    {
        var tools = new[]
        {
            new { name = "compliance_assess", description = "Run NIST 800-53 compliance assessment" },
            new { name = "compliance_get_control_family", description = "Get control family details" },
            new { name = "compliance_generate_document", description = "Generate compliance documentation" },
            new { name = "compliance_collect_evidence", description = "Collect compliance evidence" },
            new { name = "compliance_remediate", description = "Remediate compliance findings" },
            new { name = "compliance_validate_remediation", description = "Validate remediations" },
            new { name = "compliance_generate_plan", description = "Generate remediation plan" },
            new { name = "compliance_audit_log", description = "Get assessment audit log" },
            new { name = "compliance_history", description = "Get compliance history" },
            new { name = "compliance_status", description = "Get compliance status" },
            new { name = "compliance_monitoring", description = "Compliance monitoring operations" },
            new { name = "compliance_chat", description = "Natural language compliance interaction" }
        };

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
}

public class ChatMessage
{
    public string Role { get; set; } = "user";
    public string Content { get; set; } = string.Empty;
}
