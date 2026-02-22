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
/// Unit tests for KanbanService audit/export operations:
/// ExportBoardCsvAsync, ExportBoardHistoryAsync — CSV format, headers, chronological sort.
/// </summary>
public class KanbanServiceAuditTests : IDisposable
{
    private readonly AtoCopilotContext _context;
    private readonly KanbanService _service;

    public KanbanServiceAuditTests()
    {
        var options = new DbContextOptionsBuilder<AtoCopilotContext>()
            .UseInMemoryDatabase($"KanbanAudit_{Guid.NewGuid()}")
            .Options;
        _context = new AtoCopilotContext(options);

        _service = new KanbanService(
            _context,
            Mock.Of<ILogger<KanbanService>>(),
            Mock.Of<INotificationService>(),
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
    public async Task ExportCsv_HasCorrectHeader()
    {
        var board = await SeedBoardWithTask();

        var csv = await _service.ExportBoardCsvAsync(board.Id, "user1", ComplianceRoles.Auditor);

        var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        lines[0].Should().Contain("TaskNumber");
        lines[0].Should().Contain("ControlId");
        lines[0].Should().Contain("Severity");
        lines[0].Should().Contain("Status");
        lines[0].Should().Contain("DueDate");
    }

    [Fact]
    public async Task ExportCsv_ContainsTaskData()
    {
        var board = await SeedBoardWithTask();

        var csv = await _service.ExportBoardCsvAsync(board.Id, "user1", ComplianceRoles.Auditor);

        var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        lines.Should().HaveCountGreaterThan(1);
        lines[1].Should().Contain("REM-001");
        lines[1].Should().Contain("AC-1");
    }

    [Fact]
    public async Task ExportCsv_BoardNotFound_Throws()
    {
        var act = () => _service.ExportBoardCsvAsync("nonexistent", "user1", ComplianceRoles.Auditor);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not found*");
    }

    [Fact]
    public async Task ExportHistory_HasCorrectHeader()
    {
        var board = await SeedBoardWithTaskAndHistory();

        var csv = await _service.ExportBoardHistoryAsync(board.Id, "user1", ComplianceRoles.Auditor);

        var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        lines[0].Should().Contain("EventType");
        lines[0].Should().Contain("TaskNumber");
        lines[0].Should().Contain("Timestamp");
    }

    [Fact]
    public async Task ExportHistory_ContainsHistoryData()
    {
        var board = await SeedBoardWithTaskAndHistory();

        var csv = await _service.ExportBoardHistoryAsync(board.Id, "user1", ComplianceRoles.Auditor);

        var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        lines.Should().HaveCountGreaterThan(1);
        lines[1].Should().Contain("Created");
    }

    [Fact]
    public async Task ExportHistory_ChronologicalOrder()
    {
        var board = await SeedBoardWithMultipleHistory();

        var csv = await _service.ExportBoardHistoryAsync(board.Id, "user1", ComplianceRoles.Auditor);

        var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        // First entry should be the older one
        lines[1].Should().Contain("Created");
    }

    [Fact]
    public async Task ExportHistory_BoardNotFound_Throws()
    {
        var act = () => _service.ExportBoardHistoryAsync("nonexistent", "user1", ComplianceRoles.Auditor);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not found*");
    }

    private async Task<RemediationBoard> SeedBoardWithTask()
    {
        var board = new RemediationBoard { Name = "Board", SubscriptionId = "sub", Owner = "owner" };
        board.Tasks.Add(new RemediationTask
        {
            BoardId = board.Id, TaskNumber = "REM-001", Title = "Test",
            ControlId = "AC-1", ControlFamily = "AC",
            Severity = FindingSeverity.High, CreatedBy = "owner",
            AssigneeName = "Alice",
        });
        _context.RemediationBoards.Add(board);
        await _context.SaveChangesAsync();
        return board;
    }

    private async Task<RemediationBoard> SeedBoardWithTaskAndHistory()
    {
        var board = new RemediationBoard { Name = "Board", SubscriptionId = "sub", Owner = "owner" };
        var task = new RemediationTask
        {
            BoardId = board.Id, TaskNumber = "REM-001", Title = "Test",
            ControlId = "AC-1", ControlFamily = "AC",
            Severity = FindingSeverity.High, CreatedBy = "owner",
        };
        task.History.Add(new TaskHistoryEntry
        {
            TaskId = task.Id, EventType = HistoryEventType.Created,
            NewValue = "Backlog", ActingUserId = "owner", ActingUserName = "Owner",
        });
        board.Tasks.Add(task);
        _context.RemediationBoards.Add(board);
        await _context.SaveChangesAsync();
        return board;
    }

    private async Task<RemediationBoard> SeedBoardWithMultipleHistory()
    {
        var board = new RemediationBoard { Name = "Board", SubscriptionId = "sub", Owner = "owner" };
        var task = new RemediationTask
        {
            BoardId = board.Id, TaskNumber = "REM-001", Title = "Test",
            ControlId = "AC-1", ControlFamily = "AC",
            Severity = FindingSeverity.High, CreatedBy = "owner",
        };
        task.History.Add(new TaskHistoryEntry
        {
            TaskId = task.Id, EventType = HistoryEventType.Created,
            NewValue = "Backlog", ActingUserId = "owner", ActingUserName = "Owner",
            Timestamp = DateTime.UtcNow.AddHours(-2),
        });
        task.History.Add(new TaskHistoryEntry
        {
            TaskId = task.Id, EventType = HistoryEventType.StatusChanged,
            OldValue = "Backlog", NewValue = "ToDo", ActingUserId = "owner", ActingUserName = "Owner",
            Timestamp = DateTime.UtcNow.AddHours(-1),
        });
        board.Tasks.Add(task);
        _context.RemediationBoards.Add(board);
        await _context.SaveChangesAsync();
        return board;
    }
}
