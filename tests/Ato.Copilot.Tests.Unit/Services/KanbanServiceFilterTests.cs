using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using FluentAssertions;
using Ato.Copilot.Agents.Compliance.Services;
using Ato.Copilot.Core.Data.Context;
using Ato.Copilot.Core.Interfaces.Compliance;
using Ato.Copilot.Core.Interfaces.Kanban;
using Ato.Copilot.Core.Models.Compliance;
using Ato.Copilot.Core.Models.Kanban;
using Ato.Copilot.State.Abstractions;
using TaskStatus = Ato.Copilot.Core.Models.Kanban.TaskStatus;

namespace Ato.Copilot.Tests.Unit.Services;

/// <summary>
/// Unit tests for KanbanService.ListTasksAsync:
/// filtering by status, severity, assignee, controlFamily, isOverdue,
/// sorting, and pagination.
/// </summary>
public class KanbanServiceFilterTests : IDisposable
{
    private readonly AtoCopilotContext _context;
    private readonly KanbanService _service;
    private readonly string _boardId;

    public KanbanServiceFilterTests()
    {
        var options = new DbContextOptionsBuilder<AtoCopilotContext>()
            .UseInMemoryDatabase($"KanbanFilter_{Guid.NewGuid()}")
            .Options;
        _context = new AtoCopilotContext(options);

        _service = new KanbanService(
            _context,
            Mock.Of<ILogger<KanbanService>>(),
            Mock.Of<INotificationService>(),
            Mock.Of<IAgentStateManager>(),
            Mock.Of<IAtoComplianceEngine>(),
            Mock.Of<IRemediationEngine>());

        var board = new RemediationBoard { Name = "Board", SubscriptionId = "sub", Owner = "owner" };
        _boardId = board.Id;
        _context.RemediationBoards.Add(board);

        // Seed 6 diverse tasks
        _context.RemediationTasks.AddRange(
            new RemediationTask { BoardId = _boardId, TaskNumber = "REM-001", Title = "T1", ControlId = "AC-1", ControlFamily = "AC", Status = TaskStatus.Backlog, Severity = FindingSeverity.Critical, AssigneeId = "alice", DueDate = DateTime.UtcNow.AddDays(-1), CreatedBy = "owner" },
            new RemediationTask { BoardId = _boardId, TaskNumber = "REM-002", Title = "T2", ControlId = "AC-2", ControlFamily = "AC", Status = TaskStatus.InProgress, Severity = FindingSeverity.High, AssigneeId = "bob", DueDate = DateTime.UtcNow.AddDays(5), CreatedBy = "owner" },
            new RemediationTask { BoardId = _boardId, TaskNumber = "REM-003", Title = "T3", ControlId = "SC-1", ControlFamily = "SC", Status = TaskStatus.InReview, Severity = FindingSeverity.Medium, AssigneeId = "alice", DueDate = DateTime.UtcNow.AddDays(10), CreatedBy = "owner" },
            new RemediationTask { BoardId = _boardId, TaskNumber = "REM-004", Title = "T4", ControlId = "SC-2", ControlFamily = "SC", Status = TaskStatus.Done, Severity = FindingSeverity.Low, AssigneeId = "charlie", DueDate = DateTime.UtcNow.AddDays(20), CreatedBy = "owner" },
            new RemediationTask { BoardId = _boardId, TaskNumber = "REM-005", Title = "T5", ControlId = "AU-1", ControlFamily = "AU", Status = TaskStatus.Blocked, Severity = FindingSeverity.High, AssigneeId = "alice", DueDate = DateTime.UtcNow.AddDays(-3), CreatedBy = "owner" },
            new RemediationTask { BoardId = _boardId, TaskNumber = "REM-006", Title = "T6", ControlId = "AU-2", ControlFamily = "AU", Status = TaskStatus.ToDo, Severity = FindingSeverity.Medium, DueDate = DateTime.UtcNow.AddDays(15), CreatedBy = "owner" }
        );
        _context.SaveChanges();
    }

    public void Dispose()
    {
        _context.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task ListTasks_NoFilter_ReturnsAll()
    {
        var result = await _service.ListTasksAsync(_boardId);

        result.TotalCount.Should().Be(6);
        result.Items.Should().HaveCount(6);
    }

    [Fact]
    public async Task ListTasks_FilterByStatus_ReturnsMatching()
    {
        var result = await _service.ListTasksAsync(_boardId, status: TaskStatus.InProgress);

        result.TotalCount.Should().Be(1);
        result.Items[0].TaskNumber.Should().Be("REM-002");
    }

    [Fact]
    public async Task ListTasks_FilterBySeverity_ReturnsMatching()
    {
        var result = await _service.ListTasksAsync(_boardId, severity: FindingSeverity.High);

        result.TotalCount.Should().Be(2);
    }

    [Fact]
    public async Task ListTasks_FilterByAssignee_ReturnsMatching()
    {
        var result = await _service.ListTasksAsync(_boardId, assigneeId: "alice");

        result.TotalCount.Should().Be(3);
    }

    [Fact]
    public async Task ListTasks_FilterByControlFamily_ReturnsMatching()
    {
        var result = await _service.ListTasksAsync(_boardId, controlFamily: "SC");

        result.TotalCount.Should().Be(2);
    }

    [Fact]
    public async Task ListTasks_FilterByOverdue_ExcludesDone()
    {
        // REM-001 overdue (Backlog, past due), REM-005 overdue (Blocked, past due but excluded because Blocked)
        // Done tasks are excluded. Blocked tasks are not filtered by status!=Done, but isOverdue checks status!=Done
        var result = await _service.ListTasksAsync(_boardId, isOverdue: true);

        // The filter uses: t.DueDate < UtcNow && t.Status != Done
        // REM-001: past due, Backlog → included
        // REM-005: past due, Blocked → included (status not Done)
        result.TotalCount.Should().BeGreaterThanOrEqualTo(1);
        result.Items.Should().OnlyContain(t => t.DueDate < DateTime.UtcNow && t.Status != TaskStatus.Done);
    }

    [Fact]
    public async Task ListTasks_SortByDueDateAsc()
    {
        var result = await _service.ListTasksAsync(_boardId, sortBy: "duedate", sortOrder: "asc");

        result.Items.Should().BeInAscendingOrder(t => t.DueDate);
    }

    [Fact]
    public async Task ListTasks_SortByStatusDesc()
    {
        var result = await _service.ListTasksAsync(_boardId, sortBy: "status", sortOrder: "desc");

        result.Items.First().Status.Should().BeOneOf(TaskStatus.Done, TaskStatus.Blocked, TaskStatus.InReview);
    }

    [Fact]
    public async Task ListTasks_Pagination_Page1()
    {
        var result = await _service.ListTasksAsync(_boardId, pageSize: 2, page: 1);

        result.Items.Should().HaveCount(2);
        result.TotalCount.Should().Be(6);
        result.Page.Should().Be(1);
        result.TotalPages.Should().Be(3);
        result.HasMore.Should().BeTrue();
    }

    [Fact]
    public async Task ListTasks_Pagination_LastPage()
    {
        var result = await _service.ListTasksAsync(_boardId, pageSize: 2, page: 3);

        result.Items.Should().HaveCount(2);
        result.HasMore.Should().BeFalse();
    }

    [Fact]
    public async Task ListTasks_CombinedFilters()
    {
        var result = await _service.ListTasksAsync(_boardId, controlFamily: "AC", assigneeId: "alice");

        result.TotalCount.Should().Be(1);
        result.Items[0].TaskNumber.Should().Be("REM-001");
    }

    [Fact]
    public async Task ListTasks_EmptyBoard_ReturnsZero()
    {
        var emptyBoard = new RemediationBoard { Name = "Empty", SubscriptionId = "sub", Owner = "owner" };
        _context.RemediationBoards.Add(emptyBoard);
        await _context.SaveChangesAsync();

        var result = await _service.ListTasksAsync(emptyBoard.Id);

        result.TotalCount.Should().Be(0);
        result.Items.Should().BeEmpty();
    }
}
