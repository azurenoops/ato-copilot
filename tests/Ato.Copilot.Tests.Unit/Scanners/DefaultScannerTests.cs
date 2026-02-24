using Xunit;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Ato.Copilot.Agents.Compliance.Scanners;
using Ato.Copilot.Core.Constants;
using Ato.Copilot.Core.Interfaces.Compliance;
using Ato.Copilot.Core.Models.Compliance;

namespace Ato.Copilot.Tests.Unit.Scanners;

public class DefaultScannerTests
{
    private readonly Mock<IAzurePolicyComplianceService> _policyMock = new();
    private readonly Mock<IDefenderForCloudService> _defenderMock = new();
    private readonly Mock<ILogger<DefaultComplianceScanner>> _loggerMock = new();

    private DefaultComplianceScanner CreateScanner() =>
        new(_policyMock.Object, _defenderMock.Object, _loggerMock.Object);

    private static List<NistControl> CreateControls(string family = "AT") => new()
    {
        new NistControl
        {
            Id = $"{family.ToLowerInvariant()}-1", Family = family,
            Title = "Test Control 1",
            AzurePolicyDefinitionIds = new List<string> { "policy-def-1" }
        },
        new NistControl
        {
            Id = $"{family.ToLowerInvariant()}-2", Family = family,
            Title = "Test Control 2",
            AzurePolicyDefinitionIds = new List<string> { "policy-def-2" }
        }
    };

    [Fact]
    public void FamilyCode_ShouldBeDEFAULT()
    {
        CreateScanner().FamilyCode.Should().Be("DEFAULT");
    }

    [Fact]
    public async Task ScanAsync_NonCompliantPolicy_ProducesFinding()
    {
        _policyMock.Setup(x => x.GetPolicyStatesAsync(It.IsAny<string>(), "policy-def-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync("NonCompliant");
        _policyMock.Setup(x => x.GetPolicyStatesAsync(It.IsAny<string>(), "policy-def-2", It.IsAny<CancellationToken>()))
            .ReturnsAsync("Compliant");
        _defenderMock.Setup(x => x.GetAssessmentsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("Healthy");

        var result = await CreateScanner().ScanAsync("sub-1", null, CreateControls());

        result.Findings.Should().HaveCount(1);
        result.Findings[0].ControlId.Should().Be("AT-1");
        result.Findings[0].ScanSource.Should().Be(ScanSourceType.Policy);
        result.Findings[0].PolicyDefinitionId.Should().Be("policy-def-1");
    }

    [Fact]
    public async Task ScanAsync_AllCompliant_NoFindings()
    {
        _policyMock.Setup(x => x.GetPolicyStatesAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("Compliant");
        _defenderMock.Setup(x => x.GetAssessmentsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("Healthy");

        var result = await CreateScanner().ScanAsync("sub-1", null, CreateControls());

        result.Findings.Should().BeEmpty();
        result.ComplianceScore.Should().Be(100.0);
    }

    [Fact]
    public async Task ScanAsync_NoPolicyMappings_FallsBackToDefender()
    {
        var controlsWithoutPolicies = new List<NistControl>
        {
            new() { Id = "at-1", Family = "AT", Title = "Test", AzurePolicyDefinitionIds = new() }
        };

        _defenderMock.Setup(x => x.GetAssessmentsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("Unhealthy assessments found");

        var result = await CreateScanner().ScanAsync("sub-1", null, controlsWithoutPolicies);

        result.Findings.Should().HaveCount(1);
        result.Findings[0].ScanSource.Should().Be(ScanSourceType.Defender);
    }

    [Fact]
    public async Task ScanAsync_HandlesMultipleFamilies()
    {
        _policyMock.Setup(x => x.GetPolicyStatesAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("NonCompliant");
        _defenderMock.Setup(x => x.GetAssessmentsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("Healthy");

        var result = await CreateScanner().ScanAsync("sub-1", null, CreateControls("MA"));

        result.Findings.Should().OnlyContain(f => f.ControlFamily == "MA");
    }

    [Fact]
    public async Task ScanAsync_PolicyCheckThrows_ContinuesGracefully()
    {
        _policyMock.Setup(x => x.GetPolicyStatesAsync(It.IsAny<string>(), "policy-def-1", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("API error"));
        _policyMock.Setup(x => x.GetPolicyStatesAsync(It.IsAny<string>(), "policy-def-2", It.IsAny<CancellationToken>()))
            .ReturnsAsync("NonCompliant");
        _defenderMock.Setup(x => x.GetAssessmentsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("Healthy");

        var result = await CreateScanner().ScanAsync("sub-1", null, CreateControls());

        // Should still produce finding for policy-def-2
        result.Status.Should().Be(FamilyAssessmentStatus.Completed);
        result.Findings.Should().HaveCount(1);
    }

    [Fact]
    public async Task ScanAsync_Cancelled_Skipped()
    {
        _policyMock.Setup(x => x.GetPolicyStatesAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        var result = await CreateScanner().ScanAsync("sub-1", null, CreateControls());
        result.Status.Should().Be(FamilyAssessmentStatus.Skipped);
    }

    [Fact]
    public async Task ScanAsync_FindingsAreAutoRemediable()
    {
        _policyMock.Setup(x => x.GetPolicyStatesAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("NonCompliant");
        _defenderMock.Setup(x => x.GetAssessmentsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("Healthy");

        var result = await CreateScanner().ScanAsync("sub-1", null, CreateControls());

        result.Findings.Should().OnlyContain(f =>
            f.AutoRemediable && f.RemediationType == RemediationType.PolicyRemediation);
    }
}
