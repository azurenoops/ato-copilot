using Xunit;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Ato.Copilot.Agents.Compliance.Scanners;
using Ato.Copilot.Core.Constants;
using Ato.Copilot.Core.Interfaces.Compliance;
using Ato.Copilot.Core.Models.Compliance;

namespace Ato.Copilot.Tests.Unit.Scanners;

public class CertAccreditationScannerTests
{
    private readonly Mock<IDefenderForCloudService> _defenderMock = new();
    private readonly Mock<IAzurePolicyComplianceService> _policyMock = new();
    private readonly Mock<ILogger<CertAccreditationScanner>> _loggerMock = new();

    private CertAccreditationScanner CreateScanner() =>
        new(_defenderMock.Object, _policyMock.Object, _loggerMock.Object);

    private static List<NistControl> CreateControls() => new()
    {
        new NistControl { Id = "ca-2", Family = "CA", Title = "Control Assessments" },
        new NistControl { Id = "ca-5", Family = "CA", Title = "Plan of Action" },
        new NistControl { Id = "ca-7", Family = "CA", Title = "Continuous Monitoring" }
    };

    [Fact]
    public void FamilyCode_ShouldBeCA() => CreateScanner().FamilyCode.Should().Be("CA");

    [Fact]
    public async Task ScanAsync_UnhealthyAssessments_ProducesFinding()
    {
        _defenderMock.Setup(x => x.GetAssessmentsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("Unhealthy");
        _defenderMock.Setup(x => x.GetRecommendationsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("No recommendations");
        _policyMock.Setup(x => x.GetComplianceSummaryAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("Compliant");

        var result = await CreateScanner().ScanAsync("sub-1", null, CreateControls());

        result.Findings.Should().Contain(f => f.ControlId == "CA-2" && f.Severity == FindingSeverity.High);
    }

    [Fact]
    public async Task ScanAsync_SecurityRecommendations_ProducesFinding()
    {
        _defenderMock.Setup(x => x.GetAssessmentsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("Healthy");
        _defenderMock.Setup(x => x.GetRecommendationsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("Security assessment recommended");
        _policyMock.Setup(x => x.GetComplianceSummaryAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("Compliant");

        var result = await CreateScanner().ScanAsync("sub-1", null, CreateControls());

        result.Findings.Should().Contain(f => f.ControlId == "CA-7");
    }

    [Fact]
    public async Task ScanAsync_NonCompliantPolicies_ProducesFinding()
    {
        _defenderMock.Setup(x => x.GetAssessmentsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("Healthy");
        _defenderMock.Setup(x => x.GetRecommendationsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("No recommendations");
        _policyMock.Setup(x => x.GetComplianceSummaryAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("NonCompliant: policy violations");

        var result = await CreateScanner().ScanAsync("sub-1", null, CreateControls());

        result.Findings.Should().Contain(f => f.ControlId == "CA-5");
    }

    [Fact]
    public async Task ScanAsync_AllCompliant_NoFindings()
    {
        _defenderMock.Setup(x => x.GetAssessmentsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("Healthy");
        _defenderMock.Setup(x => x.GetRecommendationsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("No recommendations");
        _policyMock.Setup(x => x.GetComplianceSummaryAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("Compliant");

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
