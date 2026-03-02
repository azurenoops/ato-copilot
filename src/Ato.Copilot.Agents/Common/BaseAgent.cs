using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Ato.Copilot.Core.Configuration;

namespace Ato.Copilot.Agents.Common;

/// <summary>
/// Base class for all agents in the ATO Copilot.
/// All agents MUST extend this class (Constitution Principle II).
/// </summary>
public abstract class BaseAgent
{
    protected readonly ILogger Logger;
    private readonly IChatClient? _chatClient;
    private readonly AzureOpenAIGatewayOptions? _aiOptions;

    protected BaseAgent(ILogger logger)
    {
        Logger = logger;
    }

    /// <summary>
    /// Constructor overload for AI-enabled agents. Accepts optional IChatClient and AI options.
    /// When chatClient is null, agents fall back to deterministic tool routing.
    /// </summary>
    protected BaseAgent(ILogger logger, IChatClient? chatClient, AzureOpenAIGatewayOptions? aiOptions)
        : this(logger)
    {
        _chatClient = chatClient;
        _aiOptions = aiOptions;
    }

    /// <summary>
    /// Unique identifier for this agent
    /// </summary>
    public abstract string AgentId { get; }

    /// <summary>
    /// Display name of the agent
    /// </summary>
    public abstract string AgentName { get; }

    /// <summary>
    /// Description of the agent's capabilities
    /// </summary>
    public abstract string Description { get; }

    /// <summary>
    /// Get the system prompt for this agent
    /// </summary>
    public abstract string GetSystemPrompt();

    /// <summary>
    /// Evaluate confidence that this agent can handle the given message.
    /// Returns a score between 0.0 (cannot handle) and 1.0 (perfect match).
    /// The orchestrator routes to the agent with the highest score above the
    /// configurable minimum threshold (default: 0.3).
    /// </summary>
    /// <param name="message">The user's input message.</param>
    /// <returns>Confidence score from 0.0 to 1.0.</returns>
    public abstract double CanHandle(string message);

    /// <summary>
    /// Process a user message through this agent
    /// </summary>
    public abstract Task<AgentResponse> ProcessAsync(
        string message,
        AgentConversationContext context,
        CancellationToken cancellationToken = default,
        IProgress<string>? progress = null);

    /// <summary>
    /// Registered tools for this agent
    /// </summary>
    protected List<BaseTool> Tools { get; } = new();

    /// <summary>
    /// Register a tool for use by this agent
    /// </summary>
    protected void RegisterTool(BaseTool tool)
    {
        if (tool == null) return;
        Tools.Add(tool);
        Logger?.LogDebug("Registered tool {ToolName} for agent {AgentName}", tool.Name, AgentName);
    }

    /// <summary>
    /// Attempt AI-powered processing via Azure OpenAI. Returns null if AI is unavailable
    /// or disabled, signaling the caller to fall back to deterministic tool routing.
    /// Implements manual tool-calling loop per research decision R4.
    /// </summary>
    protected async Task<AgentResponse?> TryProcessWithAiAsync(
        string message,
        AgentConversationContext context,
        CancellationToken cancellationToken = default,
        IProgress<string>? progress = null)
    {
        if (_chatClient is null || _aiOptions is null || !_aiOptions.AgentAIEnabled)
            return null;

        var stopwatch = Stopwatch.StartNew();

        try
        {
            Logger.LogInformation(
                "AI processing started for agent {AgentName}, conversation {ConversationId}",
                AgentName, context.ConversationId);

            var chatMessages = BuildChatContext(message, context);
            var toolDefinitions = BuildToolDefinitions(message);

            var chatOptions = new ChatOptions
            {
                Tools = toolDefinitions,
                Temperature = (float)_aiOptions.Temperature
            };

            var toolsExecuted = new List<ToolExecutionResult>();
            var maxRounds = _aiOptions.MaxToolCallRounds;

            for (var round = 0; round < maxRounds; round++)
            {
                progress?.Report($"ATO Copilot is thinking (round {round + 1})...");

                var response = await _chatClient.GetResponseAsync(
                    chatMessages, chatOptions, cancellationToken);

                // Check if the response contains tool calls
                var toolCalls = response.Messages
                    .SelectMany(m => m.Contents)
                    .OfType<FunctionCallContent>()
                    .ToList();

                if (toolCalls.Count == 0)
                {
                    // No tool calls — extract final text response
                    var textContent = response.Messages
                        .SelectMany(m => m.Contents)
                        .OfType<TextContent>()
                        .Select(t => t.Text)
                        .Where(t => !string.IsNullOrWhiteSpace(t));

                    var finalText = string.Join("\n", textContent);
                    if (string.IsNullOrWhiteSpace(finalText))
                        finalText = "I processed your request but have no additional details to share.";

                    // Add response messages to chat context for multi-turn
                    foreach (var msg in response.Messages)
                        chatMessages.Add(msg);

                    stopwatch.Stop();
                    Logger.LogInformation(
                        "AI processing completed for agent {AgentName} in {ElapsedMs}ms, {ToolCount} tools executed, {Rounds} rounds",
                        AgentName, stopwatch.ElapsedMilliseconds, toolsExecuted.Count, round + 1);

                    return new AgentResponse
                    {
                        Success = true,
                        Response = finalText,
                        AgentName = AgentName,
                        ToolsExecuted = toolsExecuted,
                        ProcessingTimeMs = stopwatch.ElapsedMilliseconds
                    };
                }

                // Add response messages (including tool call requests) to context
                foreach (var msg in response.Messages)
                    chatMessages.Add(msg);

                // Execute each tool call
                foreach (var toolCall in toolCalls)
                {
                    var toolStopwatch = Stopwatch.StartNew();
                    var tool = Tools.FirstOrDefault(t =>
                        t.Name.Equals(toolCall.Name, StringComparison.OrdinalIgnoreCase));

                    if (tool is null)
                    {
                        Logger.LogWarning(
                            "Unknown tool {ToolName} requested by LLM in agent {AgentName}",
                            toolCall.Name, AgentName);

                        progress?.Report($"Tool '{toolCall.Name}' not found, skipping...");

                        chatMessages.Add(new ChatMessage(ChatRole.Tool,
                            [new FunctionResultContent(toolCall.CallId,
                                $"Error: Tool '{toolCall.Name}' is not available. Available tools: {string.Join(", ", Tools.Select(t => t.Name))}")]));

                        toolsExecuted.Add(new ToolExecutionResult
                        {
                            ToolName = toolCall.Name,
                            Success = false,
                            Result = "Unknown tool",
                            ExecutionTimeMs = 0
                        });
                        continue;
                    }

                    try
                    {
                        // Convert tool call arguments to Dictionary<string, object?>
                        var args = new Dictionary<string, object?>();
                        if (toolCall.Arguments is not null)
                        {
                            foreach (var kvp in toolCall.Arguments)
                                args[kvp.Key] = kvp.Value;
                        }

                        Logger.LogDebug(
                            "Executing tool {ToolName} for agent {AgentName}, round {Round}",
                            tool.Name, AgentName, round + 1);

                        progress?.Report($"Running {tool.Description ?? tool.Name}...");

                        var toolResult = await tool.ExecuteAsync(args!, cancellationToken);
                        toolStopwatch.Stop();

                        chatMessages.Add(new ChatMessage(ChatRole.Tool,
                            [new FunctionResultContent(toolCall.CallId, toolResult)]));

                        toolsExecuted.Add(new ToolExecutionResult
                        {
                            ToolName = tool.Name,
                            Success = true,
                            Result = toolResult?.ToString()?.Length > 200
                                ? toolResult.ToString()![..200] + "..."
                                : toolResult?.ToString() ?? string.Empty,
                            ExecutionTimeMs = toolStopwatch.ElapsedMilliseconds
                        });

                        Logger.LogDebug(
                            "Tool {ToolName} completed in {ElapsedMs}ms for agent {AgentName}",
                            tool.Name, toolStopwatch.ElapsedMilliseconds, AgentName);
                    }
                    catch (Exception ex)
                    {
                        toolStopwatch.Stop();
                        Logger.LogError(ex,
                            "Tool {ToolName} failed in agent {AgentName}: {Error}",
                            tool.Name, AgentName, ex.Message);

                        chatMessages.Add(new ChatMessage(ChatRole.Tool,
                            [new FunctionResultContent(toolCall.CallId,
                                $"Error executing tool: {ex.Message}")]));

                        toolsExecuted.Add(new ToolExecutionResult
                        {
                            ToolName = tool.Name,
                            Success = false,
                            Result = ex.Message,
                            ExecutionTimeMs = toolStopwatch.ElapsedMilliseconds
                        });
                    }
                }
            }

            // Max rounds exceeded — return summary
            stopwatch.Stop();
            Logger.LogWarning(
                "AI processing hit max rounds ({MaxRounds}) for agent {AgentName} in {ElapsedMs}ms",
                maxRounds, AgentName, stopwatch.ElapsedMilliseconds);

            return new AgentResponse
            {
                Success = true,
                Response = $"I executed {toolsExecuted.Count} tool operations but reached the maximum processing rounds ({maxRounds}). " +
                           $"Here's what was completed: {string.Join("; ", toolsExecuted.Where(t => t.Success).Select(t => t.ToolName))}",
                AgentName = AgentName,
                ToolsExecuted = toolsExecuted,
                ProcessingTimeMs = stopwatch.ElapsedMilliseconds
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            Logger.LogError(ex,
                "AI processing failed for agent {AgentName} after {ElapsedMs}ms, falling back to deterministic routing: {Error}",
                AgentName, stopwatch.ElapsedMilliseconds, ex.Message);
            return null;
        }
    }

    /// <summary>
    /// Builds the chat message context for an LLM call: system prompt + conversation history + user message.
    /// </summary>
    private List<ChatMessage> BuildChatContext(string message, AgentConversationContext context)
    {
        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, GetSystemPrompt())
        };

        // Add conversation history
        foreach (var (role, content) in context.MessageHistory)
        {
            var chatRole = role.Equals("user", StringComparison.OrdinalIgnoreCase)
                ? ChatRole.User
                : ChatRole.Assistant;
            messages.Add(new ChatMessage(chatRole, content));
        }

        // Add current user message
        messages.Add(new ChatMessage(ChatRole.User, message));

        return messages;
    }

    /// <summary>
    /// Maximum number of tools that can be sent in a single Azure OpenAI request.
    /// The API returns HTTP 400 if this limit is exceeded (array_above_max_length).
    /// </summary>
    private const int MaxToolsPerRequest = 128;

    /// <summary>
    /// Tool category prefixes mapped to message keywords.
    /// When tool count exceeds <see cref="MaxToolsPerRequest"/>, only categories
    /// matching the user's message are included, keeping the request within limits
    /// and improving LLM focus.
    /// </summary>
    private static readonly Dictionary<string, string[]> ToolCategoryKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        // Kanban / task board tools
        ["kanban_"] = new[] { "kanban", "board", "task", "sprint", "ticket", "assign", "backlog", "column" },
        // CAC / PIV authentication tools
        ["cac_"] = new[] { "cac", "piv", "certificate", "smart card", "smartcard", "sign out", "signout" },
        // Privileged Identity Management tools
        ["pim_"] = new[] { "pim", "privileged", "eligible", "activate role", "deactivate role", "role assignment", "approval" },
        // Just-in-Time access tools
        ["jit_"] = new[] { "jit", "just-in-time", "just in time", "access request", "session", "revoke access" },
        // Continuous monitoring / watch tools
        ["watch_"] = new[] { "watch", "alert", "monitor", "rule", "suppress", "notification", "escalation", "quiet hour", "trend", "auto-remediation", "auto remediation" },
    };

    /// <summary>
    /// Prefixes for tools that are always included regardless of message content.
    /// These are core compliance tools essential for general RMF/ATO workflows.
    /// </summary>
    private static readonly string[] AlwaysIncludePrefixes = new[]
    {
        "compliance_", "assessment_", "control_", "document_", "evidence_",
        "remediation_", "audit_", "nist_", "rmf_", "conmon_", "emass_",
        "system_", "poam_", "ssp_", "ato_", "authorization_", "categorize_",
        "role_", "narrative_", "boundary_",
    };

    /// <summary>
    /// Converts registered BaseTool instances into AITool definitions for the LLM.
    /// Creates ToolAIFunction wrappers that provide Azure OpenAI-compliant
    /// schemas (with additionalProperties: false) from tool metadata.
    /// When the total tool count exceeds <see cref="MaxToolsPerRequest"/>,
    /// selects relevant tools based on the user's message keywords.
    /// </summary>
    private List<AITool> BuildToolDefinitions(string? message = null)
    {
        var selectedTools = Tools.Count <= MaxToolsPerRequest
            ? Tools
            : SelectToolsForMessage(message);

        return selectedTools.Select(tool => (AITool)new ToolAIFunction(tool)).ToList();
    }

    /// <summary>
    /// Selects a subset of tools relevant to the user's message.
    /// Always includes core compliance tools; adds category-specific tools
    /// only when the message matches their keywords. Falls back to
    /// truncating at <see cref="MaxToolsPerRequest"/> if still over limit.
    /// </summary>
    private List<BaseTool> SelectToolsForMessage(string? message)
    {
        var lowerMessage = message?.ToLowerInvariant() ?? string.Empty;
        var selected = new List<BaseTool>();
        var addedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Phase 1: Always include core compliance tools
        foreach (var tool in Tools)
        {
            var name = tool.Name.ToLowerInvariant();
            var isCore = Array.Exists(AlwaysIncludePrefixes,
                prefix => name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));

            // Also include tools that don't match ANY category prefix (uncategorized = core)
            if (!isCore)
            {
                var matchesAnyCategory = ToolCategoryKeywords.Keys.Any(
                    prefix => name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
                if (!matchesAnyCategory)
                    isCore = true;
            }

            if (isCore && addedNames.Add(tool.Name))
                selected.Add(tool);
        }

        // Phase 2: Add category-specific tools when message matches keywords
        foreach (var (prefix, keywords) in ToolCategoryKeywords)
        {
            var categoryMatches = keywords.Any(kw =>
                lowerMessage.Contains(kw, StringComparison.OrdinalIgnoreCase));

            if (!categoryMatches) continue;

            foreach (var tool in Tools)
            {
                if (tool.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) &&
                    addedNames.Add(tool.Name))
                {
                    selected.Add(tool);
                }
            }
        }

        Logger.LogInformation(
            "Tool selection: {SelectedCount}/{TotalCount} tools selected for message (max {Max})",
            selected.Count, Tools.Count, MaxToolsPerRequest);

        // Phase 3: Hard cap — if still over limit, truncate
        if (selected.Count > MaxToolsPerRequest)
        {
            Logger.LogWarning(
                "Tool selection still exceeds limit ({Count}/{Max}), truncating",
                selected.Count, MaxToolsPerRequest);
            selected = selected.Take(MaxToolsPerRequest).ToList();
        }

        return selected;
    }
}

/// <summary>
/// Wraps a <see cref="BaseTool"/> as an <see cref="AIFunction"/> with an
/// Azure OpenAI-compliant JSON schema (additionalProperties: false).
/// </summary>
internal sealed class ToolAIFunction : AIFunction
{
    private readonly BaseTool _tool;
    private readonly JsonElement _schema;

    public ToolAIFunction(BaseTool tool)
    {
        _tool = tool;
        _schema = BuildSchema();
    }

    public override string Name => _tool.Name;
    public override string Description => _tool.Description;
    public override JsonElement JsonSchema => _schema;

    protected override async ValueTask<object?> InvokeCoreAsync(
        AIFunctionArguments arguments,
        CancellationToken cancellationToken)
    {
        var args = new Dictionary<string, object?>();
        foreach (var kvp in arguments)
            args[kvp.Key] = kvp.Value;

        return await _tool.ExecuteAsync(args!, cancellationToken);
    }

    private JsonElement BuildSchema()
    {
        var properties = new Dictionary<string, object>();
        var required = new List<string>();

        foreach (var param in _tool.Parameters)
        {
            var prop = new Dictionary<string, object>();
            var typeStr = param.Value.Type?.ToLowerInvariant() switch
            {
                "integer" or "int" => "integer",
                "number" or "double" or "float" => "number",
                "boolean" or "bool" => "boolean",
                "array" => "array",
                _ => "string"
            };

            prop["type"] = typeStr;
            prop["description"] = param.Value.Description ?? param.Key;

            // Azure OpenAI requires array schemas to have an "items" definition
            if (typeStr == "array")
            {
                prop["items"] = new Dictionary<string, object> { ["type"] = "string" };
            }

            properties[param.Key] = prop;

            // Azure OpenAI strict mode requires ALL properties in `required`
            required.Add(param.Key);
        }

        var schema = new Dictionary<string, object>
        {
            ["type"] = "object",
            ["properties"] = properties,
            ["required"] = required,
            ["additionalProperties"] = false
        };

        return System.Text.Json.JsonSerializer.SerializeToElement(schema);
    }
}

/// <summary>
/// Response from an agent processing operation
/// </summary>
public class AgentResponse
{
    public bool Success { get; set; }
    public string Response { get; set; } = string.Empty;
    public string AgentName { get; set; } = string.Empty;
    public List<ToolExecutionResult> ToolsExecuted { get; set; } = new();
    public double ProcessingTimeMs { get; set; }

    /// <summary>
    /// Suggested follow-up actions the UI can present as clickable buttons.
    /// Each action has a display title and a pre-filled prompt sent when clicked.
    /// Populated by agents based on the result context (e.g., failing scan → "Generate remediation plan").
    /// </summary>
    public List<AgentSuggestedAction> Suggestions { get; set; } = new();

    /// <summary>
    /// Indicates whether the agent needs additional information from the user to complete the request.
    /// When true, <see cref="FollowUpPrompt"/> and/or <see cref="MissingFields"/> should be populated.
    /// </summary>
    public bool RequiresFollowUp { get; set; }

    /// <summary>
    /// A human-readable prompt asking the user for missing information when <see cref="RequiresFollowUp"/> is true.
    /// </summary>
    public string? FollowUpPrompt { get; set; }

    /// <summary>
    /// List of field names or descriptions that the agent needs from the user to proceed.
    /// Used by the UI to render input fields or quick-reply buttons.
    /// </summary>
    public List<string> MissingFields { get; set; } = new();

    /// <summary>
    /// Intent-specific structured data payload (e.g., assessment results, finding details, kanban board).
    /// The <c>type</c> key in the dictionary determines the Adaptive Card routing on the client side.
    /// </summary>
    public Dictionary<string, object>? ResponseData { get; set; }
}

/// <summary>
/// Result of a tool execution
/// </summary>
public class ToolExecutionResult
{
    public string ToolName { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string Result { get; set; } = string.Empty;
    public double ExecutionTimeMs { get; set; }
}

/// <summary>
/// Conversation context passed to agents
/// </summary>
public class AgentConversationContext
{
    public string ConversationId { get; set; } = Guid.NewGuid().ToString();
    public string UserId { get; set; } = string.Empty;
    public List<(string Role, string Content)> MessageHistory { get; set; } = new();
    public Dictionary<string, object> WorkflowState { get; set; } = new();

    public void AddMessage(string content, bool isUser = true)
    {
        MessageHistory.Add((isUser ? "user" : "assistant", content));
    }
}

/// <summary>
/// A suggested follow-up action with a display title and a pre-filled prompt.
/// Serialized as <c>{ "title": "…", "prompt": "…" }</c> for the frontend.
/// </summary>
public class AgentSuggestedAction
{
    /// <summary>Short display label shown on the button.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Pre-filled prompt text sent when the user clicks the button.</summary>
    public string Prompt { get; set; } = string.Empty;

    /// <summary>Creates a new suggested action.</summary>
    public AgentSuggestedAction() { }

    /// <summary>Creates a new suggested action with the given title and prompt.</summary>
    public AgentSuggestedAction(string title, string prompt)
    {
        Title = title;
        Prompt = prompt;
    }

    /// <summary>Creates a suggested action where the title is also used as the prompt.</summary>
    public AgentSuggestedAction(string title) : this(title, title) { }
}
