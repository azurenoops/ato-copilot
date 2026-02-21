using Microsoft.Extensions.Logging;

namespace Ato.Copilot.Agents.Common;

/// <summary>
/// Base class for all agents in the ATO Copilot.
/// All agents MUST extend this class (Constitution Principle II).
/// </summary>
public abstract class BaseAgent
{
    protected readonly ILogger Logger;

    protected BaseAgent(ILogger logger)
    {
        Logger = logger;
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
    /// Process a user message through this agent
    /// </summary>
    public abstract Task<AgentResponse> ProcessAsync(
        string message,
        AgentConversationContext context,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Registered tools for this agent
    /// </summary>
    protected List<BaseTool> Tools { get; } = new();

    /// <summary>
    /// Register a tool for use by this agent
    /// </summary>
    protected void RegisterTool(BaseTool tool)
    {
        Tools.Add(tool);
        Logger.LogDebug("Registered tool {ToolName} for agent {AgentName}", tool.Name, AgentName);
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
