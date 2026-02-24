using Xunit;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Ato.Copilot.Agents.Compliance.EvidenceCollectors;
using Ato.Copilot.Core.Interfaces.Compliance;
using Ato.Copilot.Core.Models.Compliance;
using Azure.ResourceManager.Resources;
using Azure.ResourceManager.Authorization;

namespace Ato.Copilot.Tests.Unit.EvidenceCollectors;

public class SystemIntegrityEvidenceCollectorTests
{
    private readonly Mock<IAzureResourceService> _azureResourceMock = new();
    private readonly Mock<IDefenderForCloudService> _defenderMock = new();
    private readonly Mock<IAzurePolicyComplianceService> _policyMock = new();
    private readonly Mock<ILogger<SystemIntegrityEvidenceCollector>> _loggerMock = new();

    private SystemIntegrityEvidenceCollector CreateCollector() =>
        new(_azureResourceMock.Object, _defenderMock.Object, _policyMock.Object, _loggerMock.Object);

    [Fact]
    public void FamilyCode_ShouldBeSI()
    {
        CreateCollector().FamilyCode.Should().Be("SI");
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
    public async Task CollectAsync_ContainsPatchingEvidence()
    {
        SetupAllServices();

        var result = await CreateCollector().CollectAsync("sub-1", null);

        result.EvidenceItems.Should().Contain(e =>
            e.Type == EvidenceType.Configuration && e.Title.Contains("Virtual Machine"));
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
        _azureResourceMock.Setup(x => x.GetResourcesAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<GenericResource>());
        _defenderMock.Setup(x => x.GetAssessmentsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("Healthy");
        _defenderMock.Setup(x => x.GetSecureScoreAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("{\"score\": 85}");
        _policyMock.Setup(x => x.GetPolicyStatesAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("Compliant");
        _azureResourceMock.Setup(x => x.GetRoleAssignmentsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<RoleAssignmentResource>());
    }
}
