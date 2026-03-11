using System.Text.Json;
using Moq;
using Xunit;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Ato.Copilot.Agents.Compliance.Tools;
using Ato.Copilot.Core.Interfaces.Compliance;
using Ato.Copilot.Core.Models.Compliance;

namespace Ato.Copilot.Tests.Unit.Tools;

/// <summary>
/// Unit tests for Feature 024 — Narrative Governance Tools.
/// T042: NarrativeHistoryTool, NarrativeDiffTool, RollbackNarrativeTool
/// T044: SubmitNarrativeTool, ReviewNarrativeTool, BatchReviewNarrativesTool,
///       NarrativeApprovalProgressTool, BatchSubmitNarrativesTool
/// </summary>
public class NarrativeGovernanceToolTests
{
    private readonly Mock<INarrativeGovernanceService> _govMock = new();

    // =========================================================================
    // T042: NarrativeHistoryTool Tests
    // =========================================================================

    [Fact]
    public async Task NarrativeHistory_ReturnsSuccess()
    {
        _govMock
            .Setup(s => s.GetNarrativeHistoryAsync("sys-1", "AC-1", 1, 50, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<NarrativeVersion>
            {
                new()
                {
                    Id = "v1",
                    ControlImplementationId = "ci1",
                    VersionNumber = 2,
                    Content = "Updated content",
                    Status = SspSectionStatus.Draft,
                    AuthoredBy = "user",
                    AuthoredAt = DateTime.UtcNow
                },
                new()
                {
                    Id = "v2",
                    ControlImplementationId = "ci1",
                    VersionNumber = 1,
                    Content = "Original content",
                    Status = SspSectionStatus.Approved,
                    AuthoredBy = "user",
                    AuthoredAt = DateTime.UtcNow.AddHours(-1)
                }
            }, 2));

        var tool = new NarrativeHistoryTool(_govMock.Object, Mock.Of<ILogger<NarrativeHistoryTool>>());
        var result = await tool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["system_id"] = "sys-1",
            ["control_id"] = "AC-1"
        });

        var json = JsonDocument.Parse(result);
        json.RootElement.GetProperty("status").GetString().Should().Be("success");
        json.RootElement.GetProperty("data").GetProperty("total_versions").GetInt32().Should().Be(2);
    }

    [Fact]
    public async Task NarrativeHistory_MissingSystemId_ReturnsError()
    {
        var tool = new NarrativeHistoryTool(_govMock.Object, Mock.Of<ILogger<NarrativeHistoryTool>>());
        var result = await tool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["control_id"] = "AC-1"
        });

        var json = JsonDocument.Parse(result);
        json.RootElement.GetProperty("status").GetString().Should().Be("error");
        json.RootElement.GetProperty("errorCode").GetString().Should().Be("INVALID_INPUT");
    }

    [Fact]
    public async Task NarrativeHistory_SystemNotFound_ReturnsError()
    {
        _govMock
            .Setup(s => s.GetNarrativeHistoryAsync("bad-sys", "AC-1", 1, 50, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("SYSTEM_NOT_FOUND: System 'bad-sys' not found."));

        var tool = new NarrativeHistoryTool(_govMock.Object, Mock.Of<ILogger<NarrativeHistoryTool>>());
        var result = await tool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["system_id"] = "bad-sys",
            ["control_id"] = "AC-1"
        });

        var json = JsonDocument.Parse(result);
        json.RootElement.GetProperty("status").GetString().Should().Be("error");
        json.RootElement.GetProperty("errorCode").GetString().Should().Be("SYSTEM_NOT_FOUND");
    }

    // =========================================================================
    // T042: NarrativeDiffTool Tests
    // =========================================================================

    [Fact]
    public async Task NarrativeDiff_ReturnsSuccess()
    {
        _govMock
            .Setup(s => s.GetNarrativeDiffAsync("sys-1", "AC-1", 1, 2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new NarrativeDiff
            {
                FromVersion = 1,
                ToVersion = 2,
                UnifiedDiff = "- Old line\n+ New line",
                LinesAdded = 1,
                LinesRemoved = 1
            });

        var tool = new NarrativeDiffTool(_govMock.Object, Mock.Of<ILogger<NarrativeDiffTool>>());
        var result = await tool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["system_id"] = "sys-1",
            ["control_id"] = "AC-1",
            ["from_version"] = "1",
            ["to_version"] = "2"
        });

        var json = JsonDocument.Parse(result);
        json.RootElement.GetProperty("status").GetString().Should().Be("success");
        json.RootElement.GetProperty("data").GetProperty("lines_added").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task NarrativeDiff_VersionNotFound_ReturnsError()
    {
        _govMock
            .Setup(s => s.GetNarrativeDiffAsync("sys-1", "AC-1", 1, 99, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("VERSION_NOT_FOUND: Version 99 not found."));

        var tool = new NarrativeDiffTool(_govMock.Object, Mock.Of<ILogger<NarrativeDiffTool>>());
        var result = await tool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["system_id"] = "sys-1",
            ["control_id"] = "AC-1",
            ["from_version"] = "1",
            ["to_version"] = "99"
        });

        var json = JsonDocument.Parse(result);
        json.RootElement.GetProperty("status").GetString().Should().Be("error");
        json.RootElement.GetProperty("errorCode").GetString().Should().Be("VERSION_NOT_FOUND");
    }

    // =========================================================================
    // T042: RollbackNarrativeTool Tests
    // =========================================================================

    [Fact]
    public async Task RollbackNarrative_ReturnsSuccess()
    {
        _govMock
            .Setup(s => s.RollbackNarrativeAsync("sys-1", "AC-1", 1, "mcp-user", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new NarrativeVersion
            {
                Id = "v3",
                ControlImplementationId = "ci1",
                VersionNumber = 3,
                Content = "Rolled back content",
                Status = SspSectionStatus.Draft,
                AuthoredBy = "mcp-user",
                AuthoredAt = DateTime.UtcNow,
                ChangeReason = "Rolled back to version 1"
            });

        var tool = new RollbackNarrativeTool(_govMock.Object, Mock.Of<ILogger<RollbackNarrativeTool>>());
        var result = await tool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["system_id"] = "sys-1",
            ["control_id"] = "AC-1",
            ["target_version"] = "1"
        });

        var json = JsonDocument.Parse(result);
        json.RootElement.GetProperty("status").GetString().Should().Be("success");
        json.RootElement.GetProperty("data").GetProperty("new_version_number").GetInt32().Should().Be(3);
        json.RootElement.GetProperty("data").GetProperty("rolled_back_to").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task RollbackNarrative_UnderReview_ReturnsError()
    {
        _govMock
            .Setup(s => s.RollbackNarrativeAsync("sys-1", "AC-1", 1, "mcp-user", null, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("UNDER_REVIEW: Cannot modify while under review."));

        var tool = new RollbackNarrativeTool(_govMock.Object, Mock.Of<ILogger<RollbackNarrativeTool>>());
        var result = await tool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["system_id"] = "sys-1",
            ["control_id"] = "AC-1",
            ["target_version"] = "1"
        });

        var json = JsonDocument.Parse(result);
        json.RootElement.GetProperty("status").GetString().Should().Be("error");
        json.RootElement.GetProperty("errorCode").GetString().Should().Be("UNDER_REVIEW");
    }

    [Fact]
    public async Task RollbackNarrative_InvalidTargetVersion_ReturnsError()
    {
        var tool = new RollbackNarrativeTool(_govMock.Object, Mock.Of<ILogger<RollbackNarrativeTool>>());
        var result = await tool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["system_id"] = "sys-1",
            ["control_id"] = "AC-1",
            ["target_version"] = "not-a-number"
        });

        var json = JsonDocument.Parse(result);
        json.RootElement.GetProperty("status").GetString().Should().Be("error");
        json.RootElement.GetProperty("errorCode").GetString().Should().Be("INVALID_INPUT");
    }

    // =========================================================================
    // T044: SubmitNarrativeTool Tests
    // =========================================================================

    [Fact]
    public async Task SubmitNarrative_ReturnsSuccess()
    {
        _govMock
            .Setup(s => s.SubmitNarrativeAsync("sys-1", "AC-1", "mcp-user", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new NarrativeVersion
            {
                Id = "v1",
                ControlImplementationId = "ci1",
                VersionNumber = 1,
                Content = "Draft content",
                Status = SspSectionStatus.UnderReview,
                AuthoredBy = "user",
                AuthoredAt = DateTime.UtcNow.AddHours(-1),
                SubmittedBy = "mcp-user",
                SubmittedAt = DateTime.UtcNow
            });

        var tool = new SubmitNarrativeTool(_govMock.Object, Mock.Of<ILogger<SubmitNarrativeTool>>());
        var result = await tool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["system_id"] = "sys-1",
            ["control_id"] = "AC-1"
        });

        var json = JsonDocument.Parse(result);
        json.RootElement.GetProperty("status").GetString().Should().Be("success");
        json.RootElement.GetProperty("data").GetProperty("new_status").GetString().Should().Be("UnderReview");
    }

    [Fact]
    public async Task SubmitNarrative_InvalidStatus_ReturnsError()
    {
        _govMock
            .Setup(s => s.SubmitNarrativeAsync("sys-1", "AC-1", "mcp-user", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("INVALID_STATUS: Cannot submit."));

        var tool = new SubmitNarrativeTool(_govMock.Object, Mock.Of<ILogger<SubmitNarrativeTool>>());
        var result = await tool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["system_id"] = "sys-1",
            ["control_id"] = "AC-1"
        });

        var json = JsonDocument.Parse(result);
        json.RootElement.GetProperty("status").GetString().Should().Be("error");
        json.RootElement.GetProperty("errorCode").GetString().Should().Be("INVALID_STATUS");
    }

    // =========================================================================
    // T044: ReviewNarrativeTool Tests
    // =========================================================================

    [Fact]
    public async Task ReviewNarrative_Approve_ReturnsSuccess()
    {
        _govMock
            .Setup(s => s.ReviewNarrativeAsync("sys-1", "AC-1", ReviewDecision.Approve, "mcp-user", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new NarrativeReview
            {
                Id = "r1",
                NarrativeVersionId = "v1",
                ReviewedBy = "mcp-user",
                Decision = ReviewDecision.Approve,
                ReviewedAt = DateTime.UtcNow
            });

        var tool = new ReviewNarrativeTool(_govMock.Object, Mock.Of<ILogger<ReviewNarrativeTool>>());
        var result = await tool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["system_id"] = "sys-1",
            ["control_id"] = "AC-1",
            ["decision"] = "approve"
        });

        var json = JsonDocument.Parse(result);
        json.RootElement.GetProperty("status").GetString().Should().Be("success");
        json.RootElement.GetProperty("data").GetProperty("new_status").GetString().Should().Be("Approved");
    }

    [Fact]
    public async Task ReviewNarrative_RequestRevision_ReturnsSuccess()
    {
        _govMock
            .Setup(s => s.ReviewNarrativeAsync("sys-1", "AC-1", ReviewDecision.RequestRevision, "mcp-user", "Fix it", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new NarrativeReview
            {
                Id = "r1",
                NarrativeVersionId = "v1",
                ReviewedBy = "mcp-user",
                Decision = ReviewDecision.RequestRevision,
                ReviewerComments = "Fix it",
                ReviewedAt = DateTime.UtcNow
            });

        var tool = new ReviewNarrativeTool(_govMock.Object, Mock.Of<ILogger<ReviewNarrativeTool>>());
        var result = await tool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["system_id"] = "sys-1",
            ["control_id"] = "AC-1",
            ["decision"] = "request_revision",
            ["comments"] = "Fix it"
        });

        var json = JsonDocument.Parse(result);
        json.RootElement.GetProperty("status").GetString().Should().Be("success");
        json.RootElement.GetProperty("data").GetProperty("new_status").GetString().Should().Be("NeedsRevision");
    }

    [Fact]
    public async Task ReviewNarrative_InvalidDecision_ReturnsError()
    {
        var tool = new ReviewNarrativeTool(_govMock.Object, Mock.Of<ILogger<ReviewNarrativeTool>>());
        var result = await tool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["system_id"] = "sys-1",
            ["control_id"] = "AC-1",
            ["decision"] = "invalid_decision"
        });

        var json = JsonDocument.Parse(result);
        json.RootElement.GetProperty("status").GetString().Should().Be("error");
        json.RootElement.GetProperty("errorCode").GetString().Should().Be("INVALID_INPUT");
    }

    // =========================================================================
    // T044: BatchReviewNarrativesTool Tests
    // =========================================================================

    [Fact]
    public async Task BatchReview_FamilyFilter_ReturnsSuccess()
    {
        _govMock
            .Setup(s => s.BatchReviewNarrativesAsync(
                "sys-1", ReviewDecision.Approve, "mcp-user", null, "AC", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<string> { "AC-1", "AC-2" }, new List<string>()));

        var tool = new BatchReviewNarrativesTool(_govMock.Object, Mock.Of<ILogger<BatchReviewNarrativesTool>>());
        var result = await tool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["system_id"] = "sys-1",
            ["decision"] = "approve",
            ["family_filter"] = "AC"
        });

        var json = JsonDocument.Parse(result);
        json.RootElement.GetProperty("status").GetString().Should().Be("success");
        json.RootElement.GetProperty("data").GetProperty("reviewed_count").GetInt32().Should().Be(2);
    }

    [Fact]
    public async Task BatchReview_MissingDecision_ReturnsError()
    {
        var tool = new BatchReviewNarrativesTool(_govMock.Object, Mock.Of<ILogger<BatchReviewNarrativesTool>>());
        var result = await tool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["system_id"] = "sys-1"
        });

        var json = JsonDocument.Parse(result);
        json.RootElement.GetProperty("status").GetString().Should().Be("error");
        json.RootElement.GetProperty("errorCode").GetString().Should().Be("INVALID_INPUT");
    }

    // =========================================================================
    // T044: NarrativeApprovalProgressTool Tests
    // =========================================================================

    [Fact]
    public async Task ApprovalProgress_ReturnsSuccess()
    {
        _govMock
            .Setup(s => s.GetNarrativeApprovalProgressAsync("sys-1", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GovernanceProgressReport
            {
                SystemId = "sys-1",
                TotalControls = 5,
                TotalApproved = 2,
                TotalDraft = 1,
                TotalUnderReview = 1,
                TotalNeedsRevision = 1,
                TotalNotStarted = 0,
                OverallApprovalPercent = 40.0,
                FamilyBreakdowns = new List<GovernanceFamilyProgress>
                {
                    new() { Family = "AC", Total = 3, Approved = 1, Draft = 1, UnderReview = 1 },
                    new() { Family = "SI", Total = 2, Approved = 1, NeedsRevision = 1 }
                },
                ReviewQueue = new List<string> { "AC-2" },
                StalenessWarnings = new List<StalenessWarning>
                {
                    new() { ControlId = "AC-3", Message = "Draft with content not yet submitted" }
                }
            });

        var tool = new NarrativeApprovalProgressTool(_govMock.Object, Mock.Of<ILogger<NarrativeApprovalProgressTool>>());
        var result = await tool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["system_id"] = "sys-1"
        });

        var json = JsonDocument.Parse(result);
        json.RootElement.GetProperty("status").GetString().Should().Be("success");
        json.RootElement.GetProperty("data").GetProperty("overall").GetProperty("total_controls").GetInt32().Should().Be(5);
        json.RootElement.GetProperty("data").GetProperty("overall").GetProperty("approval_percentage").GetDouble().Should().Be(40.0);
    }

    [Fact]
    public async Task ApprovalProgress_MissingSystemId_ReturnsError()
    {
        var tool = new NarrativeApprovalProgressTool(_govMock.Object, Mock.Of<ILogger<NarrativeApprovalProgressTool>>());
        var result = await tool.ExecuteAsync(new Dictionary<string, object?>());

        var json = JsonDocument.Parse(result);
        json.RootElement.GetProperty("status").GetString().Should().Be("error");
        json.RootElement.GetProperty("errorCode").GetString().Should().Be("INVALID_INPUT");
    }

    // =========================================================================
    // T044: BatchSubmitNarrativesTool Tests
    // =========================================================================

    [Fact]
    public async Task BatchSubmit_ReturnsSuccess()
    {
        _govMock
            .Setup(s => s.BatchSubmitNarrativesAsync("sys-1", "AC", "mcp-user", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BatchSubmitResult
            {
                SubmittedCount = 2,
                SkippedCount = 0,
                SubmittedControlIds = new List<string> { "AC-1", "AC-2" },
                SkippedReasons = new List<string>()
            });

        var tool = new BatchSubmitNarrativesTool(_govMock.Object, Mock.Of<ILogger<BatchSubmitNarrativesTool>>());
        var result = await tool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["system_id"] = "sys-1",
            ["family_filter"] = "AC"
        });

        var json = JsonDocument.Parse(result);
        json.RootElement.GetProperty("status").GetString().Should().Be("success");
        json.RootElement.GetProperty("data").GetProperty("submitted_count").GetInt32().Should().Be(2);
    }

    [Fact]
    public async Task BatchSubmit_NoDraftNarratives_ReturnsError()
    {
        _govMock
            .Setup(s => s.BatchSubmitNarrativesAsync("sys-1", null, "mcp-user", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("NO_DRAFT_NARRATIVES: No draft narratives found."));

        var tool = new BatchSubmitNarrativesTool(_govMock.Object, Mock.Of<ILogger<BatchSubmitNarrativesTool>>());
        var result = await tool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["system_id"] = "sys-1"
        });

        var json = JsonDocument.Parse(result);
        json.RootElement.GetProperty("status").GetString().Should().Be("error");
        json.RootElement.GetProperty("errorCode").GetString().Should().Be("NO_DRAFT_NARRATIVES");
    }
}
