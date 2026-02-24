using Ato.Copilot.Core.Models.Compliance;

namespace Ato.Copilot.Core.Interfaces.Compliance;

/// <summary>
/// Tier 2 structured remediation service. Applies predefined remediation templates
/// for known compliance patterns (property-correction logic for supported RemediationType values).
/// </summary>
public interface IComplianceRemediationService
{
    /// <summary>
    /// Executes structured remediation for a finding using predefined templates.
    /// </summary>
    /// <param name="finding">Finding to remediate</param>
    /// <param name="options">Execution options</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Execution result with changes applied</returns>
    Task<RemediationExecution> ExecuteStructuredRemediationAsync(
        ComplianceFinding finding,
        RemediationExecutionOptions options,
        CancellationToken ct = default);

    /// <summary>
    /// Determines whether this service can handle the finding's remediation type.
    /// Returns true when the finding's RemediationType matches a supported template.
    /// </summary>
    /// <param name="finding">Finding to check</param>
    /// <returns>True if a predefined template exists for this finding type</returns>
    bool CanHandle(ComplianceFinding finding);
}
