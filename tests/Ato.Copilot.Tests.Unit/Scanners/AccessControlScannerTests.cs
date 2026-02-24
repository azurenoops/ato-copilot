using Xunit;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Ato.Copilot.Agents.Compliance.Scanners;
using Ato.Copilot.Core.Constants;
using Ato.Copilot.Core.Interfaces.Compliance;
using Ato.Copilot.Core.Models.Compliance;
using Azure.ResourceManager.Authorization;
using Azure.ResourceManager.Authorization.Models;

namespace Ato.Copilot.Tests.Unit.Scanners;

public class AccessControlScannerTests
{
    private readonly Mock<IAzureResourceService> _azureResourceMock = new();
    private readonly Mock<IAzurePolicyComplianceService> _policyMock = new();
    private readonly Mock<ILogger<AccessControlScanner>> _loggerMock = new();

    private AccessControlScanner CreateScanner() =>
        new(_azureResourceMock.Object, _policyMock.Object, _loggerMock.Object);

    private static List<NistControl> CreateControls() => new()
    {
        new NistControl { Id = "ac-2", Family = "AC", Title = "Account Management" },
        new NistControl { Id = "ac-3", Family = "AC", Title = "Access Enforcement" },
        new NistControl { Id = "ac-6", Family = "AC", Title = "Least Privilege" }
    };

    [Fact]
    public void FamilyCode_ShouldBeAC()
    {
        CreateScanner().FamilyCode.Should().Be("AC");
    }

    [Fact]
    public async Task ScanAsync_NoRoleAssignments_ReturnsNoFindings()
    {
        _azureResourceMock.Setup(x => x.GetRoleAssignmentsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<RoleAssignmentResource>());
        _policyMock.Setup(x => x.GetPolicyStatesAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("Compliant");

        var scanner = CreateScanner();
        var result = await scanner.ScanAsync("sub-1", null, CreateControls());

        result.FamilyCode.Should().Be("AC");
        result.Status.Should().Be(FamilyAssessmentStatus.Completed);
        result.Findings.Should().BeEmpty();
    }

    [Fact]
    public async Task ScanAsync_ExcessiveOwnerAssignments_WithPolicyNonCompliance_ProducesFindings()
    {
        // Since RoleAssignmentResource is a sealed ARM SDK type, we test via policy path.
        // The role assignment checks are effectively integration tests.
        _azureResourceMock.Setup(x => x.GetRoleAssignmentsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<RoleAssignmentResource>());
        _policyMock.Setup(x => x.GetPolicyStatesAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("NonCompliant: excessive owner assignments");

        var scanner = CreateScanner();
        var result = await scanner.ScanAsync("sub-1", null, CreateControls());

        result.Status.Should().Be(FamilyAssessmentStatus.Completed);
        result.Findings.Should().Contain(f => f.ControlId == "AC-2" && f.ScanSource == ScanSourceType.Policy);
    }

    [Fact]
    public async Task ScanAsync_PolicyCompliant_NoRoleAssignments_NoFindings()
    {
        _azureResourceMock.Setup(x => x.GetRoleAssignmentsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<RoleAssignmentResource>());
        _policyMock.Setup(x => x.GetPolicyStatesAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("Compliant");

        var scanner = CreateScanner();
        var result = await scanner.ScanAsync("sub-1", null, CreateControls());

        result.Findings.Should().BeEmpty();
        result.ComplianceScore.Should().Be(100.0);
    }

    [Fact]
    public async Task ScanAsync_NonCompliantPolicies_ProducesPolicyFinding()
    {
        _azureResourceMock.Setup(x => x.GetRoleAssignmentsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<RoleAssignmentResource>());
        _policyMock.Setup(x => x.GetPolicyStatesAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("NonCompliant: 3 resources");

        var scanner = CreateScanner();
        var result = await scanner.ScanAsync("sub-1", null, CreateControls());

        result.Findings.Should().Contain(f => f.ControlId == "AC-2" && f.ScanSource == ScanSourceType.Policy);
    }

    [Fact]
    public async Task ScanAsync_NoSubscriptionScopeAssignments_NoProblem()
    {
        _azureResourceMock.Setup(x => x.GetRoleAssignmentsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<RoleAssignmentResource>());
        _policyMock.Setup(x => x.GetPolicyStatesAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("Compliant");

        var scanner = CreateScanner();
        var result = await scanner.ScanAsync("sub-1", null, CreateControls());

        result.Findings.Should().NotContain(f => f.ControlId == "AC-3");
    }

    [Fact]
    public async Task ScanAsync_Cancelled_ReturnsSkippedStatus()
    {
        _azureResourceMock.Setup(x => x.GetRoleAssignmentsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        var scanner = CreateScanner();
        var cts = new CancellationTokenSource();
        var result = await scanner.ScanAsync("sub-1", null, CreateControls(), cts.Token);

        result.Status.Should().Be(FamilyAssessmentStatus.Skipped);
    }

    [Fact]
    public async Task ScanAsync_ServiceThrows_ReturnsFailedStatus()
    {
        _azureResourceMock.Setup(x => x.GetRoleAssignmentsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Service unavailable"));

        var scanner = CreateScanner();
        var result = await scanner.ScanAsync("sub-1", null, CreateControls());

        result.Status.Should().Be(FamilyAssessmentStatus.Failed);
        result.ErrorMessage.Should().Contain("Service unavailable");
    }

    [Fact]
    public async Task ScanAsync_NonCompliantPolicies_FindingsAreHighRisk()
    {
        _azureResourceMock.Setup(x => x.GetRoleAssignmentsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<RoleAssignmentResource>());
        _policyMock.Setup(x => x.GetPolicyStatesAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("NonCompliant");

        var scanner = CreateScanner();
        var result = await scanner.ScanAsync("sub-1", null, CreateControls());

        result.Findings.Should().OnlyContain(f => f.RiskLevel == RiskLevel.High);
    }

    // ─── Helper Methods ─────────────────────────────────────────────────────

    private static IReadOnlyList<RoleAssignmentResource> CreateRoleAssignments(int ownerCount, int contributorCount)
    {
        // Since RoleAssignmentResource is sealed ARM type, we return empty
        // and test through the scanner's policy path instead.
        // The actual role assignment checks are integration tests.
        return Array.Empty<RoleAssignmentResource>();
    }

    private static IReadOnlyList<RoleAssignmentResource> CreateSubscriptionScopedAssignments(int count, string subscriptionId)
    {
        return Array.Empty<RoleAssignmentResource>();
    }
}
