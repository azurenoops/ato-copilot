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

public class ContingencyPlanningScannerTests
{
    private readonly Mock<IAzureResourceService> _azureResourceMock = new();
    private readonly Mock<IAzurePolicyComplianceService> _policyMock = new();
    private readonly Mock<ILogger<ContingencyPlanningScanner>> _loggerMock = new();

    private ContingencyPlanningScanner CreateScanner() =>
        new(_azureResourceMock.Object, _policyMock.Object, _loggerMock.Object);

    private static List<NistControl> CreateControls() => new()
    {
        new NistControl { Id = "cp-2", Family = "CP", Title = "Contingency Plan" },
        new NistControl { Id = "cp-6", Family = "CP", Title = "Alternate Storage Site" },
        new NistControl { Id = "cp-9", Family = "CP", Title = "System Backup" }
    };

    [Fact]
    public void FamilyCode_ShouldBeCP() => CreateScanner().FamilyCode.Should().Be("CP");

    [Fact]
    public async Task ScanAsync_NoBackupVaults_ProducesCriticalFinding()
    {
        SetupResourceType("Microsoft.RecoveryServices/vaults");
        SetupResourceType("Microsoft.Sql/servers");
        _policyMock.Setup(x => x.GetPolicyStatesAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("Compliant");

        var result = await CreateScanner().ScanAsync("sub-1", null, CreateControls());

        result.Findings.Should().Contain(f => f.ControlId == "CP-9" && f.Severity == FindingSeverity.Critical);
    }

    [Fact]
    public async Task ScanAsync_VaultExists_NoBackupFinding()
    {
        SetupResourceType("Microsoft.RecoveryServices/vaults", empty: false);
        SetupResourceType("Microsoft.Sql/servers");
        _policyMock.Setup(x => x.GetPolicyStatesAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("Compliant");

        var result = await CreateScanner().ScanAsync("sub-1", null, CreateControls());

        result.Findings.Should().NotContain(f => f.ControlId == "CP-9");
    }

    [Fact]
    public async Task ScanAsync_NonCompliantPolicies_ProducesFinding()
    {
        SetupResourceType("Microsoft.RecoveryServices/vaults", empty: false);
        SetupResourceType("Microsoft.Sql/servers");
        _policyMock.Setup(x => x.GetPolicyStatesAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("NonCompliant");

        var result = await CreateScanner().ScanAsync("sub-1", null, CreateControls());
        result.Findings.Should().Contain(f => f.ControlId == "CP-2");
    }

    [Fact]
    public async Task ScanAsync_Cancelled_Skipped()
    {
        _azureResourceMock.Setup(x => x.GetResourcesAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        var result = await CreateScanner().ScanAsync("sub-1", null, CreateControls());
        result.Status.Should().Be(FamilyAssessmentStatus.Skipped);
    }

    private void SetupResourceType(string resourceType, bool empty = true)
    {
        var resources = empty
            ? Array.Empty<GenericResource>()
            : new GenericResource[] { null! };
        _azureResourceMock.Setup(x => x.GetResourcesAsync(
                It.IsAny<string>(), It.IsAny<string?>(), resourceType, It.IsAny<CancellationToken>()))
            .ReturnsAsync(resources);
    }
}
