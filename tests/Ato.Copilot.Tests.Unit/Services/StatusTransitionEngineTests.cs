using Xunit;
using FluentAssertions;
using Ato.Copilot.Agents.Compliance.Services;
using TaskStatus = Ato.Copilot.Core.Models.Kanban.TaskStatus;

namespace Ato.Copilot.Tests.Unit.Services;

/// <summary>
/// Unit tests for StatusTransitionEngine: validates all 16 allowed transitions
/// and verifies invalid transitions are rejected.
/// </summary>
public class StatusTransitionEngineTests
{
    // ─── Valid Transitions ───────────────────────────────────────────────────

    [Theory]
    [InlineData(TaskStatus.Backlog, TaskStatus.ToDo)]
    [InlineData(TaskStatus.Backlog, TaskStatus.InProgress)]
    [InlineData(TaskStatus.Backlog, TaskStatus.Blocked)]
    [InlineData(TaskStatus.ToDo, TaskStatus.InProgress)]
    [InlineData(TaskStatus.ToDo, TaskStatus.Blocked)]
    [InlineData(TaskStatus.ToDo, TaskStatus.Backlog)]
    [InlineData(TaskStatus.InProgress, TaskStatus.InReview)]
    [InlineData(TaskStatus.InProgress, TaskStatus.Blocked)]
    [InlineData(TaskStatus.InProgress, TaskStatus.ToDo)]
    [InlineData(TaskStatus.InReview, TaskStatus.Done)]
    [InlineData(TaskStatus.InReview, TaskStatus.Blocked)]
    [InlineData(TaskStatus.InReview, TaskStatus.InProgress)]
    [InlineData(TaskStatus.Blocked, TaskStatus.Backlog)]
    [InlineData(TaskStatus.Blocked, TaskStatus.ToDo)]
    [InlineData(TaskStatus.Blocked, TaskStatus.InProgress)]
    [InlineData(TaskStatus.Blocked, TaskStatus.InReview)]
    public void IsTransitionAllowed_ValidPairs_ReturnsTrue(TaskStatus from, TaskStatus to)
    {
        StatusTransitionEngine.IsTransitionAllowed(from, to).Should().BeTrue();
    }

    // ─── Invalid Transitions ────────────────────────────────────────────────

    [Theory]
    [InlineData(TaskStatus.Done, TaskStatus.Backlog)]
    [InlineData(TaskStatus.Done, TaskStatus.ToDo)]
    [InlineData(TaskStatus.Done, TaskStatus.InProgress)]
    [InlineData(TaskStatus.Done, TaskStatus.InReview)]
    [InlineData(TaskStatus.Done, TaskStatus.Blocked)]
    [InlineData(TaskStatus.Backlog, TaskStatus.Done)]
    [InlineData(TaskStatus.Backlog, TaskStatus.InReview)]
    [InlineData(TaskStatus.ToDo, TaskStatus.Done)]
    [InlineData(TaskStatus.ToDo, TaskStatus.InReview)]
    [InlineData(TaskStatus.InProgress, TaskStatus.Done)]
    [InlineData(TaskStatus.InProgress, TaskStatus.Backlog)]
    [InlineData(TaskStatus.InReview, TaskStatus.Backlog)]
    [InlineData(TaskStatus.InReview, TaskStatus.ToDo)]
    [InlineData(TaskStatus.Blocked, TaskStatus.Done)]
    public void IsTransitionAllowed_InvalidPairs_ReturnsFalse(TaskStatus from, TaskStatus to)
    {
        StatusTransitionEngine.IsTransitionAllowed(from, to).Should().BeFalse();
    }

    // ─── Transition Rules ───────────────────────────────────────────────────

    [Fact]
    public void GetTransitionRule_ToBlocked_RequiresComment()
    {
        var statuses = new[] { TaskStatus.Backlog, TaskStatus.ToDo, TaskStatus.InProgress, TaskStatus.InReview };
        foreach (var from in statuses)
        {
            var rule = StatusTransitionEngine.GetTransitionRule(from, TaskStatus.Blocked);
            rule.Should().NotBeNull($"{from}→Blocked should be allowed");
            rule!.RequiresComment.Should().BeTrue($"{from}→Blocked should require blocker comment");
        }
    }

    [Fact]
    public void GetTransitionRule_FromBlocked_RequiresResolutionComment()
    {
        var targets = new[] { TaskStatus.Backlog, TaskStatus.ToDo, TaskStatus.InProgress, TaskStatus.InReview };
        foreach (var to in targets)
        {
            var rule = StatusTransitionEngine.GetTransitionRule(TaskStatus.Blocked, to);
            rule.Should().NotBeNull($"Blocked→{to} should be allowed");
            rule!.RequiresResolutionComment.Should().BeTrue($"Blocked→{to} should require resolution comment");
        }
    }

    [Fact]
    public void GetTransitionRule_InReviewToDone_RequiresValidation()
    {
        var rule = StatusTransitionEngine.GetTransitionRule(TaskStatus.InReview, TaskStatus.Done);
        rule.Should().NotBeNull();
        rule!.RequiresValidation.Should().BeTrue();
        rule.AllowSkipValidation.Should().BeTrue();
    }

    [Fact]
    public void GetTransitionRule_InProgressToInReview_TriggersValidation()
    {
        var rule = StatusTransitionEngine.GetTransitionRule(TaskStatus.InProgress, TaskStatus.InReview);
        rule.Should().NotBeNull();
        rule!.TriggersValidation.Should().BeTrue();
    }

    [Fact]
    public void GetTransitionRule_BacklogToInProgress_AutoAssigns()
    {
        var rule = StatusTransitionEngine.GetTransitionRule(TaskStatus.Backlog, TaskStatus.InProgress);
        rule.Should().NotBeNull();
        rule!.AutoAssign.Should().BeTrue();
    }

    [Fact]
    public void GetTransitionRule_ToDoToInProgress_AutoAssigns()
    {
        var rule = StatusTransitionEngine.GetTransitionRule(TaskStatus.ToDo, TaskStatus.InProgress);
        rule.Should().NotBeNull();
        rule!.AutoAssign.Should().BeTrue();
    }

    [Fact]
    public void GetTransitionRule_InvalidTransition_ReturnsNull()
    {
        var rule = StatusTransitionEngine.GetTransitionRule(TaskStatus.Done, TaskStatus.Backlog);
        rule.Should().BeNull();
    }

    // ─── GetAllowedTransitions ──────────────────────────────────────────────

    [Fact]
    public void GetAllowedTransitions_FromBacklog_Returns3Targets()
    {
        var allowed = StatusTransitionEngine.GetAllowedTransitions(TaskStatus.Backlog);
        allowed.Should().HaveCount(3);
        allowed.Should().Contain(TaskStatus.ToDo);
        allowed.Should().Contain(TaskStatus.InProgress);
        allowed.Should().Contain(TaskStatus.Blocked);
    }

    [Fact]
    public void GetAllowedTransitions_FromDone_ReturnsEmpty()
    {
        var allowed = StatusTransitionEngine.GetAllowedTransitions(TaskStatus.Done);
        allowed.Should().BeEmpty();
    }

    [Fact]
    public void GetAllowedTransitions_FromBlocked_Returns4Targets()
    {
        var allowed = StatusTransitionEngine.GetAllowedTransitions(TaskStatus.Blocked);
        allowed.Should().HaveCount(4);
        allowed.Should().Contain(TaskStatus.Backlog);
        allowed.Should().Contain(TaskStatus.ToDo);
        allowed.Should().Contain(TaskStatus.InProgress);
        allowed.Should().Contain(TaskStatus.InReview);
    }

    [Fact]
    public void TotalAllowedTransitions_ShouldBe16()
    {
        var allStatuses = Enum.GetValues<TaskStatus>();
        var count = 0;
        foreach (var from in allStatuses)
            foreach (var to in allStatuses)
                if (StatusTransitionEngine.IsTransitionAllowed(from, to))
                    count++;

        count.Should().Be(16);
    }
}
