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
/// Unit tests for KanbanService bulk operations:
/// BulkAssignAsync, BulkMoveAsync, BulkSetDueDateAsync,
/// including partial success and per-task error isolation.
/// </summary>
public class KanbanServiceBulkTests : IDisposable
{
    private readonly AtoCopilotContext _context;
    private readonly KanbanService _service;
    private readonly Mock<INotificationService> _notificationMock = new();
    private readonly string _boardId;

    public KanbanServiceBulkTests()
    {
        var options = new DbContextOptionsBuilder<AtoCopilotContext>()
            .UseInMemoryDatabase($"KanbanBulk_{Guid.NewGuid()}")
            .Options;
        _context = new AtoCopilotContext(options);

        _notificationMock.Setup(n => n.EnqueueAsync(It.IsAny<NotificationMessage>()))
            .Returns(Task.CompletedTask);

        _service = new KanbanService(
            _context,
            Mock.Of<ILogger<KanbanService>>(),
            _notificationMock.Object,
            Mock.Of<IAgentStateManager>(),
            Mock.Of<IAtoComplianceEngine>(),
            Mock.Of<IRemediationEngine>());

        var board = new RemediationBoard { Name = "Board", SubscriptionId = "sub", Owner = "owner" };
        _boardId = board.Id;
        _context.RemediationBoards.Add(board);
        _context.SaveChanges();
    }

    public void Dispose()
    {
        _context.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task BulkAssign_AllSucceed()
    {
        var t1 = await SeedTask(TaskStatus.Backlog);
        var t2 = await SeedTask(TaskStatus.ToDo);

        var result = await _service.BulkAssignAsync(
            _boardId, new List<string> { t1.Id, t2.Id },
            "alice", "Alice", "co-user", "CO", ComplianceRoles.Administrator);

        result.Succeeded.Should().Be(2);
        result.Failed.Should().Be(0);
        result.Results.Should().HaveCount(2);
        result.Results.Should().OnlyContain(r => r.Success);
    }

    [Fact]
    public async Task BulkAssign_PartialFailure_DoneTaskRejected()
    {
        var t1 = await SeedTask(TaskStatus.Backlog);
        var t2 = await SeedTask(TaskStatus.Done);

        var result = await _service.BulkAssignAsync(
            _boardId, new List<string> { t1.Id, t2.Id },
            "alice", "Alice", "co-user", "CO", ComplianceRoles.Administrator);

        result.Succeeded.Should().Be(1);
        result.Failed.Should().Be(1);
        result.Results.Last().Error.Should().Contain("Done");
    }

    [Fact]
    public async Task BulkAssign_NonexistentTask_PartialSuccess()
    {
        var t1 = await SeedTask(TaskStatus.Backlog);

        var result = await _service.BulkAssignAsync(
            _boardId, new List<string> { t1.Id, "nonexistent" },
            "alice", "Alice", "co-user", "CO", ComplianceRoles.Administrator);

        result.Succeeded.Should().Be(1);
        result.Failed.Should().Be(1);
    }

    [Fact]
    public async Task BulkMove_AllSucceed()
    {
        var t1 = await SeedTask(TaskStatus.Backlog);
        var t2 = await SeedTask(TaskStatus.Backlog);

        var result = await _service.BulkMoveAsync(
            _boardId, new List<string> { t1.Id, t2.Id },
            TaskStatus.ToDo, "co-user", "CO", ComplianceRoles.Administrator);

        result.Succeeded.Should().Be(2);
        result.Failed.Should().Be(0);
    }

    [Fact]
    public async Task BulkMove_PartialFailure_InvalidTransitionRejected()
    {
        var t1 = await SeedTask(TaskStatus.Backlog);
        var t2 = await SeedTask(TaskStatus.Done);

        var result = await _service.BulkMoveAsync(
            _boardId, new List<string> { t1.Id, t2.Id },
            TaskStatus.ToDo, "co-user", "CO", ComplianceRoles.Administrator);

        result.Succeeded.Should().Be(1);
        result.Failed.Should().Be(1);
        result.Results.First(r => !r.Success).Error.Should().Contain("TERMINAL_STATE");
    }

    [Fact]
    public async Task BulkSetDueDate_AllSucceed()
    {
        var t1 = await SeedTask(TaskStatus.Backlog);
        var t2 = await SeedTask(TaskStatus.InProgress);
        var newDate = DateTime.UtcNow.AddDays(60);

        var result = await _service.BulkSetDueDateAsync(
            _boardId, new List<string> { t1.Id, t2.Id },
            newDate, "co-user", "CO");

        result.Succeeded.Should().Be(2);
        result.Failed.Should().Be(0);

        var updated1 = await _context.RemediationTasks.FindAsync(t1.Id);
        updated1!.DueDate.Should().BeCloseTo(newDate, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task BulkSetDueDate_AddsHistoryEntry()
    {
        var t1 = await SeedTask(TaskStatus.Backlog);
        var newDate = DateTime.UtcNow.AddDays(60);

        await _service.BulkSetDueDateAsync(
            _boardId, new List<string> { t1.Id },
            newDate, "co-user", "CO");

        var history = await _context.TaskHistoryEntries
            .Where(h => h.TaskId == t1.Id && h.EventType == HistoryEventType.DueDateChanged)
            .ToListAsync();

        history.Should().HaveCount(1);
        history[0].NewValue.Should().Be(newDate.ToString("yyyy-MM-dd"));
    }

    [Fact]
    public async Task BulkSetDueDate_NonexistentTask_PartialSuccess()
    {
        var t1 = await SeedTask(TaskStatus.Backlog);

        var result = await _service.BulkSetDueDateAsync(
            _boardId, new List<string> { t1.Id, "nonexistent" },
            DateTime.UtcNow.AddDays(30), "co-user", "CO");

        result.Succeeded.Should().Be(1);
        result.Failed.Should().Be(1);
    }

    private int _taskCounter;
    private async Task<RemediationTask> SeedTask(TaskStatus status)
    {
        _taskCounter++;
        var task = new RemediationTask
        {
            BoardId = _boardId,
            TaskNumber = $"REM-{_taskCounter:D3}",
            Title = $"Task {_taskCounter}",
            ControlId = "AC-1", ControlFamily = "AC",
            Status = status,
            Severity = FindingSeverity.Medium,
            CreatedBy = "owner",
            DueDate = DateTime.UtcNow.AddDays(30),
        };
        _context.RemediationTasks.Add(task);
        await _context.SaveChangesAsync();
        return task;
    }
}
