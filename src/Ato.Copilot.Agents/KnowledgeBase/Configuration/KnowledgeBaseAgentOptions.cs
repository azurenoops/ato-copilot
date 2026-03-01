namespace Ato.Copilot.Agents.KnowledgeBase.Configuration;

/// <summary>
/// Configuration options for the KnowledgeBase Agent.
/// Bound to the "AgentConfiguration:KnowledgeBaseAgent" configuration section.
/// </summary>
public class KnowledgeBaseAgentOptions
{
    /// <summary>
    /// Whether the KnowledgeBase Agent participates in orchestrator routing.
    /// When false, the agent is registered but not aliased as BaseAgent.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Maximum tokens for AI model responses.
    /// </summary>
    public int MaxTokens { get; set; } = 4096;

    /// <summary>
    /// AI model temperature setting (lower = more deterministic).
    /// </summary>
    public double Temperature { get; set; } = 0.3;

    /// <summary>
    /// AI model identifier used for knowledge queries.
    /// </summary>
    public string ModelName { get; set; } = "gpt-4o";

    /// <summary>
    /// Tool-level cache TTL in minutes. Default: 60 minutes.
    /// Service-level data uses a separate 24-hour TTL.
    /// </summary>
    public int CacheDurationMinutes { get; set; } = 60;

    /// <summary>
    /// Relative path from AppContext.BaseDirectory to JSON data files.
    /// </summary>
    public string KnowledgeBasePath { get; set; } = "KnowledgeBase/Data";

    /// <summary>
    /// Default Azure subscription ID for context-aware responses.
    /// </summary>
    public string DefaultSubscriptionId { get; set; } = "";

    /// <summary>
    /// Minimum confidence threshold for orchestrator routing.
    /// Queries scoring below this threshold receive a graceful fallback response.
    /// </summary>
    public double MinimumConfidenceThreshold { get; set; } = 0.3;
}
