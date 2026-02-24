using Xunit;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Ato.Copilot.Agents.Compliance.Scanners;
using Ato.Copilot.Core.Constants;
using Ato.Copilot.Core.Interfaces.Compliance;
using Ato.Copilot.Core.Models.Compliance;
using Azure.ResourceManager.Monitor;
using Azure.ResourceManager.Resources;

namespace Ato.Copilot.Tests.Unit.Scanners;

public class AuditScannerTests
{
    private readonly Mock<IAzureResourceService> _azureResourceMock = new();
    private readonly Mock<IAzurePolicyComplianceService> _policyMock = new();
    private readonly Mock<ILogger<AuditScanner>> _loggerMock = new();

    private AuditScanner CreateScanner() =>
        new(_azureResourceMock.Object, _policyMock.Object, _loggerMock.Object);

    private static List<NistControl> CreateControls() => new()
    {
        new NistControl { Id = "au-2", Family = "AU", Title = "Event Logging" },
        new NistControl { Id = "au-6", Family = "AU", Title = "Audit Record Review" }
    };

    [Fact]
    public void FamilyCode_ShouldBeAU()
    {
        CreateScanner().FamilyCode.Should().Be("AU");
    }

    [Fact]
    public async Task ScanAsync_NoResources_ReturnsNoFindings()
    {
        _azureResourceMock.Setup(x => x.GetResourcesAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<GenericResource>());
        _policyMock.Setup(x => x.GetPolicyStatesAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("Compliant");

        var result = await CreateScanner().ScanAsync("sub-1", null, CreateControls());

        result.FamilyCode.Should().Be("AU");
        result.Status.Should().Be(FamilyAssessmentStatus.Completed);
    }

    [Fact]
    public async Task ScanAsync_NonCompliantPolicies_ProducesFinding()
    {
        _azureResourceMock.Setup(x => x.GetResourcesAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<GenericResource>());
        _policyMock.Setup(x => x.GetPolicyStatesAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("NonCompliant: 5 resources");

        var result = await CreateScanner().ScanAsync("sub-1", null, CreateControls());

        result.Findings.Should().Contain(f => f.ControlId == "AU-2" && f.ScanSource == ScanSourceType.Policy);
    }

    [Fact]
    public async Task ScanAsync_Cancelled_ReturnsSkippedStatus()
    {
        _azureResourceMock.Setup(x => x.GetResourcesAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        var result = await CreateScanner().ScanAsync("sub-1", null, CreateControls());
        result.Status.Should().Be(FamilyAssessmentStatus.Skipped);
    }

    [Fact]
    public async Task ScanAsync_ServiceThrows_ReturnsFailed()
    {
        _azureResourceMock.Setup(x => x.GetResourcesAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("fail"));

        var result = await CreateScanner().ScanAsync("sub-1", null, CreateControls());
        result.Status.Should().Be(FamilyAssessmentStatus.Failed);
    }

    [Fact]
    public async Task ScanAsync_ComplianceScore_CalculatedCorrectly()
    {
        _azureResourceMock.Setup(x => x.GetResourcesAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<GenericResource>());
        _policyMock.Setup(x => x.GetPolicyStatesAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("Compliant");

        var result = await CreateScanner().ScanAsync("sub-1", null, CreateControls());

        result.ComplianceScore.Should().Be(100.0);
        result.PassedControls.Should().Be(2);
        result.TotalControls.Should().Be(2);
    }
}
