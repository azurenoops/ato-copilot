using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Ato.Copilot.Agents.Common;
using Ato.Copilot.Agents.Compliance.Tools;
using Ato.Copilot.Core.Data.Context;
using Ato.Copilot.Core.Models.Compliance;

namespace Ato.Copilot.Agents.Compliance.Agents;

/// <summary>
/// Compliance Agent - handles all NIST 800-53, FedRAMP, and ATO compliance operations.
/// Extends BaseAgent per Constitution Principle II.
/// </summary>
public class ComplianceAgent : BaseAgent
{
    private readonly ComplianceAssessmentTool _assessmentTool;
    private readonly ControlFamilyTool _controlFamilyTool;
    private readonly DocumentGenerationTool _documentGenerationTool;
    private readonly EvidenceCollectionTool _evidenceCollectionTool;
    private readonly RemediationExecuteTool _remediationTool;
    private readonly ValidateRemediationTool _validateRemediationTool;
    private readonly RemediationPlanTool _remediationPlanTool;
    private readonly AssessmentAuditLogTool _auditLogTool;
    private readonly ComplianceHistoryTool _historyTool;
    private readonly ComplianceStatusTool _statusTool;
    private readonly ComplianceMonitoringTool _monitoringTool;
    private readonly IDbContextFactory<AtoCopilotContext> _dbFactory;

    // Kanban tools (Phase 3–6)
    private readonly KanbanCreateBoardTool _kanbanCreateBoard;
    private readonly KanbanBoardShowTool _kanbanBoardShow;
    private readonly KanbanGetTaskTool _kanbanGetTask;
    private readonly KanbanCreateTaskTool _kanbanCreateTask;
    private readonly KanbanAssignTaskTool _kanbanAssignTask;
    private readonly KanbanMoveTaskTool _kanbanMoveTask;
    private readonly KanbanTaskListTool _kanbanTaskList;
    private readonly KanbanTaskHistoryTool _kanbanTaskHistory;
    private readonly KanbanValidateTaskTool _kanbanValidateTask;
    private readonly KanbanAddCommentTool _kanbanAddComment;
    private readonly KanbanTaskCommentsTool _kanbanTaskComments;
    private readonly KanbanEditCommentTool _kanbanEditComment;
    private readonly KanbanDeleteCommentTool _kanbanDeleteComment;
    private readonly KanbanRemediateTaskTool _kanbanRemediateTask;
    private readonly KanbanCollectEvidenceTool _kanbanCollectEvidence;
    private readonly KanbanBulkUpdateTool _kanbanBulkUpdate;
    private readonly KanbanExportTool _kanbanExport;
    private readonly KanbanArchiveBoardTool _kanbanArchiveBoard;

    /// <summary>
    /// Initializes a new instance of the <see cref="ComplianceAgent"/> class.
    /// </summary>
    public ComplianceAgent(
        ComplianceAssessmentTool assessmentTool,
        ControlFamilyTool controlFamilyTool,
        DocumentGenerationTool documentGenerationTool,
        EvidenceCollectionTool evidenceCollectionTool,
        RemediationExecuteTool remediationTool,
        ValidateRemediationTool validateRemediationTool,
        RemediationPlanTool remediationPlanTool,
        AssessmentAuditLogTool auditLogTool,
        ComplianceHistoryTool historyTool,
        ComplianceStatusTool statusTool,
        ComplianceMonitoringTool monitoringTool,
        KanbanCreateBoardTool kanbanCreateBoard,
        KanbanBoardShowTool kanbanBoardShow,
        KanbanGetTaskTool kanbanGetTask,
        KanbanCreateTaskTool kanbanCreateTask,
        KanbanAssignTaskTool kanbanAssignTask,
        KanbanMoveTaskTool kanbanMoveTask,
        KanbanTaskListTool kanbanTaskList,
        KanbanTaskHistoryTool kanbanTaskHistory,
        KanbanValidateTaskTool kanbanValidateTask,
        KanbanAddCommentTool kanbanAddComment,
        KanbanTaskCommentsTool kanbanTaskComments,
        KanbanEditCommentTool kanbanEditComment,
        KanbanDeleteCommentTool kanbanDeleteComment,
        KanbanRemediateTaskTool kanbanRemediateTask,
        KanbanCollectEvidenceTool kanbanCollectEvidence,
        KanbanBulkUpdateTool kanbanBulkUpdate,
        KanbanExportTool kanbanExport,
        KanbanArchiveBoardTool kanbanArchiveBoard,
        IDbContextFactory<AtoCopilotContext> dbFactory,
        ILogger<ComplianceAgent> logger)
        : base(logger)
    {
        _assessmentTool = assessmentTool;
        _controlFamilyTool = controlFamilyTool;
        _documentGenerationTool = documentGenerationTool;
        _evidenceCollectionTool = evidenceCollectionTool;
        _remediationTool = remediationTool;
        _validateRemediationTool = validateRemediationTool;
        _remediationPlanTool = remediationPlanTool;
        _auditLogTool = auditLogTool;
        _historyTool = historyTool;
        _statusTool = statusTool;
        _monitoringTool = monitoringTool;
        _kanbanCreateBoard = kanbanCreateBoard;
        _kanbanBoardShow = kanbanBoardShow;
        _kanbanGetTask = kanbanGetTask;
        _kanbanCreateTask = kanbanCreateTask;
        _kanbanAssignTask = kanbanAssignTask;
        _kanbanMoveTask = kanbanMoveTask;
        _kanbanTaskList = kanbanTaskList;
        _kanbanTaskHistory = kanbanTaskHistory;
        _kanbanValidateTask = kanbanValidateTask;
        _kanbanAddComment = kanbanAddComment;
        _kanbanTaskComments = kanbanTaskComments;
        _kanbanEditComment = kanbanEditComment;
        _kanbanDeleteComment = kanbanDeleteComment;
        _kanbanRemediateTask = kanbanRemediateTask;
        _kanbanCollectEvidence = kanbanCollectEvidence;
        _kanbanBulkUpdate = kanbanBulkUpdate;
        _kanbanExport = kanbanExport;
        _kanbanArchiveBoard = kanbanArchiveBoard;
        _dbFactory = dbFactory;

        // Register all tools per Constitution Principle II
        RegisterTool(_assessmentTool);
        RegisterTool(_controlFamilyTool);
        RegisterTool(_documentGenerationTool);
        RegisterTool(_evidenceCollectionTool);
        RegisterTool(_remediationTool);
        RegisterTool(_validateRemediationTool);
        RegisterTool(_remediationPlanTool);
        RegisterTool(_auditLogTool);
        RegisterTool(_historyTool);
        RegisterTool(_statusTool);
        RegisterTool(_monitoringTool);

        // Register Kanban tools
        RegisterTool(_kanbanCreateBoard);
        RegisterTool(_kanbanBoardShow);
        RegisterTool(_kanbanGetTask);
        RegisterTool(_kanbanCreateTask);
        RegisterTool(_kanbanAssignTask);
        RegisterTool(_kanbanMoveTask);
        RegisterTool(_kanbanTaskList);
        RegisterTool(_kanbanTaskHistory);
        RegisterTool(_kanbanValidateTask);
        RegisterTool(_kanbanAddComment);
        RegisterTool(_kanbanTaskComments);
        RegisterTool(_kanbanEditComment);
        RegisterTool(_kanbanDeleteComment);
        RegisterTool(_kanbanRemediateTask);
        RegisterTool(_kanbanCollectEvidence);
        RegisterTool(_kanbanBulkUpdate);
        RegisterTool(_kanbanExport);
        RegisterTool(_kanbanArchiveBoard);
    }

    /// <inheritdoc />
    public override string AgentId => "compliance-agent";
    /// <inheritdoc />
    public override string AgentName => "Compliance Agent";
    /// <inheritdoc />
    public override string Description => "Handles NIST 800-53, FedRAMP, and ATO compliance assessments, remediation, and documentation";

    /// <inheritdoc />
    public override string GetSystemPrompt()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = "Ato.Copilot.Agents.Compliance.Prompts.ComplianceAgent.prompt.txt";
        
        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream == null)
        {
            Logger.LogWarning("System prompt resource not found: {Resource}", resourceName);
            return "You are a compliance agent for Azure Government NIST 800-53 assessments.";
        }

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    /// <summary>
    /// Processes a compliance request, routing to the appropriate tool and logging the action.
    /// </summary>
    public override async Task<AgentResponse> ProcessAsync(
        string message,
        AgentConversationContext context,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        Logger.LogInformation("ComplianceAgent processing: {Message}", message[..Math.Min(100, message.Length)]);
        var actionType = ClassifyIntent(message);

        try
        {
            // Analyze intent and route to appropriate tool
            var toolResult = await RouteToToolAsync(message, context, cancellationToken);

            stopwatch.Stop();

            // Log successful action to audit trail
            await LogAuditEntryAsync(actionType, GetContextValue(context, "subscription_id"),
                AuditOutcome.Success, $"Processed: {message[..Math.Min(200, message.Length)]}",
                stopwatch.Elapsed, cancellationToken);

            return new AgentResponse
            {
                Success = true,
                Response = toolResult,
                AgentName = AgentName,
                ProcessingTimeMs = stopwatch.ElapsedMilliseconds
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            Logger.LogError(ex, "Error in ComplianceAgent processing");

            // Log failed action to audit trail
            await LogAuditEntryAsync(actionType, GetContextValue(context, "subscription_id"),
                AuditOutcome.Failure, $"Error: {ex.Message}", stopwatch.Elapsed, cancellationToken);

            return new AgentResponse
            {
                Success = false,
                Response = $"Error processing compliance request: {ex.Message}",
                AgentName = AgentName,
                ProcessingTimeMs = stopwatch.ElapsedMilliseconds
            };
        }
    }

    /// <summary>Routes a user message to the appropriate compliance tool based on intent analysis.</summary>
    private async Task<string> RouteToToolAsync(
        string message,
        AgentConversationContext context,
        CancellationToken cancellationToken)
    {
        var lowerMessage = message.ToLowerInvariant();

        // ── Context-aware task resolution ────────────────────────────────────
        // Resolve ordinal references ("the first one", "the second task", etc.)
        // from previously stored task list context.
        var resolvedTaskId = ResolveTaskFromContext(lowerMessage, context);
        if (resolvedTaskId != null)
        {
            context.WorkflowState["task_id"] = resolvedTaskId;
        }

        // Route based on intent keywords
        if (ContainsAny(lowerMessage, "assess", "scan", "audit", "check compliance", "run assessment"))
        {
            return await _assessmentTool.ExecuteAsync(new Dictionary<string, object?>
            {
                ["subscription_id"] = GetContextValue(context, "subscription_id"),
                ["scan_type"] = "quick"
            }, cancellationToken);
        }

        if (ContainsAny(lowerMessage, "control family", "nist control", "control details"))
        {
            var family = ExtractControlFamily(lowerMessage);
            return await _controlFamilyTool.ExecuteAsync(new Dictionary<string, object?>
            {
                ["family_id"] = family
            }, cancellationToken);
        }

        if (ContainsAny(lowerMessage, "generate ssp", "generate document", "poam", "poa&m", "sar", "system security plan"))
        {
            var docType = ExtractDocumentType(lowerMessage);
            return await _documentGenerationTool.ExecuteAsync(new Dictionary<string, object?>
            {
                ["document_type"] = docType,
                ["subscription_id"] = GetContextValue(context, "subscription_id")
            }, cancellationToken);
        }

        if (ContainsAny(lowerMessage, "collect evidence", "evidence collection", "gather evidence"))
        {
            return await _evidenceCollectionTool.ExecuteAsync(new Dictionary<string, object?>
            {
                ["subscription_id"] = GetContextValue(context, "subscription_id")
            }, cancellationToken);
        }

        if (ContainsAny(lowerMessage, "remediate", "fix finding", "apply fix"))
        {
            return await _remediationTool.ExecuteAsync(new Dictionary<string, object?>
            {
                ["dry_run"] = true
            }, cancellationToken);
        }

        if (ContainsAny(lowerMessage, "remediation plan", "plan remediation"))
        {
            return await _remediationPlanTool.ExecuteAsync(new Dictionary<string, object?>
            {
                ["subscription_id"] = GetContextValue(context, "subscription_id")
            }, cancellationToken);
        }

        if (ContainsAny(lowerMessage, "compliance status", "current status", "posture"))
        {
            return await _statusTool.ExecuteAsync(new Dictionary<string, object?>
            {
                ["subscription_id"] = GetContextValue(context, "subscription_id")
            }, cancellationToken);
        }

        if (ContainsAny(lowerMessage, "compliance history", "trend", "historical"))
        {
            return await _historyTool.ExecuteAsync(new Dictionary<string, object?>
            {
                ["subscription_id"] = GetContextValue(context, "subscription_id")
            }, cancellationToken);
        }

        if (ContainsAny(lowerMessage, "audit log", "audit trail"))
        {
            return await _auditLogTool.ExecuteAsync(new Dictionary<string, object?>
            {
                ["subscription_id"] = GetContextValue(context, "subscription_id")
            }, cancellationToken);
        }

        if (ContainsAny(lowerMessage, "monitor", "alert", "continuous"))
        {
            return await _monitoringTool.ExecuteAsync(new Dictionary<string, object?>
            {
                ["action"] = "status",
                ["subscription_id"] = GetContextValue(context, "subscription_id")
            }, cancellationToken);
        }

        // ─── Kanban routing ──────────────────────────────────────────────────
        if (ContainsAny(lowerMessage, "create board", "new board", "kanban board create"))
        {
            return await _kanbanCreateBoard.ExecuteAsync(new Dictionary<string, object?>
            {
                ["name"] = "New Remediation Board",
                ["subscription_id"] = GetContextValue(context, "subscription_id")
            }, cancellationToken);
        }

        if (ContainsAny(lowerMessage, "show board", "board overview", "board status", "kanban board"))
        {
            return await _kanbanBoardShow.ExecuteAsync(new Dictionary<string, object?>
            {
                ["board_id"] = GetContextValue(context, "board_id"),
                ["include_task_summaries"] = true
            }, cancellationToken);
        }

        if (ContainsAny(lowerMessage, "get task", "task detail", "rem-", "task info"))
        {
            return await _kanbanGetTask.ExecuteAsync(new Dictionary<string, object?>
            {
                ["task_id"] = GetContextValue(context, "task_id")
            }, cancellationToken);
        }

        if (ContainsAny(lowerMessage, "create task", "new task", "add task"))
        {
            return await _kanbanCreateTask.ExecuteAsync(new Dictionary<string, object?>
            {
                ["board_id"] = GetContextValue(context, "board_id"),
                ["title"] = "New Remediation Task", ["control_id"] = "AC-1"
            }, cancellationToken);
        }

        if (ContainsAny(lowerMessage, "assign task", "reassign", "unassign"))
        {
            return await _kanbanAssignTask.ExecuteAsync(new Dictionary<string, object?>
            {
                ["task_id"] = GetContextValue(context, "task_id")
            }, cancellationToken);
        }

        if (ContainsAny(lowerMessage, "move task", "transition", "change status"))
        {
            return await _kanbanMoveTask.ExecuteAsync(new Dictionary<string, object?>
            {
                ["task_id"] = GetContextValue(context, "task_id"),
                ["target_status"] = "InProgress"
            }, cancellationToken);
        }

        if (ContainsAny(lowerMessage, "list task", "task list", "tasks on board", "show tasks"))
        {
            var result = await _kanbanTaskList.ExecuteAsync(new Dictionary<string, object?>
            {
                ["board_id"] = GetContextValue(context, "board_id")
            }, cancellationToken);

            // Store task IDs in context for ordinal resolution
            StoreTaskListResults(result, context);
            return result;
        }

        if (ContainsAny(lowerMessage, "task history", "history of task", "audit task"))
        {
            return await _kanbanTaskHistory.ExecuteAsync(new Dictionary<string, object?>
            {
                ["task_id"] = GetContextValue(context, "task_id")
            }, cancellationToken);
        }

        if (ContainsAny(lowerMessage, "validate task", "verify task", "check task fix"))
        {
            return await _kanbanValidateTask.ExecuteAsync(new Dictionary<string, object?>
            {
                ["task_id"] = GetContextValue(context, "task_id"),
                ["subscription_id"] = GetContextValue(context, "subscription_id")
            }, cancellationToken);
        }

        if (ContainsAny(lowerMessage, "add comment", "comment on task"))
        {
            return await _kanbanAddComment.ExecuteAsync(new Dictionary<string, object?>
            {
                ["task_id"] = GetContextValue(context, "task_id"),
                ["content"] = "Comment added via agent"
            }, cancellationToken);
        }

        if (ContainsAny(lowerMessage, "task comments", "list comments", "show comments"))
        {
            return await _kanbanTaskComments.ExecuteAsync(new Dictionary<string, object?>
            {
                ["task_id"] = GetContextValue(context, "task_id")
            }, cancellationToken);
        }

        if (ContainsAny(lowerMessage, "remediate task", "run remediation", "execute fix", "apply remediation"))
        {
            return await _kanbanRemediateTask.ExecuteAsync(new Dictionary<string, object?>
            {
                ["task_id"] = GetContextValue(context, "task_id")
            }, cancellationToken);
        }

        if (ContainsAny(lowerMessage, "collect evidence", "task evidence", "gather evidence"))
        {
            return await _kanbanCollectEvidence.ExecuteAsync(new Dictionary<string, object?>
            {
                ["task_id"] = GetContextValue(context, "task_id"),
                ["subscription_id"] = GetContextValue(context, "subscription_id")
            }, cancellationToken);
        }

        if (ContainsAny(lowerMessage, "bulk update", "bulk assign", "bulk move"))
        {
            return await _kanbanBulkUpdate.ExecuteAsync(new Dictionary<string, object?>
            {
                ["board_id"] = GetContextValue(context, "board_id"),
                ["operation"] = "assign", ["task_ids"] = new string[0]
            }, cancellationToken);
        }

        if (ContainsAny(lowerMessage, "export board", "export csv", "export poam", "kanban export"))
        {
            return await _kanbanExport.ExecuteAsync(new Dictionary<string, object?>
            {
                ["board_id"] = GetContextValue(context, "board_id"),
                ["format"] = "csv"
            }, cancellationToken);
        }

        if (ContainsAny(lowerMessage, "archive board", "close board"))
        {
            return await _kanbanArchiveBoard.ExecuteAsync(new Dictionary<string, object?>
            {
                ["board_id"] = GetContextValue(context, "board_id"),
                ["confirm"] = false
            }, cancellationToken);
        }

        // Default: return compliance status
        return await _statusTool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["subscription_id"] = GetContextValue(context, "subscription_id")
        }, cancellationToken);
    }

    /// <summary>Returns true if the text contains any of the specified keywords (case-insensitive).</summary>
    private static bool ContainsAny(string text, params string[] keywords) =>
        keywords.Any(k => text.Contains(k, StringComparison.OrdinalIgnoreCase));

    /// <summary>Retrieves a context value from the agent conversation workflow state.</summary>
    private static string? GetContextValue(AgentConversationContext context, string key) =>
        context.WorkflowState.TryGetValue(key, out var value) ? value?.ToString() : null;

    /// <summary>Extracts the NIST control family abbreviation from the user message.</summary>
    private static string ExtractControlFamily(string message)
    {
        var families = new[] { "AC", "AU", "AT", "CM", "CP", "IA", "IR", "MA", "MP", "PE", "PL", "PM", "PS", "RA", "SA", "SC", "SI", "SR" };
        foreach (var family in families)
        {
            if (message.Contains(family, StringComparison.OrdinalIgnoreCase))
                return family;
        }
        return "AC";
    }

    /// <summary>Extracts the document type (ssp, poam, sar) from the user message.</summary>
    private static string ExtractDocumentType(string message)
    {
        if (message.Contains("ssp", StringComparison.OrdinalIgnoreCase)) return "ssp";
        if (message.Contains("poam", StringComparison.OrdinalIgnoreCase) || message.Contains("poa&m", StringComparison.OrdinalIgnoreCase)) return "poam";
        if (message.Contains("sar", StringComparison.OrdinalIgnoreCase)) return "sar";
        return "ssp";
    }

    /// <summary>
    /// Classifies the user message intent for audit logging.
    /// </summary>
    private static string ClassifyIntent(string message)
    {
        var lower = message.ToLowerInvariant();
        if (ContainsAny(lower, "assess", "scan")) return "Assessment";
        if (ContainsAny(lower, "remediat", "fix")) return "Remediation";
        if (ContainsAny(lower, "evidence", "collect")) return "EvidenceCollection";
        if (ContainsAny(lower, "document", "ssp", "sar", "poam")) return "DocumentGeneration";
        if (ContainsAny(lower, "monitor", "alert")) return "Monitoring";
        if (ContainsAny(lower, "audit", "log")) return "AuditQuery";
        if (ContainsAny(lower, "history", "trend")) return "HistoryQuery";
        if (ContainsAny(lower, "status", "posture")) return "StatusQuery";
        if (ContainsAny(lower, "control", "nist")) return "ControlQuery";
        if (ContainsAny(lower, "board", "kanban")) return "KanbanQuery";
        if (ContainsAny(lower, "task", "rem-", "assign", "move task")) return "KanbanTask";
        if (ContainsAny(lower, "comment")) return "KanbanComment";
        if (ContainsAny(lower, "bulk")) return "KanbanBulk";
        if (ContainsAny(lower, "export", "csv", "poam export")) return "KanbanExport";
        return "GeneralQuery";
    }

    /// <summary>
    /// Persists an audit log entry to the database.
    /// </summary>
    private async Task LogAuditEntryAsync(
        string action,
        string? subscriptionId,
        AuditOutcome outcome,
        string details,
        TimeSpan duration,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
            db.AuditLogs.Add(new AuditLogEntry
            {
                UserId = "system",
                UserRole = "Agent",
                Action = action,
                SubscriptionId = subscriptionId,
                Outcome = outcome,
                Details = details,
                Duration = duration
            });
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            // Audit logging should never fail the main operation
            Logger.LogWarning(ex, "Failed to persist audit log entry for action {Action}", action);
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Context-aware task resolution (US14)
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>Key used to store the last displayed task ID list in workflow state.</summary>
    public const string LastResultsKey = "kanban:lastResults";

    /// <summary>
    /// Ordinal patterns mapped to zero-based index.
    /// Keys are regex patterns, values are the resolved index.
    /// </summary>
    private static readonly (Regex Pattern, int Index)[] OrdinalPatterns =
    [
        (new Regex(@"\b(the\s+)?first(\s+one|\s+task)?\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), 0),
        (new Regex(@"\b(the\s+)?second(\s+one|\s+task)?\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), 1),
        (new Regex(@"\b(the\s+)?third(\s+one|\s+task)?\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), 2),
        (new Regex(@"\b(the\s+)?fourth(\s+one|\s+task)?\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), 3),
        (new Regex(@"\b(the\s+)?fifth(\s+one|\s+task)?\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), 4),
        (new Regex(@"\b(the\s+)?last(\s+one|\s+task)?\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), -1),
    ];

    /// <summary>
    /// Resolves an ordinal reference ("first", "second", "last") to a task ID
    /// from the previously stored task list. Returns null if no match or no context.
    /// </summary>
    public static string? ResolveTaskFromContext(string lowerMessage, AgentConversationContext context)
    {
        // Only attempt resolution if context has stored results
        if (!context.WorkflowState.TryGetValue(LastResultsKey, out var stored) || stored is not List<string> taskIds || taskIds.Count == 0)
            return null;

        foreach (var (pattern, index) in OrdinalPatterns)
        {
            if (!pattern.IsMatch(lowerMessage)) continue;

            var resolvedIndex = index == -1 ? taskIds.Count - 1 : index;
            if (resolvedIndex >= 0 && resolvedIndex < taskIds.Count)
            {
                return taskIds[resolvedIndex];
            }
        }

        return null;
    }

    /// <summary>
    /// Extracts task IDs from a JSON tool result (standard envelope format)
    /// and stores them in the conversation context for ordinal resolution.
    /// </summary>
    public static void StoreTaskListResults(string toolResult, AgentConversationContext context)
    {
        try
        {
            using var doc = JsonDocument.Parse(toolResult);
            var root = doc.RootElement;

            if (root.TryGetProperty("data", out var data))
            {
                var taskIds = new List<string>();

                // Handle array of tasks in data
                if (data.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in data.EnumerateArray())
                    {
                        if (item.TryGetProperty("id", out var id))
                            taskIds.Add(id.GetString() ?? "");
                        else if (item.TryGetProperty("taskId", out var taskId))
                            taskIds.Add(taskId.GetString() ?? "");
                    }
                }
                // Handle object with tasks array inside
                else if (data.ValueKind == JsonValueKind.Object && data.TryGetProperty("tasks", out var tasks) && tasks.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in tasks.EnumerateArray())
                    {
                        if (item.TryGetProperty("id", out var id))
                            taskIds.Add(id.GetString() ?? "");
                        else if (item.TryGetProperty("taskId", out var taskId))
                            taskIds.Add(taskId.GetString() ?? "");
                    }
                }

                if (taskIds.Count > 0)
                {
                    context.WorkflowState[LastResultsKey] = taskIds;
                }
            }
        }
        catch
        {
            // Non-JSON results or parse errors — silently skip context storage
        }
    }
}
