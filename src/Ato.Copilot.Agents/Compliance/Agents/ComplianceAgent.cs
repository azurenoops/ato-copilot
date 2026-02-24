using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Ato.Copilot.Agents.Common;
using Ato.Copilot.Agents.Compliance.Tools;
using Ato.Copilot.Core.Configuration;
using Ato.Copilot.Core.Data.Context;
using Ato.Copilot.Core.Interfaces.Auth;
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
    private readonly IServiceScopeFactory _scopeFactory;

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

    // Auth/PIM tools (Phase 3 — US1)
    private readonly CacStatusTool _cacStatus;
    private readonly CacSignOutTool _cacSignOut;

    // CAC session config (Phase 10 — US8)
    private readonly CacSetTimeoutTool _cacSetTimeout;

    // Certificate mapping (Phase 11 — US9)
    private readonly CacMapCertificateTool _cacMapCertificate;

    // PIM tools (Phase 5 — US3)
    private readonly PimListEligibleTool _pimListEligible;
    private readonly PimActivateRoleTool _pimActivateRole;
    private readonly PimDeactivateRoleTool _pimDeactivateRole;

    // PIM session management tools (Phase 6 — US4)
    private readonly PimListActiveTool _pimListActive;
    private readonly PimExtendRoleTool _pimExtendRole;

    // PIM approval workflow tools (Phase 7 — US5)
    private readonly PimApproveRequestTool _pimApproveRequest;
    private readonly PimDenyRequestTool _pimDenyRequest;

    // JIT VM access tools (Phase 9 — US7)
    private readonly JitRequestAccessTool _jitRequestAccess;
    private readonly JitListSessionsTool _jitListSessions;
    private readonly JitRevokeAccessTool _jitRevokeAccess;

    // PIM audit trail (Phase 12 — US10)
    private readonly PimHistoryTool _pimHistory;

    // Compliance Watch monitoring tools (Feature 005)
    private readonly WatchEnableMonitoringTool _watchEnableMonitoring;
    private readonly WatchDisableMonitoringTool _watchDisableMonitoring;
    private readonly WatchConfigureMonitoringTool _watchConfigureMonitoring;
    private readonly WatchMonitoringStatusTool _watchMonitoringStatus;

    // Compliance Watch alert lifecycle tools (Feature 005 — US2)
    private readonly WatchShowAlertsTool _watchShowAlerts;
    private readonly WatchGetAlertTool _watchGetAlert;
    private readonly WatchAcknowledgeAlertTool _watchAcknowledgeAlert;
    private readonly WatchFixAlertTool _watchFixAlert;
    private readonly WatchDismissAlertTool _watchDismissAlert;

    // Compliance Watch alert rules & suppression tools (Feature 005 — US3)
    private readonly WatchCreateRuleTool _watchCreateRule;
    private readonly WatchListRulesTool _watchListRules;
    private readonly WatchSuppressAlertsTool _watchSuppressAlerts;
    private readonly WatchListSuppressionsTool _watchListSuppressions;
    private readonly WatchConfigureQuietHoursTool _watchConfigureQuietHours;

    // Compliance Watch notification & escalation tools (US4)
    private readonly WatchConfigureNotificationsTool _watchConfigureNotifications;
    private readonly WatchConfigureEscalationTool _watchConfigureEscalation;

    // Compliance Watch dashboard & reporting tools (US5)
    private readonly WatchAlertHistoryTool _watchAlertHistory;
    private readonly WatchComplianceTrendTool _watchComplianceTrend;
    private readonly WatchAlertStatisticsTool _watchAlertStatistics;

    // Compliance Watch integration tools (US8)
    private readonly WatchCreateTaskFromAlertTool _watchCreateTaskFromAlert;
    private readonly WatchCollectEvidenceFromAlertTool _watchCollectEvidenceFromAlert;

    // Compliance Watch auto-remediation tools (US9)
    private readonly WatchCreateAutoRemediationRuleTool _watchCreateAutoRemediationRule;
    private readonly WatchListAutoRemediationRulesTool _watchListAutoRemediationRules;

    // NIST Controls knowledge tools (Feature 007)
    private readonly NistControlSearchTool _nistControlSearchTool;
    private readonly NistControlExplainerTool _nistControlExplainerTool;

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
        CacStatusTool cacStatus,
        CacSignOutTool cacSignOut,
        CacSetTimeoutTool cacSetTimeout,
        CacMapCertificateTool cacMapCertificate,
        PimListEligibleTool pimListEligible,
        PimActivateRoleTool pimActivateRole,
        PimDeactivateRoleTool pimDeactivateRole,
        PimListActiveTool pimListActive,
        PimExtendRoleTool pimExtendRole,
        PimApproveRequestTool pimApproveRequest,
        PimDenyRequestTool pimDenyRequest,
        JitRequestAccessTool jitRequestAccess,
        JitListSessionsTool jitListSessions,
        JitRevokeAccessTool jitRevokeAccess,
        PimHistoryTool pimHistory,
        WatchEnableMonitoringTool watchEnableMonitoring,
        WatchDisableMonitoringTool watchDisableMonitoring,
        WatchConfigureMonitoringTool watchConfigureMonitoring,
        WatchMonitoringStatusTool watchMonitoringStatus,
        WatchShowAlertsTool watchShowAlerts,
        WatchGetAlertTool watchGetAlert,
        WatchAcknowledgeAlertTool watchAcknowledgeAlert,
        WatchFixAlertTool watchFixAlert,
        WatchDismissAlertTool watchDismissAlert,
        WatchCreateRuleTool watchCreateRule,
        WatchListRulesTool watchListRules,
        WatchSuppressAlertsTool watchSuppressAlerts,
        WatchListSuppressionsTool watchListSuppressions,
        WatchConfigureQuietHoursTool watchConfigureQuietHours,
        WatchConfigureNotificationsTool watchConfigureNotifications,
        WatchConfigureEscalationTool watchConfigureEscalation,
        WatchAlertHistoryTool watchAlertHistory,
        WatchComplianceTrendTool watchComplianceTrend,
        WatchAlertStatisticsTool watchAlertStatistics,
        WatchCreateTaskFromAlertTool watchCreateTaskFromAlert,
        WatchCollectEvidenceFromAlertTool watchCollectEvidenceFromAlert,
        WatchCreateAutoRemediationRuleTool watchCreateAutoRemediationRule,
        WatchListAutoRemediationRulesTool watchListAutoRemediationRules,
        NistControlSearchTool nistControlSearchTool,
        NistControlExplainerTool nistControlExplainerTool,
        IDbContextFactory<AtoCopilotContext> dbFactory,
        IServiceScopeFactory scopeFactory,
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
        _cacStatus = cacStatus;
        _cacSignOut = cacSignOut;
        _cacSetTimeout = cacSetTimeout;
        _cacMapCertificate = cacMapCertificate;
        _pimListEligible = pimListEligible;
        _pimActivateRole = pimActivateRole;
        _pimDeactivateRole = pimDeactivateRole;
        _pimListActive = pimListActive;
        _pimExtendRole = pimExtendRole;
        _pimApproveRequest = pimApproveRequest;
        _pimDenyRequest = pimDenyRequest;
        _jitRequestAccess = jitRequestAccess;
        _jitListSessions = jitListSessions;
        _jitRevokeAccess = jitRevokeAccess;
        _pimHistory = pimHistory;
        _watchEnableMonitoring = watchEnableMonitoring;
        _watchDisableMonitoring = watchDisableMonitoring;
        _watchConfigureMonitoring = watchConfigureMonitoring;
        _watchMonitoringStatus = watchMonitoringStatus;
        _watchShowAlerts = watchShowAlerts;
        _watchGetAlert = watchGetAlert;
        _watchAcknowledgeAlert = watchAcknowledgeAlert;
        _watchFixAlert = watchFixAlert;
        _watchDismissAlert = watchDismissAlert;
        _watchCreateRule = watchCreateRule;
        _watchListRules = watchListRules;
        _watchSuppressAlerts = watchSuppressAlerts;
        _watchListSuppressions = watchListSuppressions;
        _watchConfigureQuietHours = watchConfigureQuietHours;
        _watchConfigureNotifications = watchConfigureNotifications;
        _watchConfigureEscalation = watchConfigureEscalation;
        _watchAlertHistory = watchAlertHistory;
        _watchComplianceTrend = watchComplianceTrend;
        _watchAlertStatistics = watchAlertStatistics;
        _watchCreateTaskFromAlert = watchCreateTaskFromAlert;
        _watchCollectEvidenceFromAlert = watchCollectEvidenceFromAlert;
        _watchCreateAutoRemediationRule = watchCreateAutoRemediationRule;
        _watchListAutoRemediationRules = watchListAutoRemediationRules;
        _nistControlSearchTool = nistControlSearchTool;
        _nistControlExplainerTool = nistControlExplainerTool;
        _dbFactory = dbFactory;
        _scopeFactory = scopeFactory;

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

        // Register Auth/PIM tools
        RegisterTool(_cacStatus);
        RegisterTool(_cacSignOut);
        RegisterTool(_cacSetTimeout);
        RegisterTool(_cacMapCertificate);
        RegisterTool(_pimListEligible);
        RegisterTool(_pimActivateRole);
        RegisterTool(_pimDeactivateRole);
        RegisterTool(_pimListActive);
        RegisterTool(_pimExtendRole);
        RegisterTool(_pimApproveRequest);
        RegisterTool(_pimDenyRequest);
        RegisterTool(_jitRequestAccess);
        RegisterTool(_jitListSessions);
        RegisterTool(_jitRevokeAccess);
        RegisterTool(_pimHistory);

        // Register Compliance Watch tools
        RegisterTool(_watchEnableMonitoring);
        RegisterTool(_watchDisableMonitoring);
        RegisterTool(_watchConfigureMonitoring);
        RegisterTool(_watchMonitoringStatus);

        // Register Compliance Watch alert lifecycle tools
        RegisterTool(_watchShowAlerts);
        RegisterTool(_watchGetAlert);
        RegisterTool(_watchAcknowledgeAlert);
        RegisterTool(_watchFixAlert);
        RegisterTool(_watchDismissAlert);

        // Register Compliance Watch alert rules & suppression tools
        RegisterTool(_watchCreateRule);
        RegisterTool(_watchListRules);
        RegisterTool(_watchSuppressAlerts);
        RegisterTool(_watchListSuppressions);
        RegisterTool(_watchConfigureQuietHours);

        // Register Compliance Watch notification & escalation tools
        RegisterTool(_watchConfigureNotifications);
        RegisterTool(_watchConfigureEscalation);

        // Register Compliance Watch dashboard & reporting tools
        RegisterTool(_watchAlertHistory);
        RegisterTool(_watchComplianceTrend);
        RegisterTool(_watchAlertStatistics);

        // Register Compliance Watch integration tools
        RegisterTool(_watchCreateTaskFromAlert);
        RegisterTool(_watchCollectEvidenceFromAlert);

        // Register Compliance Watch auto-remediation tools
        RegisterTool(_watchCreateAutoRemediationRule);
        RegisterTool(_watchListAutoRemediationRules);

        // Register NIST Controls knowledge tools (Feature 007)
        RegisterTool(_nistControlSearchTool);
        RegisterTool(_nistControlExplainerTool);
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
            // ── Auth-gate: check PIM eligibility for Tier 2 operations (FR-019) ──
            var authGateResult = await CheckAuthGateAsync(message, context, cancellationToken);
            if (authGateResult != null)
            {
                stopwatch.Stop();
                return new AgentResponse
                {
                    Success = true,
                    Response = authGateResult,
                    AgentName = AgentName,
                    ProcessingTimeMs = stopwatch.ElapsedMilliseconds
                };
            }

            // Analyze intent and route to appropriate tool
            var toolResult = await RouteToToolAsync(message, context, cancellationToken);

            // ── Post-operation deactivation offer (FR-020/FR-021) ────────────
            toolResult = await AppendDeactivationOfferAsync(toolResult, message, context, cancellationToken);

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

        // ─── Auth/PIM routing ────────────────────────────────────────────────
        if (ContainsAny(lowerMessage, "cac status", "auth status", "am i authenticated", "authentication status"))
        {
            return await _cacStatus.ExecuteAsync(new Dictionary<string, object?>
            {
                ["user_id"] = GetContextValue(context, "user_id")
            }, cancellationToken);
        }

        if (ContainsAny(lowerMessage, "sign out", "cac sign out", "log out", "logout", "cac logout"))
        {
            return await _cacSignOut.ExecuteAsync(new Dictionary<string, object?>
            {
                ["user_id"] = GetContextValue(context, "user_id")
            }, cancellationToken);
        }

        if (ContainsAny(lowerMessage, "set timeout", "session timeout", "cac timeout", "change timeout", "set my timeout", "timeout to"))
        {
            return await _cacSetTimeout.ExecuteAsync(new Dictionary<string, object?>
            {
                ["user_id"] = GetContextValue(context, "user_id"),
                ["timeoutHours"] = ExtractHours(lowerMessage)
            }, cancellationToken);
        }

        if (ContainsAny(lowerMessage, "map certificate", "map cert", "certificate mapping", "cert mapping", "map my cert", "map my cac", "assign role to cert"))
        {
            return await _cacMapCertificate.ExecuteAsync(new Dictionary<string, object?>
            {
                ["user_id"] = GetContextValue(context, "user_id"),
                ["role"] = ExtractRole(lowerMessage)
            }, cancellationToken);
        }

        // ─── PIM routing ─────────────────────────────────────────────────────
        if (ContainsAny(lowerMessage, "eligible roles", "pim eligible", "list eligible", "what roles can i activate"))
        {
            return await _pimListEligible.ExecuteAsync(new Dictionary<string, object?>
            {
                ["user_id"] = GetContextValue(context, "user_id"),
                ["scope"] = GetContextValue(context, "subscription_id")
            }, cancellationToken);
        }

        if (ContainsAny(lowerMessage, "activate role", "pim activate", "i need", "give me access", "enable role"))
        {
            return await _pimActivateRole.ExecuteAsync(new Dictionary<string, object?>
            {
                ["user_id"] = GetContextValue(context, "user_id"),
                ["session_id"] = GetContextValue(context, "session_id"),
                ["roleName"] = ExtractRoleName(lowerMessage),
                ["scope"] = GetContextValue(context, "subscription_id") ?? "default",
                ["justification"] = message
            }, cancellationToken);
        }

        if (ContainsAny(lowerMessage, "deactivate role", "pim deactivate", "remove access", "revoke role", "disable role"))
        {
            return await _pimDeactivateRole.ExecuteAsync(new Dictionary<string, object?>
            {
                ["user_id"] = GetContextValue(context, "user_id"),
                ["roleName"] = ExtractRoleName(lowerMessage),
                ["scope"] = GetContextValue(context, "subscription_id") ?? "default"
            }, cancellationToken);
        }

        // ─── PIM Session Management routing ──────────────────────────────────
        if (ContainsAny(lowerMessage, "active roles", "pim active", "list active", "my active pim", "show my active", "current roles"))
        {
            return await _pimListActive.ExecuteAsync(new Dictionary<string, object?>
            {
                ["user_id"] = GetContextValue(context, "user_id")
            }, cancellationToken);
        }

        if (ContainsAny(lowerMessage, "extend role", "pim extend", "extend by", "extend access", "more time"))
        {
            return await _pimExtendRole.ExecuteAsync(new Dictionary<string, object?>
            {
                ["user_id"] = GetContextValue(context, "user_id"),
                ["roleName"] = ExtractRoleName(lowerMessage),
                ["scope"] = GetContextValue(context, "subscription_id") ?? "default",
                ["additionalHours"] = ExtractHours(lowerMessage)
            }, cancellationToken);
        }

        if (ContainsAny(lowerMessage, "approve request", "pim approve", "approve role", "approve activation"))
        {
            return await _pimApproveRequest.ExecuteAsync(new Dictionary<string, object?>
            {
                ["user_id"] = GetContextValue(context, "user_id"),
                ["user_role"] = GetContextValue(context, "user_role"),
                ["requestId"] = ExtractRequestId(lowerMessage),
                ["comments"] = null
            }, cancellationToken);
        }

        if (ContainsAny(lowerMessage, "deny request", "pim deny", "reject request", "deny activation"))
        {
            return await _pimDenyRequest.ExecuteAsync(new Dictionary<string, object?>
            {
                ["user_id"] = GetContextValue(context, "user_id"),
                ["user_role"] = GetContextValue(context, "user_role"),
                ["requestId"] = ExtractRequestId(lowerMessage),
                ["reason"] = null
            }, cancellationToken);
        }

        // ─── JIT VM Access routing (Phase 9 — US7) ──────────────────────
        if (ContainsAny(lowerMessage, "ssh access", "rdp access", "vm access", "jit access", "jit request", "i need ssh", "i need rdp", "connect to vm"))
        {
            var vmName = ExtractVmName(lowerMessage);
            return await _jitRequestAccess.ExecuteAsync(new Dictionary<string, object?>
            {
                ["user_id"] = GetContextValue(context, "user_id"),
                ["session_id"] = GetContextValue(context, "session_id"),
                ["vmName"] = vmName,
                ["resourceGroup"] = GetContextValue(context, "resource_group") ?? "default-rg",
                ["justification"] = message
            }, cancellationToken);
        }

        if (ContainsAny(lowerMessage, "jit sessions", "list jit", "active jit", "my jit sessions", "vm sessions"))
        {
            return await _jitListSessions.ExecuteAsync(new Dictionary<string, object?>
            {
                ["user_id"] = GetContextValue(context, "user_id")
            }, cancellationToken);
        }

        if (ContainsAny(lowerMessage, "revoke jit", "revoke vm", "revoke access", "jit revoke", "remove jit", "close jit"))
        {
            var vmName = ExtractVmName(lowerMessage);
            return await _jitRevokeAccess.ExecuteAsync(new Dictionary<string, object?>
            {
                ["user_id"] = GetContextValue(context, "user_id"),
                ["vmName"] = vmName,
                ["resourceGroup"] = GetContextValue(context, "resource_group") ?? "default-rg"
            }, cancellationToken);
        }

        // ─── PIM Audit Trail routing (Phase 12 — US10) ──────────────────
        if (ContainsAny(lowerMessage, "pim history", "pim audit", "pim log", "activation history", "role history", "audit trail", "compliance evidence"))
        {
            return await _pimHistory.ExecuteAsync(new Dictionary<string, object?>
            {
                ["user_id"] = GetContextValue(context, "user_id"),
                ["is_auditor"] = GetContextValue(context, "user_role")?.Equals("Compliance.Auditor", StringComparison.OrdinalIgnoreCase) ?? false,
                ["scope"] = GetContextValue(context, "subscription_id")
            }, cancellationToken);
        }

        // Default: return compliance status
        return await _statusTool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["subscription_id"] = GetContextValue(context, "subscription_id")
        }, cancellationToken);
    }

    // ─── Auth-Gate: PIM Inline Activation (FR-019 / T048) ────────────────────

    /// <summary>
    /// Maps user intent keywords to the tool name that would be invoked.
    /// Used to determine if the target operation requires Tier 2 auth.
    /// </summary>
    private static string? ResolveTargetToolName(string lowerMessage)
    {
        // Tier 2 compliance tools
        if (ContainsAny(lowerMessage, "assess", "scan", "audit", "check compliance", "run assessment")) return "run_assessment";
        if (ContainsAny(lowerMessage, "collect evidence", "evidence collection", "gather evidence")) return "collect_evidence";
        if (ContainsAny(lowerMessage, "remediate", "fix finding", "apply fix")) return "execute_remediation";
        if (ContainsAny(lowerMessage, "validate remediation", "verify fix")) return "compliance_validate_remediation";
        if (ContainsAny(lowerMessage, "monitor", "alert", "continuous")) return "compliance_monitoring";

        // Tier 2 kanban tools
        if (ContainsAny(lowerMessage, "remediate task", "run remediation", "execute fix", "apply remediation")) return "kanban_remediate_task";
        if (ContainsAny(lowerMessage, "validate task")) return "kanban_validate_task";
        if (ContainsAny(lowerMessage, "collect evidence", "task evidence")) return "kanban_collect_evidence";

        // PIM tools are themselves Tier 2 but handled via PIM routing directly
        return null;
    }

    /// <summary>
    /// Maps a tool name to the Azure RBAC role typically required for that operation.
    /// </summary>
    private static string? GetRequiredRoleForTool(string toolName) => toolName switch
    {
        "run_assessment" => "Reader",
        "collect_evidence" => "Reader",
        "compliance_monitoring" => "Reader",
        "execute_remediation" => "Contributor",
        "compliance_validate_remediation" => "Reader",
        "kanban_remediate_task" => "Contributor",
        "kanban_validate_task" => "Reader",
        "kanban_collect_evidence" => "Reader",
        _ => null
    };

    /// <summary>
    /// Checks if the user needs PIM activation before executing a Tier 2 operation.
    /// Returns a JSON response with inline activation offer if eligible, null if no gate needed.
    /// Per FR-019: detects missing RBAC role, checks PIM eligibility, offers inline activation.
    /// </summary>
    private async Task<string?> CheckAuthGateAsync(
        string message,
        AgentConversationContext context,
        CancellationToken cancellationToken)
    {
        var lowerMessage = message.ToLowerInvariant();
        var targetTool = ResolveTargetToolName(lowerMessage);

        // Not a Tier 2 operation or not a tool we track
        if (targetTool == null || !AuthTierClassification.IsTier2(targetTool))
            return null;

        var userId = GetContextValue(context, "user_id");
        if (string.IsNullOrEmpty(userId))
            return null; // Auth check handled by middleware

        // Check if user already has active PIM roles for this scope
        var scope = GetContextValue(context, "subscription_id") ?? "default";
        var requiredRole = GetRequiredRoleForTool(targetTool);
        if (requiredRole == null)
            return null; // No role mapping for this tool

        using var serviceScope = _scopeFactory.CreateScope();
        var pimService = serviceScope.ServiceProvider.GetRequiredService<IPimService>();

        // Check if user already has the required role active
        var activeRoles = await pimService.ListActiveRolesAsync(userId, cancellationToken);
        var hasActiveRole = activeRoles.Any(r =>
            r.RoleName.Contains(requiredRole, StringComparison.OrdinalIgnoreCase) &&
            (r.Scope.Contains(scope, StringComparison.OrdinalIgnoreCase) ||
             scope.Equals("default", StringComparison.OrdinalIgnoreCase)));

        if (hasActiveRole)
            return null; // User has the needed role, proceed with tool execution

        // Check PIM eligibility
        var eligibleRoles = await pimService.ListEligibleRolesAsync(userId, scope, cancellationToken);
        var matchingRole = eligibleRoles.FirstOrDefault(r =>
            r.RoleName.Contains(requiredRole, StringComparison.OrdinalIgnoreCase));

        if (matchingRole == null)
            return null; // No eligible role found, let the tool handle the error

        // Store the intent for post-activation continuation
        context.WorkflowState["pending_tool"] = targetTool;
        context.WorkflowState["pending_role"] = matchingRole.RoleName;
        context.WorkflowState["pending_scope"] = scope;

        Logger.LogInformation(
            "Auth-gate: User {UserId} needs {Role} for {Tool}. Offering inline PIM activation.",
            userId, matchingRole.RoleName, targetTool);

        return JsonSerializer.Serialize(new
        {
            status = "auth_gate",
            data = new
            {
                message = $"The operation '{targetTool}' requires the '{matchingRole.RoleName}' role on scope '{scope}'.",
                eligibleRole = matchingRole.RoleName,
                scope,
                requiresApproval = matchingRole.RequiresApproval,
                maxDuration = matchingRole.MaxDuration,
                suggestion = matchingRole.RequiresApproval
                    ? $"You are eligible for '{matchingRole.RoleName}' but it requires approval. Say 'activate role {matchingRole.RoleName}' to submit an activation request."
                    : $"You are eligible for '{matchingRole.RoleName}'. Say 'activate role {matchingRole.RoleName}' to activate it and proceed.",
                action = "pim_activate_role"
            },
            metadata = new { toolName = targetTool, agentName = AgentName }
        });
    }

    // ─── Post-Operation Deactivation Offer (FR-020/FR-021 / T049) ────────────

    /// <summary>
    /// Checks if a PIM role was activated inline during this conversation and offers deactivation
    /// after successful Tier 2 operation completion.
    /// Per FR-020: offers deactivation after triggering operation completes.
    /// Per FR-021: respects AutoDeactivateAfterRemediation config.
    /// </summary>
    private async Task<string> AppendDeactivationOfferAsync(
        string toolResult,
        string message,
        AgentConversationContext context,
        CancellationToken cancellationToken)
    {
        var lowerMessage = message.ToLowerInvariant();
        var targetTool = ResolveTargetToolName(lowerMessage);

        // Only apply to Tier 2 operations
        if (targetTool == null || !AuthTierClassification.IsTier2(targetTool))
            return toolResult;

        // Check if there's a PIM role that was activated inline during this conversation
        var inlineActivatedRole = GetContextValue(context, "inline_activated_role");
        var inlineActivatedScope = GetContextValue(context, "inline_activated_scope");

        if (string.IsNullOrEmpty(inlineActivatedRole))
            return toolResult;

        // Check if AutoDeactivateAfterRemediation is enabled for remediation operations
        var isRemediation = ContainsAny(lowerMessage, "remediate", "fix finding", "apply fix",
            "remediate task", "run remediation", "execute fix", "apply remediation");

        using var serviceScope = _scopeFactory.CreateScope();
        var pimOptions = serviceScope.ServiceProvider.GetRequiredService<IOptions<PimServiceOptions>>();

        if (isRemediation && pimOptions.Value.AutoDeactivateAfterRemediation)
        {
            // Auto-deactivate per FR-021
            var userId = GetContextValue(context, "user_id");
            if (!string.IsNullOrEmpty(userId))
            {
                var pimService = serviceScope.ServiceProvider.GetRequiredService<IPimService>();
                var deactivateResult = await pimService.DeactivateRoleAsync(
                    userId, inlineActivatedRole, inlineActivatedScope ?? "default", cancellationToken);

                // Clear the inline activation tracking
                context.WorkflowState.Remove("inline_activated_role");
                context.WorkflowState.Remove("inline_activated_scope");

                Logger.LogInformation(
                    "Auto-deactivated PIM role {Role} after remediation (FR-021)",
                    inlineActivatedRole);

                // Parse and augment the tool result with deactivation info
                try
                {
                    var doc = JsonDocument.Parse(toolResult);
                    var resultObj = new Dictionary<string, object?>
                    {
                        ["status"] = doc.RootElement.GetProperty("status").GetString(),
                        ["data"] = JsonSerializer.Deserialize<object>(doc.RootElement.GetProperty("data").GetRawText()),
                        ["deactivation"] = new
                        {
                            autoDeactivated = true,
                            role = inlineActivatedRole,
                            message = $"PIM role '{inlineActivatedRole}' was automatically deactivated after remediation."
                        },
                        ["metadata"] = JsonSerializer.Deserialize<object>(doc.RootElement.GetProperty("metadata").GetRawText())
                    };
                    return JsonSerializer.Serialize(resultObj);
                }
                catch
                {
                    return toolResult;
                }
            }
        }

        // Offer to deactivate per FR-020
        try
        {
            var doc = JsonDocument.Parse(toolResult);
            var resultObj = new Dictionary<string, object?>
            {
                ["status"] = doc.RootElement.GetProperty("status").GetString(),
                ["data"] = JsonSerializer.Deserialize<object>(doc.RootElement.GetProperty("data").GetRawText()),
                ["deactivation_offer"] = new
                {
                    role = inlineActivatedRole,
                    scope = inlineActivatedScope,
                    message = $"Operation complete. The PIM role '{inlineActivatedRole}' is still active. Say 'deactivate role {inlineActivatedRole}' to restore least-privilege access."
                },
                ["metadata"] = JsonSerializer.Deserialize<object>(doc.RootElement.GetProperty("metadata").GetRawText())
            };
            return JsonSerializer.Serialize(resultObj);
        }
        catch
        {
            return toolResult;
        }
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

    /// <summary>Extracts an Azure role name from user message text.</summary>
    private static string? ExtractRoleName(string message)
    {
        var roles = new[] { "Owner", "Contributor", "Reader", "User Access Administrator",
            "Security Administrator", "Global Administrator", "Privileged Role Administrator" };
        foreach (var role in roles)
        {
            if (message.Contains(role, StringComparison.OrdinalIgnoreCase))
                return role;
        }
        return null;
    }

    /// <summary>Extracts a numeric hours value from user message text (e.g., "extend by 2 hours" → 2).</summary>
    private static int ExtractHours(string message)
    {
        var match = System.Text.RegularExpressions.Regex.Match(message, @"(\d+)\s*hour");
        if (match.Success && int.TryParse(match.Groups[1].Value, out var hours))
            return hours;
        return 2; // Default extension of 2 hours
    }

    /// <summary>Extracts a GUID request ID from user message text.</summary>
    private static string? ExtractRequestId(string message)
    {
        var match = System.Text.RegularExpressions.Regex.Match(message, @"[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}");
        return match.Success ? match.Value : null;
    }

    /// <summary>Extracts a VM name from user message text (e.g., "SSH access to vm-web01" → "vm-web01").</summary>
    private static string? ExtractVmName(string message)
    {
        // Match common VM naming patterns: vm-xxx, hostname with dots, or alphanumeric-with-dashes
        var match = System.Text.RegularExpressions.Regex.Match(message, @"(?:to|for|on|access)\s+([a-zA-Z][a-zA-Z0-9\-\.]+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (match.Success)
            return match.Groups[1].Value;
        return null;
    }

    /// <summary>Extracts a platform role name from user message text (e.g., "map cert as Auditor" → "Auditor").</summary>
    private static string? ExtractRole(string message)
    {
        var roles = new[] { "Administrator", "Admin", "Auditor", "Analyst", "Viewer",
            "SecurityLead", "Security Lead", "PlatformEngineer", "Platform Engineer", "Engineer" };
        foreach (var role in roles)
        {
            if (message.Contains(role, StringComparison.OrdinalIgnoreCase))
                return role;
        }
        return null;
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
