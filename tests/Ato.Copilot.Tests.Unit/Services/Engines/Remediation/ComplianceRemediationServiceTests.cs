using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using FluentAssertions;
using Ato.Copilot.Agents.Compliance.Services.Engines.Remediation;
using Ato.Copilot.Core.Interfaces.Compliance;
using Ato.Copilot.Core.Models.Compliance;

namespace Ato.Copilot.Tests.Unit.Services.Engines.Remediation;

/// <summary>
/// Unit tests for ComplianceRemediationService: CanHandle returns true for supported types,
/// structured execution delegates to script executor, unsupported type returns failure,
/// execution status tracking, tier assignment.
/// </summary>
public class ComplianceRemediationServiceTests
{
    private readonly Mock<IRemediationScriptExecutor> _scriptExecutorMock = new();
    private readonly Mock<ILogger<ComplianceRemediationService>> _loggerMock = new();

    private ComplianceRemediationService CreateService() =>
        new(_scriptExecutorMock.Object, _loggerMock.Object);

    private static ComplianceFinding CreateFinding(
        RemediationType remType = RemediationType.ResourceConfiguration,
        string controlId = "SC-8") =>
        new()
        {
            Id = Guid.NewGuid().ToString(),
            ControlId = controlId,
            ControlFamily = "SC",
            Title = $"Finding for {controlId}",
            Description = "Non-compliance",
            Severity = FindingSeverity.High,
            Status = FindingStatus.Open,
            ResourceId = "/subscriptions/sub/resourceGroups/rg/providers/Microsoft.Storage/storageAccounts/test",
            ResourceType = "Microsoft.Storage/storageAccounts",
            RemediationGuidance = "Fix this issue",
            RemediationType = remType,
            AutoRemediable = true,
            Source = "PolicyInsights",
            SubscriptionId = "sub-1"
        };

    private static RemediationExecutionOptions CreateOptions(bool dryRun = false) =>
        new() { DryRun = dryRun };

    // ─── CanHandle ────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(RemediationType.ResourceConfiguration)]
    [InlineData(RemediationType.PolicyAssignment)]
    [InlineData(RemediationType.PolicyRemediation)]
    public void CanHandle_SupportedTypes_ReturnsTrue(RemediationType type)
    {
        var service = CreateService();
        var finding = CreateFinding(type);

        service.CanHandle(finding).Should().BeTrue();
    }

    [Theory]
    [InlineData(RemediationType.Unknown)]
    [InlineData(RemediationType.Manual)]
    public void CanHandle_UnsupportedTypes_ReturnsFalse(RemediationType type)
    {
        var service = CreateService();
        var finding = CreateFinding(type);

        service.CanHandle(finding).Should().BeFalse();
    }

    // ─── ExecuteStructuredRemediationAsync ─────────────────────────────────────

    [Fact]
    public async Task ExecuteStructuredRemediationAsync_SupportedType_DelegatesToExecutor()
    {
        var expectedExecution = new RemediationExecution
        {
            FindingId = "test-finding",
            Status = RemediationExecutionStatus.Completed,
            TierUsed = 1,
            StepsExecuted = 1,
            ChangesApplied = new List<string> { "Applied change" }
        };
        _scriptExecutorMock
            .Setup(e => e.ExecuteScriptAsync(
                It.IsAny<RemediationScript>(),
                It.IsAny<string>(),
                It.IsAny<RemediationExecutionOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedExecution);

        var service = CreateService();
        var finding = CreateFinding(RemediationType.ResourceConfiguration);

        var result = await service.ExecuteStructuredRemediationAsync(finding, CreateOptions());

        result.Status.Should().Be(RemediationExecutionStatus.Completed);
        result.TierUsed.Should().Be(2); // Overridden to Tier 2
        _scriptExecutorMock.Verify(e => e.ExecuteScriptAsync(
            It.IsAny<RemediationScript>(),
            It.Is<string>(id => id == finding.Id),
            It.IsAny<RemediationExecutionOptions>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteStructuredRemediationAsync_OverridesTierToTwo()
    {
        _scriptExecutorMock
            .Setup(e => e.ExecuteScriptAsync(
                It.IsAny<RemediationScript>(),
                It.IsAny<string>(),
                It.IsAny<RemediationExecutionOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RemediationExecution { TierUsed = 1 });

        var service = CreateService();
        var finding = CreateFinding();

        var result = await service.ExecuteStructuredRemediationAsync(finding, CreateOptions());

        result.TierUsed.Should().Be(2);
    }

    [Fact]
    public async Task ExecuteStructuredRemediationAsync_UnsupportedType_ReturnsFailed()
    {
        var service = CreateService();
        var finding = CreateFinding(RemediationType.Manual);

        var result = await service.ExecuteStructuredRemediationAsync(finding, CreateOptions());

        result.Status.Should().Be(RemediationExecutionStatus.Failed);
        result.Error.Should().Contain("Unsupported");
        result.TierUsed.Should().Be(2);
    }

    [Fact]
    public async Task ExecuteStructuredRemediationAsync_UnsupportedType_NeverCallsExecutor()
    {
        var service = CreateService();
        var finding = CreateFinding(RemediationType.Unknown);

        await service.ExecuteStructuredRemediationAsync(finding, CreateOptions());

        _scriptExecutorMock.Verify(e => e.ExecuteScriptAsync(
            It.IsAny<RemediationScript>(),
            It.IsAny<string>(),
            It.IsAny<RemediationExecutionOptions>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    // ─── Script Generation ────────────────────────────────────────────────────

    [Fact]
    public async Task ExecuteStructuredRemediationAsync_PolicyAssignment_GeneratesPolicyScript()
    {
        RemediationScript? capturedScript = null;
        _scriptExecutorMock
            .Setup(e => e.ExecuteScriptAsync(
                It.IsAny<RemediationScript>(),
                It.IsAny<string>(),
                It.IsAny<RemediationExecutionOptions>(),
                It.IsAny<CancellationToken>()))
            .Callback<RemediationScript, string, RemediationExecutionOptions, CancellationToken>(
                (script, _, _, _) => capturedScript = script)
            .ReturnsAsync(new RemediationExecution());

        var service = CreateService();
        var finding = CreateFinding(RemediationType.PolicyAssignment);

        await service.ExecuteStructuredRemediationAsync(finding, CreateOptions());

        capturedScript.Should().NotBeNull();
        capturedScript!.Content.Should().Contain("az policy assignment create");
        capturedScript.ScriptType.Should().Be(ScriptType.AzureCli);
    }

    [Fact]
    public async Task ExecuteStructuredRemediationAsync_PolicyRemediation_GeneratesRemediationScript()
    {
        RemediationScript? capturedScript = null;
        _scriptExecutorMock
            .Setup(e => e.ExecuteScriptAsync(
                It.IsAny<RemediationScript>(),
                It.IsAny<string>(),
                It.IsAny<RemediationExecutionOptions>(),
                It.IsAny<CancellationToken>()))
            .Callback<RemediationScript, string, RemediationExecutionOptions, CancellationToken>(
                (script, _, _, _) => capturedScript = script)
            .ReturnsAsync(new RemediationExecution());

        var service = CreateService();
        var finding = CreateFinding(RemediationType.PolicyRemediation);

        await service.ExecuteStructuredRemediationAsync(finding, CreateOptions());

        capturedScript.Should().NotBeNull();
        capturedScript!.Content.Should().Contain("az policy remediation create");
    }

    [Fact]
    public async Task ExecuteStructuredRemediationAsync_ResourceConfig_WithExistingScript_UsesIt()
    {
        RemediationScript? capturedScript = null;
        _scriptExecutorMock
            .Setup(e => e.ExecuteScriptAsync(
                It.IsAny<RemediationScript>(),
                It.IsAny<string>(),
                It.IsAny<RemediationExecutionOptions>(),
                It.IsAny<CancellationToken>()))
            .Callback<RemediationScript, string, RemediationExecutionOptions, CancellationToken>(
                (script, _, _, _) => capturedScript = script)
            .ReturnsAsync(new RemediationExecution());

        var service = CreateService();
        var finding = CreateFinding(RemediationType.ResourceConfiguration);
        finding.RemediationScript = "az storage account update --name test --min-tls-version TLS1_2";

        await service.ExecuteStructuredRemediationAsync(finding, CreateOptions());

        capturedScript.Should().NotBeNull();
        capturedScript!.Content.Should().Contain("az storage account update");
    }

    // ─── Execution Status Tracking ────────────────────────────────────────────

    [Fact]
    public async Task ExecuteStructuredRemediationAsync_Failure_PropagatesStatus()
    {
        _scriptExecutorMock
            .Setup(e => e.ExecuteScriptAsync(
                It.IsAny<RemediationScript>(),
                It.IsAny<string>(),
                It.IsAny<RemediationExecutionOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RemediationExecution
            {
                Status = RemediationExecutionStatus.Failed,
                Error = "Script timed out"
            });

        var service = CreateService();
        var finding = CreateFinding();

        var result = await service.ExecuteStructuredRemediationAsync(finding, CreateOptions());

        result.Status.Should().Be(RemediationExecutionStatus.Failed);
        result.TierUsed.Should().Be(2);
    }
}
