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

public class IdentificationAuthScannerTests
{
    private readonly Mock<IAzureResourceService> _azureResourceMock = new();
    private readonly Mock<IAzurePolicyComplianceService> _policyMock = new();
    private readonly Mock<IDefenderForCloudService> _defenderMock = new();
    private readonly Mock<ILogger<IdentificationAuthScanner>> _loggerMock = new();

    private IdentificationAuthScanner CreateScanner() =>
        new(_azureResourceMock.Object, _policyMock.Object, _defenderMock.Object, _loggerMock.Object);

    private static List<NistControl> CreateControls() => new()
    {
        new NistControl { Id = "ia-2", Family = "IA", Title = "Identification and Authentication" },
        new NistControl { Id = "ia-2(1)", Family = "IA", Title = "MFA Privileged" },
        new NistControl { Id = "ia-5", Family = "IA", Title = "Authenticator Management" }
    };

    [Fact]
    public void FamilyCode_ShouldBeIA() => CreateScanner().FamilyCode.Should().Be("IA");

    [Fact]
    public async Task ScanAsync_MFARecommendation_ProducesCriticalFinding()
    {
        SetupResourcesEmpty();
        _defenderMock.Setup(x => x.GetRecommendationsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("MFA should be enabled for all accounts");
        _policyMock.Setup(x => x.GetPolicyStatesAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("Compliant");

        var result = await CreateScanner().ScanAsync("sub-1", null, CreateControls());

        result.Findings.Should().Contain(f => f.ControlId == "IA-2(1)" && f.Severity == FindingSeverity.Critical);
    }

    [Fact]
    public async Task ScanAsync_NoMFAIssues_NoMFAFinding()
    {
        SetupResourcesEmpty();
        _defenderMock.Setup(x => x.GetRecommendationsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("All recommendations resolved");
        _policyMock.Setup(x => x.GetPolicyStatesAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("Compliant");

        var result = await CreateScanner().ScanAsync("sub-1", null, CreateControls());

        result.Findings.Should().NotContain(f => f.ControlId == "IA-2(1)");
    }

    [Fact]
    public async Task ScanAsync_NonCompliantPolicies_ProducesFinding()
    {
        SetupResourcesEmpty();
        _defenderMock.Setup(x => x.GetRecommendationsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("");
        _policyMock.Setup(x => x.GetPolicyStatesAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("NonCompliant");

        var result = await CreateScanner().ScanAsync("sub-1", null, CreateControls());

        result.Findings.Should().Contain(f => f.ControlId == "IA-5");
    }

    [Fact]
    public async Task ScanAsync_AllFindingsAreHighRisk()
    {
        SetupResourcesEmpty();
        _defenderMock.Setup(x => x.GetRecommendationsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("MFA required");
        _policyMock.Setup(x => x.GetPolicyStatesAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("NonCompliant");

        var result = await CreateScanner().ScanAsync("sub-1", null, CreateControls());

        result.Findings.Should().OnlyContain(f => f.RiskLevel == RiskLevel.High);
    }

    [Fact]
    public async Task ScanAsync_Cancelled_Skipped()
    {
        _azureResourceMock.Setup(x => x.GetResourcesAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        var result = await CreateScanner().ScanAsync("sub-1", null, CreateControls());
        result.Status.Should().Be(FamilyAssessmentStatus.Skipped);
    }

    private void SetupResourcesEmpty()
    {
        _azureResourceMock.Setup(x => x.GetResourcesAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<GenericResource>());
    }
}
