namespace Ato.Copilot.Core.Models.Compliance;

/// <summary>
/// AI-prioritized finding with business context.
/// </summary>
public class PrioritizedFinding
{
    /// <summary>The compliance finding.</summary>
    public ComplianceFinding Finding { get; set; } = null!;

    /// <summary>AI-assigned priority.</summary>
    public RemediationPriority AiPriority { get; set; }

    /// <summary>Why this priority was assigned.</summary>
    public string Justification { get; set; } = string.Empty;

    /// <summary>Business impact assessment.</summary>
    public string BusinessImpact { get; set; } = string.Empty;

    /// <summary>Severity-based priority before AI adjustment.</summary>
    public RemediationPriority OriginalPriority { get; set; }
}
