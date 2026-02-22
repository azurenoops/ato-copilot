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
/// Unit tests for KanbanService.GetOpenTasksForPoamAsync:
/// excludes Done tasks, returns all other statuses, ordering by severity then due date.
/// </summary>
public class KanbanServicePoamTests : IDisposable
{
    private readonly AtoCopilotContext _context;
    private readonly KanbanService _service;
    private readonly string _boardId;

    public KanbanServicePoamTests()
    {
        var options = new DbContextOptionsBuilder<AtoCopilotContext>()
            .UseInMemoryDatabase($"KanbanPoam_{Guid.NewGuid()}")
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
        _context.SaveChanges();
    }

    public void Dispose()
    {
        _context.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task GetOpenTasksForPoam_ExcludesDone()
    {
        _context.RemediationTasks.AddRange(
            MakeTask(TaskStatus.Backlog, FindingSeverity.High),
            MakeTask(TaskStatus.InProgress, FindingSeverity.Medium),
            MakeTask(TaskStatus.Done, FindingSeverity.Low));
        await _context.SaveChangesAsync();

        var result = await _service.GetOpenTasksForPoamAsync(_boardId);

        result.Should().HaveCount(2);
        result.Should().NotContain(t => t.Status == TaskStatus.Done);
    }

    [Fact]
    public async Task GetOpenTasksForPoam_IncludesBlocked()
    {
        _context.RemediationTasks.AddRange(
            MakeTask(TaskStatus.Blocked, FindingSeverity.High),
            MakeTask(TaskStatus.InReview, FindingSeverity.Medium));
        await _context.SaveChangesAsync();

        var result = await _service.GetOpenTasksForPoamAsync(_boardId);

        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetOpenTasksForPoam_OrdersBySeverityThenDueDate()
    {
        _context.RemediationTasks.AddRange(
            MakeTask(TaskStatus.Backlog, FindingSeverity.Low, DateTime.UtcNow.AddDays(10)),
            MakeTask(TaskStatus.Backlog, FindingSeverity.Critical, DateTime.UtcNow.AddDays(1)),
            MakeTask(TaskStatus.Backlog, FindingSeverity.Critical, DateTime.UtcNow.AddDays(3)));
        await _context.SaveChangesAsync();

        var result = await _service.GetOpenTasksForPoamAsync(_boardId);

        // OrderBy(Severity) then ThenBy(DueDate)
        result.Should().HaveCount(3);
        result[0].Severity.Should().Be(FindingSeverity.Critical);
    }

    [Fact]
    public async Task GetOpenTasksForPoam_EmptyBoard_ReturnsEmpty()
    {
        var result = await _service.GetOpenTasksForPoamAsync(_boardId);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetOpenTasksForPoam_AllDone_ReturnsEmpty()
    {
        _context.RemediationTasks.AddRange(
            MakeTask(TaskStatus.Done, FindingSeverity.High),
            MakeTask(TaskStatus.Done, FindingSeverity.Low));
        await _context.SaveChangesAsync();

        var result = await _service.GetOpenTasksForPoamAsync(_boardId);

        result.Should().BeEmpty();
    }

    private int _counter;
    private RemediationTask MakeTask(TaskStatus status, FindingSeverity severity, DateTime? dueDate = null)
    {
        _counter++;
        return new RemediationTask
        {
            BoardId = _boardId,
            TaskNumber = $"REM-{_counter:D3}",
            Title = $"POAM task {_counter}",
            ControlId = "AC-1", ControlFamily = "AC",
            Status = status,
            Severity = severity,
            DueDate = dueDate ?? DateTime.UtcNow.AddDays(30),
            CreatedBy = "owner",
        };
    }
}
