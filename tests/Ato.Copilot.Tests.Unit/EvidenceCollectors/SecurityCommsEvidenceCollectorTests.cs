using Xunit;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Ato.Copilot.Agents.Compliance.EvidenceCollectors;
using Ato.Copilot.Core.Interfaces.Compliance;
using Ato.Copilot.Core.Models.Compliance;
using Azure.ResourceManager.Resources;

namespace Ato.Copilot.Tests.Unit.EvidenceCollectors;

public class SecurityCommsEvidenceCollectorTests
{
    private readonly Mock<IAzureResourceService> _azureResourceMock = new();
    private readonly Mock<IDefenderForCloudService> _defenderMock = new();
    private readonly Mock<ILogger<SecurityCommsEvidenceCollector>> _loggerMock = new();

    private SecurityCommsEvidenceCollector CreateCollector() =>
        new(_azureResourceMock.Object, _defenderMock.Object, _loggerMock.Object);

    [Fact]
    public void FamilyCode_ShouldBeSC()
    {
        CreateCollector().FamilyCode.Should().Be("SC");
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
    public async Task CollectAsync_ContainsNSGEvidence()
    {
        SetupAllServices();

        var result = await CreateCollector().CollectAsync("sub-1", null);

        result.EvidenceItems.Should().Contain(e =>
            e.Type == EvidenceType.Configuration && e.Title.Contains("Network Security Group"));
    }

    [Fact]
    public async Task CollectAsync_ContainsEncryptionEvidence()
    {
        SetupAllServices();

        var result = await CreateCollector().CollectAsync("sub-1", null);

        result.EvidenceItems.Should().Contain(e =>
            e.Type == EvidenceType.Log && e.Title.Contains("Encryption"));
    }

    [Fact]
    public async Task CollectAsync_HasAttestation()
    {
        SetupAllServices();

        var result = await CreateCollector().CollectAsync("sub-1", null);

        result.AttestationStatement.Should().Contain("System and Communications Protection");
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
        _defenderMock.Setup(x => x.GetRecommendationsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("No recommendations");
    }
}
