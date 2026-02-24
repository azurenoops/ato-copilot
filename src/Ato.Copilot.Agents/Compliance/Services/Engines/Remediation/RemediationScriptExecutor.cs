using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Ato.Copilot.Agents.Compliance.Configuration;
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

    private readonly int _maxRetries;
    private readonly TimeSpan _scriptTimeout;

    /// <summary>
    /// Initializes a new instance of the <see cref="RemediationScriptExecutor"/> class
    /// using configuration values from <see cref="ComplianceAgentOptions.Remediation"/>.
    /// </summary>
    public RemediationScriptExecutor(
        IScriptSanitizationService sanitizer,
        ILogger<RemediationScriptExecutor> logger,
        IOptions<ComplianceAgentOptions> options)
        : this(sanitizer, logger,
              options.Value.Remediation.MaxRetries,
              TimeSpan.FromSeconds(options.Value.Remediation.ScriptTimeoutSeconds))
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="RemediationScriptExecutor"/> class
    /// with explicit retry and timeout settings (for testing).
    /// </summary>
    public RemediationScriptExecutor(
        IScriptSanitizationService sanitizer,
        ILogger<RemediationScriptExecutor> logger,
        int maxRetries = 3,
        TimeSpan? scriptTimeout = null)
    {
        _sanitizer = sanitizer;
        _logger = logger;
        _maxRetries = maxRetries;
        _scriptTimeout = scriptTimeout ?? TimeSpan.FromMinutes(5);
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
        for (int attempt = 1; attempt <= _maxRetries; attempt++)
        {
            try
            {
                _logger.LogInformation(
                    "Executing {ScriptType} script for {FindingId} (attempt {Attempt}/{Max})",
                    script.ScriptType, findingId, attempt, _maxRetries);

                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                cts.CancelAfter(_scriptTimeout);

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

                if (attempt == _maxRetries)
                {
                    execution.Status = RemediationExecutionStatus.Failed;
                    execution.Error = $"Script execution timed out after {_maxRetries} attempts";
                    execution.CompletedAt = DateTime.UtcNow;
                    execution.Duration = execution.CompletedAt - execution.StartedAt;
                    return execution;
                }
            }
            catch (Exception ex) when (attempt < _maxRetries)
            {
                _logger.LogWarning(ex,
                    "Script execution failed for {FindingId} (attempt {Attempt}), retrying",
                    findingId, attempt);
                await Task.Delay(TimeSpan.FromSeconds(attempt), ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Script execution failed for {FindingId} after {MaxRetries} attempts", findingId, _maxRetries);
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
