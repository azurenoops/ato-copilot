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
/// Unit tests for KanbanService comment operations: AddComment, EditComment,
/// DeleteComment, ListComments — including time windows and RBAC.
/// </summary>
public class KanbanServiceCommentTests : IDisposable
{
    private readonly AtoCopilotContext _context;
    private readonly KanbanService _service;
    private readonly Mock<INotificationService> _notificationMock = new();

    public KanbanServiceCommentTests()
    {
        var options = new DbContextOptionsBuilder<AtoCopilotContext>()
            .UseInMemoryDatabase($"KanbanComment_{Guid.NewGuid()}")
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

    // ─── AddComment ─────────────────────────────────────────────────────────

    [Fact]
    public async Task AddComment_CreatesWithCorrectFields()
    {
        var task = await SeedTask();

        var comment = await _service.AddCommentAsync(
            task.Id, "user1", "User One", "This is a test comment", ComplianceRoles.Administrator);

        comment.Should().NotBeNull();
        comment.AuthorId.Should().Be("user1");
        comment.AuthorName.Should().Be("User One");
        comment.Content.Should().Be("This is a test comment");
        comment.IsEdited.Should().BeFalse();
        comment.IsDeleted.Should().BeFalse();
    }

    [Fact]
    public async Task AddComment_ExtractsMentions()
    {
        var task = await SeedTask();

        var comment = await _service.AddCommentAsync(
            task.Id, "user1", "User One", "Hey @john.doe and @jane.smith please review",
            ComplianceRoles.Administrator);

        comment.Mentions.Should().Contain("john.doe");
        comment.Mentions.Should().Contain("jane.smith");
    }

    [Fact]
    public async Task AddComment_LogsHistoryEntry()
    {
        var task = await SeedTask();

        await _service.AddCommentAsync(
            task.Id, "user1", "User One", "Test", ComplianceRoles.Administrator);

        var history = await _context.TaskHistoryEntries
            .Where(h => h.TaskId == task.Id && h.EventType == HistoryEventType.CommentAdded)
            .ToListAsync();

        history.Should().HaveCount(1);
    }

    [Fact]
    public async Task AddComment_EnqueuesNotification()
    {
        var task = await SeedTask();

        await _service.AddCommentAsync(
            task.Id, "user1", "User One", "Test", ComplianceRoles.Administrator);

        _notificationMock.Verify(
            n => n.EnqueueAsync(It.Is<NotificationMessage>(m =>
                m.EventType == NotificationEventType.CommentAdded)),
            Times.Once);
    }

    [Fact]
    public async Task AddComment_WithMentions_EnqueuesMentionNotifications()
    {
        var task = await SeedTask();

        await _service.AddCommentAsync(
            task.Id, "user1", "User One", "Hey @john.doe check this", ComplianceRoles.Administrator);

        _notificationMock.Verify(
            n => n.EnqueueAsync(It.Is<NotificationMessage>(m =>
                m.EventType == NotificationEventType.Mentioned &&
                m.TargetUserId == "john.doe")),
            Times.Once);
    }

    [Fact]
    public async Task AddComment_AuditorBlocked()
    {
        var task = await SeedTask();

        var act = () => _service.AddCommentAsync(
            task.Id, "aud1", "Auditor", "Comment", ComplianceRoles.Auditor);

        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*UNAUTHORIZED*");
    }

    [Fact]
    public async Task AddComment_SupportsThreading()
    {
        var task = await SeedTask();

        var parent = await _service.AddCommentAsync(
            task.Id, "user1", "User One", "Parent comment", ComplianceRoles.Administrator);

        var reply = await _service.AddCommentAsync(
            task.Id, "user2", "User Two", "Reply to parent", ComplianceRoles.Administrator,
            parentCommentId: parent.Id);

        reply.ParentCommentId.Should().Be(parent.Id);
    }

    // ─── EditComment ────────────────────────────────────────────────────────

    [Fact]
    public async Task EditComment_Within24Hours_Succeeds()
    {
        var task = await SeedTask();
        var comment = await SeedComment(task.Id, "user1", DateTime.UtcNow);

        var edited = await _service.EditCommentAsync(
            comment.Id, "user1", "Updated content");

        edited.Content.Should().Be("Updated content");
        edited.IsEdited.Should().BeTrue();
        edited.EditedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task EditComment_After24Hours_ThrowsExpired()
    {
        var task = await SeedTask();
        var comment = await SeedComment(task.Id, "user1", DateTime.UtcNow.AddHours(-25));

        var act = () => _service.EditCommentAsync(comment.Id, "user1", "Updated");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*COMMENT_EDIT_WINDOW_EXPIRED*");
    }

    [Fact]
    public async Task EditComment_ByDifferentUser_Throws()
    {
        var task = await SeedTask();
        var comment = await SeedComment(task.Id, "user1", DateTime.UtcNow);

        var act = () => _service.EditCommentAsync(comment.Id, "user2", "Updated");

        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*another user*");
    }

    [Fact]
    public async Task EditComment_OnDoneTask_Throws()
    {
        var task = await SeedTask(TaskStatus.Done);
        var comment = await SeedComment(task.Id, "user1", DateTime.UtcNow);

        var act = () => _service.EditCommentAsync(comment.Id, "user1", "Updated");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Done*");
    }

    [Fact]
    public async Task EditComment_DeletedComment_Throws()
    {
        var task = await SeedTask();
        var comment = await SeedComment(task.Id, "user1", DateTime.UtcNow);
        comment.IsDeleted = true;
        await _context.SaveChangesAsync();

        var act = () => _service.EditCommentAsync(comment.Id, "user1", "Updated");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*deleted*");
    }

    // ─── DeleteComment ──────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteComment_Within1Hour_AuthorCanDelete()
    {
        var task = await SeedTask();
        var comment = await SeedComment(task.Id, "user1", DateTime.UtcNow);

        var deleted = await _service.DeleteCommentAsync(
            comment.Id, "user1", ComplianceRoles.Analyst);

        deleted.IsDeleted.Should().BeTrue();
        deleted.Content.Should().Be(KanbanConstants.DeletedCommentContent);
    }

    [Fact]
    public async Task DeleteComment_After1Hour_NonCO_ThrowsExpired()
    {
        var task = await SeedTask();
        var comment = await SeedComment(task.Id, "user1", DateTime.UtcNow.AddHours(-2));

        var act = () => _service.DeleteCommentAsync(
            comment.Id, "user1", ComplianceRoles.Analyst);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*COMMENT_DELETE_WINDOW_EXPIRED*");
    }

    [Fact]
    public async Task DeleteComment_CO_CanDeleteAnyTime()
    {
        var task = await SeedTask();
        var comment = await SeedComment(task.Id, "user1", DateTime.UtcNow.AddHours(-48));

        var deleted = await _service.DeleteCommentAsync(
            comment.Id, "admin1", ComplianceRoles.Administrator);

        deleted.IsDeleted.Should().BeTrue();
    }

    [Fact]
    public async Task DeleteComment_NonAuthor_NonCO_Throws()
    {
        var task = await SeedTask();
        var comment = await SeedComment(task.Id, "user1", DateTime.UtcNow);

        var act = () => _service.DeleteCommentAsync(
            comment.Id, "user2", ComplianceRoles.Analyst);

        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*another user*");
    }

    [Fact]
    public async Task DeleteComment_OnDoneTask_Throws()
    {
        var task = await SeedTask(TaskStatus.Done);
        var comment = await SeedComment(task.Id, "user1", DateTime.UtcNow);

        var act = () => _service.DeleteCommentAsync(
            comment.Id, "user1", ComplianceRoles.Analyst);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Done*");
    }

    [Fact]
    public async Task DeleteComment_AlreadyDeleted_Throws()
    {
        var task = await SeedTask();
        var comment = await SeedComment(task.Id, "user1", DateTime.UtcNow);
        comment.IsDeleted = true;
        await _context.SaveChangesAsync();

        var act = () => _service.DeleteCommentAsync(
            comment.Id, "user1", ComplianceRoles.Analyst);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*already deleted*");
    }

    // ─── ListComments ───────────────────────────────────────────────────────

    [Fact]
    public async Task ListComments_ExcludesDeletedByDefault()
    {
        var task = await SeedTask();
        var c1 = await SeedComment(task.Id, "user1", DateTime.UtcNow);
        var c2 = await SeedComment(task.Id, "user2", DateTime.UtcNow);
        c2.IsDeleted = true;
        await _context.SaveChangesAsync();

        var result = await _service.ListCommentsAsync(task.Id);

        result.Items.Should().HaveCount(1);
        result.TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task ListComments_IncludesDeletedWhenRequested()
    {
        var task = await SeedTask();
        await SeedComment(task.Id, "user1", DateTime.UtcNow);
        var c2 = await SeedComment(task.Id, "user2", DateTime.UtcNow);
        c2.IsDeleted = true;
        await _context.SaveChangesAsync();

        var result = await _service.ListCommentsAsync(task.Id, includeDeleted: true);

        result.Items.Should().HaveCount(2);
    }

    [Fact]
    public async Task ListComments_Pagination()
    {
        var task = await SeedTask();
        for (int i = 0; i < 5; i++)
            await SeedComment(task.Id, $"user{i}", DateTime.UtcNow.AddMinutes(i));

        var page1 = await _service.ListCommentsAsync(task.Id, page: 1, pageSize: 2);
        var page2 = await _service.ListCommentsAsync(task.Id, page: 2, pageSize: 2);

        page1.Items.Should().HaveCount(2);
        page2.Items.Should().HaveCount(2);
        page1.TotalCount.Should().Be(5);
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private async Task<RemediationTask> SeedTask(TaskStatus status = TaskStatus.InProgress)
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
            Severity = FindingSeverity.High,
            CreatedBy = "owner",
        };
        _context.RemediationTasks.Add(task);
        await _context.SaveChangesAsync();
        return task;
    }

    private async Task<TaskComment> SeedComment(string taskId, string authorId, DateTime createdAt)
    {
        var comment = new TaskComment
        {
            TaskId = taskId,
            AuthorId = authorId,
            AuthorName = $"{authorId}-name",
            Content = "Test comment",
            CreatedAt = createdAt,
        };
        _context.TaskComments.Add(comment);
        await _context.SaveChangesAsync();
        return comment;
    }
}
