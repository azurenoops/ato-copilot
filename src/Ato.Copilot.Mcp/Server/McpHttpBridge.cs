using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
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

    /// <summary>Initializes a new instance of the <see cref="McpHttpBridge"/> class.</summary>
    /// <param name="mcpServer">The MCP server to delegate requests to.</param>
    /// <param name="logger">Logger instance.</param>
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
                chatRequest.ConversationHistory?.Select(m => (m.Role, m.Content)).ToList());

            return Results.Json(result, _jsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing chat request");
            return Results.Problem(ex.Message);
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
            version = "1.0.0",
            timestamp = DateTime.UtcNow,
            capabilities = new[] { "compliance-assessment", "nist-800-53", "fedramp", "remediation", "evidence-collection" },
            agents = agentHealthEntries,
            totalDurationMs = healthCheckService is not null ? 0 : -1
        };

        return Results.Json(health, _jsonOptions);
    }

    /// <summary>Returns the list of available compliance tools.</summary>
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
            new { name = "compliance_chat", description = "Natural language compliance interaction" },
            // CAC Authentication tools (Tier 2)
            new { name = "cac_status", description = "Check CAC/PIV session status" },
            new { name = "cac_sign_out", description = "Terminate CAC/PIV session" },
            new { name = "cac_set_timeout", description = "Configure session timeout" },
            new { name = "cac_map_certificate", description = "Map certificate to compliance role" },
            // PIM tools (Tier 2)
            new { name = "pim_list_eligible", description = "List eligible PIM roles" },
            new { name = "pim_activate_role", description = "Activate a PIM role" },
            new { name = "pim_deactivate_role", description = "Deactivate a PIM role" },
            new { name = "pim_list_active", description = "List active PIM roles" },
            new { name = "pim_extend_role", description = "Extend a PIM role activation" },
            new { name = "pim_approve_request", description = "Approve a PIM activation request" },
            new { name = "pim_deny_request", description = "Deny a PIM activation request" },
            new { name = "pim_history", description = "Query PIM activation history" },
            // JIT VM Access tools (Tier 2)
            new { name = "jit_request_access", description = "Request JIT VM access" },
            new { name = "jit_list_sessions", description = "List active JIT sessions" },
            new { name = "jit_revoke_access", description = "Revoke JIT VM access" },
            // Kanban tools
            new { name = "kanban_create_board", description = "Create a remediation kanban board" },
            new { name = "kanban_board_show", description = "Show a kanban board" },
            new { name = "kanban_get_task", description = "Get task details" },
            new { name = "kanban_create_task", description = "Create a remediation task" },
            new { name = "kanban_assign_task", description = "Assign a task" },
            new { name = "kanban_move_task", description = "Move a task between columns" },
            new { name = "kanban_add_task_comment", description = "Add a comment to a task" },
            new { name = "kanban_search_tasks", description = "Search tasks" },
            new { name = "kanban_list_boards", description = "List all boards" },
            new { name = "kanban_overdue_tasks", description = "List overdue tasks" },
            // KnowledgeBase tools (Feature 010)
            new { name = "kb_explain_nist_control", description = "Explain a NIST 800-53 control with Azure implementation guidance" },
            new { name = "kb_search_nist_controls", description = "Search NIST 800-53 controls by keyword" },
            new { name = "kb_explain_stig", description = "Explain a STIG rule with fix/check guidance" },
            new { name = "kb_search_stigs", description = "Search STIG rules by keyword or severity" },
            new { name = "kb_explain_rmf", description = "Explain the RMF process, steps, or DoD instructions" },
            new { name = "kb_explain_impact_level", description = "Explain DoD impact levels (IL2-IL6) or FedRAMP baselines" },
            new { name = "kb_get_fedramp_template_guidance", description = "Get FedRAMP authorization package template guidance" }
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
