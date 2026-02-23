using Xunit;
using FluentAssertions;
using Moq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Ato.Copilot.Agents.Compliance.Agents;
using Ato.Copilot.Agents.Compliance.Tools;
using Ato.Copilot.Agents.Common;
using Ato.Copilot.Core.Data.Context;
using Ato.Copilot.Core.Interfaces.Compliance;

namespace Ato.Copilot.Tests.Unit.Agents;

public class ComplianceAgentTests
{
    private readonly ComplianceAgent _agent;

    public ComplianceAgentTests()
    {
        var logger = Mock.Of<ILogger<ComplianceAgent>>();

        // Create mock service dependencies
        var complianceEngine = Mock.Of<IAtoComplianceEngine>();
        var remediationEngine = Mock.Of<IRemediationEngine>();
        var nistControls = Mock.Of<INistControlsService>();
        var evidence = Mock.Of<IEvidenceStorageService>();
        var monitoring = Mock.Of<IComplianceMonitoringService>();
        var docGen = Mock.Of<IDocumentGenerationService>();
        var audit = Mock.Of<IAssessmentAuditService>();
        var history = Mock.Of<IComplianceHistoryService>();
        var status = Mock.Of<IComplianceStatusService>();

        // Create mock IServiceScopeFactory for tools that need it
        var scopeFactory = Mock.Of<IServiceScopeFactory>();

        // Create tool instances with mocked services
        var assessmentTool = new ComplianceAssessmentTool(complianceEngine, scopeFactory, Mock.Of<ILogger<ComplianceAssessmentTool>>());
        var controlFamilyTool = new ControlFamilyTool(nistControls, Mock.Of<ILogger<ControlFamilyTool>>());
        var documentGenerationTool = new DocumentGenerationTool(docGen, scopeFactory, Mock.Of<ILogger<DocumentGenerationTool>>());
        var evidenceCollectionTool = new EvidenceCollectionTool(evidence, Mock.Of<ILogger<EvidenceCollectionTool>>());
        var remediationExecuteTool = new RemediationExecuteTool(remediationEngine, Mock.Of<ILogger<RemediationExecuteTool>>());
        var validateRemediationTool = new ValidateRemediationTool(remediationEngine, Mock.Of<ILogger<ValidateRemediationTool>>());
        var remediationPlanTool = new RemediationPlanTool(remediationEngine, Mock.Of<ILogger<RemediationPlanTool>>());
        var auditLogTool = new AssessmentAuditLogTool(audit, Mock.Of<ILogger<AssessmentAuditLogTool>>());
        var historyTool = new ComplianceHistoryTool(history, Mock.Of<ILogger<ComplianceHistoryTool>>());
        var statusTool = new ComplianceStatusTool(status, Mock.Of<ILogger<ComplianceStatusTool>>());
        var monitoringTool = new ComplianceMonitoringTool(monitoring, Mock.Of<ILogger<ComplianceMonitoringTool>>());

        // Create Kanban tool instances
        var kanbanCreateBoard = new KanbanCreateBoardTool(scopeFactory, Mock.Of<ILogger<KanbanCreateBoardTool>>());
        var kanbanBoardShow = new KanbanBoardShowTool(scopeFactory, Mock.Of<ILogger<KanbanBoardShowTool>>());
        var kanbanGetTask = new KanbanGetTaskTool(scopeFactory, Mock.Of<ILogger<KanbanGetTaskTool>>());
        var kanbanCreateTask = new KanbanCreateTaskTool(scopeFactory, Mock.Of<ILogger<KanbanCreateTaskTool>>());
        var kanbanAssignTask = new KanbanAssignTaskTool(scopeFactory, Mock.Of<ILogger<KanbanAssignTaskTool>>());
        var kanbanMoveTask = new KanbanMoveTaskTool(scopeFactory, Mock.Of<ILogger<KanbanMoveTaskTool>>());
        var kanbanTaskList = new KanbanTaskListTool(scopeFactory, Mock.Of<ILogger<KanbanTaskListTool>>());
        var kanbanTaskHistory = new KanbanTaskHistoryTool(scopeFactory, Mock.Of<ILogger<KanbanTaskHistoryTool>>());
        var kanbanValidateTask = new KanbanValidateTaskTool(scopeFactory, Mock.Of<ILogger<KanbanValidateTaskTool>>());
        var kanbanAddComment = new KanbanAddCommentTool(scopeFactory, Mock.Of<ILogger<KanbanAddCommentTool>>());
        var kanbanTaskComments = new KanbanTaskCommentsTool(scopeFactory, Mock.Of<ILogger<KanbanTaskCommentsTool>>());
        var kanbanEditComment = new KanbanEditCommentTool(scopeFactory, Mock.Of<ILogger<KanbanEditCommentTool>>());
        var kanbanDeleteComment = new KanbanDeleteCommentTool(scopeFactory, Mock.Of<ILogger<KanbanDeleteCommentTool>>());
        var kanbanRemediateTask = new KanbanRemediateTaskTool(scopeFactory, Mock.Of<ILogger<KanbanRemediateTaskTool>>());
        var kanbanCollectEvidence = new KanbanCollectEvidenceTool(scopeFactory, Mock.Of<ILogger<KanbanCollectEvidenceTool>>());
        var kanbanBulkUpdate = new KanbanBulkUpdateTool(scopeFactory, Mock.Of<ILogger<KanbanBulkUpdateTool>>());
        var kanbanExport = new KanbanExportTool(scopeFactory, Mock.Of<ILogger<KanbanExportTool>>());
        var kanbanArchiveBoard = new KanbanArchiveBoardTool(scopeFactory, Mock.Of<ILogger<KanbanArchiveBoardTool>>());

        // Create Auth/PIM tool instances
        var cacStatus = new CacStatusTool(scopeFactory, Mock.Of<ILogger<CacStatusTool>>());
        var cacSignOut = new CacSignOutTool(scopeFactory, Mock.Of<ILogger<CacSignOutTool>>());
        var cacSetTimeout = new CacSetTimeoutTool(scopeFactory, Mock.Of<ILogger<CacSetTimeoutTool>>());
        var cacMapCertificate = new CacMapCertificateTool(scopeFactory, Mock.Of<ILogger<CacMapCertificateTool>>());
        var pimListEligible = new PimListEligibleTool(scopeFactory, Mock.Of<ILogger<PimListEligibleTool>>());
        var pimActivateRole = new PimActivateRoleTool(scopeFactory, Mock.Of<ILogger<PimActivateRoleTool>>());
        var pimDeactivateRole = new PimDeactivateRoleTool(scopeFactory, Mock.Of<ILogger<PimDeactivateRoleTool>>());
        var pimListActive = new PimListActiveTool(scopeFactory, Mock.Of<ILogger<PimListActiveTool>>());
        var pimExtendRole = new PimExtendRoleTool(scopeFactory, Mock.Of<ILogger<PimExtendRoleTool>>());
        var pimApproveRequest = new PimApproveRequestTool(scopeFactory, Mock.Of<ILogger<PimApproveRequestTool>>());
        var pimDenyRequest = new PimDenyRequestTool(scopeFactory, Mock.Of<ILogger<PimDenyRequestTool>>());
        var jitRequestAccess = new JitRequestAccessTool(scopeFactory, Mock.Of<ILogger<JitRequestAccessTool>>());
        var jitListSessions = new JitListSessionsTool(scopeFactory, Mock.Of<ILogger<JitListSessionsTool>>());
        var jitRevokeAccess = new JitRevokeAccessTool(scopeFactory, Mock.Of<ILogger<JitRevokeAccessTool>>());
        var pimHistory = new PimHistoryTool(scopeFactory, Mock.Of<ILogger<PimHistoryTool>>());
        var watchService = Mock.Of<IComplianceWatchService>();
        var watchEnable = new WatchEnableMonitoringTool(watchService, Mock.Of<ILogger<WatchEnableMonitoringTool>>());
        var watchDisable = new WatchDisableMonitoringTool(watchService, Mock.Of<ILogger<WatchDisableMonitoringTool>>());
        var watchConfigure = new WatchConfigureMonitoringTool(watchService, Mock.Of<ILogger<WatchConfigureMonitoringTool>>());
        var watchStatus = new WatchMonitoringStatusTool(watchService, Mock.Of<ILogger<WatchMonitoringStatusTool>>());
        var alertManager = Mock.Of<IAlertManager>();
        var watchShowAlerts = new WatchShowAlertsTool(alertManager, Mock.Of<ILogger<WatchShowAlertsTool>>());
        var watchGetAlert = new WatchGetAlertTool(alertManager, Mock.Of<ILogger<WatchGetAlertTool>>());
        var watchAckAlert = new WatchAcknowledgeAlertTool(alertManager, Mock.Of<ILogger<WatchAcknowledgeAlertTool>>());
        var watchFixAlert = new WatchFixAlertTool(alertManager, Mock.Of<IAtoComplianceEngine>(), Mock.Of<ILogger<WatchFixAlertTool>>());
        var watchDismissAlert = new WatchDismissAlertTool(alertManager, Mock.Of<ILogger<WatchDismissAlertTool>>());
        var watchCreateRule = new WatchCreateRuleTool(watchService, Mock.Of<ILogger<WatchCreateRuleTool>>());
        var watchListRules = new WatchListRulesTool(watchService, Mock.Of<ILogger<WatchListRulesTool>>());
        var watchSuppressAlerts = new WatchSuppressAlertsTool(watchService, Mock.Of<ILogger<WatchSuppressAlertsTool>>());
        var watchListSuppressions = new WatchListSuppressionsTool(watchService, Mock.Of<ILogger<WatchListSuppressionsTool>>());
        var watchConfigureQuietHours = new WatchConfigureQuietHoursTool(watchService, Mock.Of<ILogger<WatchConfigureQuietHoursTool>>());
        var watchConfigureNotifications = new WatchConfigureNotificationsTool(Mock.Of<IEscalationService>(), Mock.Of<ILogger<WatchConfigureNotificationsTool>>());
        var watchConfigureEscalation = new WatchConfigureEscalationTool(Mock.Of<IEscalationService>(), Mock.Of<ILogger<WatchConfigureEscalationTool>>());
        var watchAlertHistory = new WatchAlertHistoryTool(alertManager, Mock.Of<ILogger<WatchAlertHistoryTool>>());
        var watchComplianceTrend = new WatchComplianceTrendTool(
            new InMemoryDbContextFactory(
                new DbContextOptionsBuilder<AtoCopilotContext>()
                    .UseInMemoryDatabase($"AgentTests_Trend_{Guid.NewGuid()}")
                    .Options),
            Mock.Of<ILogger<WatchComplianceTrendTool>>());
        var watchAlertStatistics = new WatchAlertStatisticsTool(
            new InMemoryDbContextFactory(
                new DbContextOptionsBuilder<AtoCopilotContext>()
                    .UseInMemoryDatabase($"AgentTests_Stats_{Guid.NewGuid()}")
                    .Options),
            Mock.Of<ILogger<WatchAlertStatisticsTool>>());
        var watchCreateTaskFromAlert = new WatchCreateTaskFromAlertTool(
            alertManager, scopeFactory, Mock.Of<ILogger<WatchCreateTaskFromAlertTool>>());
        var watchCollectEvidenceFromAlert = new WatchCollectEvidenceFromAlertTool(
            alertManager,
            new InMemoryDbContextFactory(
                new DbContextOptionsBuilder<AtoCopilotContext>()
                    .UseInMemoryDatabase($"AgentTests_Evidence_{Guid.NewGuid()}")
                    .Options),
            Mock.Of<ILogger<WatchCollectEvidenceFromAlertTool>>());
        var watchCreateAutoRemediationRule = new WatchCreateAutoRemediationRuleTool(
            watchService, Mock.Of<ILogger<WatchCreateAutoRemediationRuleTool>>());
        var watchListAutoRemediationRules = new WatchListAutoRemediationRulesTool(
            watchService, Mock.Of<ILogger<WatchListAutoRemediationRulesTool>>());

        _agent = new ComplianceAgent(
            assessmentTool,
            controlFamilyTool,
            documentGenerationTool,
            evidenceCollectionTool,
            remediationExecuteTool,
            validateRemediationTool,
            remediationPlanTool,
            auditLogTool,
            historyTool,
            statusTool,
            monitoringTool,
            kanbanCreateBoard,
            kanbanBoardShow,
            kanbanGetTask,
            kanbanCreateTask,
            kanbanAssignTask,
            kanbanMoveTask,
            kanbanTaskList,
            kanbanTaskHistory,
            kanbanValidateTask,
            kanbanAddComment,
            kanbanTaskComments,
            kanbanEditComment,
            kanbanDeleteComment,
            kanbanRemediateTask,
            kanbanCollectEvidence,
            kanbanBulkUpdate,
            kanbanExport,
            kanbanArchiveBoard,
            cacStatus,
            cacSignOut,
            cacSetTimeout,
            cacMapCertificate,
            pimListEligible,
            pimActivateRole,
            pimDeactivateRole,
            pimListActive,
            pimExtendRole,
            pimApproveRequest,
            pimDenyRequest,
            jitRequestAccess,
            jitListSessions,
            jitRevokeAccess,
            pimHistory,
            watchEnable,
            watchDisable,
            watchConfigure,
            watchStatus,
            watchShowAlerts,
            watchGetAlert,
            watchAckAlert,
            watchFixAlert,
            watchDismissAlert,
            watchCreateRule,
            watchListRules,
            watchSuppressAlerts,
            watchListSuppressions,
            watchConfigureQuietHours,
            watchConfigureNotifications,
            watchConfigureEscalation,
            watchAlertHistory,
            watchComplianceTrend,
            watchAlertStatistics,
            watchCreateTaskFromAlert,
            watchCollectEvidenceFromAlert,
            watchCreateAutoRemediationRule,
            watchListAutoRemediationRules,
            new InMemoryDbContextFactory(
                new DbContextOptionsBuilder<AtoCopilotContext>()
                    .UseInMemoryDatabase($"AgentTests_{Guid.NewGuid()}")
                    .Options),
            scopeFactory,
            logger);
    }

    [Fact]
    public void Agent_Should_Have_Correct_Identity()
    {
        _agent.AgentId.Should().Be("compliance-agent");
        _agent.AgentName.Should().Be("Compliance Agent");
    }

    [Fact]
    public void Agent_Should_Have_Description()
    {
        _agent.Description.Should().NotBeNullOrEmpty();
        _agent.Description.Should().Contain("compliance", because: "agent is focused on compliance");
    }

    [Fact]
    public void Agent_Should_Have_System_Prompt()
    {
        var prompt = _agent.GetSystemPrompt();
        prompt.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task ProcessAsync_WithEmptyMessage_ShouldReturnResponse()
    {
        var context = new AgentConversationContext
        {
            ConversationId = "test-1",
            UserId = "test-user"
        };

        var result = await _agent.ProcessAsync("", context);

        result.Should().NotBeNull();
        result.AgentName.Should().Be("Compliance Agent");
    }

    [Fact]
    public async Task ProcessAsync_WithComplianceQuery_ShouldReturnResponse()
    {
        var context = new AgentConversationContext
        {
            ConversationId = "test-2",
            UserId = "test-user"
        };

        var result = await _agent.ProcessAsync("What is the compliance status?", context);

        result.Should().NotBeNull();
        result.AgentName.Should().Be("Compliance Agent");
    }

    private class InMemoryDbContextFactory : IDbContextFactory<AtoCopilotContext>
    {
        private readonly DbContextOptions<AtoCopilotContext> _options;

        public InMemoryDbContextFactory(DbContextOptions<AtoCopilotContext> options)
        {
            _options = options;
        }

        public AtoCopilotContext CreateDbContext() => new(_options);

        public Task<AtoCopilotContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(CreateDbContext());
    }
}
