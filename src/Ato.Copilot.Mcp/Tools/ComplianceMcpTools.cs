using System.ComponentModel;
using Ato.Copilot.Agents.Compliance.Tools;

namespace Ato.Copilot.Mcp.Tools;

/// <summary>
/// MCP tools for compliance operations. Wraps Agent Framework compliance tools
/// for exposure via the MCP protocol (GitHub Copilot, Claude Desktop, etc.)
/// </summary>
public class ComplianceMcpTools
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

    // Kanban tools
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

    // Auth/PIM tools
    private readonly CacStatusTool _cacStatus;
    private readonly CacSignOutTool _cacSignOut;

    // CAC session config (Phase 10 — US8)
    private readonly CacSetTimeoutTool _cacSetTimeout;

    // Certificate mapping (Phase 11 — US9)
    private readonly CacMapCertificateTool _cacMapCertificate;

    // PIM role management tools
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

    // Compliance Watch notification & escalation tools (Feature 005 — US4)
    private readonly WatchConfigureNotificationsTool _watchConfigureNotifications;
    private readonly WatchConfigureEscalationTool _watchConfigureEscalation;

    // Compliance Watch dashboard & reporting tools (Feature 005 — US5)
    private readonly WatchAlertHistoryTool _watchAlertHistory;
    private readonly WatchComplianceTrendTool _watchComplianceTrend;
    private readonly WatchAlertStatisticsTool _watchAlertStatistics;

    // Compliance Watch integration tools (Feature 005 — US8)
    private readonly WatchCreateTaskFromAlertTool _watchCreateTaskFromAlert;
    private readonly WatchCollectEvidenceFromAlertTool _watchCollectEvidenceFromAlert;

    // Compliance Watch auto-remediation tools (Feature 005 — US9)
    private readonly WatchCreateAutoRemediationRuleTool _watchCreateAutoRemediationRule;
    private readonly WatchListAutoRemediationRulesTool _watchListAutoRemediationRules;

    // NIST Controls knowledge tools (Feature 007)
    private readonly NistControlSearchTool _nistControlSearchTool;
    private readonly NistControlExplainerTool _nistControlExplainerTool;

    public ComplianceMcpTools(
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
        NistControlExplainerTool nistControlExplainerTool)
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
    }

    [Description("Run a NIST 800-53 compliance assessment. Scan types: quick, policy, full.")]
    public async Task<string> RunComplianceAssessmentAsync(
        string? subscriptionId = null, string? framework = null,
        string? controlFamilies = null, string? resourceTypes = null,
        string? scanType = null, bool includePassed = false,
        CancellationToken cancellationToken = default)
    {
        var args = new Dictionary<string, object?>
        {
            ["subscription_id"] = subscriptionId, ["framework"] = framework,
            ["control_families"] = controlFamilies, ["resource_types"] = resourceTypes,
            ["scan_type"] = scanType, ["include_passed"] = includePassed
        };
        return await _assessmentTool.ExecuteAsync(args, cancellationToken);
    }

    [Description("Get detailed information about a NIST 800-53 control family.")]
    public async Task<string> GetControlFamilyInfoAsync(
        string familyId, bool includeControls = true, CancellationToken cancellationToken = default)
    {
        var args = new Dictionary<string, object?> { ["family_id"] = familyId, ["include_controls"] = includeControls };
        return await _controlFamilyTool.ExecuteAsync(args, cancellationToken);
    }

    [Description("Generate compliance documentation (SSP, POA&M, SAR).")]
    public async Task<string> GenerateComplianceDocumentAsync(
        string documentType, string? subscriptionId = null,
        string? framework = null, string? systemName = null,
        CancellationToken cancellationToken = default)
    {
        var args = new Dictionary<string, object?>
        {
            ["document_type"] = documentType, ["subscription_id"] = subscriptionId,
            ["framework"] = framework, ["system_name"] = systemName
        };
        return await _documentGenerationTool.ExecuteAsync(args, cancellationToken);
    }

    [Description("Collect compliance evidence from Azure resources.")]
    public async Task<string> CollectComplianceEvidenceAsync(
        string controlId, string? subscriptionId = null,
        string? resourceGroup = null, CancellationToken cancellationToken = default)
    {
        var args = new Dictionary<string, object?>
        {
            ["control_id"] = controlId, ["subscription_id"] = subscriptionId, ["resource_group"] = resourceGroup
        };
        return await _evidenceCollectionTool.ExecuteAsync(args, cancellationToken);
    }

    [Description("Remediate a compliance finding with guided or automated fixes.")]
    public async Task<string> RemediateComplianceFindingAsync(
        string findingId, bool applyRemediation = false, bool dryRun = true,
        CancellationToken cancellationToken = default)
    {
        var args = new Dictionary<string, object?>
        {
            ["finding_id"] = findingId, ["apply_remediation"] = applyRemediation, ["dry_run"] = dryRun
        };
        return await _remediationTool.ExecuteAsync(args, cancellationToken);
    }

    [Description("Validate that a remediation was successfully applied.")]
    public async Task<string> ValidateRemediationAsync(
        string findingId, string? executionId = null, string? subscriptionId = null,
        CancellationToken cancellationToken = default)
    {
        var args = new Dictionary<string, object?>
        {
            ["finding_id"] = findingId, ["execution_id"] = executionId, ["subscription_id"] = subscriptionId
        };
        return await _validateRemediationTool.ExecuteAsync(args, cancellationToken);
    }

    [Description("Generate a prioritized remediation plan for compliance findings.")]
    public async Task<string> GenerateRemediationPlanAsync(
        string? subscriptionId = null, string? resourceGroupName = null,
        CancellationToken cancellationToken = default)
    {
        var args = new Dictionary<string, object?>
        {
            ["subscription_id"] = subscriptionId, ["resource_group_name"] = resourceGroupName
        };
        return await _remediationPlanTool.ExecuteAsync(args, cancellationToken);
    }

    [Description("Get the audit trail of compliance assessments.")]
    public async Task<string> GetAssessmentAuditLogAsync(
        string? subscriptionId = null, int days = 7, CancellationToken cancellationToken = default)
    {
        var args = new Dictionary<string, object?> { ["subscription_id"] = subscriptionId, ["days"] = days };
        return await _auditLogTool.ExecuteAsync(args, cancellationToken);
    }

    [Description("Get compliance history and trends over time.")]
    public async Task<string> GetComplianceHistoryAsync(
        string? subscriptionId = null, int days = 30, CancellationToken cancellationToken = default)
    {
        var args = new Dictionary<string, object?> { ["subscription_id"] = subscriptionId, ["days"] = days };
        return await _historyTool.ExecuteAsync(args, cancellationToken);
    }

    [Description("Get current compliance status summary.")]
    public async Task<string> GetComplianceStatusAsync(
        string? subscriptionId = null, string? framework = null,
        CancellationToken cancellationToken = default)
    {
        var args = new Dictionary<string, object?> { ["subscription_id"] = subscriptionId, ["framework"] = framework };
        return await _statusTool.ExecuteAsync(args, cancellationToken);
    }

    [Description("Query continuous compliance monitoring status, alerts, and trends.")]
    public async Task<string> GetComplianceMonitoringAsync(
        string action, string? subscriptionId = null, int days = 30,
        CancellationToken cancellationToken = default)
    {
        var args = new Dictionary<string, object?>
        {
            ["action"] = action, ["subscription_id"] = subscriptionId, ["days"] = days
        };
        return await _monitoringTool.ExecuteAsync(args, cancellationToken);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Kanban Board Tools
    // ═══════════════════════════════════════════════════════════════════════════

    [Description("Create a new Kanban remediation board, optionally from an assessment.")]
    public async Task<string> KanbanCreateBoardAsync(
        string name, string? subscriptionId = null, string? assessmentId = null,
        string? owner = null, CancellationToken cancellationToken = default)
    {
        var args = new Dictionary<string, object?>
        {
            ["name"] = name, ["subscription_id"] = subscriptionId,
            ["assessment_id"] = assessmentId, ["owner"] = owner
        };
        return await _kanbanCreateBoard.ExecuteAsync(args, cancellationToken);
    }

    [Description("Show a Kanban board overview with columns and task summaries.")]
    public async Task<string> KanbanBoardShowAsync(
        string boardId, bool includeTaskSummaries = true,
        int page = 1, int pageSize = 25,
        CancellationToken cancellationToken = default)
    {
        var args = new Dictionary<string, object?>
        {
            ["board_id"] = boardId, ["include_task_summaries"] = includeTaskSummaries,
            ["page"] = page, ["page_size"] = pageSize
        };
        return await _kanbanBoardShow.ExecuteAsync(args, cancellationToken);
    }

    [Description("Get full details of a single remediation task.")]
    public async Task<string> KanbanGetTaskAsync(
        string taskId, string? boardId = null,
        CancellationToken cancellationToken = default)
    {
        var args = new Dictionary<string, object?>
        {
            ["task_id"] = taskId, ["board_id"] = boardId
        };
        return await _kanbanGetTask.ExecuteAsync(args, cancellationToken);
    }

    [Description("Create a new remediation task on a Kanban board.")]
    public async Task<string> KanbanCreateTaskAsync(
        string boardId, string title, string controlId,
        string? description = null, string? severity = null,
        string? assigneeId = null, string? assigneeName = null,
        string? dueDate = null, string? remediationScript = null,
        string? validationCriteria = null,
        CancellationToken cancellationToken = default)
    {
        var args = new Dictionary<string, object?>
        {
            ["board_id"] = boardId, ["title"] = title, ["control_id"] = controlId,
            ["description"] = description, ["severity"] = severity,
            ["assignee_id"] = assigneeId, ["assignee_name"] = assigneeName,
            ["due_date"] = dueDate, ["remediation_script"] = remediationScript,
            ["validation_criteria"] = validationCriteria
        };
        return await _kanbanCreateTask.ExecuteAsync(args, cancellationToken);
    }

    [Description("Assign or unassign a remediation task.")]
    public async Task<string> KanbanAssignTaskAsync(
        string taskId, string? boardId = null,
        string? assigneeId = null, string? assigneeName = null,
        CancellationToken cancellationToken = default)
    {
        var args = new Dictionary<string, object?>
        {
            ["task_id"] = taskId, ["board_id"] = boardId,
            ["assignee_id"] = assigneeId, ["assignee_name"] = assigneeName
        };
        return await _kanbanAssignTask.ExecuteAsync(args, cancellationToken);
    }

    [Description("Move a remediation task to a new Kanban column (status transition).")]
    public async Task<string> KanbanMoveTaskAsync(
        string taskId, string targetStatus,
        string? boardId = null, string? comment = null,
        bool skipValidation = false,
        CancellationToken cancellationToken = default)
    {
        var args = new Dictionary<string, object?>
        {
            ["task_id"] = taskId, ["target_status"] = targetStatus,
            ["board_id"] = boardId, ["comment"] = comment,
            ["skip_validation"] = skipValidation
        };
        return await _kanbanMoveTask.ExecuteAsync(args, cancellationToken);
    }

    [Description("List and filter remediation tasks on a Kanban board.")]
    public async Task<string> KanbanTaskListAsync(
        string boardId, string? status = null, string? severity = null,
        string? assigneeId = null, string? controlFamily = null,
        bool? isOverdue = null, string? sortBy = null, string? sortOrder = null,
        int page = 1, int pageSize = 25,
        CancellationToken cancellationToken = default)
    {
        var args = new Dictionary<string, object?>
        {
            ["board_id"] = boardId, ["status"] = status, ["severity"] = severity,
            ["assignee_id"] = assigneeId, ["control_family"] = controlFamily,
            ["is_overdue"] = isOverdue, ["sort_by"] = sortBy, ["sort_order"] = sortOrder,
            ["page"] = page, ["page_size"] = pageSize
        };
        return await _kanbanTaskList.ExecuteAsync(args, cancellationToken);
    }

    [Description("View the full audit trail and history of a remediation task.")]
    public async Task<string> KanbanTaskHistoryAsync(
        string taskId, string? boardId = null, string? eventType = null,
        int page = 1, int pageSize = 25,
        CancellationToken cancellationToken = default)
    {
        var args = new Dictionary<string, object?>
        {
            ["task_id"] = taskId, ["board_id"] = boardId,
            ["event_type"] = eventType, ["page"] = page, ["page_size"] = pageSize
        };
        return await _kanbanTaskHistory.ExecuteAsync(args, cancellationToken);
    }

    [Description("Run a targeted validation scan to verify a remediation fix.")]
    public async Task<string> KanbanValidateTaskAsync(
        string taskId, string? boardId = null, string? subscriptionId = null,
        CancellationToken cancellationToken = default)
    {
        var args = new Dictionary<string, object?>
        {
            ["task_id"] = taskId, ["board_id"] = boardId,
            ["subscription_id"] = subscriptionId
        };
        return await _kanbanValidateTask.ExecuteAsync(args, cancellationToken);
    }

    [Description("Add a comment to a remediation task (Markdown, @mentions).")]
    public async Task<string> KanbanAddCommentAsync(
        string taskId, string content,
        string? boardId = null, string? parentCommentId = null,
        CancellationToken cancellationToken = default)
    {
        var args = new Dictionary<string, object?>
        {
            ["task_id"] = taskId, ["content"] = content,
            ["board_id"] = boardId, ["parent_comment_id"] = parentCommentId
        };
        return await _kanbanAddComment.ExecuteAsync(args, cancellationToken);
    }

    [Description("List all comments on a remediation task.")]
    public async Task<string> KanbanTaskCommentsAsync(
        string taskId, string? boardId = null, bool includeDeleted = false,
        int page = 1, int pageSize = 25,
        CancellationToken cancellationToken = default)
    {
        var args = new Dictionary<string, object?>
        {
            ["task_id"] = taskId, ["board_id"] = boardId,
            ["include_deleted"] = includeDeleted, ["page"] = page, ["page_size"] = pageSize
        };
        return await _kanbanTaskComments.ExecuteAsync(args, cancellationToken);
    }

    [Description("Edit an existing comment (within 24h window).")]
    public async Task<string> KanbanEditCommentAsync(
        string commentId, string taskId, string content,
        string? boardId = null,
        CancellationToken cancellationToken = default)
    {
        var args = new Dictionary<string, object?>
        {
            ["comment_id"] = commentId, ["task_id"] = taskId,
            ["content"] = content, ["board_id"] = boardId
        };
        return await _kanbanEditComment.ExecuteAsync(args, cancellationToken);
    }

    [Description("Delete a comment on a remediation task (soft delete).")]
    public async Task<string> KanbanDeleteCommentAsync(
        string commentId, string taskId,
        string? boardId = null,
        CancellationToken cancellationToken = default)
    {
        var args = new Dictionary<string, object?>
        {
            ["comment_id"] = commentId, ["task_id"] = taskId, ["board_id"] = boardId
        };
        return await _kanbanDeleteComment.ExecuteAsync(args, cancellationToken);
    }

    [Description("Execute the remediation script for a task.")]
    public async Task<string> KanbanRemediateTaskAsync(
        string taskId, string? boardId = null,
        CancellationToken cancellationToken = default)
    {
        var args = new Dictionary<string, object?>
        {
            ["task_id"] = taskId, ["board_id"] = boardId
        };
        return await _kanbanRemediateTask.ExecuteAsync(args, cancellationToken);
    }

    [Description("Collect compliance evidence for a remediation task.")]
    public async Task<string> KanbanCollectEvidenceAsync(
        string taskId, string? boardId = null, string? subscriptionId = null,
        CancellationToken cancellationToken = default)
    {
        var args = new Dictionary<string, object?>
        {
            ["task_id"] = taskId, ["board_id"] = boardId,
            ["subscription_id"] = subscriptionId
        };
        return await _kanbanCollectEvidence.ExecuteAsync(args, cancellationToken);
    }

    [Description("Perform bulk operations on multiple remediation tasks.")]
    public async Task<string> KanbanBulkUpdateAsync(
        string boardId, string operation,
        string? assigneeId = null, string? assigneeName = null,
        string? targetStatus = null, string? dueDate = null,
        string? comment = null,
        CancellationToken cancellationToken = default)
    {
        var args = new Dictionary<string, object?>
        {
            ["board_id"] = boardId, ["operation"] = operation,
            ["task_ids"] = Array.Empty<string>(),
            ["assignee_id"] = assigneeId, ["assignee_name"] = assigneeName,
            ["target_status"] = targetStatus, ["due_date"] = dueDate, ["comment"] = comment
        };
        return await _kanbanBulkUpdate.ExecuteAsync(args, cancellationToken);
    }

    [Description("Export Kanban board data as CSV or POA&M.")]
    public async Task<string> KanbanExportAsync(
        string boardId, string format = "csv",
        string? statuses = null, bool includeHistory = false,
        CancellationToken cancellationToken = default)
    {
        var args = new Dictionary<string, object?>
        {
            ["board_id"] = boardId, ["format"] = format,
            ["statuses"] = statuses, ["include_history"] = includeHistory
        };
        return await _kanbanExport.ExecuteAsync(args, cancellationToken);
    }

    [Description("Archive a Kanban board (read-only).")]
    public async Task<string> KanbanArchiveBoardAsync(
        string boardId, bool confirm = false,
        CancellationToken cancellationToken = default)
    {
        var args = new Dictionary<string, object?>
        {
            ["board_id"] = boardId, ["confirm"] = confirm
        };
        return await _kanbanArchiveBoard.ExecuteAsync(args, cancellationToken);
    }

    // ─── Auth/PIM MCP Wrappers ───────────────────────────────────────────

    [Description("Check current CAC authentication status, session information, and active PIM roles.")]
    public async Task<string> CacStatusAsync(
        string? userId = null,
        CancellationToken cancellationToken = default)
    {
        var args = new Dictionary<string, object?> { ["user_id"] = userId };
        return await _cacStatus.ExecuteAsync(args, cancellationToken);
    }

    [Description("End the current CAC session, clear cached tokens, and revert to unauthenticated state.")]
    public async Task<string> CacSignOutAsync(
        string? userId = null,
        CancellationToken cancellationToken = default)
    {
        var args = new Dictionary<string, object?> { ["user_id"] = userId };
        return await _cacSignOut.ExecuteAsync(args, cancellationToken);
    }

    [Description("Set the CAC session timeout duration within policy limits (1-24 hours).")]
    public async Task<string> CacSetTimeoutAsync(
        string? userId = null, int? timeoutHours = null,
        CancellationToken cancellationToken = default)
    {
        var args = new Dictionary<string, object?> { ["user_id"] = userId, ["timeoutHours"] = timeoutHours };
        return await _cacSetTimeout.ExecuteAsync(args, cancellationToken);
    }

    [Description("Map the current CAC certificate identity to a platform role for automatic role resolution.")]
    public async Task<string> CacMapCertificateAsync(
        string? userId = null, string? role = null,
        CancellationToken cancellationToken = default)
    {
        var args = new Dictionary<string, object?> { ["user_id"] = userId, ["role"] = role };
        return await _cacMapCertificate.ExecuteAsync(args, cancellationToken);
    }

    // ─── PIM Role Management MCP Wrappers ────────────────────────────────

    [Description("List all PIM-eligible role assignments for the authenticated user.")]
    public async Task<string> PimListEligibleAsync(
        string? userId = null, string? scope = null,
        CancellationToken cancellationToken = default)
    {
        var args = new Dictionary<string, object?> { ["user_id"] = userId, ["scope"] = scope };
        return await _pimListEligible.ExecuteAsync(args, cancellationToken);
    }

    [Description("Activate an eligible PIM role with justification and optional ticket number.")]
    public async Task<string> PimActivateRoleAsync(
        string? userId = null, string? roleName = null, string? scope = null,
        string? justification = null, string? ticketNumber = null,
        int? durationHours = null, string? sessionId = null,
        CancellationToken cancellationToken = default)
    {
        var args = new Dictionary<string, object?>
        {
            ["user_id"] = userId, ["roleName"] = roleName, ["scope"] = scope,
            ["justification"] = justification, ["ticketNumber"] = ticketNumber,
            ["durationHours"] = durationHours, ["session_id"] = sessionId
        };
        return await _pimActivateRole.ExecuteAsync(args, cancellationToken);
    }

    [Description("Deactivate an active PIM role to restore least-privilege posture.")]
    public async Task<string> PimDeactivateRoleAsync(
        string? userId = null, string? roleName = null, string? scope = null,
        CancellationToken cancellationToken = default)
    {
        var args = new Dictionary<string, object?> { ["user_id"] = userId, ["roleName"] = roleName, ["scope"] = scope };
        return await _pimDeactivateRole.ExecuteAsync(args, cancellationToken);
    }

    // ─── PIM Session Management MCP Wrappers ─────────────────────────────

    [Description("List all currently active PIM role assignments for the authenticated user.")]
    public async Task<string> PimListActiveAsync(
        string? userId = null,
        CancellationToken cancellationToken = default)
    {
        var args = new Dictionary<string, object?> { ["user_id"] = userId };
        return await _pimListActive.ExecuteAsync(args, cancellationToken);
    }

    [Description("Extend an active PIM role's duration within policy limits.")]
    public async Task<string> PimExtendRoleAsync(
        string? userId = null, string? roleName = null, string? scope = null,
        int? additionalHours = null,
        CancellationToken cancellationToken = default)
    {
        var args = new Dictionary<string, object?>
        {
            ["user_id"] = userId, ["roleName"] = roleName,
            ["scope"] = scope, ["additionalHours"] = additionalHours
        };
        return await _pimExtendRole.ExecuteAsync(args, cancellationToken);
    }

    [Description("Approve a pending PIM role activation request. Requires SecurityLead or Administrator role.")]
    public async Task<string> PimApproveRequestAsync(
        string? userId = null, string? userRole = null, string? requestId = null,
        string? comments = null,
        CancellationToken cancellationToken = default)
    {
        var args = new Dictionary<string, object?>
        {
            ["user_id"] = userId, ["user_role"] = userRole,
            ["requestId"] = requestId, ["comments"] = comments
        };
        return await _pimApproveRequest.ExecuteAsync(args, cancellationToken);
    }

    [Description("Deny a pending PIM role activation request. Requires SecurityLead or Administrator role.")]
    public async Task<string> PimDenyRequestAsync(
        string? userId = null, string? userRole = null, string? requestId = null,
        string? reason = null,
        CancellationToken cancellationToken = default)
    {
        var args = new Dictionary<string, object?>
        {
            ["user_id"] = userId, ["user_role"] = userRole,
            ["requestId"] = requestId, ["reason"] = reason
        };
        return await _pimDenyRequest.ExecuteAsync(args, cancellationToken);
    }

    // ─── JIT VM Access Wrappers (Phase 9 — US7) ─────────────────────────

    [Description("Request Just-in-Time VM access through Azure Defender for Cloud. Creates a temporary NSG rule.")]
    public async Task<string> JitRequestAccessAsync(
        string? userId = null, string? vmName = null, string? resourceGroup = null,
        string? subscriptionId = null, int? port = null, string? protocol = null,
        string? sourceIp = null, int? durationHours = null,
        string? justification = null, string? ticketNumber = null,
        string? sessionId = null,
        CancellationToken cancellationToken = default)
    {
        var args = new Dictionary<string, object?>
        {
            ["user_id"] = userId, ["vmName"] = vmName, ["resourceGroup"] = resourceGroup,
            ["subscriptionId"] = subscriptionId, ["port"] = port, ["protocol"] = protocol,
            ["sourceIp"] = sourceIp, ["durationHours"] = durationHours,
            ["justification"] = justification, ["ticketNumber"] = ticketNumber,
            ["session_id"] = sessionId
        };
        return await _jitRequestAccess.ExecuteAsync(args, cancellationToken);
    }

    [Description("List all active JIT VM access sessions for the authenticated user.")]
    public async Task<string> JitListSessionsAsync(
        string? userId = null,
        CancellationToken cancellationToken = default)
    {
        var args = new Dictionary<string, object?> { ["user_id"] = userId };
        return await _jitListSessions.ExecuteAsync(args, cancellationToken);
    }

    [Description("Immediately revoke JIT VM access, removing the NSG rule.")]
    public async Task<string> JitRevokeAccessAsync(
        string? userId = null, string? vmName = null, string? resourceGroup = null,
        CancellationToken cancellationToken = default)
    {
        var args = new Dictionary<string, object?>
        {
            ["user_id"] = userId, ["vmName"] = vmName, ["resourceGroup"] = resourceGroup
        };
        return await _jitRevokeAccess.ExecuteAsync(args, cancellationToken);
    }

    // ─── PIM Audit Trail MCP Wrapper ────────────────────────────────────────

    [Description("Query PIM action history for compliance evidence and audit trail.")]
    public async Task<string> PimHistoryAsync(
        string? userId = null, int? days = null, string? roleName = null,
        string? filterUserId = null, string? scope = null,
        CancellationToken cancellationToken = default)
    {
        var args = new Dictionary<string, object?>
        {
            ["user_id"] = userId, ["days"] = days, ["roleName"] = roleName,
            ["filterUserId"] = filterUserId, ["scope"] = scope
        };
        return await _pimHistory.ExecuteAsync(args, cancellationToken);
    }

    // ─── Compliance Watch Monitoring MCP Wrappers ────────────────────────────

    [Description("Enable continuous compliance monitoring for a subscription or resource group.")]
    public async Task<string> WatchEnableMonitoringAsync(
        string subscriptionId, string? resourceGroup = null,
        string? frequency = null, string? mode = null,
        CancellationToken cancellationToken = default)
    {
        var args = new Dictionary<string, object?>
        {
            ["subscription_id"] = subscriptionId, ["resource_group"] = resourceGroup,
            ["frequency"] = frequency, ["mode"] = mode
        };
        return await _watchEnableMonitoring.ExecuteAsync(args, cancellationToken);
    }

    [Description("Disable monitoring for a subscription or resource group.")]
    public async Task<string> WatchDisableMonitoringAsync(
        string subscriptionId, string? resourceGroup = null,
        CancellationToken cancellationToken = default)
    {
        var args = new Dictionary<string, object?>
        {
            ["subscription_id"] = subscriptionId, ["resource_group"] = resourceGroup
        };
        return await _watchDisableMonitoring.ExecuteAsync(args, cancellationToken);
    }

    [Description("Update monitoring settings for an existing configuration.")]
    public async Task<string> WatchConfigureMonitoringAsync(
        string subscriptionId, string? resourceGroup = null,
        string? frequency = null, string? mode = null,
        CancellationToken cancellationToken = default)
    {
        var args = new Dictionary<string, object?>
        {
            ["subscription_id"] = subscriptionId, ["resource_group"] = resourceGroup,
            ["frequency"] = frequency, ["mode"] = mode
        };
        return await _watchConfigureMonitoring.ExecuteAsync(args, cancellationToken);
    }

    [Description("Show current monitoring configuration and status.")]
    public async Task<string> WatchMonitoringStatusAsync(
        string? subscriptionId = null,
        CancellationToken cancellationToken = default)
    {
        var args = new Dictionary<string, object?>
        {
            ["subscription_id"] = subscriptionId
        };
        return await _watchMonitoringStatus.ExecuteAsync(args, cancellationToken);
    }

    // ─── Alert Lifecycle Tools (US2) ────────────────────────────────────────

    [Description("List active compliance alerts with optional severity, status, control family, and date filters.")]
    public async Task<string> WatchShowAlertsAsync(
        string? subscriptionId = null,
        string? severity = null,
        string? status = null,
        string? controlFamily = null,
        int? days = null,
        int? page = null,
        int? pageSize = null,
        CancellationToken cancellationToken = default)
    {
        var args = new Dictionary<string, object?>
        {
            ["subscription_id"] = subscriptionId, ["severity"] = severity,
            ["status"] = status, ["control_family"] = controlFamily,
            ["days"] = days, ["page"] = page, ["page_size"] = pageSize
        };
        return await _watchShowAlerts.ExecuteAsync(args, cancellationToken);
    }

    [Description("Get full details of a specific compliance alert.")]
    public async Task<string> WatchGetAlertAsync(
        string alertId,
        CancellationToken cancellationToken = default)
    {
        var args = new Dictionary<string, object?> { ["alert_id"] = alertId };
        return await _watchGetAlert.ExecuteAsync(args, cancellationToken);
    }

    [Description("Acknowledge a compliance alert, pausing the escalation timer.")]
    public async Task<string> WatchAcknowledgeAlertAsync(
        string alertId,
        string userId,
        string userRole,
        CancellationToken cancellationToken = default)
    {
        var args = new Dictionary<string, object?>
        {
            ["alert_id"] = alertId, ["user_id"] = userId, ["user_role"] = userRole
        };
        return await _watchAcknowledgeAlert.ExecuteAsync(args, cancellationToken);
    }

    [Description("Execute remediation for an alert and transition to Resolved.")]
    public async Task<string> WatchFixAlertAsync(
        string alertId,
        string userId,
        string userRole,
        bool dryRun = false,
        CancellationToken cancellationToken = default)
    {
        var args = new Dictionary<string, object?>
        {
            ["alert_id"] = alertId, ["user_id"] = userId,
            ["user_role"] = userRole, ["dry_run"] = dryRun
        };
        return await _watchFixAlert.ExecuteAsync(args, cancellationToken);
    }

    [Description("Dismiss a compliance alert. Requires Compliance Officer role and justification.")]
    public async Task<string> WatchDismissAlertAsync(
        string alertId,
        string justification,
        string userId,
        string userRole,
        CancellationToken cancellationToken = default)
    {
        var args = new Dictionary<string, object?>
        {
            ["alert_id"] = alertId, ["justification"] = justification,
            ["user_id"] = userId, ["user_role"] = userRole
        };
        return await _watchDismissAlert.ExecuteAsync(args, cancellationToken);
    }

    // ─── Alert Rules & Suppression MCP Tools (US3) ───────────────────────

    [Description("Create a custom alert rule to control severity and routing for compliance alerts.")]
    public async Task<string> WatchCreateRuleAsync(
        string name,
        string userRole,
        string? description = null,
        string? subscriptionId = null,
        string? resourceGroup = null,
        string? resourceType = null,
        string? resourceId = null,
        string? controlFamily = null,
        string? controlId = null,
        string? severityOverride = null,
        string? userId = null,
        CancellationToken cancellationToken = default)
    {
        var args = new Dictionary<string, object?>
        {
            ["name"] = name, ["description"] = description,
            ["subscription_id"] = subscriptionId, ["resource_group"] = resourceGroup,
            ["resource_type"] = resourceType, ["resource_id"] = resourceId,
            ["control_family"] = controlFamily, ["control_id"] = controlId,
            ["severity_override"] = severityOverride,
            ["user_role"] = userRole, ["user_id"] = userId
        };
        return await _watchCreateRule.ExecuteAsync(args, cancellationToken);
    }

    [Description("List configured alert rules for a subscription.")]
    public async Task<string> WatchListRulesAsync(
        string? subscriptionId = null,
        CancellationToken cancellationToken = default)
    {
        var args = new Dictionary<string, object?> { ["subscription_id"] = subscriptionId };
        return await _watchListRules.ExecuteAsync(args, cancellationToken);
    }

    [Description("Create a suppression rule to suppress alerts matching specific criteria.")]
    public async Task<string> WatchSuppressAlertsAsync(
        string type,
        string userRole,
        string? subscriptionId = null,
        string? resourceId = null,
        string? controlFamily = null,
        string? controlId = null,
        string? justification = null,
        string? expiresAt = null,
        string? userId = null,
        CancellationToken cancellationToken = default)
    {
        var args = new Dictionary<string, object?>
        {
            ["subscription_id"] = subscriptionId, ["resource_id"] = resourceId,
            ["control_family"] = controlFamily, ["control_id"] = controlId,
            ["type"] = type, ["justification"] = justification,
            ["expires_at"] = expiresAt,
            ["user_role"] = userRole, ["user_id"] = userId
        };
        return await _watchSuppressAlerts.ExecuteAsync(args, cancellationToken);
    }

    [Description("List active alert suppression rules.")]
    public async Task<string> WatchListSuppressionsAsync(
        string? subscriptionId = null,
        CancellationToken cancellationToken = default)
    {
        var args = new Dictionary<string, object?> { ["subscription_id"] = subscriptionId };
        return await _watchListSuppressions.ExecuteAsync(args, cancellationToken);
    }

    [Description("Configure quiet hours during which non-Critical alerts are suppressed. Critical alerts always deliver.")]
    public async Task<string> WatchConfigureQuietHoursAsync(
        string subscriptionId,
        string startTime,
        string endTime,
        string userRole,
        string? userId = null,
        CancellationToken cancellationToken = default)
    {
        var args = new Dictionary<string, object?>
        {
            ["subscription_id"] = subscriptionId,
            ["start_time"] = startTime, ["end_time"] = endTime,
            ["user_role"] = userRole, ["user_id"] = userId
        };
        return await _watchConfigureQuietHours.ExecuteAsync(args, cancellationToken);
    }

    [Description("Configure notification channels (email, webhook) for compliance alerts.")]
    public async Task<string> WatchConfigureNotificationsAsync(
        string channel,
        string target,
        string userRole,
        string? severity = null,
        string? subscriptionId = null,
        string? userId = null,
        CancellationToken cancellationToken = default)
    {
        var args = new Dictionary<string, object?>
        {
            ["channel"] = channel, ["target"] = target,
            ["severity"] = severity, ["subscription_id"] = subscriptionId,
            ["role"] = userRole, ["user_id"] = userId
        };
        return await _watchConfigureNotifications.ExecuteAsync(args, cancellationToken);
    }

    [Description("Configure an escalation path for SLA violation detection and automatic escalation.")]
    public async Task<string> WatchConfigureEscalationAsync(
        string name,
        string severity,
        int delayMinutes,
        string recipients,
        string userRole,
        string? channel = null,
        int? repeatMinutes = null,
        string? webhookUrl = null,
        string? userId = null,
        CancellationToken cancellationToken = default)
    {
        var args = new Dictionary<string, object?>
        {
            ["name"] = name, ["severity"] = severity,
            ["delay_minutes"] = delayMinutes.ToString(),
            ["recipients"] = recipients, ["channel"] = channel,
            ["repeat_minutes"] = repeatMinutes?.ToString(),
            ["webhook_url"] = webhookUrl,
            ["role"] = userRole, ["user_id"] = userId
        };
        return await _watchConfigureEscalation.ExecuteAsync(args, cancellationToken);
    }

    // ── Dashboard & Reporting tools (Feature 005 — US5) ─────────────────

    [Description("Query compliance alert history with natural-language support. " +
        "Supports queries like 'What drifted this week?' or structured filters by severity, status, control family.")]
    public async Task<string> WatchAlertHistoryAsync(
        string? query = null,
        string? subscriptionId = null,
        string? severity = null,
        string? status = null,
        string? controlFamily = null,
        int? days = null,
        int? page = null,
        int? pageSize = null,
        CancellationToken cancellationToken = default)
    {
        var args = new Dictionary<string, object?>
        {
            ["query"] = query, ["subscriptionId"] = subscriptionId,
            ["severity"] = severity, ["status"] = status,
            ["controlFamily"] = controlFamily,
            ["days"] = days?.ToString(), ["page"] = page?.ToString(),
            ["pageSize"] = pageSize?.ToString()
        };
        return await _watchAlertHistory.ExecuteAsync(args, cancellationToken);
    }

    [Description("View compliance score trends over time with direction indicators (improving/declining/stable).")]
    public async Task<string> WatchComplianceTrendAsync(
        string subscriptionId,
        int? days = null,
        bool? weekly = null,
        CancellationToken cancellationToken = default)
    {
        var args = new Dictionary<string, object?>
        {
            ["subscriptionId"] = subscriptionId,
            ["days"] = days?.ToString(),
            ["weekly"] = weekly?.ToString()
        };
        return await _watchComplianceTrend.ExecuteAsync(args, cancellationToken);
    }

    [Description("Get alert statistics including counts by severity, type, and status; " +
        "average resolution time; escalation count; and auto-resolved count.")]
    public async Task<string> WatchAlertStatisticsAsync(
        string? subscriptionId = null,
        int? days = null,
        CancellationToken cancellationToken = default)
    {
        var args = new Dictionary<string, object?>
        {
            ["subscriptionId"] = subscriptionId,
            ["days"] = days?.ToString()
        };
        return await _watchAlertStatistics.ExecuteAsync(args, cancellationToken);
    }

    [Description("Create a Kanban remediation task from a compliance alert. " +
        "Pre-populates title, description, severity, and control mapping.")]
    public async Task<string> WatchCreateTaskFromAlertAsync(
        string alertId,
        string? boardId = null,
        CancellationToken cancellationToken = default)
    {
        var args = new Dictionary<string, object?>
        {
            ["alertId"] = alertId,
            ["boardId"] = boardId
        };
        return await _watchCreateTaskFromAlert.ExecuteAsync(args, cancellationToken);
    }

    [Description("Capture alert details, timeline, and context as compliance evidence for audit trails.")]
    public async Task<string> WatchCollectEvidenceFromAlertAsync(
        string alertId,
        CancellationToken cancellationToken = default)
    {
        var args = new Dictionary<string, object?>
        {
            ["alertId"] = alertId
        };
        return await _watchCollectEvidenceFromAlert.ExecuteAsync(args, cancellationToken);
    }

    [Description("Create an opt-in auto-remediation rule. AC, IA, SC control families are blocked and always require human approval.")]
    public async Task<string> WatchCreateAutoRemediationRuleAsync(
        string name,
        string action,
        string? subscriptionId = null,
        string? resourceGroup = null,
        string? controlFamily = null,
        string? controlId = null,
        string? approvalMode = null,
        CancellationToken cancellationToken = default)
    {
        var args = new Dictionary<string, object?>
        {
            ["name"] = name,
            ["action"] = action,
            ["subscriptionId"] = subscriptionId,
            ["resourceGroup"] = resourceGroup,
            ["controlFamily"] = controlFamily,
            ["controlId"] = controlId,
            ["approvalMode"] = approvalMode
        };
        return await _watchCreateAutoRemediationRule.ExecuteAsync(args, cancellationToken);
    }

    [Description("List auto-remediation rules and their execution history.")]
    public async Task<string> WatchListAutoRemediationRulesAsync(
        string? subscriptionId = null,
        bool includeDisabled = false,
        CancellationToken cancellationToken = default)
    {
        var args = new Dictionary<string, object?>
        {
            ["subscriptionId"] = subscriptionId,
            ["includeDisabled"] = includeDisabled.ToString()
        };
        return await _watchListAutoRemediationRules.ExecuteAsync(args, cancellationToken);
    }

    [Description("Search NIST SP 800-53 Rev 5 controls by keyword, phrase, or control family.")]
    public async Task<string> SearchNistControlsAsync(
        string query,
        string? family = null,
        int? maxResults = null,
        CancellationToken cancellationToken = default)
    {
        var args = new Dictionary<string, object?>
        {
            ["query"] = query,
            ["family"] = family,
            ["max_results"] = maxResults
        };
        return await _nistControlSearchTool.ExecuteAsync(args, cancellationToken);
    }

    [Description("Get a detailed explanation of a specific NIST SP 800-53 control including statement, guidance, and assessment objectives.")]
    public async Task<string> ExplainNistControlAsync(
        string controlId,
        CancellationToken cancellationToken = default)
    {
        var args = new Dictionary<string, object?>
        {
            ["control_id"] = controlId
        };
        return await _nistControlExplainerTool.ExecuteAsync(args, cancellationToken);
    }
}
