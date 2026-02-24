using Ato.Copilot.Core.Models.Compliance;

namespace Ato.Copilot.Core.Interfaces.Compliance;

/// <summary>
/// Azure ARM resource operations for remediation: snapshot capture,
/// legacy ARM remediation execution, and snapshot-based rollback.
/// </summary>
public interface IAzureArmRemediationService
{
    /// <summary>
    /// Captures a JSON snapshot of an Azure resource's current state for rollback.
    /// </summary>
    /// <param name="resourceId">Azure resource ID</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>JSON snapshot of the resource, or null if capture fails</returns>
    Task<string?> CaptureResourceSnapshotAsync(
        string resourceId,
        CancellationToken ct = default);

    /// <summary>
    /// Executes a legacy ARM remediation operation against a finding's resource.
    /// Supports 8 operations: TLS, diagnostics, alerts, log retention, encryption,
    /// NSG, policy assignment, HTTPS enforcement.
    /// </summary>
    /// <param name="finding">Finding to remediate</param>
    /// <param name="options">Execution options</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Execution result with changes applied</returns>
    Task<RemediationExecution> ExecuteArmRemediationAsync(
        ComplianceFinding finding,
        RemediationExecutionOptions options,
        CancellationToken ct = default);

    /// <summary>
    /// Restores a resource to a previously captured snapshot state.
    /// </summary>
    /// <param name="resourceId">Azure resource ID to restore</param>
    /// <param name="snapshotJson">JSON snapshot to restore from</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Rollback result</returns>
    Task<RemediationRollbackResult> RestoreFromSnapshotAsync(
        string resourceId,
        string snapshotJson,
        CancellationToken ct = default);
}
