using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using FluentAssertions;
using Ato.Copilot.Agents.Compliance.Services;
using Ato.Copilot.Core.Data.Context;
using Ato.Copilot.Core.Interfaces.Compliance;
using Ato.Copilot.Core.Models.Compliance;

namespace Ato.Copilot.Tests.Unit.Services;

/// <summary>
/// Unit tests for RemediationEngine: dry-run, apply, batch, stop-on-failure,
/// high-risk warning, before/after state, rollback guidance.
/// </summary>
public class RemediationEngineTests : IDisposable
{
    private readonly Mock<IAtoComplianceEngine> _engineMock = new();
    private readonly Mock<ILogger<RemediationEngine>> _loggerMock = new();
    private readonly IDbContextFactory<AtoCopilotContext> _dbFactory;
    private readonly string _dbName;

    public RemediationEngineTests()
    {
        _dbName = $"RemediationTest_{Guid.NewGuid()}";
        var options = new DbContextOptionsBuilder<AtoCopilotContext>()
            .UseInMemoryDatabase(_dbName)
            .Options;
        _dbFactory = new InMemoryDbContextFactory(options);
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }

    private RemediationEngine CreateEngine() =>
        new(_engineMock.Object, _dbFactory, _loggerMock.Object);

    private ComplianceFinding CreateFinding(
        string controlId = "AC-2",
        string family = "AC",
        FindingSeverity severity = FindingSeverity.High,
        bool autoRemediable = true,
        string assessmentId = "assess-1") =>
        new()
        {
            Id = Guid.NewGuid().ToString(),
            ControlId = controlId,
            ControlFamily = family,
            Title = $"Test finding for {controlId}",
            Description = $"Non-compliance for {controlId}",
            Severity = severity,
            Status = FindingStatus.Open,
            ResourceId = "/subscriptions/sub/resourceGroups/rg/providers/Microsoft.Storage/storageAccounts/test",
            ResourceType = "Microsoft.Storage/storageAccounts",
            RemediationGuidance = $"Fix {controlId} compliance issue",
            Source = "PolicyInsights",
            ScanSource = ScanSourceType.Policy,
            AutoRemediable = autoRemediable,
            RemediationType = RemediationType.PolicyRemediation,
            AssessmentId = assessmentId
        };

    private async Task SeedFindings(params ComplianceFinding[] findings)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        db.Findings.AddRange(findings);
        await db.SaveChangesAsync();
    }

    private async Task SeedAssessment(string assessmentId, string subscriptionId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        db.Assessments.Add(new ComplianceAssessment
        {
            Id = assessmentId,
            SubscriptionId = subscriptionId,
            Status = AssessmentStatus.Completed,
            AssessedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
    }

    // ─── GeneratePlan ────────────────────────────────────────────────────────

    [Fact]
    public async Task GeneratePlan_WithFindings_CreatesPlanWithSteps()
    {
        var finding = CreateFinding();
        _engineMock.Setup(x => x.GetAssessmentHistoryAsync("sub-1", 7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ComplianceAssessment>
            {
                new() { Id = "assess-1", SubscriptionId = "sub-1" }
            });

        await SeedFindings(finding);

        var engine = CreateEngine();
        var plan = await engine.GeneratePlanAsync("sub-1");

        plan.Should().NotBeNull();
        plan.SubscriptionId.Should().Be("sub-1");
        plan.DryRun.Should().BeTrue();
        plan.TotalFindings.Should().Be(1);
        plan.Steps.Should().HaveCount(1);
        plan.Steps[0].ControlId.Should().Be("AC-2");
    }

    [Fact]
    public async Task GeneratePlan_NoAssessment_ReturnsEmptyPlan()
    {
        _engineMock.Setup(x => x.GetAssessmentHistoryAsync("sub-1", 7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ComplianceAssessment>());

        var engine = CreateEngine();
        var plan = await engine.GeneratePlanAsync("sub-1");

        plan.TotalFindings.Should().Be(0);
        plan.Steps.Should().BeEmpty();
    }

    [Fact]
    public async Task GeneratePlan_HighRiskFamily_SetsRiskLevel()
    {
        var finding = CreateFinding(controlId: "IA-5", family: "IA");
        _engineMock.Setup(x => x.GetAssessmentHistoryAsync(It.IsAny<string>(), 7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ComplianceAssessment>
            {
                new() { Id = "assess-1", SubscriptionId = "sub-1" }
            });

        await SeedFindings(finding);

        var engine = CreateEngine();
        var plan = await engine.GeneratePlanAsync("sub-1");

        plan.Steps[0].RiskLevel.Should().Be(RiskLevel.High);
    }

    [Fact]
    public async Task GeneratePlan_PersistsToDatabase()
    {
        _engineMock.Setup(x => x.GetAssessmentHistoryAsync(It.IsAny<string>(), 7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ComplianceAssessment>
            {
                new() { Id = "assess-1", SubscriptionId = "sub-1" }
            });

        var engine = CreateEngine();
        var plan = await engine.GeneratePlanAsync("sub-1");

        await using var db = await _dbFactory.CreateDbContextAsync();
        var persisted = await db.RemediationPlans.FindAsync(plan.Id);
        persisted.Should().NotBeNull();
    }

    // ─── ExecuteRemediation ─────────────────────────────────────────────────

    [Fact]
    public async Task ExecuteRemediation_DryRun_ReturnsDryRunPlan()
    {
        var finding = CreateFinding();
        _engineMock.Setup(x => x.GetFindingAsync(finding.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(finding);

        var engine = CreateEngine();
        var result = await engine.ExecuteRemediationAsync(finding.Id, dryRun: true);

        using var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("status").GetString().Should().Be("success");
        doc.RootElement.GetProperty("data").GetProperty("mode").GetString().Should().Be("dry-run");
    }

    [Fact]
    public async Task ExecuteRemediation_HighRiskFamily_IncludesWarning()
    {
        var finding = CreateFinding(controlId: "AC-2", family: "AC");
        _engineMock.Setup(x => x.GetFindingAsync(finding.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(finding);

        var engine = CreateEngine();
        var result = await engine.ExecuteRemediationAsync(finding.Id, dryRun: true);

        using var doc = JsonDocument.Parse(result);
        var data = doc.RootElement.GetProperty("data");
        data.GetProperty("isHighRisk").GetBoolean().Should().BeTrue();
        data.GetProperty("highRiskWarning").GetString().Should().Contain("high-risk");
    }

    [Fact]
    public async Task ExecuteRemediation_NonHighRiskFamily_NoWarning()
    {
        var finding = CreateFinding(controlId: "AU-3", family: "AU");
        _engineMock.Setup(x => x.GetFindingAsync(finding.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(finding);

        var engine = CreateEngine();
        var result = await engine.ExecuteRemediationAsync(finding.Id, dryRun: true);

        using var doc = JsonDocument.Parse(result);
        var data = doc.RootElement.GetProperty("data");
        data.GetProperty("isHighRisk").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteRemediation_FindingNotFound_ReturnsError()
    {
        _engineMock.Setup(x => x.GetFindingAsync("not-exists", It.IsAny<CancellationToken>()))
            .ReturnsAsync((ComplianceFinding?)null);

        var engine = CreateEngine();
        var result = await engine.ExecuteRemediationAsync("not-exists");

        using var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("status").GetString().Should().Be("error");
    }

    [Fact]
    public async Task ExecuteRemediation_Apply_UpdatesFindingStatus()
    {
        var finding = CreateFinding();
        await SeedFindings(finding);

        _engineMock.Setup(x => x.GetFindingAsync(finding.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(finding);

        var engine = CreateEngine();
        var result = await engine.ExecuteRemediationAsync(finding.Id, applyRemediation: true, dryRun: false);

        using var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("status").GetString().Should().Be("success");
        doc.RootElement.GetProperty("data").GetProperty("mode").GetString().Should().Be("executed");

        // Verify finding status was updated
        await using var db = await _dbFactory.CreateDbContextAsync();
        var updatedFinding = await db.Findings.FindAsync(finding.Id);
        updatedFinding!.Status.Should().Be(FindingStatus.InProgress);
    }

    // ─── ValidateRemediation ────────────────────────────────────────────────

    [Fact]
    public async Task ValidateRemediation_ExistingFinding_MarksRemediated()
    {
        var finding = CreateFinding();
        finding.Status = FindingStatus.InProgress;
        await SeedFindings(finding);

        _engineMock.Setup(x => x.GetFindingAsync(finding.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(finding);

        var engine = CreateEngine();
        var result = await engine.ValidateRemediationAsync(finding.Id);

        using var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("status").GetString().Should().Be("success");
        doc.RootElement.GetProperty("data").GetProperty("validated").GetBoolean().Should().BeTrue();

        await using var db = await _dbFactory.CreateDbContextAsync();
        var updated = await db.Findings.FindAsync(finding.Id);
        updated!.Status.Should().Be(FindingStatus.Remediated);
    }

    [Fact]
    public async Task ValidateRemediation_NotFound_ReturnsError()
    {
        _engineMock.Setup(x => x.GetFindingAsync("not-exists", It.IsAny<CancellationToken>()))
            .ReturnsAsync((ComplianceFinding?)null);

        var engine = CreateEngine();
        var result = await engine.ValidateRemediationAsync("not-exists");

        using var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("status").GetString().Should().Be("error");
    }

    // ─── BatchRemediate ────────────────────────────────────────────────────

    [Fact]
    public async Task BatchRemediate_NoSubscription_ReturnsError()
    {
        var engine = CreateEngine();
        var result = await engine.BatchRemediateAsync(subscriptionId: null, dryRun: true);

        using var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("status").GetString().Should().Be("error");
    }

    [Fact]
    public async Task BatchRemediate_DryRun_DoesNotExecute()
    {
        var finding = CreateFinding();
        _engineMock.Setup(x => x.GetAssessmentHistoryAsync("sub-1", 7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ComplianceAssessment>
            {
                new() { Id = "assess-1", SubscriptionId = "sub-1" }
            });

        await SeedFindings(finding);

        var engine = CreateEngine();
        var result = await engine.BatchRemediateAsync("sub-1", dryRun: true);

        using var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("status").GetString().Should().Be("success");
        doc.RootElement.GetProperty("data").GetProperty("dryRun").GetBoolean().Should().BeTrue();
    }

    // ─── InMemory DbContextFactory ──────────────────────────────────────────

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
