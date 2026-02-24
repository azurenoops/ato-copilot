using Xunit;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Ato.Copilot.Agents.Compliance.EvidenceCollectors;
using Ato.Copilot.Core.Interfaces.Compliance;
using Ato.Copilot.Core.Models.Compliance;

namespace Ato.Copilot.Tests.Unit.EvidenceCollectors;

public class DefaultEvidenceCollectorTests
{
    private readonly Mock<IAzurePolicyComplianceService> _policyMock = new();
    private readonly Mock<IDefenderForCloudService> _defenderMock = new();
    private readonly Mock<ILogger<DefaultEvidenceCollector>> _loggerMock = new();

    private DefaultEvidenceCollector CreateCollector() =>
        new(_policyMock.Object, _defenderMock.Object, _loggerMock.Object);

    [Fact]
    public void FamilyCode_ShouldBeDEFAULT()
    {
        CreateCollector().FamilyCode.Should().Be("DEFAULT");
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
    public async Task CollectAsync_ContainsPolicyEvidence()
    {
        SetupAllServices();

        var result = await CreateCollector().CollectAsync("sub-1", null);

        result.EvidenceItems.Should().Contain(e => e.Type == EvidenceType.Policy);
    }

    [Fact]
    public async Task CollectAsync_ContainsDefenderEvidence()
    {
        SetupAllServices();

        var result = await CreateCollector().CollectAsync("sub-1", null);

        result.EvidenceItems.Should().Contain(e => e.Type == EvidenceType.Log);
    }

    [Fact]
    public async Task CollectAsync_PartialFailure_StillCollectsAvailable()
    {
        _policyMock.Setup(x => x.GetPolicyStatesAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Service unavailable"));
        _defenderMock.Setup(x => x.GetAssessmentsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("Healthy");
        _defenderMock.Setup(x => x.GetSecureScoreAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("{\"score\": 85}");
        _defenderMock.Setup(x => x.GetRecommendationsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("No recs");
        _policyMock.Setup(x => x.GetComplianceSummaryAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("Summary");

        var result = await CreateCollector().CollectAsync("sub-1", null);

        result.EvidenceItems.Should().NotBeEmpty();
    }

    [Fact]
    public async Task CollectAsync_Cancelled_Throws()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            CreateCollector().CollectAsync("sub-1", null, cts.Token));
    }

    [Fact]
    public async Task CollectAsync_AllItemsHaveContentHash()
    {
        SetupAllServices();

        var result = await CreateCollector().CollectAsync("sub-1", null);

        result.EvidenceItems.Should().AllSatisfy(item =>
            item.ContentHash.Should().NotBeNullOrEmpty());
    }

    private void SetupAllServices()
    {
        _policyMock.Setup(x => x.GetPolicyStatesAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("Compliant");
        _policyMock.Setup(x => x.GetComplianceSummaryAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("Overall compliant");
        _defenderMock.Setup(x => x.GetAssessmentsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("Healthy");
        _defenderMock.Setup(x => x.GetSecureScoreAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("{\"score\": 85}");
        _defenderMock.Setup(x => x.GetRecommendationsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("No recommendations");
    }
}
