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

public class AccessControlEvidenceCollectorTests
{
    private readonly Mock<IAzureResourceService> _azureResourceMock = new();
    private readonly Mock<IAzurePolicyComplianceService> _policyMock = new();
    private readonly Mock<ILogger<AccessControlEvidenceCollector>> _loggerMock = new();

    private AccessControlEvidenceCollector CreateCollector() =>
        new(_azureResourceMock.Object, _policyMock.Object, _loggerMock.Object);

    [Fact]
    public void FamilyCode_ShouldBeAC()
    {
        CreateCollector().FamilyCode.Should().Be("AC");
    }

    [Fact]
    public async Task CollectAsync_AllServicesSucceed_Returns5EvidenceTypes()
    {
        SetupRoleAssignments();
        SetupPolicyState("Compliant");
        SetupDiagnostics();
        SetupResources();

        var result = await CreateCollector().CollectAsync("sub-1", null);

        result.EvidenceItems.Should().HaveCount(5);
        result.CollectedEvidenceTypes.Should().Be(5);
        result.CompletenessScore.Should().Be(100.0);
    }

    [Fact]
    public async Task CollectAsync_HasAttestation()
    {
        SetupRoleAssignments();
        SetupPolicyState("Compliant");
        SetupDiagnostics();
        SetupResources();

        var result = await CreateCollector().CollectAsync("sub-1", null);

        result.AttestationStatement.Should().NotBeNullOrEmpty();
        result.AttestationStatement.Should().Contain("Access Control");
    }

    [Fact]
    public async Task CollectAsync_AllItemsHaveContentHash()
    {
        SetupRoleAssignments();
        SetupPolicyState("Compliant");
        SetupDiagnostics();
        SetupResources();

        var result = await CreateCollector().CollectAsync("sub-1", null);

        result.EvidenceItems.Should().AllSatisfy(item =>
            item.ContentHash.Should().NotBeNullOrEmpty());
    }

    [Fact]
    public async Task CollectAsync_ServiceFailure_GracefulDegradation()
    {
        _azureResourceMock.Setup(x => x.GetRoleAssignmentsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Service unavailable"));
        SetupPolicyState("Compliant");
        SetupDiagnostics();
        SetupResources();

        var result = await CreateCollector().CollectAsync("sub-1", null);

        // Should still collect other evidence types even if role assignments fail
        result.EvidenceItems.Should().NotBeEmpty();
    }

    [Fact]
    public async Task CollectAsync_Cancelled_ThrowsOperationCancelled()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            CreateCollector().CollectAsync("sub-1", null, cts.Token));
    }

    private void SetupRoleAssignments()
    {
        _azureResourceMock.Setup(x => x.GetRoleAssignmentsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<RoleAssignmentResource>());
    }

    private void SetupPolicyState(string state)
    {
        _policyMock.Setup(x => x.GetPolicyStatesAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(state);
    }

    private void SetupDiagnostics()
    {
        _azureResourceMock.Setup(x => x.GetDiagnosticSettingsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Azure.ResourceManager.Monitor.DiagnosticSettingResource>());
    }

    private void SetupResources()
    {
        _azureResourceMock.Setup(x => x.GetResourcesAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<GenericResource>());
    }
}
