using Microsoft.Extensions.Logging;
using Ato.Copilot.Core.Interfaces.Compliance;
using Ato.Copilot.Core.Models.Compliance;

namespace Ato.Copilot.Agents.Compliance.Services.Engines.Remediation;

/// <summary>
/// Tier 2 structured remediation service. Applies predefined remediation
/// templates for known compliance patterns. Delegates to
/// IRemediationScriptExecutor for script-based remediation.
/// </summary>
public class ComplianceRemediationService : IComplianceRemediationService
{
    private readonly IRemediationScriptExecutor _scriptExecutor;
    private readonly ILogger<ComplianceRemediationService> _logger;

    // Supported remediation types with predefined templates
    private static readonly HashSet<RemediationType> SupportedTypes = new()
    {
        RemediationType.ResourceConfiguration,
        RemediationType.PolicyAssignment,
        RemediationType.PolicyRemediation
    };

    /// <summary>
    /// Initializes a new instance of the <see cref="ComplianceRemediationService"/> class.
    /// </summary>
    public ComplianceRemediationService(
        IRemediationScriptExecutor scriptExecutor,
        ILogger<ComplianceRemediationService> logger)
    {
        _scriptExecutor = scriptExecutor;
        _logger = logger;
    }

    /// <inheritdoc />
    public bool CanHandle(ComplianceFinding finding)
    {
        var canHandle = SupportedTypes.Contains(finding.RemediationType);
        _logger.LogDebug(
            "CanHandle for {FindingId} ({RemType}): {Result}",
            finding.Id, finding.RemediationType, canHandle);
        return canHandle;
    }

    /// <inheritdoc />
    public async Task<RemediationExecution> ExecuteStructuredRemediationAsync(
        ComplianceFinding finding,
        RemediationExecutionOptions options,
        CancellationToken ct = default)
    {
        if (!CanHandle(finding))
        {
            _logger.LogWarning(
                "Unsupported remediation type {RemType} for {FindingId}",
                finding.RemediationType, finding.Id);

            return new RemediationExecution
            {
                FindingId = finding.Id,
                Status = RemediationExecutionStatus.Failed,
                Error = $"Unsupported remediation type: {finding.RemediationType}",
                TierUsed = 2,
                CompletedAt = DateTime.UtcNow
            };
        }

        _logger.LogInformation(
            "Executing structured remediation for {FindingId} ({RemType})",
            finding.Id, finding.RemediationType);

        // Generate a remediation script from the template
        var script = GenerateTemplateScript(finding);

        // Delegate to script executor for execution with retry/timeout
        var execution = await _scriptExecutor.ExecuteScriptAsync(script, finding.Id, options, ct);
        execution.TierUsed = 2; // Override tier — this is Tier 2

        _logger.LogInformation(
            "Structured remediation for {FindingId} completed with status {Status}",
            finding.Id, execution.Status);

        return execution;
    }

    // ─── Private Helpers ──────────────────────────────────────────────────────

    /// <summary>Generates a remediation script from a predefined template.</summary>
    private static RemediationScript GenerateTemplateScript(ComplianceFinding finding)
    {
        (string Content, string Description) template = finding.RemediationType switch
        {
            RemediationType.PolicyAssignment => (
                $"# Assign Azure Policy for {finding.ControlId}\n" +
                $"az policy assignment create --name 'remediate-{finding.ControlId.ToLowerInvariant()}' " +
                $"--scope '/subscriptions/{finding.SubscriptionId ?? "<subscription-id>"}' " +
                $"--policy '{finding.PolicyDefinitionId ?? "<policy-id>"}'",
                $"Policy assignment for {finding.ControlId}"
            ),
            RemediationType.PolicyRemediation => (
                $"# Start policy remediation task for {finding.ControlId}\n" +
                $"az policy remediation create --name 'remediate-{finding.ControlId.ToLowerInvariant()}' " +
                $"--policy-assignment '{finding.PolicyAssignmentId ?? "<assignment-id>"}' " +
                $"--resource-discovery-mode ExistingNonCompliant",
                $"Policy remediation for {finding.ControlId}"
            ),
            RemediationType.ResourceConfiguration => (
                !string.IsNullOrEmpty(finding.RemediationScript)
                    ? finding.RemediationScript
                    : $"# Configure resource for {finding.ControlId}\n" +
                      $"# Target: {finding.ResourceId}\n" +
                      $"# {finding.RemediationGuidance}",
                $"Resource configuration for {finding.ControlId}"
            ),
            _ => (
                $"# Remediate {finding.ControlId}\n# {finding.RemediationGuidance}",
                $"Remediation for {finding.ControlId}"
            )
        };

        return new RemediationScript
        {
            Content = template.Content,
            ScriptType = ScriptType.AzureCli,
            Description = template.Description,
            Parameters = new Dictionary<string, string>
            {
                ["resourceId"] = finding.ResourceId,
                ["controlId"] = finding.ControlId
            },
            EstimatedDuration = TimeSpan.FromMinutes(5),
            IsSanitized = false
        };
    }
}
