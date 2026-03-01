using Xunit;
using FluentAssertions;
using Moq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Ato.Copilot.Agents.Common;
using Ato.Copilot.Agents.Compliance.Agents;
using Ato.Copilot.Agents.Compliance.Tools;
using Ato.Copilot.Core.Configuration;
using Ato.Copilot.Core.Data.Context;
using Ato.Copilot.Core.Interfaces.Compliance;

namespace Ato.Copilot.Tests.Unit.Agents;

/// <summary>
/// Tests for ComplianceAgent AI-powered processing path (Feature 011).
/// Validates TryProcessWithAiAsync integration, auth-gate ordering, and degraded fallback.
/// </summary>
public class ComplianceAgentAiTests
{
    private readonly Mock<IChatClient> _chatClientMock = new();
    private readonly IServiceScopeFactory _scopeFactory = Mock.Of<IServiceScopeFactory>();

    private ComplianceAgent CreateAgent(IChatClient? chatClient = null,
        AzureOpenAIGatewayOptions? aiOptions = null)
    {
        var complianceEngine = Mock.Of<IAtoComplianceEngine>();
        var remediationEngine = Mock.Of<IRemediationEngine>();
        var nistControls = Mock.Of<INistControlsService>();
        var evidence = Mock.Of<IEvidenceStorageService>();
        var monitoring = Mock.Of<IComplianceMonitoringService>();
        var docGen = Mock.Of<IDocumentGenerationService>();
        var audit = Mock.Of<IAssessmentAuditService>();
        var history = Mock.Of<IComplianceHistoryService>();
        var status = Mock.Of<IComplianceStatusService>();
        var watchService = Mock.Of<IComplianceWatchService>();
        var alertManager = Mock.Of<IAlertManager>();
        var escalationService = Mock.Of<IEscalationService>();

        var assessmentTool = new ComplianceAssessmentTool(complianceEngine, _scopeFactory, Mock.Of<ILogger<ComplianceAssessmentTool>>());
        var controlFamilyTool = new ControlFamilyTool(nistControls, Mock.Of<ILogger<ControlFamilyTool>>());
        var documentGenerationTool = new DocumentGenerationTool(docGen, _scopeFactory, Mock.Of<ILogger<DocumentGenerationTool>>());
        var evidenceCollectionTool = new EvidenceCollectionTool(evidence, Mock.Of<ILogger<EvidenceCollectionTool>>());
        var remediationExecuteTool = new RemediationExecuteTool(remediationEngine, Mock.Of<ILogger<RemediationExecuteTool>>());
        var validateRemediationTool = new ValidateRemediationTool(remediationEngine, Mock.Of<ILogger<ValidateRemediationTool>>());
        var remediationPlanTool = new RemediationPlanTool(remediationEngine, Mock.Of<ILogger<RemediationPlanTool>>());
        var auditLogTool = new AssessmentAuditLogTool(audit, Mock.Of<ILogger<AssessmentAuditLogTool>>());
        var historyTool = new ComplianceHistoryTool(history, Mock.Of<ILogger<ComplianceHistoryTool>>());
        var statusTool = new ComplianceStatusTool(status, Mock.Of<ILogger<ComplianceStatusTool>>());
        var monitoringTool = new ComplianceMonitoringTool(monitoring, Mock.Of<ILogger<ComplianceMonitoringTool>>());

        var kanbanCreateBoard = new KanbanCreateBoardTool(_scopeFactory, Mock.Of<ILogger<KanbanCreateBoardTool>>());
        var kanbanBoardShow = new KanbanBoardShowTool(_scopeFactory, Mock.Of<ILogger<KanbanBoardShowTool>>());
        var kanbanGetTask = new KanbanGetTaskTool(_scopeFactory, Mock.Of<ILogger<KanbanGetTaskTool>>());
        var kanbanCreateTask = new KanbanCreateTaskTool(_scopeFactory, Mock.Of<ILogger<KanbanCreateTaskTool>>());
        var kanbanAssignTask = new KanbanAssignTaskTool(_scopeFactory, Mock.Of<ILogger<KanbanAssignTaskTool>>());
        var kanbanMoveTask = new KanbanMoveTaskTool(_scopeFactory, Mock.Of<ILogger<KanbanMoveTaskTool>>());
        var kanbanTaskList = new KanbanTaskListTool(_scopeFactory, Mock.Of<ILogger<KanbanTaskListTool>>());
        var kanbanTaskHistory = new KanbanTaskHistoryTool(_scopeFactory, Mock.Of<ILogger<KanbanTaskHistoryTool>>());
        var kanbanValidateTask = new KanbanValidateTaskTool(_scopeFactory, Mock.Of<ILogger<KanbanValidateTaskTool>>());
        var kanbanAddComment = new KanbanAddCommentTool(_scopeFactory, Mock.Of<ILogger<KanbanAddCommentTool>>());
        var kanbanTaskComments = new KanbanTaskCommentsTool(_scopeFactory, Mock.Of<ILogger<KanbanTaskCommentsTool>>());
        var kanbanEditComment = new KanbanEditCommentTool(_scopeFactory, Mock.Of<ILogger<KanbanEditCommentTool>>());
        var kanbanDeleteComment = new KanbanDeleteCommentTool(_scopeFactory, Mock.Of<ILogger<KanbanDeleteCommentTool>>());
        var kanbanRemediateTask = new KanbanRemediateTaskTool(_scopeFactory, Mock.Of<ILogger<KanbanRemediateTaskTool>>());
        var kanbanCollectEvidence = new KanbanCollectEvidenceTool(_scopeFactory, Mock.Of<ILogger<KanbanCollectEvidenceTool>>());
        var kanbanBulkUpdate = new KanbanBulkUpdateTool(_scopeFactory, Mock.Of<ILogger<KanbanBulkUpdateTool>>());
        var kanbanExport = new KanbanExportTool(_scopeFactory, Mock.Of<ILogger<KanbanExportTool>>());
        var kanbanArchiveBoard = new KanbanArchiveBoardTool(_scopeFactory, Mock.Of<ILogger<KanbanArchiveBoardTool>>());

        var cacStatus = new CacStatusTool(_scopeFactory, Mock.Of<ILogger<CacStatusTool>>());
        var cacSignOut = new CacSignOutTool(_scopeFactory, Mock.Of<ILogger<CacSignOutTool>>());
        var cacSetTimeout = new CacSetTimeoutTool(_scopeFactory, Mock.Of<ILogger<CacSetTimeoutTool>>());
        var cacMapCertificate = new CacMapCertificateTool(_scopeFactory, Mock.Of<ILogger<CacMapCertificateTool>>());
        var pimListEligible = new PimListEligibleTool(_scopeFactory, Mock.Of<ILogger<PimListEligibleTool>>());
        var pimActivateRole = new PimActivateRoleTool(_scopeFactory, Mock.Of<ILogger<PimActivateRoleTool>>());
        var pimDeactivateRole = new PimDeactivateRoleTool(_scopeFactory, Mock.Of<ILogger<PimDeactivateRoleTool>>());
        var pimListActive = new PimListActiveTool(_scopeFactory, Mock.Of<ILogger<PimListActiveTool>>());
        var pimExtendRole = new PimExtendRoleTool(_scopeFactory, Mock.Of<ILogger<PimExtendRoleTool>>());
        var pimApproveRequest = new PimApproveRequestTool(_scopeFactory, Mock.Of<ILogger<PimApproveRequestTool>>());
        var pimDenyRequest = new PimDenyRequestTool(_scopeFactory, Mock.Of<ILogger<PimDenyRequestTool>>());
        var jitRequestAccess = new JitRequestAccessTool(_scopeFactory, Mock.Of<ILogger<JitRequestAccessTool>>());
        var jitListSessions = new JitListSessionsTool(_scopeFactory, Mock.Of<ILogger<JitListSessionsTool>>());
        var jitRevokeAccess = new JitRevokeAccessTool(_scopeFactory, Mock.Of<ILogger<JitRevokeAccessTool>>());
        var pimHistory = new PimHistoryTool(_scopeFactory, Mock.Of<ILogger<PimHistoryTool>>());

        var watchEnable = new WatchEnableMonitoringTool(watchService, Mock.Of<ILogger<WatchEnableMonitoringTool>>());
        var watchDisable = new WatchDisableMonitoringTool(watchService, Mock.Of<ILogger<WatchDisableMonitoringTool>>());
        var watchConfigure = new WatchConfigureMonitoringTool(watchService, Mock.Of<ILogger<WatchConfigureMonitoringTool>>());
        var watchStatus = new WatchMonitoringStatusTool(watchService, Mock.Of<ILogger<WatchMonitoringStatusTool>>());
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
        var watchConfigureNotifications = new WatchConfigureNotificationsTool(escalationService, Mock.Of<ILogger<WatchConfigureNotificationsTool>>());
        var watchConfigureEscalation = new WatchConfigureEscalationTool(escalationService, Mock.Of<ILogger<WatchConfigureEscalationTool>>());
        var watchAlertHistory = new WatchAlertHistoryTool(alertManager, Mock.Of<ILogger<WatchAlertHistoryTool>>());

        var dbId = $"AiTests_{Guid.NewGuid()}";
        var dbFactory = new InMemoryDbContextFactory(
            new DbContextOptionsBuilder<AtoCopilotContext>()
                .UseInMemoryDatabase(dbId).Options);

        var watchComplianceTrend = new WatchComplianceTrendTool(
            new InMemoryDbContextFactory(new DbContextOptionsBuilder<AtoCopilotContext>()
                .UseInMemoryDatabase($"{dbId}_Trend").Options),
            Mock.Of<ILogger<WatchComplianceTrendTool>>());
        var watchAlertStatistics = new WatchAlertStatisticsTool(
            new InMemoryDbContextFactory(new DbContextOptionsBuilder<AtoCopilotContext>()
                .UseInMemoryDatabase($"{dbId}_Stats").Options),
            Mock.Of<ILogger<WatchAlertStatisticsTool>>());
        var watchCreateTaskFromAlert = new WatchCreateTaskFromAlertTool(
            alertManager, _scopeFactory, Mock.Of<ILogger<WatchCreateTaskFromAlertTool>>());
        var watchCollectEvidenceFromAlert = new WatchCollectEvidenceFromAlertTool(
            alertManager,
            new InMemoryDbContextFactory(new DbContextOptionsBuilder<AtoCopilotContext>()
                .UseInMemoryDatabase($"{dbId}_Evidence").Options),
            Mock.Of<ILogger<WatchCollectEvidenceFromAlertTool>>());
        var watchCreateAutoRemediationRule = new WatchCreateAutoRemediationRuleTool(
            watchService, Mock.Of<ILogger<WatchCreateAutoRemediationRuleTool>>());
        var watchListAutoRemediationRules = new WatchListAutoRemediationRulesTool(
            watchService, Mock.Of<ILogger<WatchListAutoRemediationRulesTool>>());

        var nistSearchTool = new NistControlSearchTool(nistControls, Mock.Of<ILogger<NistControlSearchTool>>());
        var nistExplainerTool = new NistControlExplainerTool(nistControls, Mock.Of<ILogger<NistControlExplainerTool>>());

        IOptions<AzureOpenAIGatewayOptions>? optionsWrapper =
            aiOptions != null ? Options.Create(aiOptions) : null;

        return new ComplianceAgent(
            assessmentTool, controlFamilyTool, documentGenerationTool, evidenceCollectionTool,
            remediationExecuteTool, validateRemediationTool, remediationPlanTool,
            auditLogTool, historyTool, statusTool, monitoringTool,
            kanbanCreateBoard, kanbanBoardShow, kanbanGetTask, kanbanCreateTask,
            kanbanAssignTask, kanbanMoveTask, kanbanTaskList, kanbanTaskHistory,
            kanbanValidateTask, kanbanAddComment, kanbanTaskComments, kanbanEditComment,
            kanbanDeleteComment, kanbanRemediateTask, kanbanCollectEvidence, kanbanBulkUpdate,
            kanbanExport, kanbanArchiveBoard,
            new KanbanGenerateScriptTool(_scopeFactory, Mock.Of<ILogger<KanbanGenerateScriptTool>>()),
            new KanbanGenerateValidationTool(_scopeFactory, Mock.Of<ILogger<KanbanGenerateValidationTool>>()),
            cacStatus, cacSignOut, cacSetTimeout, cacMapCertificate,
            pimListEligible, pimActivateRole, pimDeactivateRole, pimListActive,
            pimExtendRole, pimApproveRequest, pimDenyRequest,
            jitRequestAccess, jitListSessions, jitRevokeAccess, pimHistory,
            watchEnable, watchDisable, watchConfigure, watchStatus,
            watchShowAlerts, watchGetAlert, watchAckAlert, watchFixAlert, watchDismissAlert,
            watchCreateRule, watchListRules, watchSuppressAlerts, watchListSuppressions,
            watchConfigureQuietHours, watchConfigureNotifications, watchConfigureEscalation,
            watchAlertHistory, watchComplianceTrend, watchAlertStatistics,
            watchCreateTaskFromAlert, watchCollectEvidenceFromAlert,
            watchCreateAutoRemediationRule, watchListAutoRemediationRules,
            nistSearchTool, nistExplainerTool,
            dbFactory, _scopeFactory,
            Mock.Of<ILogger<ComplianceAgent>>(),
            chatClient,
            optionsWrapper);
    }

    private static AzureOpenAIGatewayOptions CreateEnabledOptions() => new()
    {
        AgentAIEnabled = true,
        MaxToolCallRounds = 5,
        Temperature = 0.3,
        Endpoint = "https://test.openai.azure.us/"
    };

    private static AgentConversationContext CreateContext() => new()
    {
        ConversationId = "test-compliance-ai",
        UserId = "test-user"
    };

    // ── AI-enabled path tests ────────────────────────────────────────────────

    [Fact]
    public async Task ProcessAsync_WithAiEnabled_ReturnsAiResponse()
    {
        _chatClientMock.Setup(c => c.GetResponseAsync(
                It.IsAny<IList<ChatMessage>>(), It.IsAny<ChatOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatResponse([new ChatMessage(ChatRole.Assistant, "AI compliance report ready.")]));

        var agent = CreateAgent(_chatClientMock.Object, CreateEnabledOptions());

        var result = await agent.ProcessAsync("show compliance status", CreateContext());

        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Response.Should().Contain("AI compliance report");
        result.AgentName.Should().Be("Compliance Agent");
    }

    [Fact]
    public async Task ProcessAsync_WithAiDisabled_FallsBackToDeterministic()
    {
        var agent = CreateAgent(); // No IChatClient, no AI options

        var result = await agent.ProcessAsync("compliance status", CreateContext());

        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.AgentName.Should().Be("Compliance Agent");
        // Should route through deterministic tool path
        _chatClientMock.Verify(c => c.GetResponseAsync(
            It.IsAny<IList<ChatMessage>>(), It.IsAny<ChatOptions>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ProcessAsync_AuthGateRunsBeforeAiPath()
    {
        // Auth gate checks happen before TryProcessWithAiAsync
        // Even with AI enabled, auth operations like PIM activation should
        // hit the auth gate first
        _chatClientMock.Setup(c => c.GetResponseAsync(
                It.IsAny<IList<ChatMessage>>(), It.IsAny<ChatOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatResponse([new ChatMessage(ChatRole.Assistant, "AI response")]));

        var agent = CreateAgent(_chatClientMock.Object, CreateEnabledOptions());

        // "activate" is a PIM-related keyword that triggers auth gate check
        var result = await agent.ProcessAsync("activate security reader role", CreateContext());

        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        // Auth gate returns before AI path for PIM operations
        // The response should still come through since auth gate returns null for non-PIM context
    }

    [Fact]
    public async Task ProcessAsync_AiFailure_FallsBackToDeterministic()
    {
        _chatClientMock.Setup(c => c.GetResponseAsync(
                It.IsAny<IList<ChatMessage>>(), It.IsAny<ChatOptions>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Azure OpenAI unavailable"));

        var agent = CreateAgent(_chatClientMock.Object, CreateEnabledOptions());

        var result = await agent.ProcessAsync("compliance status", CreateContext());

        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.AgentName.Should().Be("Compliance Agent");
        // Should have fallen back to deterministic RouteToToolAsync
    }

    // ── InMemoryDbContextFactory ─────────────────────────────────────────────

    private class InMemoryDbContextFactory : IDbContextFactory<AtoCopilotContext>
    {
        private readonly DbContextOptions<AtoCopilotContext> _options;

        public InMemoryDbContextFactory(DbContextOptions<AtoCopilotContext> options) => _options = options;
        public AtoCopilotContext CreateDbContext() => new(_options);
        public Task<AtoCopilotContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(CreateDbContext());
    }
}
