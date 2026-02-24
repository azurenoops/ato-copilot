using Xunit;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Ato.Copilot.Agents.Compliance.Scanners;
using Ato.Copilot.Core.Constants;
using Ato.Copilot.Core.Interfaces.Compliance;
using Ato.Copilot.Core.Models.Compliance;

namespace Ato.Copilot.Tests.Unit.Scanners;

public class RiskAssessmentScannerTests
{
    private readonly Mock<IDefenderForCloudService> _defenderMock = new();
    private readonly Mock<IAzurePolicyComplianceService> _policyMock = new();
    private readonly Mock<ILogger<RiskAssessmentScanner>> _loggerMock = new();

    private RiskAssessmentScanner CreateScanner() =>
        new(_defenderMock.Object, _policyMock.Object, _loggerMock.Object);

    private static List<NistControl> CreateControls() => new()
    {
        new NistControl { Id = "ra-3", Family = "RA", Title = "Risk Assessment" },
        new NistControl { Id = "ra-5", Family = "RA", Title = "Vulnerability Monitoring" }
    };

    [Fact]
    public void FamilyCode_ShouldBeRA() => CreateScanner().FamilyCode.Should().Be("RA");

    [Fact]
    public async Task ScanAsync_UnhealthyAssessments_ProducesFinding()
    {
        _defenderMock.Setup(x => x.GetAssessmentsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("Unhealthy");
        _defenderMock.Setup(x => x.GetSecureScoreAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("{\"score\": 85}");
        _defenderMock.Setup(x => x.GetRecommendationsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("No recommendations");

        var result = await CreateScanner().ScanAsync("sub-1", null, CreateControls());

        result.Findings.Should().Contain(f => f.ControlId == "RA-5" && f.Severity == FindingSeverity.High);
    }

    [Fact]
    public async Task ScanAsync_LowSecureScore_ProdusesFinding()
    {
        _defenderMock.Setup(x => x.GetAssessmentsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("Healthy");
        _defenderMock.Setup(x => x.GetSecureScoreAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("{\"score\": 45}");
        _defenderMock.Setup(x => x.GetRecommendationsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("No recommendations");

        var result = await CreateScanner().ScanAsync("sub-1", null, CreateControls());

        result.Findings.Should().Contain(f => f.ControlId == "RA-3" && f.Severity == FindingSeverity.Critical);
    }

    [Fact]
    public async Task ScanAsync_VulnerabilityRecommendations_ProducesFinding()
    {
        _defenderMock.Setup(x => x.GetAssessmentsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("Healthy");
        _defenderMock.Setup(x => x.GetSecureScoreAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("{\"score\": 85}");
        _defenderMock.Setup(x => x.GetRecommendationsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("vulnerability scan recommended");

        var result = await CreateScanner().ScanAsync("sub-1", null, CreateControls());

        result.Findings.Should().Contain(f => f.ControlId == "RA-5" && f.Severity == FindingSeverity.Medium);
    }

    [Fact]
    public async Task ScanAsync_AllHealthy_NoFindings()
    {
        _defenderMock.Setup(x => x.GetAssessmentsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("Healthy");
        _defenderMock.Setup(x => x.GetSecureScoreAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("{\"score\": 90}");
        _defenderMock.Setup(x => x.GetRecommendationsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("All clear");

        var result = await CreateScanner().ScanAsync("sub-1", null, CreateControls());

        result.Findings.Should().BeEmpty();
        result.ComplianceScore.Should().Be(100.0);
    }

    [Fact]
    public async Task ScanAsync_Cancelled_Skipped()
    {
        _defenderMock.Setup(x => x.GetAssessmentsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        var result = await CreateScanner().ScanAsync("sub-1", null, CreateControls());
        result.Status.Should().Be(FamilyAssessmentStatus.Skipped);
    }
}
