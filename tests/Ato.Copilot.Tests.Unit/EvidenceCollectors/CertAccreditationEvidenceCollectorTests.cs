using Xunit;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Ato.Copilot.Agents.Compliance.EvidenceCollectors;
using Ato.Copilot.Core.Interfaces.Compliance;
using Ato.Copilot.Core.Models.Compliance;

namespace Ato.Copilot.Tests.Unit.EvidenceCollectors;

public class CertAccreditationEvidenceCollectorTests
{
    private readonly Mock<IDefenderForCloudService> _defenderMock = new();
    private readonly Mock<IAzurePolicyComplianceService> _policyMock = new();
    private readonly Mock<ILogger<CertAccreditationEvidenceCollector>> _loggerMock = new();

    private CertAccreditationEvidenceCollector CreateCollector() =>
        new(_defenderMock.Object, _policyMock.Object, _loggerMock.Object);

    [Fact]
    public void FamilyCode_ShouldBeCA()
    {
        CreateCollector().FamilyCode.Should().Be("CA");
    }

    [Fact]
    public async Task CollectAsync_AllServicesSucceed_Returns5EvidenceTypes()
    {
        SetupAllServices();

        var result = await CreateCollector().CollectAsync("sub-1", null);

        result.CollectedEvidenceTypes.Should().Be(5);
        result.CompletenessScore.Should().Be(100.0);
    }

    [Fact]
    public async Task CollectAsync_ContainsRegulatoryComplianceEvidence()
    {
        SetupAllServices();

        var result = await CreateCollector().CollectAsync("sub-1", null);

        result.EvidenceItems.Should().Contain(e =>
            e.Type == EvidenceType.Configuration && e.Title.Contains("Regulatory"));
    }

    [Fact]
    public async Task CollectAsync_ContainsRecommendationEvidence()
    {
        SetupAllServices();

        var result = await CreateCollector().CollectAsync("sub-1", null);

        result.EvidenceItems.Should().Contain(e =>
            e.Type == EvidenceType.Log && e.Title.Contains("Security Recommendations"));
    }

    [Fact]
    public async Task CollectAsync_Cancelled_Throws()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            CreateCollector().CollectAsync("sub-1", null, cts.Token));
    }

    private void SetupAllServices()
    {
        _defenderMock.Setup(x => x.GetAssessmentsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("Healthy");
        _defenderMock.Setup(x => x.GetRecommendationsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("No recommendations");
        _defenderMock.Setup(x => x.GetSecureScoreAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("{\"score\": 85}");
        _policyMock.Setup(x => x.GetPolicyStatesAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("Compliant");
        _policyMock.Setup(x => x.GetComplianceSummaryAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("Overall compliant");
    }
}
