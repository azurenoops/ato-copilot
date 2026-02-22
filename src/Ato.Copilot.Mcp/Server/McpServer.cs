using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http;
using Ato.Copilot.Mcp.Tools;
using Ato.Copilot.Mcp.Models;
using Ato.Copilot.Mcp.Prompts;
using Ato.Copilot.Agents.Compliance.Agents;
using Ato.Copilot.Agents.Configuration.Agents;
using Ato.Copilot.Agents.Configuration.Tools;
using Ato.Copilot.Agents.Common;
using System.Diagnostics;
using System.Text.Json;

namespace Ato.Copilot.Mcp.Server;

/// <summary>
/// MCP server for the ATO Copilot - compliance-only agent.
/// Exposes compliance tools via stdio/HTTP for GitHub Copilot, Claude Desktop, etc.
/// </summary>
public class McpServer
{
    private readonly ComplianceMcpTools _complianceTools;
    private readonly ComplianceAgent _complianceAgent;
    private readonly ConfigurationAgent _configurationAgent;
    private readonly ConfigurationTool _configurationTool;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<McpServer> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public McpServer(
        ComplianceMcpTools complianceTools,
        ComplianceAgent complianceAgent,
        ConfigurationAgent configurationAgent,
        ConfigurationTool configurationTool,
        IHttpContextAccessor httpContextAccessor,
        ILogger<McpServer> logger)
    {
        _complianceTools = complianceTools;
        _complianceAgent = complianceAgent;
        _configurationAgent = configurationAgent;
        _configurationTool = configurationTool;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };
    }

    /// <summary>
    /// Resolves the current user ID from the ambient HTTP context.
    /// Falls back to "mcp-user" when no authenticated user is present.
    /// </summary>
    private string ResolveCurrentUserId()
    {
        var user = _httpContextAccessor.HttpContext?.User;
        if (user?.Identity?.IsAuthenticated != true)
            return "mcp-user";

        var oid = user.FindFirst("oid")?.Value;
        if (!string.IsNullOrEmpty(oid))
            return oid;

        var sub = user.FindFirst("sub")?.Value;
        return !string.IsNullOrEmpty(sub) ? sub : "mcp-user";
    }

    /// <summary>
    /// Process a chat request through the compliance agent
    /// </summary>
    public async Task<McpChatResponse> ProcessChatRequestAsync(
        string message,
        string? conversationId = null,
        Dictionary<string, object>? context = null,
        List<(string Role, string Content)>? conversationHistory = null,
        CancellationToken cancellationToken = default)
    {
        conversationId ??= Guid.NewGuid().ToString();
        var stopwatch = Stopwatch.StartNew();

        _logger.LogInformation("Processing compliance chat | ConvId: {ConvId}", conversationId);

        try
        {
            var agentContext = new AgentConversationContext
            {
                ConversationId = conversationId,
                UserId = ResolveCurrentUserId()
            };

            if (conversationHistory != null)
            {
                foreach (var (role, content) in conversationHistory)
                    agentContext.AddMessage(content, isUser: role.Equals("user", StringComparison.OrdinalIgnoreCase));
            }

            if (context != null)
            {
                foreach (var kvp in context)
                    agentContext.WorkflowState[kvp.Key] = kvp.Value;
            }

            // Route to appropriate agent based on intent classification
            var targetAgent = ClassifyAndRouteAgent(message);
            var response = await targetAgent.ProcessAsync(message, agentContext, cancellationToken);
            stopwatch.Stop();

            _logger.LogInformation("Routed to {Agent} | ConvId: {ConvId}",
                targetAgent.AgentName, conversationId);

            return new McpChatResponse
            {
                Success = response.Success,
                Response = response.Response,
                ConversationId = conversationId,
                AgentName = response.AgentName,
                ProcessingTimeMs = stopwatch.ElapsedMilliseconds
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Error processing compliance chat request");

            return new McpChatResponse
            {
                Success = false,
                Response = $"Error processing request: {ex.Message}",
                ConversationId = conversationId,
                ProcessingTimeMs = stopwatch.ElapsedMilliseconds,
                Errors = new List<string> { ex.Message }
            };
        }
    }

    /// <summary>
    /// Start the MCP server in stdio mode
    /// </summary>
    public async Task StartAsync()
    {
        _logger.LogInformation("Starting ATO Copilot MCP Server (compliance-only)");

        try
        {
            using var reader = new StreamReader(Console.OpenStandardInput());
            using var writer = new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true };

            string? line;
            while ((line = await reader.ReadLineAsync()) != null)
            {
                try
                {
                    var request = JsonSerializer.Deserialize<McpRequest>(line, _jsonOptions);
                    if (request != null)
                    {
                        var response = await HandleRequestAsync(request);
                        var responseJson = JsonSerializer.Serialize(response, _jsonOptions);
                        await writer.WriteLineAsync(responseJson);
                    }
                }
                catch (JsonException ex)
                {
                    _logger.LogError(ex, "Invalid JSON received");
                    var errorResponse = CreateErrorResponse(0, -32700, "Parse error");
                    await writer.WriteLineAsync(JsonSerializer.Serialize(errorResponse, _jsonOptions));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing request");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fatal error in MCP server");
            throw;
        }
    }

    private async Task<McpResponse> HandleRequestAsync(McpRequest request)
    {
        try
        {
            return request.Method switch
            {
                "initialize" => HandleInitialize(request),
                "tools/list" => HandleToolsList(request),
                "tools/call" => await HandleToolCallAsync(request),
                "prompts/list" => HandlePromptsList(request),
                "prompts/get" => HandlePromptsGet(request),
                "ping" => HandlePing(request),
                _ => CreateErrorResponse(request.Id, -32601, $"Method not found: {request.Method}")
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling request {Method}", request.Method);
            return CreateErrorResponse(request.Id, -32603, "Internal error", ex.Message);
        }
    }

    private McpResponse HandleInitialize(McpRequest request)
    {
        _logger.LogInformation("Client initialized MCP connection");

        return new McpResponse
        {
            Id = request.Id,
            Result = new
            {
                protocolVersion = "2024-11-05",
                capabilities = new
                {
                    tools = new { listChanged = false },
                    prompts = new { listChanged = false }
                },
                serverInfo = new
                {
                    name = "ATO Copilot",
                    version = "1.0.0"
                }
            }
        };
    }

    private McpResponse HandleToolsList(McpRequest request)
    {
        var tools = new List<McpTool>();

        // Compliance Assessment Tools
        tools.Add(CreateTool("compliance_assess", "Run a NIST 800-53 compliance assessment against Azure resources. Supports scan types: quick (fast summary), policy (Azure Policy with NIST mapping), full (deep scan with remediation).", new
        {
            type = "object",
            properties = new
            {
                subscription_id = new { type = "string", description = "Azure subscription ID" },
                resource_group = new { type = "string", description = "Optional resource group filter" },
                impact_level = new { type = "string", description = "FIPS 199 impact level: Low, Moderate, or High" },
                scan_type = new { type = "string", description = "Scan type: quick, policy, or full" },
                control_families = new { type = "array", items = new { type = "string" }, description = "Control families to assess (e.g., AC, AU, IA)" }
            },
            required = new[] { "subscription_id" }
        }));

        tools.Add(CreateTool("compliance_get_control_family", "Get detailed information about a NIST 800-53 control family including controls, descriptions, and Azure implementation guidance", new
        {
            type = "object",
            properties = new
            {
                family = new { type = "string", description = "Control family (e.g., AC, AU, IA, CM, SC)" },
                impact_level = new { type = "string", description = "FIPS 199 impact level" }
            },
            required = new[] { "family" }
        }));

        tools.Add(CreateTool("compliance_generate_document", "Generate compliance documentation (SSP, POA&M, SAR) for FedRAMP/ATO authorization", new
        {
            type = "object",
            properties = new
            {
                document_type = new { type = "string", description = "Document type: ssp, poam, sar, or assessment" },
                system_name = new { type = "string", description = "System name" },
                subscription_id = new { type = "string", description = "Azure subscription for evidence" }
            },
            required = new[] { "document_type", "system_name" }
        }));

        tools.Add(CreateTool("compliance_collect_evidence", "Collect compliance evidence from Azure resources for audit documentation", new
        {
            type = "object",
            properties = new
            {
                control_id = new { type = "string", description = "NIST control ID (e.g., AC-2, AU-3)" },
                subscription_id = new { type = "string", description = "Azure subscription ID" }
            },
            required = new[] { "control_id", "subscription_id" }
        }));

        tools.Add(CreateTool("compliance_remediate", "Remediate a compliance finding with guided or automated fixes", new
        {
            type = "object",
            properties = new
            {
                finding_id = new { type = "string", description = "Finding ID to remediate" },
                auto_fix = new { type = "boolean", description = "Apply fix automatically" },
                dry_run = new { type = "boolean", description = "Preview without applying (default: true)" }
            },
            required = new[] { "finding_id" }
        }));

        tools.Add(CreateTool("compliance_validate_remediation", "Validate that a remediation was successfully applied", new
        {
            type = "object",
            properties = new
            {
                finding_id = new { type = "string", description = "Finding ID to validate" },
                execution_id = new { type = "string", description = "Execution ID from remediation" }
            },
            required = new[] { "finding_id" }
        }));

        tools.Add(CreateTool("compliance_generate_plan", "Generate a prioritized remediation plan for compliance findings", new
        {
            type = "object",
            properties = new
            {
                subscription_id = new { type = "string", description = "Azure subscription" },
                resource_group = new { type = "string", description = "Resource group filter" }
            },
            required = Array.Empty<string>()
        }));

        tools.Add(CreateTool("compliance_audit_log", "Get the audit trail of compliance assessments", new
        {
            type = "object",
            properties = new
            {
                subscription_id = new { type = "string", description = "Azure subscription" },
                days = new { type = "integer", description = "Days to look back (default: 7)" }
            },
            required = Array.Empty<string>()
        }));

        tools.Add(CreateTool("compliance_history", "Get compliance history and trends over time", new
        {
            type = "object",
            properties = new
            {
                subscription_id = new { type = "string", description = "Azure subscription" },
                days = new { type = "integer", description = "Days to look back (default: 30)" }
            },
            required = Array.Empty<string>()
        }));

        tools.Add(CreateTool("compliance_status", "Get current compliance status and posture summary", new
        {
            type = "object",
            properties = new
            {
                subscription_id = new { type = "string", description = "Azure subscription" },
                framework = new { type = "string", description = "Compliance framework" }
            },
            required = Array.Empty<string>()
        }));

        tools.Add(CreateTool("compliance_monitoring", "Query continuous compliance monitoring: status, scan, alerts, trend", new
        {
            type = "object",
            properties = new
            {
                action = new { type = "string", description = "Action: status, scan, alerts, acknowledge, trend, history" },
                subscription_id = new { type = "string", description = "Azure subscription" },
                days = new { type = "integer", description = "Days to look back (default: 30)" }
            },
            required = new[] { "action" }
        }));

        // Chat tool for natural language processing
        tools.Add(CreateTool("compliance_chat", "Process compliance requests through the AI compliance agent for natural language interaction", new
        {
            type = "object",
            properties = new
            {
                message = new { type = "string", description = "The compliance question or request" },
                conversation_id = new { type = "string", description = "Optional conversation ID for context" }
            },
            required = new[] { "message" }
        }));

        // Configuration Management Tool
        tools.Add(CreateTool("configuration_manage", "Manage ATO Copilot settings: subscription, framework, baseline, and preferences", new
        {
            type = "object",
            properties = new
            {
                action = new { type = "string", description = "Action: get_configuration, set_subscription, set_framework, set_baseline, set_preference" },
                subscriptionId = new { type = "string", description = "Azure subscription GUID (for set_subscription)" },
                framework = new { type = "string", description = "Compliance framework: NIST80053, FedRAMPHigh, FedRAMPModerate, DoDIL5 (for set_framework)" },
                baseline = new { type = "string", description = "Security baseline: Low, Moderate, High (for set_baseline)" },
                preferenceName = new { type = "string", description = "Preference name (for set_preference)" },
                preferenceValue = new { type = "string", description = "Preference value (for set_preference)" }
            },
            required = new[] { "action" }
        }));

        // Configuration Chat Tool
        tools.Add(CreateTool("configuration_chat", "Process configuration requests through the AI configuration agent for natural language interaction", new
        {
            type = "object",
            properties = new
            {
                message = new { type = "string", description = "The configuration question or request" },
                conversation_id = new { type = "string", description = "Optional conversation ID for context" }
            },
            required = new[] { "message" }
        }));

        return new McpResponse { Id = request.Id, Result = new { tools } };
    }

    private async Task<McpResponse> HandleToolCallAsync(McpRequest request)
    {
        try
        {
            var toolCall = JsonSerializer.Deserialize<McpToolCall>(
                JsonSerializer.Serialize(request.Params, _jsonOptions), _jsonOptions);

            if (toolCall == null)
                return CreateErrorResponse(request.Id, -32602, "Invalid tool call parameters");

            _logger.LogInformation("Executing tool: {ToolName}", toolCall.Name);
            var args = toolCall.Arguments ?? new Dictionary<string, object>();
            var result = await ExecuteToolAsync(toolCall.Name, args);

            return new McpResponse { Id = request.Id, Result = result };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing tool call");
            return CreateErrorResponse(request.Id, -32603, "Tool execution failed", ex.Message);
        }
    }

    private async Task<McpToolResult> ExecuteToolAsync(string toolName, Dictionary<string, object> args)
    {
        try
        {
            string result = toolName switch
            {
                "compliance_assess" => await _complianceTools.RunComplianceAssessmentAsync(
                    GetArg<string>(args, "subscription_id"),
                    GetArg<string>(args, "framework"),
                    GetArg<string>(args, "control_families"),
                    GetArg<string>(args, "resource_types"),
                    GetArg<string>(args, "scan_type"),
                    GetArg<bool?>(args, "include_passed") ?? false),

                "compliance_get_control_family" => await _complianceTools.GetControlFamilyInfoAsync(
                    GetArg<string>(args, "family") ?? "",
                    GetArg<bool?>(args, "include_controls") ?? true),

                "compliance_generate_document" => await _complianceTools.GenerateComplianceDocumentAsync(
                    GetArg<string>(args, "document_type") ?? "ssp",
                    GetArg<string>(args, "subscription_id"),
                    GetArg<string>(args, "framework"),
                    GetArg<string>(args, "system_name")),

                "compliance_collect_evidence" => await _complianceTools.CollectComplianceEvidenceAsync(
                    GetArg<string>(args, "control_id") ?? "",
                    GetArg<string>(args, "subscription_id"),
                    GetArg<string>(args, "resource_group")),

                "compliance_remediate" => await _complianceTools.RemediateComplianceFindingAsync(
                    GetArg<string>(args, "finding_id") ?? "",
                    GetArg<bool?>(args, "apply_remediation") ?? false,
                    GetArg<bool?>(args, "dry_run") ?? true),

                "compliance_validate_remediation" => await _complianceTools.ValidateRemediationAsync(
                    GetArg<string>(args, "finding_id") ?? "",
                    GetArg<string>(args, "execution_id"),
                    GetArg<string>(args, "subscription_id")),

                "compliance_generate_plan" => await _complianceTools.GenerateRemediationPlanAsync(
                    GetArg<string>(args, "subscription_id"),
                    GetArg<string>(args, "resource_group")),

                "compliance_audit_log" => await _complianceTools.GetAssessmentAuditLogAsync(
                    GetArg<string>(args, "subscription_id"),
                    GetArg<int?>(args, "days") ?? 7),

                "compliance_history" => await _complianceTools.GetComplianceHistoryAsync(
                    GetArg<string>(args, "subscription_id"),
                    GetArg<int?>(args, "days") ?? 30),

                "compliance_status" => await _complianceTools.GetComplianceStatusAsync(
                    GetArg<string>(args, "subscription_id"),
                    GetArg<string>(args, "framework")),

                "compliance_monitoring" => await _complianceTools.GetComplianceMonitoringAsync(
                    GetArg<string>(args, "action") ?? "status",
                    GetArg<string>(args, "subscription_id"),
                    GetArg<int?>(args, "days") ?? 30),

                "compliance_chat" => await ExecuteChatAsync(
                    GetArg<string>(args, "message") ?? "",
                    GetArg<string>(args, "conversation_id")),

                "configuration_manage" => await ExecuteConfigurationToolAsync(args),

                "configuration_chat" => await ExecuteConfigurationChatAsync(
                    GetArg<string>(args, "message") ?? "",
                    GetArg<string>(args, "conversation_id")),

                _ => $"Unknown tool: {toolName}"
            };

            return McpToolResult.Success(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing tool {ToolName}", toolName);
            return McpToolResult.Error($"Error: {ex.Message}");
        }
    }

    private async Task<string> ExecuteChatAsync(string message, string? conversationId)
    {
        var result = await ProcessChatRequestAsync(message, conversationId);
        return result.Response;
    }

    private async Task<string> ExecuteConfigurationToolAsync(Dictionary<string, object> args)
    {
        // Convert to nullable dictionary expected by BaseTool
        var toolArgs = args.ToDictionary(
            kvp => kvp.Key,
            kvp => (object?)kvp.Value);

        return await _configurationTool.ExecuteAsync(toolArgs);
    }

    private async Task<string> ExecuteConfigurationChatAsync(string message, string? conversationId)
    {
        conversationId ??= Guid.NewGuid().ToString();

        var agentContext = new AgentConversationContext
        {
            ConversationId = conversationId,
            UserId = ResolveCurrentUserId()
        };

        var response = await _configurationAgent.ProcessAsync(message, agentContext);
        return response.Response;
    }

    private McpResponse HandlePromptsList(McpRequest request)
    {
        var prompts = PromptRegistry.GetAllPrompts().Select(p => new
        {
            name = p.Name,
            description = p.Description,
            arguments = p.Arguments.Select(a => new { name = a.Name, description = a.Description, required = a.Required }).ToList()
        }).ToList();

        return new McpResponse { Id = request.Id, Result = new { prompts } };
    }

    private McpResponse HandlePromptsGet(McpRequest request)
    {
        var paramsJson = JsonSerializer.Serialize(request.Params, _jsonOptions);
        var promptRequest = JsonSerializer.Deserialize<Dictionary<string, object>>(paramsJson, _jsonOptions);
        var promptName = promptRequest?.GetValueOrDefault("name")?.ToString();

        if (string.IsNullOrEmpty(promptName))
            return CreateErrorResponse(request.Id, -32602, "Prompt name required");

        var prompt = PromptRegistry.FindPrompt(promptName);
        if (prompt == null)
            return CreateErrorResponse(request.Id, -32602, $"Prompt not found: {promptName}");

        return new McpResponse
        {
            Id = request.Id,
            Result = new
            {
                description = prompt.Description,
                messages = new[] { new { role = "user", content = new { type = "text", text = $"Execute the {prompt.Name} prompt with the provided arguments." } } }
            }
        };
    }

    private McpResponse HandlePing(McpRequest request) =>
        new() { Id = request.Id, Result = new { status = "ok", timestamp = DateTime.UtcNow } };

    private static McpTool CreateTool(string name, string description, object inputSchema) =>
        new() { Name = name, Description = description, InputSchema = inputSchema };

    private static McpResponse CreateErrorResponse(object id, int code, string message, string? data = null) =>
        new() { Id = id, Error = new McpError { Code = code, Message = message, Data = data } };

    private static T? GetArg<T>(Dictionary<string, object> args, string key)
    {
        if (!args.TryGetValue(key, out var value) || value == null) return default;
        if (value is T typedValue) return typedValue;
        if (value is JsonElement jsonElement)
        {
            try { return JsonSerializer.Deserialize<T>(jsonElement.GetRawText()); }
            catch { return default; }
        }
        try { return (T)Convert.ChangeType(value, typeof(T)); }
        catch { return default; }
    }

    /// <summary>
    /// Classifies the user message and routes to the appropriate agent.
    /// Configuration intents go to ConfigurationAgent; everything else goes to ComplianceAgent.
    /// </summary>
    private BaseAgent ClassifyAndRouteAgent(string message)
    {
        var lower = message.ToLowerInvariant();

        // Configuration-related intents → ConfigurationAgent
        if (ContainsAny(lower,
            "configure", "subscription", "set framework", "set baseline",
            "set cloud", "get config", "show config", "configuration",
            "settings", "setup", "set dry", "set scan"))
        {
            return _configurationAgent;
        }

        // Everything else → ComplianceAgent (assessment, remediation, evidence, documents, monitoring, etc.)
        return _complianceAgent;
    }

    private static bool ContainsAny(string text, params string[] keywords) =>
        keywords.Any(k => text.Contains(k, StringComparison.OrdinalIgnoreCase));
}
