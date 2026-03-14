using System.Diagnostics;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Ato.Copilot.Core.Configuration;
using Ato.Copilot.Core.Interfaces.Compliance;
using Ato.Copilot.Core.Interfaces.Kanban;
using Ato.Copilot.Core.Models.Compliance;
using Ato.Copilot.Core.Models.Kanban;

namespace Ato.Copilot.Agents.Compliance.Services;

/// <summary>
/// FeatureSpec: 012-task-enrichment
/// AI-powered task enrichment service that populates remediation scripts and validation criteria
/// on RemediationTask entities. Uses IRemediationEngine (AI-first → NIST fallback) for scripts
/// and a dedicated IChatClient prompt for validation criteria.
/// Registered as Scoped per research R5 (matches KanbanService lifetime).
/// </summary>
public class TaskEnrichmentService : ITaskEnrichmentService
{
    /// <summary>Static message for Informational severity tasks (Constitution VI — no magic values).</summary>
    internal const string InformationalRemediationMessage = "Informational finding — no remediation required";

    /// <summary>Static message for Informational severity task validation (Constitution VI — no magic values).</summary>
    internal const string InformationalValidationMessage = "STIG reference — no validation required";

    /// <summary>Maximum concurrent AI calls for board enrichment (research R7).</summary>
    private const int MaxConcurrency = 5;

    /// <summary>Per-task enrichment timeout in seconds (research R7).</summary>
    private const int PerTaskTimeoutSeconds = 30;

    /// <summary>Maximum length for remediation script content (matches DB column).</summary>
    private const int MaxScriptLength = 8000;

    /// <summary>Maximum length for validation criteria content (matches DB column).</summary>
    private const int MaxValidationCriteriaLength = 2000;

    /// <summary>Truncation marker appended when content exceeds column limits (CHK036/CHK037).</summary>
    private const string TruncationMarker = "\n<!-- Truncated -->";

    private readonly IRemediationEngine _remediationEngine;
    private readonly IAiRemediationPlanGenerator _aiGenerator;
    private readonly IChatClient? _chatClient;
    private readonly ILogger<TaskEnrichmentService> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="TaskEnrichmentService"/>.
    /// </summary>
    /// <param name="remediationEngine">AI-first → NIST-template fallback engine for script generation.</param>
    /// <param name="aiGenerator">AI plan generator — used for IsAvailable check (CHK013).</param>
    /// <param name="chatClient">Optional AI chat client for dedicated validation criteria prompt (per research R3).</param>
    /// <param name="logger">Structured logger (Constitution Principle V).</param>
    public TaskEnrichmentService(
        IRemediationEngine remediationEngine,
        IAiRemediationPlanGenerator aiGenerator,
        ILogger<TaskEnrichmentService> logger,
        IChatClient? chatClient = null)
    {
        _remediationEngine = remediationEngine;
        _aiGenerator = aiGenerator;
        _chatClient = chatClient;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<TaskEnrichmentResult> EnrichTaskAsync(
        RemediationTask task,
        ComplianceFinding? finding,
        ScriptType scriptType = ScriptType.AzureCli,
        bool force = false,
        CancellationToken ct = default)
    {
        var result = new TaskEnrichmentResult
        {
            TaskId = task.Id,
            TaskNumber = task.TaskNumber
        };

        // Step 1: Skip if already enriched and not forcing (data-model decision flow step 1)
        if (!string.IsNullOrEmpty(task.RemediationScript) && !force)
        {
            result.Skipped = true;
            result.GenerationMethod = "Skipped";
            _logger.LogDebug("Enrichment skipped for task {TaskNumber} — already has script (force={Force})",
                task.TaskNumber, force);
            return result;
        }

        // Step 2: Skip if no finding context (data-model decision flow step 2, research R4)
        if (finding == null)
        {
            result.Skipped = true;
            result.GenerationMethod = "Skipped";
            _logger.LogDebug("Enrichment skipped for task {TaskNumber} — no linked finding", task.TaskNumber);
            return result;
        }

        // Step 3: Informational severity — static strings, no AI call (research R9)
        if (finding.Severity == FindingSeverity.Informational)
        {
            task.RemediationScript = InformationalRemediationMessage;
            task.RemediationScriptType = null;
            task.ValidationCriteria = InformationalValidationMessage;

            result.ScriptGenerated = true;
            result.ValidationCriteriaGenerated = true;
            result.GenerationMethod = "Template";
            _logger.LogInformation("Task {TaskNumber} enriched with Informational static strings", task.TaskNumber);
            return result;
        }

        // Determine generation method before calling engine (CHK013)
        var generationMethod = _aiGenerator.IsAvailable ? "AI" : "Template";

        try
        {
            // Step 4: Generate remediation script via IRemediationEngine (AI-first → NIST fallback)
            var script = await _remediationEngine.GenerateRemediationScriptAsync(finding, scriptType, ct);

            // CHK039: Handle empty content — treat as failure, fall back to template
            if (string.IsNullOrWhiteSpace(script.Content))
            {
                _logger.LogWarning("Empty script content returned for task {TaskNumber}, treating as failure",
                    task.TaskNumber);
                throw new InvalidOperationException("RemediationEngine returned empty script content");
            }

            task.RemediationScript = TruncateIfNeeded(script.Content, MaxScriptLength);
            task.RemediationScriptType = scriptType.ToString();
            result.ScriptGenerated = true;
            result.ScriptType = scriptType.ToString();

            // Step 5: Generate validation criteria if null or forcing
            if (string.IsNullOrEmpty(task.ValidationCriteria) || force)
            {
                var validationCriteria = await GenerateValidationCriteriaAsync(finding, script.Content, ct);
                task.ValidationCriteria = TruncateIfNeeded(validationCriteria, MaxValidationCriteriaLength);
                result.ValidationCriteriaGenerated = true;
            }

            result.GenerationMethod = generationMethod;

            _logger.LogInformation(
                "Task {TaskNumber} enriched via {GenerationMethod} — ControlId={ControlId}, ScriptType={ScriptType}",
                task.TaskNumber, generationMethod, finding.ControlId, scriptType);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Enrichment failed for task {TaskNumber}, error: {Error}",
                task.TaskNumber, ex.Message);
            result.Error = ex.Message;
            result.GenerationMethod = "Failed";
        }

        return result;
    }

    /// <inheritdoc />
    public async Task<BoardEnrichmentResult> EnrichBoardTasksAsync(
        RemediationBoard board,
        IReadOnlyList<ComplianceFinding> findings,
        IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var result = new BoardEnrichmentResult
        {
            BoardId = board.Id,
            TotalTasks = board.Tasks.Count
        };

        if (board.Tasks.Count == 0)
        {
            sw.Stop();
            result.Duration = sw.Elapsed;
            return result;
        }

        // Build findingId → finding lookup
        var findingLookup = findings
            .Where(f => !string.IsNullOrEmpty(f.Id))
            .ToDictionary(f => f.Id, f => f);

        // Process tasks with bounded concurrency (SemaphoreSlim per research R7)
        using var semaphore = new SemaphoreSlim(MaxConcurrency);
        var taskCount = board.Tasks.Count;
        var completed = 0;

        var enrichmentTasks = board.Tasks.Select(async (task, index) =>
        {
            await semaphore.WaitAsync(ct);
            try
            {
                // Per-task timeout (research R7)
                using var taskCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                taskCts.CancelAfter(TimeSpan.FromSeconds(PerTaskTimeoutSeconds));

                // Resolve finding for this task
                ComplianceFinding? finding = null;
                if (!string.IsNullOrEmpty(task.FindingId) && findingLookup.TryGetValue(task.FindingId, out var f))
                {
                    finding = f;
                }

                var enrichResult = await EnrichTaskAsync(task, finding, ct: taskCts.Token);

                var current = Interlocked.Increment(ref completed);
                progress?.Report($"Enriching task {current}/{taskCount}: {task.TaskNumber} ({task.ControlId})...");

                return enrichResult;
            }
            catch (OperationCanceledException)
            {
                Interlocked.Increment(ref completed);
                return new TaskEnrichmentResult
                {
                    TaskId = task.Id,
                    TaskNumber = task.TaskNumber,
                    Error = "Enrichment timed out",
                    GenerationMethod = "Failed"
                };
            }
            finally
            {
                semaphore.Release();
            }
        }).ToArray();

        var results = await Task.WhenAll(enrichmentTasks);

        foreach (var r in results)
        {
            result.Results.Add(r);
            if (r.Skipped)
                result.TasksSkipped++;
            else if (r.Error != null)
                result.TasksFailed++;
            else
                result.TasksEnriched++;
        }

        sw.Stop();
        result.Duration = sw.Elapsed;

        _logger.LogInformation(
            "Board {BoardId} enrichment complete: {Enriched} enriched, {Skipped} skipped, {Failed} failed, {TotalDurationMs}ms",
            board.Id, result.TasksEnriched, result.TasksSkipped, result.TasksFailed,
            (int)result.Duration.TotalMilliseconds);

        return result;
    }

    /// <inheritdoc />
    public async Task<string> GenerateValidationCriteriaAsync(
        ComplianceFinding finding,
        string? scriptContent = null,
        CancellationToken ct = default)
    {
        // AI path: dedicated prompt via IChatClient (per research R3 — NOT GetGuidanceAsync)
        if (_chatClient != null && _aiGenerator.IsAvailable)
        {
            try
            {
                var prompt = BuildValidationPrompt(finding, scriptContent);
                var response = await _chatClient.GetResponseAsync(prompt, cancellationToken: ct);

                var responseText = response.Text?.Trim();
                if (!string.IsNullOrWhiteSpace(responseText))
                {
                    return responseText;
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "AI validation criteria generation failed for {ControlId}, using template",
                    finding.ControlId);
            }
        }

        // Template fallback
        return BuildValidationTemplate(finding);
    }

    /// <summary>
    /// Builds an AI prompt for generating validation criteria.
    /// </summary>
    private static string BuildValidationPrompt(ComplianceFinding finding, string? scriptContent)
    {
        var prompt = $"""
            Generate 2-3 concise validation steps to verify that the following compliance remediation was applied correctly.

            **Control**: {finding.ControlId} ({finding.ControlFamily})
            **Finding**: {finding.Title}
            **Severity**: {finding.Severity}
            **Resource**: {finding.ResourceId}
            """;

        if (!string.IsNullOrWhiteSpace(scriptContent))
        {
            // Limit script context to first 2000 chars for token efficiency
            var truncatedScript = scriptContent.Length > 2000
                ? scriptContent[..2000] + "..."
                : scriptContent;
            prompt += $"\n\n**Remediation Script**:\n```\n{truncatedScript}\n```";
        }

        prompt += """

            Respond with numbered validation steps only. Be specific to the control and resource.
            Include CLI commands where applicable (Azure CLI preferred for Azure Gov).
            """;

        return prompt;
    }

    /// <summary>
    /// Builds a deterministic template-based validation criteria string.
    /// </summary>
    private static string BuildValidationTemplate(ComplianceFinding finding)
    {
        return $"1. Re-scan {finding.ResourceId} for control {finding.ControlId}\n" +
               $"2. Verify finding status changed to Remediated\n" +
               $"3. Confirm no new non-compliance alerts for {finding.ControlFamily} family";
    }

    /// <summary>
    /// Truncates content to the specified max length with a truncation marker (CHK036/CHK037).
    /// </summary>
    private static string TruncateIfNeeded(string content, int maxLength)
    {
        if (content.Length <= maxLength)
            return content;

        var truncatedLength = maxLength - TruncationMarker.Length;
        return content[..truncatedLength] + TruncationMarker;
    }
}
