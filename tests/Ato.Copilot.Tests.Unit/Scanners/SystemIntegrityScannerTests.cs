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

public class SystemIntegrityScannerTests
{
    private readonly Mock<IAzureResourceService> _azureResourceMock = new();
    private readonly Mock<IAzurePolicyComplianceService> _policyMock = new();
    private readonly Mock<IDefenderForCloudService> _defenderMock = new();
    private readonly Mock<ILogger<SystemIntegrityScanner>> _loggerMock = new();

    private SystemIntegrityScanner CreateScanner() =>
        new(_azureResourceMock.Object, _policyMock.Object, _defenderMock.Object, _loggerMock.Object);

    private static List<NistControl> CreateControls() => new()
    {
        new NistControl { Id = "si-2", Family = "SI", Title = "Flaw Remediation" },
        new NistControl { Id = "si-3", Family = "SI", Title = "Malicious Code Protection" },
        new NistControl { Id = "si-4", Family = "SI", Title = "System Monitoring" }
    };

    [Fact]
    public void FamilyCode_ShouldBeSI() => CreateScanner().FamilyCode.Should().Be("SI");

    [Fact]
    public async Task ScanAsync_NoVMs_NoFindings()
    {
        SetupResourcesEmpty();
        _policyMock.Setup(x => x.GetPolicyStatesAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("Compliant");
        _defenderMock.Setup(x => x.GetAssessmentsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("Healthy");

        var result = await CreateScanner().ScanAsync("sub-1", null, CreateControls());
        result.Status.Should().Be(FamilyAssessmentStatus.Completed);
    }

    [Fact]
    public async Task ScanAsync_NonCompliantPatching_ProducesFinding()
    {
        SetupResourcesEmpty();
        _policyMock.Setup(x => x.GetPolicyStatesAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("Compliant");
        _defenderMock.Setup(x => x.GetAssessmentsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("Unhealthy assessments found");

        var result = await CreateScanner().ScanAsync("sub-1", null, CreateControls());
        result.Findings.Should().Contain(f => f.ControlId == "SI-4");
    }

    [Fact]
    public async Task ScanAsync_Cancelled_SkippedStatus()
    {
        _azureResourceMock.Setup(x => x.GetResourcesAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        var result = await CreateScanner().ScanAsync("sub-1", null, CreateControls());
        result.Status.Should().Be(FamilyAssessmentStatus.Skipped);
    }

    [Fact]
    public async Task ScanAsync_ServiceThrows_FailedStatus()
    {
        _azureResourceMock.Setup(x => x.GetResourcesAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("boom"));

        var result = await CreateScanner().ScanAsync("sub-1", null, CreateControls());
        result.Status.Should().Be(FamilyAssessmentStatus.Failed);
    }

    private void SetupResourcesEmpty()
    {
        _azureResourceMock.Setup(x => x.GetResourcesAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<GenericResource>());
    }
}
