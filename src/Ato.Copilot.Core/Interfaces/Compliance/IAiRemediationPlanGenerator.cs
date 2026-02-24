using Ato.Copilot.Core.Models.Compliance;

namespace Ato.Copilot.Core.Interfaces.Compliance;

/// <summary>
/// AI-powered remediation plan generation service.
/// Uses IChatClient for script generation, guidance, and prioritization.
/// Returns null/fallback results when AI is unavailable.
/// </summary>
public interface IAiRemediationPlanGenerator
{
    /// <summary>
    /// Whether an AI model is available for enhanced operations.
    /// Returns false when IChatClient is null or not configured.
    /// </summary>
    bool IsAvailable { get; }

    /// <summary>
    /// Generates a remediation script for a finding using AI.
    /// </summary>
    /// <param name="finding">Finding to generate script for</param>
    /// <param name="scriptType">Target script language (AzureCli, PowerShell, Bicep, Terraform)</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Generated script, or null if AI is unavailable</returns>
    Task<RemediationScript?> GenerateScriptAsync(
        ComplianceFinding finding,
        ScriptType scriptType,
        CancellationToken ct = default);

    /// <summary>
    /// Gets AI-enhanced remediation guidance for a finding.
    /// </summary>
    /// <param name="finding">Finding to get guidance for</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>AI guidance with confidence score, or null if AI is unavailable</returns>
    Task<RemediationGuidance?> GetGuidanceAsync(
        ComplianceFinding finding,
        CancellationToken ct = default);

    /// <summary>
    /// Prioritizes findings using AI with optional business context.
    /// </summary>
    /// <param name="findings">Findings to prioritize</param>
    /// <param name="businessContext">Optional business context for smarter prioritization</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Prioritized findings with AI-assigned priority and justification</returns>
    Task<List<PrioritizedFinding>> PrioritizeAsync(
        IEnumerable<ComplianceFinding> findings,
        string? businessContext,
        CancellationToken ct = default);

    /// <summary>
    /// Generates an enhanced remediation plan for a single finding using AI.
    /// </summary>
    /// <param name="finding">Finding to plan for</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Enhanced plan, or null if AI is unavailable</returns>
    Task<RemediationPlan?> GenerateEnhancedPlanAsync(
        ComplianceFinding finding,
        CancellationToken ct = default);
}
