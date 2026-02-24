using Xunit;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Ato.Copilot.Agents.Compliance.Scanners;
using Ato.Copilot.Core.Constants;
using Ato.Copilot.Core.Interfaces.Compliance;
using Ato.Copilot.Core.Models.Compliance;
using Azure.ResourceManager.Resources;

namespace Ato.Copilot.Tests.Unit.Scanners;

public class SecurityCommsScannerTests
{
    private readonly Mock<IAzureResourceService> _azureResourceMock = new();
    private readonly Mock<IDefenderForCloudService> _defenderMock = new();
    private readonly Mock<ILogger<SecurityCommunicationsScanner>> _loggerMock = new();

    private SecurityCommunicationsScanner CreateScanner() =>
        new(_azureResourceMock.Object, _defenderMock.Object, _loggerMock.Object);

    private static List<NistControl> CreateControls() => new()
    {
        new NistControl { Id = "sc-7", Family = "SC", Title = "Boundary Protection" },
        new NistControl { Id = "sc-8", Family = "SC", Title = "Transmission Confidentiality" },
        new NistControl { Id = "sc-12", Family = "SC", Title = "Cryptographic Key Management" },
        new NistControl { Id = "sc-28", Family = "SC", Title = "Protection of Information at Rest" }
    };

    [Fact]
    public void FamilyCode_ShouldBeSC()
    {
        CreateScanner().FamilyCode.Should().Be("SC");
    }

    [Fact]
    public async Task ScanAsync_NoNSGs_ProducesFinding()
    {
        SetupResources("Microsoft.Network/networkSecurityGroups");
        SetupResources("Microsoft.Storage/storageAccounts");
        SetupResources("Microsoft.KeyVault/vaults");
        _defenderMock.Setup(x => x.GetRecommendationsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("No recommendations");

        var result = await CreateScanner().ScanAsync("sub-1", null, CreateControls());

        result.Findings.Should().Contain(f => f.ControlId == "SC-7");
    }

    [Fact]
    public async Task ScanAsync_NoKeyVault_ProducesFinding()
    {
        SetupResources("Microsoft.Network/networkSecurityGroups", hasResources: true);
        SetupResources("Microsoft.Storage/storageAccounts");
        SetupResources("Microsoft.KeyVault/vaults");
        _defenderMock.Setup(x => x.GetRecommendationsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("No recommendations");

        var result = await CreateScanner().ScanAsync("sub-1", null, CreateControls());

        result.Findings.Should().Contain(f => f.ControlId == "SC-12");
    }

    [Fact]
    public async Task ScanAsync_DefenderEncryptionRecommendation_ProducesFinding()
    {
        SetupResources("Microsoft.Network/networkSecurityGroups", hasResources: true);
        SetupResources("Microsoft.Storage/storageAccounts");
        SetupResources("Microsoft.KeyVault/vaults", hasResources: true);
        _defenderMock.Setup(x => x.GetRecommendationsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("Pending: encryption not enabled on 3 storage accounts");

        var result = await CreateScanner().ScanAsync("sub-1", null, CreateControls());

        result.Findings.Should().Contain(f => f.ControlId == "SC-28" && f.ScanSource == ScanSourceType.Defender);
    }

    [Fact]
    public async Task ScanAsync_AllGood_NoFindings()
    {
        SetupResources("Microsoft.Network/networkSecurityGroups", hasResources: true);
        SetupResources("Microsoft.Storage/storageAccounts");
        SetupResources("Microsoft.KeyVault/vaults", hasResources: true);
        _defenderMock.Setup(x => x.GetRecommendationsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("No recommendations");

        var result = await CreateScanner().ScanAsync("sub-1", null, CreateControls());

        result.Status.Should().Be(FamilyAssessmentStatus.Completed);
        result.ComplianceScore.Should().Be(100.0);
    }

    [Fact]
    public async Task ScanAsync_AllFindingsHaveRiskLevelHigh()
    {
        SetupResources("Microsoft.Network/networkSecurityGroups");
        SetupResources("Microsoft.Storage/storageAccounts");
        SetupResources("Microsoft.KeyVault/vaults");
        _defenderMock.Setup(x => x.GetRecommendationsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("encryption recommendation pending");

        var result = await CreateScanner().ScanAsync("sub-1", null, CreateControls());

        result.Findings.Where(f => f.RiskLevel == RiskLevel.High).Should().NotBeEmpty();
    }

    [Fact]
    public async Task ScanAsync_Cancelled_ReturnsSkipped()
    {
        _azureResourceMock.Setup(x => x.GetResourcesAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        var result = await CreateScanner().ScanAsync("sub-1", null, CreateControls());
        result.Status.Should().Be(FamilyAssessmentStatus.Skipped);
    }

    private void SetupResources(string resourceType, bool hasResources = false)
    {
        var resources = hasResources
            ? new GenericResource[] { null! }
            : Array.Empty<GenericResource>();
        _azureResourceMock.Setup(x => x.GetResourcesAsync(
                It.IsAny<string>(), It.IsAny<string?>(), resourceType, It.IsAny<CancellationToken>()))
            .ReturnsAsync(resources);
    }
}
