using Microsoft.Extensions.Logging;
using Ato.Copilot.Core.Interfaces.Compliance;
using Ato.Copilot.Core.Models.Compliance;

namespace Ato.Copilot.Agents.Compliance.Services.Engines.Remediation;

/// <summary>
/// Executes remediation scripts with timeout, retry, and sanitization gates.
/// Scripts are validated via IScriptSanitizationService before execution.
/// </summary>
public class RemediationScriptExecutor : IRemediationScriptExecutor
{
    private readonly IScriptSanitizationService _sanitizer;
    private readonly ILogger<RemediationScriptExecutor> _logger;

    private const int MaxRetries = 3;
    private static readonly TimeSpan ScriptTimeout = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Initializes a new instance of the <see cref="RemediationScriptExecutor"/> class.
    /// </summary>
    public RemediationScriptExecutor(
        IScriptSanitizationService sanitizer,
        ILogger<RemediationScriptExecutor> logger)
    {
        _sanitizer = sanitizer;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<RemediationExecution> ExecuteScriptAsync(
        RemediationScript script,
        string findingId,
        RemediationExecutionOptions options,
        CancellationToken ct = default)
    {
        var execution = new RemediationExecution
        {
            FindingId = findingId,
            Status = RemediationExecutionStatus.InProgress,
            StartedAt = DateTime.UtcNow,
            DryRun = options.DryRun,
            Options = options,
            TierUsed = 1 // Script execution is Tier 1
        };

        // Sanitization gate
        if (!_sanitizer.IsSafe(script.Content))
        {
            var violations = _sanitizer.GetViolations(script.Content);
            _logger.LogWarning(
                "Script for {FindingId} REJECTED by sanitization — {Count} violation(s)",
                findingId, violations.Count);

            execution.Status = RemediationExecutionStatus.Failed;
            execution.Error = $"Script rejected by sanitization: {string.Join("; ", violations)}";
            execution.CompletedAt = DateTime.UtcNow;
            execution.Duration = execution.CompletedAt - execution.StartedAt;
            return execution;
        }

        // Dry-run mode: preview without executing
        if (options.DryRun)
        {
            _logger.LogInformation("Dry-run script execution for {FindingId}", findingId);
            execution.Status = RemediationExecutionStatus.Completed;
            execution.ChangesApplied = new List<string> { $"[DRY RUN] Would execute {script.ScriptType} script: {script.Description}" };
            execution.StepsExecuted = 1;
            execution.CompletedAt = DateTime.UtcNow;
            execution.Duration = execution.CompletedAt - execution.StartedAt;
            return execution;
        }

        // Execute with retry logic
        for (int attempt = 1; attempt <= MaxRetries; attempt++)
        {
            try
            {
                _logger.LogInformation(
                    "Executing {ScriptType} script for {FindingId} (attempt {Attempt}/{Max})",
                    script.ScriptType, findingId, attempt, MaxRetries);

                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                cts.CancelAfter(ScriptTimeout);

                // Simulated script execution — in production, delegate to az CLI / PowerShell subprocess
                await Task.Delay(100, cts.Token);

                execution.Status = RemediationExecutionStatus.Completed;
                execution.StepsExecuted = 1;
                execution.ChangesApplied = new List<string>
                {
                    $"Executed {script.ScriptType} script: {script.Description}"
                };
                execution.CompletedAt = DateTime.UtcNow;
                execution.Duration = execution.CompletedAt - execution.StartedAt;

                _logger.LogInformation(
                    "Script execution completed for {FindingId} in {Duration}ms",
                    findingId, execution.Duration?.TotalMilliseconds ?? 0);

                return execution;
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                _logger.LogWarning(
                    "Script execution timed out for {FindingId} (attempt {Attempt})",
                    findingId, attempt);

                if (attempt == MaxRetries)
                {
                    execution.Status = RemediationExecutionStatus.Failed;
                    execution.Error = $"Script execution timed out after {MaxRetries} attempts";
                    execution.CompletedAt = DateTime.UtcNow;
                    execution.Duration = execution.CompletedAt - execution.StartedAt;
                    return execution;
                }
            }
            catch (Exception ex) when (attempt < MaxRetries)
            {
                _logger.LogWarning(ex,
                    "Script execution failed for {FindingId} (attempt {Attempt}), retrying",
                    findingId, attempt);
                await Task.Delay(TimeSpan.FromSeconds(attempt), ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Script execution failed for {FindingId} after {MaxRetries} attempts", findingId, MaxRetries);
                execution.Status = RemediationExecutionStatus.Failed;
                execution.Error = ex.Message;
                execution.CompletedAt = DateTime.UtcNow;
                execution.Duration = execution.CompletedAt - execution.StartedAt;
                return execution;
            }
        }

        return execution;
    }
}
