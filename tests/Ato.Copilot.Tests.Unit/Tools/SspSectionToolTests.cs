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
/// Unit tests for Feature 022 SSP section tools:
/// T010: WriteSspSectionTool, T011: ReviewSspSectionTool, T012: SspCompletenessTool
/// </summary>
public class SspSectionToolTests
{
    private readonly Mock<ISspService> _sspMock = new();

    // ────────────────────────────────────────────────────────────────────────
    // T010: WriteSspSectionTool Tests
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task WriteSspSection_Success_ReturnsExpectedPayload()
    {
        var section = CreateSection(5, "Rules of Behavior", SspSectionStatus.Draft, 1);
        _sspMock
            .Setup(s => s.WriteSspSectionAsync("sys-1", 5, "Content", "author@test.com",
                null, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(section);

        var tool = CreateWriteSspSectionTool();
        var result = await tool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["system_id"] = "sys-1",
            ["section_number"] = 5,
            ["content"] = "Content",
            ["authored_by"] = "author@test.com"
        });

        var json = JsonDocument.Parse(result);
        json.RootElement.GetProperty("status").GetString().Should().Be("success");
        var data = json.RootElement.GetProperty("data");
        data.GetProperty("section_number").GetInt32().Should().Be(5);
        data.GetProperty("section_title").GetString().Should().Be("Rules of Behavior");
        data.GetProperty("status").GetString().Should().Be("Draft");
        data.GetProperty("version").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task WriteSspSection_WithSubmitForReview_PassesFlag()
    {
        var section = CreateSection(5, "Rules of Behavior", SspSectionStatus.UnderReview, 1);
        _sspMock
            .Setup(s => s.WriteSspSectionAsync("sys-1", 5, "Content", "author@test.com",
                null, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(section);

        var tool = CreateWriteSspSectionTool();
        var result = await tool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["system_id"] = "sys-1",
            ["section_number"] = 5,
            ["content"] = "Content",
            ["authored_by"] = "author@test.com",
            ["submit_for_review"] = true
        });

        var json = JsonDocument.Parse(result);
        json.RootElement.GetProperty("status").GetString().Should().Be("success");
        json.RootElement.GetProperty("data").GetProperty("status").GetString().Should().Be("UnderReview");
    }

    [Fact]
    public async Task WriteSspSection_MissingSystemId_ReturnsError()
    {
        var tool = CreateWriteSspSectionTool();
        var result = await tool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["section_number"] = 5,
            ["authored_by"] = "author@test.com"
        });

        var json = JsonDocument.Parse(result);
        json.RootElement.GetProperty("status").GetString().Should().Be("error");
        json.RootElement.GetProperty("errorCode").GetString().Should().Be("INVALID_INPUT");
    }

    [Fact]
    public async Task WriteSspSection_InvalidSectionNumber_ReturnsError()
    {
        var tool = CreateWriteSspSectionTool();
        var result = await tool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["system_id"] = "sys-1",
            ["section_number"] = 99,
            ["authored_by"] = "author@test.com"
        });

        var json = JsonDocument.Parse(result);
        json.RootElement.GetProperty("status").GetString().Should().Be("error");
        json.RootElement.GetProperty("errorCode").GetString().Should().Be("INVALID_SECTION_NUMBER");
    }

    [Fact]
    public async Task WriteSspSection_ConcurrencyConflict_ReturnsError()
    {
        _sspMock
            .Setup(s => s.WriteSspSectionAsync("sys-1", 5, "Content", "author@test.com",
                10, false, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("CONCURRENCY_CONFLICT: Version mismatch"));

        var tool = CreateWriteSspSectionTool();
        var result = await tool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["system_id"] = "sys-1",
            ["section_number"] = 5,
            ["content"] = "Content",
            ["authored_by"] = "author@test.com",
            ["expected_version"] = 10
        });

        var json = JsonDocument.Parse(result);
        json.RootElement.GetProperty("status").GetString().Should().Be("error");
        json.RootElement.GetProperty("errorCode").GetString().Should().Be("CONCURRENCY_CONFLICT");
    }

    [Fact]
    public async Task WriteSspSection_SystemNotFound_ReturnsError()
    {
        _sspMock
            .Setup(s => s.WriteSspSectionAsync("bad-id", 5, "Content", "author@test.com",
                null, false, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("SYSTEM_NOT_FOUND: No system with id 'bad-id'"));

        var tool = CreateWriteSspSectionTool();
        var result = await tool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["system_id"] = "bad-id",
            ["section_number"] = 5,
            ["content"] = "Content",
            ["authored_by"] = "author@test.com"
        });

        var json = JsonDocument.Parse(result);
        json.RootElement.GetProperty("status").GetString().Should().Be("error");
        json.RootElement.GetProperty("errorCode").GetString().Should().Be("SYSTEM_NOT_FOUND");
    }

    // ────────────────────────────────────────────────────────────────────────
    // T011: ReviewSspSectionTool Tests
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ReviewSspSection_Approve_ReturnsSuccess()
    {
        var section = CreateSection(5, "Rules of Behavior", SspSectionStatus.Approved, 1);
        section.ReviewedBy = "reviewer@test.com";
        section.ReviewedAt = DateTime.UtcNow;
        _sspMock
            .Setup(s => s.ReviewSspSectionAsync("sys-1", 5, "approve", "reviewer@test.com",
                null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(section);

        var tool = CreateReviewSspSectionTool();
        var result = await tool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["system_id"] = "sys-1",
            ["section_number"] = 5,
            ["decision"] = "approve",
            ["reviewer"] = "reviewer@test.com"
        });

        var json = JsonDocument.Parse(result);
        json.RootElement.GetProperty("status").GetString().Should().Be("success");
        var data = json.RootElement.GetProperty("data");
        data.GetProperty("status").GetString().Should().Be("Approved");
        data.GetProperty("reviewed_by").GetString().Should().Be("reviewer@test.com");
    }

    [Fact]
    public async Task ReviewSspSection_RequestRevision_ReturnsComments()
    {
        var section = CreateSection(5, "Rules of Behavior", SspSectionStatus.Draft, 1);
        section.ReviewedBy = "reviewer@test.com";
        section.ReviewedAt = DateTime.UtcNow;
        section.ReviewerComments = "Needs more detail";
        _sspMock
            .Setup(s => s.ReviewSspSectionAsync("sys-1", 5, "request_revision", "reviewer@test.com",
                "Needs more detail", It.IsAny<CancellationToken>()))
            .ReturnsAsync(section);

        var tool = CreateReviewSspSectionTool();
        var result = await tool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["system_id"] = "sys-1",
            ["section_number"] = 5,
            ["decision"] = "request_revision",
            ["reviewer"] = "reviewer@test.com",
            ["comments"] = "Needs more detail"
        });

        var json = JsonDocument.Parse(result);
        json.RootElement.GetProperty("status").GetString().Should().Be("success");
        json.RootElement.GetProperty("data").GetProperty("reviewer_comments").GetString()
            .Should().Be("Needs more detail");
    }

    [Fact]
    public async Task ReviewSspSection_MissingDecision_ReturnsError()
    {
        var tool = CreateReviewSspSectionTool();
        var result = await tool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["system_id"] = "sys-1",
            ["section_number"] = 5,
            ["reviewer"] = "reviewer@test.com"
        });

        var json = JsonDocument.Parse(result);
        json.RootElement.GetProperty("status").GetString().Should().Be("error");
        json.RootElement.GetProperty("errorCode").GetString().Should().Be("INVALID_INPUT");
    }

    [Fact]
    public async Task ReviewSspSection_CommentsRequired_ReturnsError()
    {
        _sspMock
            .Setup(s => s.ReviewSspSectionAsync("sys-1", 5, "request_revision", "reviewer@test.com",
                null, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("COMMENTS_REQUIRED: Comments are required for revision"));

        var tool = CreateReviewSspSectionTool();
        var result = await tool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["system_id"] = "sys-1",
            ["section_number"] = 5,
            ["decision"] = "request_revision",
            ["reviewer"] = "reviewer@test.com"
        });

        var json = JsonDocument.Parse(result);
        json.RootElement.GetProperty("status").GetString().Should().Be("error");
        json.RootElement.GetProperty("errorCode").GetString().Should().Be("COMMENTS_REQUIRED");
    }

    // ────────────────────────────────────────────────────────────────────────
    // T012: SspCompletenessTool Tests
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task SspCompleteness_ReturnsFullReport()
    {
        var report = CreateCompletenessReport();
        _sspMock
            .Setup(s => s.GetSspCompletenessAsync("sys-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(report);

        var tool = CreateSspCompletenessTool();
        var result = await tool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["system_id"] = "sys-1"
        });

        var json = JsonDocument.Parse(result);
        json.RootElement.GetProperty("status").GetString().Should().Be("success");
        var data = json.RootElement.GetProperty("data");
        data.GetProperty("system_name").GetString().Should().Be("Test System");
        data.GetProperty("overall_readiness_percent").GetDouble().Should().Be(8);
        data.GetProperty("approved_count").GetInt32().Should().Be(1);
        data.GetProperty("total_sections").GetInt32().Should().Be(13);
        data.GetProperty("sections").GetArrayLength().Should().Be(1);
    }

    [Fact]
    public async Task SspCompleteness_MissingSystemId_ReturnsError()
    {
        var tool = CreateSspCompletenessTool();
        var result = await tool.ExecuteAsync(new Dictionary<string, object?>());

        var json = JsonDocument.Parse(result);
        json.RootElement.GetProperty("status").GetString().Should().Be("error");
        json.RootElement.GetProperty("errorCode").GetString().Should().Be("INVALID_INPUT");
    }

    [Fact]
    public async Task SspCompleteness_SystemNotFound_ReturnsError()
    {
        _sspMock
            .Setup(s => s.GetSspCompletenessAsync("bad-id", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("SYSTEM_NOT_FOUND: No system with id 'bad-id'"));

        var tool = CreateSspCompletenessTool();
        var result = await tool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["system_id"] = "bad-id"
        });

        var json = JsonDocument.Parse(result);
        json.RootElement.GetProperty("status").GetString().Should().Be("error");
        json.RootElement.GetProperty("errorCode").GetString().Should().Be("SYSTEM_NOT_FOUND");
    }

    // ────────────────────────────────────────────────────────────────────────
    // Tool factories
    // ────────────────────────────────────────────────────────────────────────

    private WriteSspSectionTool CreateWriteSspSectionTool() =>
        new(_sspMock.Object, Mock.Of<ILogger<WriteSspSectionTool>>());

    private ReviewSspSectionTool CreateReviewSspSectionTool() =>
        new(_sspMock.Object, Mock.Of<ILogger<ReviewSspSectionTool>>());

    private SspCompletenessTool CreateSspCompletenessTool() =>
        new(_sspMock.Object, Mock.Of<INarrativeGovernanceService>(), Mock.Of<ILogger<SspCompletenessTool>>());

    // ────────────────────────────────────────────────────────────────────────
    // Helpers
    // ────────────────────────────────────────────────────────────────────────

    private static SspSection CreateSection(int number, string title, SspSectionStatus status, int version) => new()
    {
        Id = Guid.NewGuid().ToString(),
        RegisteredSystemId = "sys-1",
        SectionNumber = number,
        SectionTitle = title,
        Content = $"Content for section {number}",
        Status = status,
        IsAutoGenerated = number is 1 or 2 or 3 or 4 or 7 or 9 or 10 or 11,
        HasManualOverride = false,
        AuthoredBy = "author@test.com",
        AuthoredAt = DateTime.UtcNow,
        Version = version
    };

    private static SspCompletenessReport CreateCompletenessReport() => new(
        SystemName: "Test System",
        OverallReadinessPercent: 8,
        ApprovedCount: 1,
        TotalSections: 13,
        Sections: new List<SspSectionSummary>
        {
            new(
                SectionNumber: 5,
                SectionTitle: "Rules of Behavior",
                Status: "Approved",
                IsAutoGenerated: false,
                HasManualOverride: false,
                AuthoredBy: "author@test.com",
                AuthoredAt: DateTime.UtcNow,
                WordCount: 42,
                Version: 1)
        },
        BlockingIssues: new List<string> { "§1 System Name/Title: Not started" }
    );
}
