namespace Ato.Copilot.Core.Models.Compliance;

// ───────────────────────────── Knowledge Query Types (Feature 010) ─────────────────────────────

/// <summary>
/// Classification of incoming knowledge queries used by KnowledgeBaseAgent.AnalyzeQueryType().
/// Determines which tool is dispatched for a given user message.
/// </summary>
public enum KnowledgeQueryType
{
    /// <summary>Specific NIST 800-53 control explanation (e.g., "What is AC-2?").</summary>
    NistControl,

    /// <summary>Search NIST controls by keyword (e.g., "Find controls about encryption").</summary>
    NistSearch,

    /// <summary>Specific STIG control explanation (e.g., "Explain STIG V-12345").</summary>
    Stig,

    /// <summary>Search STIGs by keyword/severity (e.g., "Show high severity STIGs").</summary>
    StigSearch,

    /// <summary>RMF process explanation (e.g., "Explain the RMF process").</summary>
    Rmf,

    /// <summary>DoD Impact Level explanation (e.g., "What is IL5?").</summary>
    ImpactLevel,

    /// <summary>FedRAMP template guidance (e.g., "Show me POA&amp;M template").</summary>
    FedRamp,

    /// <summary>General knowledge query that doesn't match a specific domain.</summary>
    GeneralKnowledge
}
