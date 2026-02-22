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
            new InMemoryDbContextFactory(
                new DbContextOptionsBuilder<AtoCopilotContext>()
                    .UseInMemoryDatabase($"AgentTests_{Guid.NewGuid()}")
                    .Options),
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
