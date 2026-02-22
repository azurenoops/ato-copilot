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
/// Unit tests for KanbanService board management:
/// ArchiveBoardAsync, GetBoardAsync, ListBoardsAsync.
/// </summary>
public class KanbanServiceBoardMgmtTests : IDisposable
{
    private readonly AtoCopilotContext _context;
    private readonly KanbanService _service;

    public KanbanServiceBoardMgmtTests()
    {
        var options = new DbContextOptionsBuilder<AtoCopilotContext>()
            .UseInMemoryDatabase($"KanbanBoardMgmt_{Guid.NewGuid()}")
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
    public async Task ArchiveBoard_AllDone_Succeeds()
    {
        var board = new RemediationBoard { Name = "Board", SubscriptionId = "sub", Owner = "owner" };
        board.Tasks.Add(new RemediationTask
        {
            BoardId = board.Id, TaskNumber = "REM-001", Title = "T1",
            ControlId = "AC-1", ControlFamily = "AC", Status = TaskStatus.Done,
            Severity = FindingSeverity.High, CreatedBy = "owner",
        });
        _context.RemediationBoards.Add(board);
        await _context.SaveChangesAsync();

        var result = await _service.ArchiveBoardAsync(board.Id, "co-user", "CO");

        result.IsArchived.Should().BeTrue();
    }

    [Fact]
    public async Task ArchiveBoard_OpenTasks_Throws()
    {
        var board = new RemediationBoard { Name = "Board", SubscriptionId = "sub", Owner = "owner" };
        board.Tasks.Add(new RemediationTask
        {
            BoardId = board.Id, TaskNumber = "REM-001", Title = "T1",
            ControlId = "AC-1", ControlFamily = "AC", Status = TaskStatus.InProgress,
            Severity = FindingSeverity.High, CreatedBy = "owner",
        });
        _context.RemediationBoards.Add(board);
        await _context.SaveChangesAsync();

        var act = () => _service.ArchiveBoardAsync(board.Id, "co-user", "CO");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Cannot archive*");
    }

    [Fact]
    public async Task ArchiveBoard_EmptyBoard_Succeeds()
    {
        var board = new RemediationBoard { Name = "Board", SubscriptionId = "sub", Owner = "owner" };
        _context.RemediationBoards.Add(board);
        await _context.SaveChangesAsync();

        var result = await _service.ArchiveBoardAsync(board.Id, "co-user", "CO");

        result.IsArchived.Should().BeTrue();
    }

    [Fact]
    public async Task ArchiveBoard_NotFound_Throws()
    {
        var act = () => _service.ArchiveBoardAsync("nonexistent", "co-user", "CO");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not found*");
    }

    [Fact]
    public async Task GetBoard_ReturnsWithTasks()
    {
        var board = new RemediationBoard { Name = "Board", SubscriptionId = "sub", Owner = "owner" };
        board.Tasks.Add(new RemediationTask
        {
            BoardId = board.Id, TaskNumber = "REM-001", Title = "T1",
            ControlId = "AC-1", ControlFamily = "AC",
            Severity = FindingSeverity.High, CreatedBy = "owner",
        });
        _context.RemediationBoards.Add(board);
        await _context.SaveChangesAsync();

        var result = await _service.GetBoardAsync(board.Id);

        result.Should().NotBeNull();
        result!.Tasks.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetBoard_NotFound_ReturnsNull()
    {
        var result = await _service.GetBoardAsync("nonexistent");

        result.Should().BeNull();
    }

    [Fact]
    public async Task ListBoards_FilterBySubscription()
    {
        _context.RemediationBoards.AddRange(
            new RemediationBoard { Name = "B1", SubscriptionId = "sub1", Owner = "o" },
            new RemediationBoard { Name = "B2", SubscriptionId = "sub1", Owner = "o" },
            new RemediationBoard { Name = "B3", SubscriptionId = "sub2", Owner = "o" });
        await _context.SaveChangesAsync();

        var result = await _service.ListBoardsAsync("sub1");

        result.TotalCount.Should().Be(2);
    }

    [Fact]
    public async Task ListBoards_FilterByArchived()
    {
        _context.RemediationBoards.AddRange(
            new RemediationBoard { Name = "Active", SubscriptionId = "sub", Owner = "o", IsArchived = false },
            new RemediationBoard { Name = "Archived", SubscriptionId = "sub", Owner = "o", IsArchived = true });
        await _context.SaveChangesAsync();

        var result = await _service.ListBoardsAsync("sub", isArchived: true);

        result.TotalCount.Should().Be(1);
        result.Items[0].Name.Should().Be("Archived");
    }

    [Fact]
    public async Task ListBoards_Pagination()
    {
        for (int i = 0; i < 5; i++)
            _context.RemediationBoards.Add(new RemediationBoard { Name = $"B{i}", SubscriptionId = "sub", Owner = "o" });
        await _context.SaveChangesAsync();

        var result = await _service.ListBoardsAsync("sub", page: 1, pageSize: 2);

        result.TotalCount.Should().Be(5);
        result.Items.Should().HaveCount(2);
        result.TotalPages.Should().Be(3);
        result.HasMore.Should().BeTrue();
    }
}
