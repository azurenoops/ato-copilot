using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Ato.Copilot.Agents.Compliance.Services;
using Ato.Copilot.Core.Data.Context;
using Ato.Copilot.Core.Interfaces.Compliance;
using Ato.Copilot.Core.Models.Compliance;

namespace Ato.Copilot.Tests.Unit.Services;

public class ComplianceMonitoringServiceTests : IDisposable
{
    private readonly Mock<IAtoComplianceEngine> _engine;
    private readonly IDbContextFactory<AtoCopilotContext> _dbFactory;
    private readonly ComplianceMonitoringService _sut;
    private readonly DbContextOptions<AtoCopilotContext> _dbOptions;

    public ComplianceMonitoringServiceTests()
    {
        _engine = new Mock<IAtoComplianceEngine>();
        var dbName = $"MonitoringTests_{Guid.NewGuid()}";
        _dbOptions = new DbContextOptionsBuilder<AtoCopilotContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        _dbFactory = new InMemoryDbContextFactory(_dbOptions);
        var logger = Mock.Of<ILogger<ComplianceMonitoringService>>();

        _sut = new ComplianceMonitoringService(_dbFactory, _engine.Object, logger);
    }

    public void Dispose()
    {
        using var db = new AtoCopilotContext(_dbOptions);
        db.Database.EnsureDeleted();
    }

    // ─── GetStatusAsync (IComplianceMonitoringService) ────────────────────

    [Fact]
    public async Task GetStatus_NoAssessments_ReturnsNoData()
    {
        var result = await _sut.GetStatusAsync();
        var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("status").GetString().Should().Be("no_data");
    }

    [Fact]
    public async Task GetStatus_WithAssessment_ReturnsScoreAndStatus()
    {
        await SeedAssessment("sub-1", 92.0, 3);

        var result = await _sut.GetStatusAsync("sub-1");
        var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("status").GetString().Should().Be("healthy");
        doc.RootElement.GetProperty("complianceScore").GetDouble().Should().Be(92.0);
    }

    [Fact]
    public async Task GetStatus_LowScore_ReturnsCritical()
    {
        await SeedAssessment("sub-1", 55.0, 0);

        var result = await _sut.GetStatusAsync("sub-1");
        var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("status").GetString().Should().Be("critical");
    }

    [Fact]
    public async Task GetStatus_MediumScore_ReturnsWarning()
    {
        await SeedAssessment("sub-1", 75.0, 0);

        var result = await _sut.GetStatusAsync("sub-1");
        var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("status").GetString().Should().Be("warning");
    }

    // ─── TriggerScanAsync ────────────────────────────────────────────────

    [Fact]
    public async Task TriggerScan_NoSubscription_ReturnsError()
    {
        var result = await _sut.TriggerScanAsync(null);
        var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("status").GetString().Should().Be("error");
    }

    [Fact]
    public async Task TriggerScan_EngineFailure_ReturnsError()
    {
        _engine
            .Setup(e => e.RunAssessmentAsync(
                It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Azure connection failed"));

        var result = await _sut.TriggerScanAsync("sub-1");
        var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("status").GetString().Should().Be("error");
        doc.RootElement.GetProperty("message").GetString().Should().Contain("Azure connection failed");
    }

    // ─── GetAlertsAsync ──────────────────────────────────────────────────

    [Fact]
    public async Task GetAlerts_ReturnsCriticalAndHighFindings()
    {
        await SeedAssessmentWithFindings("sub-1");

        var result = await _sut.GetAlertsAsync("sub-1", 30);
        var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("alertCount").GetInt32().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GetAlerts_NoFindings_ReturnsEmptyAlerts()
    {
        var result = await _sut.GetAlertsAsync("sub-1", 30);
        var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("alertCount").GetInt32().Should().Be(0);
    }

    // ─── GetTrendAsync ──────────────────────────────────────────────────

    [Fact]
    public async Task GetTrend_NoAssessments_ReturnsNoData()
    {
        var result = await _sut.GetTrendAsync();
        var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("status").GetString().Should().Be("no_data");
    }

    [Fact]
    public async Task GetTrend_MultipleAssessments_ShowsScoreDelta()
    {
        await SeedSequentialAssessments("sub-1", new[] { 70.0, 80.0, 90.0 });

        var result = await _sut.GetTrendAsync("sub-1", 60);
        var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("trend").GetString().Should().Be("improving");
        doc.RootElement.GetProperty("scoreDelta").GetDouble().Should().Be(20.0);
        doc.RootElement.GetProperty("currentScore").GetDouble().Should().Be(90.0);
    }

    [Fact]
    public async Task GetTrend_DecliningScore_ShowsDeclining()
    {
        await SeedSequentialAssessments("sub-1", new[] { 90.0, 80.0, 70.0 });

        var result = await _sut.GetTrendAsync("sub-1", 60);
        var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("trend").GetString().Should().Be("declining");
    }

    [Fact]
    public async Task GetTrend_StableScore_ShowsStable()
    {
        await SeedSequentialAssessments("sub-1", new[] { 80.0, 80.0 });

        var result = await _sut.GetTrendAsync("sub-1", 60);
        var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("trend").GetString().Should().Be("stable");
    }

    [Fact]
    public async Task GetTrend_DriftDetection_ShowsNewFailures()
    {
        await SeedSequentialAssessments("sub-1", new[] { 90.0, 80.0 }, new[] { 10, 20 });

        var result = await _sut.GetTrendAsync("sub-1", 60);
        var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("driftSummary").GetString()
            .Should().Contain("new failing controls");
    }

    [Fact]
    public async Task GetTrend_DriftDetection_ShowsRemediation()
    {
        await SeedSequentialAssessments("sub-1", new[] { 70.0, 90.0 }, new[] { 30, 10 });

        var result = await _sut.GetTrendAsync("sub-1", 60);
        var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("driftSummary").GetString()
            .Should().Contain("remediated");
    }

    // ─── GetHistoryAsync (IComplianceHistoryService) ─────────────────────

    [Fact]
    public async Task GetHistory_ReturnsAssessmentsInPeriod()
    {
        await SeedSequentialAssessments("sub-1", new[] { 70.0, 80.0, 90.0 });

        var historyService = (IComplianceHistoryService)_sut;
        var result = await historyService.GetHistoryAsync("sub-1", 60);
        var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("count").GetInt32().Should().Be(3);
    }

    [Fact]
    public async Task GetHistory_NoAssessments_ReturnsEmptyHistory()
    {
        var historyService = (IComplianceHistoryService)_sut;
        var result = await historyService.GetHistoryAsync("sub-1", 30);
        var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("count").GetInt32().Should().Be(0);
    }

    // ─── GetAuditLogAsync (IAssessmentAuditService) ─────────────────────

    [Fact]
    public async Task GetAuditLog_ReturnsEntriesInPeriod()
    {
        await SeedAuditEntries("sub-1", 5);

        var auditService = (IAssessmentAuditService)_sut;
        var result = await auditService.GetAuditLogAsync("sub-1", 7);
        var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("count").GetInt32().Should().Be(5);
    }

    [Fact]
    public async Task GetAuditLog_NoEntries_ReturnsEmpty()
    {
        var auditService = (IAssessmentAuditService)_sut;
        var result = await auditService.GetAuditLogAsync("sub-1", 7);
        var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("count").GetInt32().Should().Be(0);
    }

    // ─── GetStatusAsync (IComplianceStatusService) ──────────────────────

    [Fact]
    public async Task StatusService_NoAssessments_ReturnsUnknown()
    {
        var statusService = (IComplianceStatusService)_sut;
        var result = await statusService.GetStatusAsync();
        var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("status").GetString().Should().Be("unknown");
    }

    [Fact]
    public async Task StatusService_WithAssessment_ReturnsFamilyBreakdown()
    {
        await SeedAssessmentWithFindings("sub-1");

        var statusService = (IComplianceStatusService)_sut;
        var result = await statusService.GetStatusAsync("sub-1");
        var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("complianceScore").GetDouble().Should().BeGreaterThan(0);
        doc.RootElement.GetProperty("familyBreakdown").GetArrayLength().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task StatusService_HighScore_ReturnsCompliant()
    {
        await SeedAssessment("sub-1", 95.0, 0);

        var statusService = (IComplianceStatusService)_sut;
        var result = await statusService.GetStatusAsync("sub-1");
        var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("status").GetString().Should().Be("compliant");
    }

    [Fact]
    public async Task StatusService_LowScore_ReturnsNonCompliant()
    {
        await SeedAssessment("sub-1", 50.0, 0);

        var statusService = (IComplianceStatusService)_sut;
        var result = await statusService.GetStatusAsync("sub-1");
        var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("status").GetString().Should().Be("non_compliant");
    }

    // ─── Helpers ──────────────────────────────────────────────────────────

    private async Task SeedAssessment(string subId, double score, int findingsCount)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var assessment = new ComplianceAssessment
        {
            SubscriptionId = subId,
            ComplianceScore = score,
            TotalControls = 100,
            PassedControls = (int)score,
            FailedControls = 100 - (int)score,
            Status = AssessmentStatus.Completed,
            CompletedAt = DateTime.UtcNow
        };
        db.Assessments.Add(assessment);

        for (int i = 0; i < findingsCount; i++)
        {
            db.Findings.Add(new ComplianceFinding
            {
                AssessmentId = assessment.Id,
                ControlId = $"AC-{i + 1}",
                ControlFamily = "AC",
                Title = $"Finding {i + 1}",
                Severity = FindingSeverity.High,
                Status = FindingStatus.Open,
                ResourceId = $"/subscriptions/{subId}/rg/res{i}"
            });
        }

        await db.SaveChangesAsync();
    }

    private async Task SeedAssessmentWithFindings(string subId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var assessment = new ComplianceAssessment
        {
            SubscriptionId = subId,
            ComplianceScore = 75.0,
            TotalControls = 100,
            PassedControls = 75,
            FailedControls = 25,
            Status = AssessmentStatus.Completed,
            CompletedAt = DateTime.UtcNow
        };
        db.Assessments.Add(assessment);

        db.Findings.AddRange(
            new ComplianceFinding
            {
                AssessmentId = assessment.Id, ControlId = "AC-2", ControlFamily = "AC",
                Title = "Critical MFA issue", Severity = FindingSeverity.Critical,
                Status = FindingStatus.Open, ResourceId = $"/subscriptions/{subId}/rg/vm1"
            },
            new ComplianceFinding
            {
                AssessmentId = assessment.Id, ControlId = "SC-7", ControlFamily = "SC",
                Title = "NSG issue", Severity = FindingSeverity.High,
                Status = FindingStatus.Open, ResourceId = $"/subscriptions/{subId}/rg/nsg1"
            },
            new ComplianceFinding
            {
                AssessmentId = assessment.Id, ControlId = "AU-3", ControlFamily = "AU",
                Title = "Audit gap", Severity = FindingSeverity.Medium,
                Status = FindingStatus.Open, ResourceId = $"/subscriptions/{subId}/rg/stor1"
            }
        );
        await db.SaveChangesAsync();
    }

    private async Task SeedSequentialAssessments(string subId, double[] scores, int[]? failedControls = null)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        for (int i = 0; i < scores.Length; i++)
        {
            var failed = failedControls != null ? failedControls[i] : 100 - (int)scores[i];
            db.Assessments.Add(new ComplianceAssessment
            {
                SubscriptionId = subId,
                ComplianceScore = scores[i],
                TotalControls = 100,
                PassedControls = 100 - failed,
                FailedControls = failed,
                Status = AssessmentStatus.Completed,
                AssessedAt = DateTime.UtcNow.AddDays(-(scores.Length - i)),
                CompletedAt = DateTime.UtcNow.AddDays(-(scores.Length - i))
            });
        }
        await db.SaveChangesAsync();
    }

    private async Task SeedAuditEntries(string subId, int count)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        for (int i = 0; i < count; i++)
        {
            db.AuditLogs.Add(new AuditLogEntry
            {
                UserId = "user@example.com",
                UserRole = "Administrator",
                Action = "Assessment",
                SubscriptionId = subId,
                Outcome = AuditOutcome.Success,
                Details = $"Test audit entry {i + 1}"
            });
        }
        await db.SaveChangesAsync();
    }

    private class InMemoryDbContextFactory : IDbContextFactory<AtoCopilotContext>
    {
        private readonly DbContextOptions<AtoCopilotContext> _options;

        public InMemoryDbContextFactory(DbContextOptions<AtoCopilotContext> options)
        {
            _options = options;
        }

        public AtoCopilotContext CreateDbContext() => new(_options);

        public Task<AtoCopilotContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(CreateDbContext());
    }
}
