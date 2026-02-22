using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using FluentAssertions;
using Ato.Copilot.Agents.Compliance.Services;
using Ato.Copilot.Core.Constants;
using Ato.Copilot.Core.Data.Context;
using Ato.Copilot.Core.Interfaces.Compliance;
using Ato.Copilot.Core.Interfaces.Kanban;
using Ato.Copilot.Core.Models.Compliance;
using Ato.Copilot.Core.Models.Kanban;
using Ato.Copilot.State.Abstractions;
using TaskStatus = Ato.Copilot.Core.Models.Kanban.TaskStatus;

namespace Ato.Copilot.Tests.Unit.Services;

/// <summary>
/// Unit tests for KanbanService.AssignTaskAsync: RBAC enforcement,
/// self-assign rules, history logging, notifications.
/// </summary>
public class KanbanServiceAssignmentTests : IDisposable
{
    private readonly AtoCopilotContext _context;
    private readonly KanbanService _service;
    private readonly Mock<INotificationService> _notificationMock = new();

    public KanbanServiceAssignmentTests()
    {
        var options = new DbContextOptionsBuilder<AtoCopilotContext>()
            .UseInMemoryDatabase($"KanbanAssignment_{Guid.NewGuid()}")
            .Options;
        _context = new AtoCopilotContext(options);

        _service = new KanbanService(
            _context,
            Mock.Of<ILogger<KanbanService>>(),
            _notificationMock.Object,
            Mock.Of<IAgentStateManager>(),
            Mock.Of<IAtoComplianceEngine>(),
            Mock.Of<IRemediationEngine>());
    }

    public void Dispose()
    {
        _context.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task AssignTask_COAssignsAnyone_Succeeds()
    {
        var task = await SeedTask();

        var result = await _service.AssignTaskAsync(
            task.Id, "co1", "CO User", ComplianceRoles.Administrator,
            "target-user", "Target User");

        result.AssigneeId.Should().Be("target-user");
        result.AssigneeName.Should().Be("Target User");
    }

    [Fact]
    public async Task AssignTask_SLAssignsAnyone_Succeeds()
    {
        var task = await SeedTask();

        var result = await _service.AssignTaskAsync(
            task.Id, "sl1", "SL User", ComplianceRoles.SecurityLead,
            "target-user", "Target User");

        result.AssigneeId.Should().Be("target-user");
    }

    [Fact]
    public async Task AssignTask_PESelfAssignUnassigned_Succeeds()
    {
        var task = await SeedTask(assigneeId: null);

        var result = await _service.AssignTaskAsync(
            task.Id, "pe1", "PE User", ComplianceRoles.Analyst,
            "pe1", "PE User");

        result.AssigneeId.Should().Be("pe1");
    }

    [Fact]
    public async Task AssignTask_PECannotReassignOthers()
    {
        var task = await SeedTask(assigneeId: "other-user");

        var act = () => _service.AssignTaskAsync(
            task.Id, "pe1", "PE User", ComplianceRoles.Analyst,
            "pe1", "PE User");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*PE can only self-assign unassigned*");
    }

    [Fact]
    public async Task AssignTask_PECannotAssignToOthers()
    {
        var task = await SeedTask(assigneeId: null);

        var act = () => _service.AssignTaskAsync(
            task.Id, "pe1", "PE User", ComplianceRoles.Analyst,
            "someone-else", "Someone Else");

        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*UNAUTHORIZED*");
    }

    [Fact]
    public async Task AssignTask_AuditorBlocked()
    {
        var task = await SeedTask(assigneeId: null);

        var act = () => _service.AssignTaskAsync(
            task.Id, "aud1", "Auditor", ComplianceRoles.Auditor,
            "aud1", "Auditor");

        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*UNAUTHORIZED*");
    }

    [Fact]
    public async Task AssignTask_DoneTask_Blocked()
    {
        var task = await SeedTask(status: TaskStatus.Done);

        var act = () => _service.AssignTaskAsync(
            task.Id, "admin1", "Admin", ComplianceRoles.Administrator,
            "user1", "User One");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Done*");
    }

    [Fact]
    public async Task AssignTask_LogsHistoryEntry()
    {
        var task = await SeedTask(assigneeId: null);

        await _service.AssignTaskAsync(
            task.Id, "co1", "CO User", ComplianceRoles.Administrator,
            "new-user", "New User");

        var history = await _context.TaskHistoryEntries
            .Where(h => h.TaskId == task.Id && h.EventType == HistoryEventType.Assigned)
            .ToListAsync();

        history.Should().HaveCount(1);
        history[0].OldValue.Should().Be("(unassigned)");
        history[0].NewValue.Should().Be("New User");
    }

    [Fact]
    public async Task AssignTask_Unassign_SetsNull()
    {
        var task = await SeedTask(assigneeId: "user1");

        var result = await _service.AssignTaskAsync(
            task.Id, "co1", "CO User", ComplianceRoles.Administrator,
            null, null);

        result.AssigneeId.Should().BeNull();
        result.AssigneeName.Should().BeNull();
    }

    [Fact]
    public async Task AssignTask_EnqueuesNotification()
    {
        var task = await SeedTask(assigneeId: null);

        await _service.AssignTaskAsync(
            task.Id, "co1", "CO User", ComplianceRoles.Administrator,
            "target-user", "Target User");

        _notificationMock.Verify(
            n => n.EnqueueAsync(It.Is<NotificationMessage>(m =>
                m.EventType == NotificationEventType.TaskAssigned &&
                m.TargetUserId == "target-user")),
            Times.Once);
    }

    [Fact]
    public async Task AssignTask_TaskNotFound_Throws()
    {
        var act = () => _service.AssignTaskAsync(
            "nonexistent", "co1", "CO", ComplianceRoles.Administrator, "u1", "U1");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not found*");
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private async Task<RemediationTask> SeedTask(
        string? assigneeId = "existing-user", TaskStatus status = TaskStatus.Backlog)
    {
        var board = new RemediationBoard
        {
            Name = "Test Board",
            SubscriptionId = "sub",
            Owner = "owner",
        };
        _context.RemediationBoards.Add(board);

        var task = new RemediationTask
        {
            BoardId = board.Id,
            TaskNumber = "REM-001",
            Title = "Test Task",
            ControlId = "AC-2",
            ControlFamily = "AC",
            Status = status,
            AssigneeId = assigneeId,
            AssigneeName = assigneeId != null ? $"{assigneeId}-name" : null,
            Severity = FindingSeverity.High,
            CreatedBy = "owner",
        };
        _context.RemediationTasks.Add(task);
        await _context.SaveChangesAsync();
        return task;
    }
}
