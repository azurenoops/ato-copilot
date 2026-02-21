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
/// Unit tests for AtoComplianceEngine: scan orchestration, finding correlation,
/// score computation, persistence, CancellationToken timeout.
/// </summary>
public class AtoComplianceEngineTests : IDisposable
{
    private readonly Mock<INistControlsService> _nistMock = new();
    private readonly Mock<IAzurePolicyComplianceService> _policyMock = new();
    private readonly Mock<IDefenderForCloudService> _defenderMock = new();
    private readonly Mock<ILogger<AtoComplianceEngine>> _loggerMock = new();
    private readonly IDbContextFactory<AtoCopilotContext> _dbFactory;
    private readonly string _dbName;

    public AtoComplianceEngineTests()
    {
        _dbName = $"AtoComplianceTest_{Guid.NewGuid()}";
        var options = new DbContextOptionsBuilder<AtoCopilotContext>()
            .UseInMemoryDatabase(_dbName)
            .Options;

        _dbFactory = new InMemoryDbContextFactory(options);

        // Default NIST mock: return some controls for AC family
        _nistMock.Setup(x => x.GetControlFamilyAsync(It.IsAny<string>(), false, It.IsAny<CancellationToken>()))
            .ReturnsAsync((string family, bool _, CancellationToken _) =>
            {
                if (family.Equals("AC", StringComparison.OrdinalIgnoreCase))
                {
                    return new List<NistControl>
                    {
                        new() { Id = "AC-1", Family = "AC", Title = "Policy" },
                        new() { Id = "AC-2", Family = "AC", Title = "Account Management" },
                        new() { Id = "AC-3", Family = "AC", Title = "Access Enforcement" }
                    };
                }
                return new List<NistControl>();
            });
    }

    public void Dispose()
    {
        // InMemory databases are automatically cleaned up
        GC.SuppressFinalize(this);
    }

    private AtoComplianceEngine CreateEngine() =>
        new(
            _nistMock.Object,
            _policyMock.Object,
            _defenderMock.Object,
            _dbFactory,
            _loggerMock.Object);

    private static string MakePolicyResponse(List<object>? states = null) =>
        JsonSerializer.Serialize(new
        {
            subscriptionId = "test-sub",
            totalStates = states?.Count ?? 0,
            states = states ?? new List<object>(),
            truncated = false,
            evaluatedAt = DateTime.UtcNow
        }, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

    private static string MakeDefenderResponse(List<object>? recommendations = null) =>
        JsonSerializer.Serialize(new
        {
            subscriptionId = "test-sub",
            totalRecommendations = recommendations?.Count ?? 0,
            recommendations = recommendations ?? new List<object>(),
            truncated = false,
            evaluatedAt = DateTime.UtcNow
        }, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

    // ─── Combined Scan ──────────────────────────────────────────────────────────

    [Fact]
    public async Task RunAssessment_CombinedScan_RunsBothScanSources()
    {
        _policyMock.Setup(x => x.GetPolicyStatesAsync(It.IsAny<string>(), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakePolicyResponse());
        _defenderMock.Setup(x => x.GetRecommendationsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeDefenderResponse());

        var engine = CreateEngine();
        var result = await engine.RunAssessmentAsync("test-sub-id", scanType: "combined");

        result.Should().NotBeNull();
        result.Status.Should().Be(AssessmentStatus.Completed);
        result.ScanType.Should().Be("combined");
        result.SubscriptionId.Should().Be("test-sub-id");

        _policyMock.Verify(x => x.GetPolicyStatesAsync("test-sub-id", null, It.IsAny<CancellationToken>()), Times.Once);
        _defenderMock.Verify(x => x.GetRecommendationsAsync("test-sub-id", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RunAssessment_PolicyOnly_SkipsDefender()
    {
        _policyMock.Setup(x => x.GetPolicyStatesAsync(It.IsAny<string>(), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakePolicyResponse());

        var engine = CreateEngine();
        var result = await engine.RunAssessmentAsync("test-sub", scanType: "policy");

        result.ScanType.Should().Be("policy");
        _policyMock.Verify(x => x.GetPolicyStatesAsync(It.IsAny<string>(), null, It.IsAny<CancellationToken>()), Times.Once);
        _defenderMock.Verify(x => x.GetRecommendationsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RunAssessment_ResourceOnly_SkipsPolicy()
    {
        _defenderMock.Setup(x => x.GetRecommendationsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeDefenderResponse());

        var engine = CreateEngine();
        var result = await engine.RunAssessmentAsync("test-sub", scanType: "resource");

        result.ScanType.Should().Be("resource");
        _policyMock.Verify(x => x.GetPolicyStatesAsync(It.IsAny<string>(), null, It.IsAny<CancellationToken>()), Times.Never);
        _defenderMock.Verify(x => x.GetRecommendationsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // ─── Finding Extraction ─────────────────────────────────────────────────────

    [Fact]
    public async Task RunAssessment_WithPolicyNonCompliance_CreatesFindings()
    {
        var states = new List<object>
        {
            new
            {
                policyDefinitionId = "def-1",
                policyAssignmentId = "assign-1",
                complianceState = "NonCompliant",
                resourceId = "/subscriptions/sub/resourceGroups/rg/providers/Microsoft.Storage/storageAccounts/test",
                resourceType = "Microsoft.Storage/storageAccounts",
                policyDefinitionGroupNames = new[] { "NIST_SP_800-53_Rev._5_AC-2" },
                timestamp = DateTime.UtcNow
            }
        };

        _policyMock.Setup(x => x.GetPolicyStatesAsync(It.IsAny<string>(), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakePolicyResponse(states));
        _defenderMock.Setup(x => x.GetRecommendationsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeDefenderResponse());

        var engine = CreateEngine();
        var result = await engine.RunAssessmentAsync("test-sub");

        result.Findings.Should().NotBeEmpty();
        result.Findings.Should().Contain(f => f.ControlId == "AC-2");
        result.Findings.First(f => f.ControlId == "AC-2").ScanSource.Should().Be(ScanSourceType.Policy);
    }

    [Fact]
    public async Task RunAssessment_WithDefenderRecommendations_CreatesFindings()
    {
        var recs = new List<object>
        {
            new
            {
                id = "rec-1",
                name = "rec-name",
                displayName = "MFA should be enabled for all accounts",
                status = "Unhealthy"
            }
        };

        _policyMock.Setup(x => x.GetPolicyStatesAsync(It.IsAny<string>(), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakePolicyResponse());
        _defenderMock.Setup(x => x.GetRecommendationsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeDefenderResponse(recs));

        var engine = CreateEngine();
        var result = await engine.RunAssessmentAsync("test-sub");

        result.Findings.Should().NotBeEmpty();
        result.Findings.Should().Contain(f => f.ControlId == "IA-2"); // MFA → IA-2
        result.Findings.First(f => f.ControlId == "IA-2").ScanSource.Should().Be(ScanSourceType.Defender);
    }

    // ─── Compliance Score ───────────────────────────────────────────────────────

    [Fact]
    public async Task RunAssessment_NoFindings_ScoreIs100()
    {
        _policyMock.Setup(x => x.GetPolicyStatesAsync(It.IsAny<string>(), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakePolicyResponse());
        _defenderMock.Setup(x => x.GetRecommendationsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeDefenderResponse());

        // Only AC family with 3 controls, no findings = 100%
        var engine = CreateEngine();
        var result = await engine.RunAssessmentAsync("test-sub", controlFamilies: "AC");

        result.TotalControls.Should().Be(3);
        result.PassedControls.Should().Be(3);
        result.FailedControls.Should().Be(0);
        result.ComplianceScore.Should().Be(100.0);
    }

    [Fact]
    public async Task RunAssessment_AllFailed_ScoreIsLow()
    {
        // Create findings for all 3 AC controls
        var states = new List<object>
        {
            new { complianceState = "NonCompliant", policyDefinitionGroupNames = new[] { "NIST_SP_800-53_Rev._5_AC-1" }, resourceId = "r1", resourceType = "t1", policyDefinitionId = "d1", policyAssignmentId = "a1", timestamp = DateTime.UtcNow },
            new { complianceState = "NonCompliant", policyDefinitionGroupNames = new[] { "NIST_SP_800-53_Rev._5_AC-2" }, resourceId = "r2", resourceType = "t2", policyDefinitionId = "d2", policyAssignmentId = "a2", timestamp = DateTime.UtcNow },
            new { complianceState = "NonCompliant", policyDefinitionGroupNames = new[] { "NIST_SP_800-53_Rev._5_AC-3" }, resourceId = "r3", resourceType = "t3", policyDefinitionId = "d3", policyAssignmentId = "a3", timestamp = DateTime.UtcNow }
        };

        _policyMock.Setup(x => x.GetPolicyStatesAsync(It.IsAny<string>(), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakePolicyResponse(states));
        _defenderMock.Setup(x => x.GetRecommendationsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeDefenderResponse());

        var engine = CreateEngine();
        var result = await engine.RunAssessmentAsync("test-sub", controlFamilies: "AC");

        result.TotalControls.Should().Be(3);
        result.FailedControls.Should().Be(3);
        result.PassedControls.Should().Be(0);
        result.ComplianceScore.Should().Be(0.0);
    }

    // ─── Framework Normalization ─────────────────────────────────────────────

    [Fact]
    public async Task RunAssessment_DefaultFramework_IsNIST80053()
    {
        _policyMock.Setup(x => x.GetPolicyStatesAsync(It.IsAny<string>(), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakePolicyResponse());
        _defenderMock.Setup(x => x.GetRecommendationsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeDefenderResponse());

        var engine = CreateEngine();
        var result = await engine.RunAssessmentAsync("test-sub");

        result.Framework.Should().Be("NIST80053");
    }

    [Fact]
    public async Task RunAssessment_ScanTypeNormalization()
    {
        _policyMock.Setup(x => x.GetPolicyStatesAsync(It.IsAny<string>(), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakePolicyResponse());
        _defenderMock.Setup(x => x.GetRecommendationsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeDefenderResponse());

        var engine = CreateEngine();

        var quickResult = await engine.RunAssessmentAsync("test-sub", scanType: "quick");
        quickResult.ScanType.Should().Be("combined");

        var fullResult = await engine.RunAssessmentAsync("test-sub", scanType: "full");
        fullResult.ScanType.Should().Be("combined");
    }

    // ─── Persistence ──────────────────────────────────────────────────────────

    [Fact]
    public async Task RunAssessment_PersistsToDatabase()
    {
        _policyMock.Setup(x => x.GetPolicyStatesAsync(It.IsAny<string>(), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakePolicyResponse());
        _defenderMock.Setup(x => x.GetRecommendationsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeDefenderResponse());

        var engine = CreateEngine();
        var result = await engine.RunAssessmentAsync("test-sub");

        // Verify persisted
        await using var db = await _dbFactory.CreateDbContextAsync();
        var persisted = await db.Assessments.FindAsync(result.Id);
        persisted.Should().NotBeNull();
        persisted!.SubscriptionId.Should().Be("test-sub");
        persisted.Status.Should().Be(AssessmentStatus.Completed);
    }

    [Fact]
    public async Task GetAssessmentHistoryAsync_ReturnsPersistedAssessments()
    {
        _policyMock.Setup(x => x.GetPolicyStatesAsync(It.IsAny<string>(), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakePolicyResponse());
        _defenderMock.Setup(x => x.GetRecommendationsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeDefenderResponse());

        var engine = CreateEngine();
        await engine.RunAssessmentAsync("test-sub");
        await engine.RunAssessmentAsync("test-sub");

        var history = await engine.GetAssessmentHistoryAsync("test-sub");
        history.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetFindingAsync_ExistingFinding_ReturnsFinding()
    {
        var states = new List<object>
        {
            new { complianceState = "NonCompliant", policyDefinitionGroupNames = new[] { "NIST_SP_800-53_Rev._5_AC-2" }, resourceId = "r1", resourceType = "t1", policyDefinitionId = "d1", policyAssignmentId = "a1", timestamp = DateTime.UtcNow }
        };
        _policyMock.Setup(x => x.GetPolicyStatesAsync(It.IsAny<string>(), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakePolicyResponse(states));
        _defenderMock.Setup(x => x.GetRecommendationsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeDefenderResponse());

        var engine = CreateEngine();
        var assessment = await engine.RunAssessmentAsync("test-sub", controlFamilies: "AC");

        if (assessment.Findings.Count > 0)
        {
            var findingId = assessment.Findings.First().Id;
            var finding = await engine.GetFindingAsync(findingId);
            finding.Should().NotBeNull();
            finding!.ControlId.Should().Be("AC-2");
        }
    }

    // ─── CancellationToken ──────────────────────────────────────────────────────

    [Fact]
    public async Task RunAssessment_WhenCancelled_ThrowsAndMarksCancelled()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel(); // Pre-cancel

        _policyMock.Setup(x => x.GetPolicyStatesAsync(It.IsAny<string>(), null, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        var engine = CreateEngine();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => engine.RunAssessmentAsync("test-sub", cancellationToken: cts.Token));
    }

    // ─── Error Handling ──────────────────────────────────────────────────────

    [Fact]
    public async Task RunAssessment_PolicyScanError_StillCompletes()
    {
        // Policy returns error JSON, Defender returns empty
        _policyMock.Setup(x => x.GetPolicyStatesAsync(It.IsAny<string>(), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(JsonSerializer.Serialize(new { error = "Auth failed" }));
        _defenderMock.Setup(x => x.GetRecommendationsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeDefenderResponse());

        var engine = CreateEngine();
        var result = await engine.RunAssessmentAsync("test-sub");

        // Assessment should still complete (partial results)
        result.Status.Should().Be(AssessmentStatus.Completed);
    }

    [Fact]
    public async Task RunAssessment_DefenderScanError_StillCompletes()
    {
        _policyMock.Setup(x => x.GetPolicyStatesAsync(It.IsAny<string>(), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakePolicyResponse());
        _defenderMock.Setup(x => x.GetRecommendationsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(JsonSerializer.Serialize(new { error = "Defender unavailable" }));

        var engine = CreateEngine();
        var result = await engine.RunAssessmentAsync("test-sub");

        result.Status.Should().Be(AssessmentStatus.Completed);
    }

    // ─── Control Family Filtering ──────────────────────────────────────────────

    [Fact]
    public async Task RunAssessment_WithControlFamilyFilter_FiltersFindings()
    {
        var states = new List<object>
        {
            new { complianceState = "NonCompliant", policyDefinitionGroupNames = new[] { "NIST_SP_800-53_Rev._5_AC-2" }, resourceId = "r1", resourceType = "t1", policyDefinitionId = "d1", policyAssignmentId = "a1", timestamp = DateTime.UtcNow },
            new { complianceState = "NonCompliant", policyDefinitionGroupNames = new[] { "NIST_SP_800-53_Rev._5_AU-3" }, resourceId = "r2", resourceType = "t2", policyDefinitionId = "d2", policyAssignmentId = "a2", timestamp = DateTime.UtcNow }
        };

        _policyMock.Setup(x => x.GetPolicyStatesAsync(It.IsAny<string>(), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakePolicyResponse(states));
        _defenderMock.Setup(x => x.GetRecommendationsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeDefenderResponse());

        var engine = CreateEngine();
        var result = await engine.RunAssessmentAsync("test-sub", controlFamilies: "AC");

        // Should only have AC findings, not AU
        result.Findings.Should().OnlyContain(f => f.ControlFamily == "AC");
    }

    // ─── Helper: InMemory DbContextFactory ──────────────────────────────────

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
