using Ato.Copilot.Core.Models.Compliance;

namespace Ato.Copilot.Core.Interfaces.Compliance;

/// <summary>
/// Executes remediation scripts with timeout, retry, and sanitization gates.
/// </summary>
public interface IRemediationScriptExecutor
{
    /// <summary>
    /// Executes a remediation script against the target resource.
    /// Applies sanitization check before execution, enforces timeout and retry limits.
    /// </summary>
    /// <param name="script">Script to execute</param>
    /// <param name="findingId">Finding being remediated</param>
    /// <param name="options">Execution options</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Execution result with status, duration, and changes applied</returns>
    Task<RemediationExecution> ExecuteScriptAsync(
        RemediationScript script,
        string findingId,
        RemediationExecutionOptions options,
        CancellationToken ct = default);
}
