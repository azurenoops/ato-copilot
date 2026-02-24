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

public class IncidentResponseScannerTests
{
    private readonly Mock<IAzureResourceService> _azureResourceMock = new();
    private readonly Mock<IAzurePolicyComplianceService> _policyMock = new();
    private readonly Mock<ILogger<IncidentResponseScanner>> _loggerMock = new();

    private IncidentResponseScanner CreateScanner() =>
        new(_azureResourceMock.Object, _policyMock.Object, _loggerMock.Object);

    private static List<NistControl> CreateControls() => new()
    {
        new NistControl { Id = "ir-2", Family = "IR", Title = "Incident Response Training" },
        new NistControl { Id = "ir-4", Family = "IR", Title = "Incident Handling" },
        new NistControl { Id = "ir-5", Family = "IR", Title = "Incident Monitoring" },
        new NistControl { Id = "ir-6", Family = "IR", Title = "Incident Reporting" }
    };

    [Fact]
    public void FamilyCode_ShouldBeIR() => CreateScanner().FamilyCode.Should().Be("IR");

    [Fact]
    public async Task ScanAsync_NoActionGroups_ProducesFinding()
    {
        SetupAllResources();
        _policyMock.Setup(x => x.GetPolicyStatesAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("Compliant");

        var result = await CreateScanner().ScanAsync("sub-1", null, CreateControls());

        result.Findings.Should().Contain(f => f.ControlId == "IR-6");
    }

    [Fact]
    public async Task ScanAsync_NoAlertRules_ProducesFinding()
    {
        SetupAllResources();
        _policyMock.Setup(x => x.GetPolicyStatesAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("Compliant");

        var result = await CreateScanner().ScanAsync("sub-1", null, CreateControls());

        result.Findings.Should().Contain(f => f.ControlId == "IR-4");
    }

    [Fact]
    public async Task ScanAsync_NonCompliantPolicies_ProducesFinding()
    {
        SetupAllResources();
        _policyMock.Setup(x => x.GetPolicyStatesAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("NonCompliant");

        var result = await CreateScanner().ScanAsync("sub-1", null, CreateControls());

        result.Findings.Should().Contain(f => f.ControlId == "IR-2");
    }

    [Fact]
    public async Task ScanAsync_Cancelled_Skipped()
    {
        _azureResourceMock.Setup(x => x.GetResourcesAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        var result = await CreateScanner().ScanAsync("sub-1", null, CreateControls());
        result.Status.Should().Be(FamilyAssessmentStatus.Skipped);
    }

    private void SetupAllResources()
    {
        _azureResourceMock.Setup(x => x.GetResourcesAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<GenericResource>());
    }
}
