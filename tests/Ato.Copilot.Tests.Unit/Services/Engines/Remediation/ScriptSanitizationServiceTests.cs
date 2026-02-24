using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using FluentAssertions;
using Ato.Copilot.Agents.Compliance.Services.Engines.Remediation;

namespace Ato.Copilot.Tests.Unit.Services.Engines.Remediation;

/// <summary>
/// Unit tests for ScriptSanitizationService: safe scripts pass, destructive
/// commands rejected, multi-line scripts checked, empty script handling,
/// GetViolations returns specific violations.
/// </summary>
public class ScriptSanitizationServiceTests
{
    private readonly ScriptSanitizationService _service;

    public ScriptSanitizationServiceTests()
    {
        var logger = new Mock<ILogger<ScriptSanitizationService>>();
        _service = new ScriptSanitizationService(logger.Object);
    }

    // ─── IsSafe ───────────────────────────────────────────────────────────────

    [Fact]
    public void IsSafe_SafeAzureCliScript_ReturnsTrue()
    {
        var script = "az storage account update --name mystorageaccount --min-tls-version TLS1_2";

        _service.IsSafe(script).Should().BeTrue();
    }

    [Fact]
    public void IsSafe_SafePowerShellScript_ReturnsTrue()
    {
        var script = "Set-AzStorageAccount -ResourceGroupName 'rg' -Name 'sa' -MinimumTlsVersion TLS1_2";

        _service.IsSafe(script).Should().BeTrue();
    }

    [Fact]
    public void IsSafe_SafePolicyScript_ReturnsTrue()
    {
        var script = "az policy assignment create --name 'my-policy' --scope '/subscriptions/sub-id' --policy 'policy-def-id'";

        _service.IsSafe(script).Should().BeTrue();
    }

    [Fact]
    public void IsSafe_EmptyScript_ReturnsTrue()
    {
        _service.IsSafe("").Should().BeTrue();
    }

    [Fact]
    public void IsSafe_WhitespaceScript_ReturnsTrue()
    {
        _service.IsSafe("   \n  ").Should().BeTrue();
    }

    [Fact]
    public void IsSafe_NullScript_ReturnsTrue()
    {
        _service.IsSafe(null!).Should().BeTrue();
    }

    // ─── Azure CLI destructive commands ───────────────────────────────────────

    [Fact]
    public void IsSafe_AzGroupDelete_ReturnsFalse()
    {
        var script = "az group delete --name my-rg --yes";

        _service.IsSafe(script).Should().BeFalse();
    }

    [Fact]
    public void IsSafe_AzResourceDelete_ReturnsFalse()
    {
        var script = "az resource delete --ids /subscriptions/sub/resourceGroups/rg/providers/Microsoft.Storage/storageAccounts/test";

        _service.IsSafe(script).Should().BeFalse();
    }

    [Fact]
    public void IsSafe_AzStorageAccountDelete_ReturnsFalse()
    {
        var script = "az storage account delete --name testacc --yes";

        _service.IsSafe(script).Should().BeFalse();
    }

    [Fact]
    public void IsSafe_AzVmDelete_ReturnsFalse()
    {
        var script = "az vm delete --name myvm --resource-group myrg --yes";

        _service.IsSafe(script).Should().BeFalse();
    }

    [Fact]
    public void IsSafe_AzKeyvaultPurge_ReturnsFalse()
    {
        var script = "az keyvault purge --name mykv";

        _service.IsSafe(script).Should().BeFalse();
    }

    // ─── PowerShell destructive commands ──────────────────────────────────────

    [Fact]
    public void IsSafe_RemoveAzResourceGroup_ReturnsFalse()
    {
        var script = "Remove-AzResourceGroup -Name 'my-rg' -Force";

        _service.IsSafe(script).Should().BeFalse();
    }

    [Fact]
    public void IsSafe_RemoveAzResource_ReturnsFalse()
    {
        var script = "Remove-AzResource -ResourceId '/subscriptions/sub/resourceGroups/rg' -Force";

        _service.IsSafe(script).Should().BeFalse();
    }

    [Fact]
    public void IsSafe_RemoveAzStorageAccount_ReturnsFalse()
    {
        var script = "Remove-AzStorageAccount -Name 'test' -ResourceGroupName 'rg'";

        _service.IsSafe(script).Should().BeFalse();
    }

    [Fact]
    public void IsSafe_RemoveAzVM_ReturnsFalse()
    {
        var script = "Remove-AzVM -Name 'myvm' -ResourceGroupName 'rg' -Force";

        _service.IsSafe(script).Should().BeFalse();
    }

    // ─── Terraform/Bicep destructive commands ─────────────────────────────────

    [Fact]
    public void IsSafe_TerraformDestroy_ReturnsFalse()
    {
        var script = "terraform destroy -auto-approve";

        _service.IsSafe(script).Should().BeFalse();
    }

    [Fact]
    public void IsSafe_TfDestroy_ReturnsFalse()
    {
        var script = "tf destroy -auto-approve";

        _service.IsSafe(script).Should().BeFalse();
    }

    // ─── General dangerous patterns ───────────────────────────────────────────

    [Fact]
    public void IsSafe_RmRf_ReturnsFalse()
    {
        var script = "rm -rf /tmp/some-dir";

        _service.IsSafe(script).Should().BeFalse();
    }

    [Fact]
    public void IsSafe_FormatVolume_ReturnsFalse()
    {
        var script = "Format-Volume -DriveLetter C -FileSystem NTFS";

        _service.IsSafe(script).Should().BeFalse();
    }

    // ─── Multi-line scripts ───────────────────────────────────────────────────

    [Fact]
    public void IsSafe_MultiLineSafeScript_ReturnsTrue()
    {
        var script = """
            az storage account update --name test --min-tls-version TLS1_2
            az policy assignment create --name 'policy1' --scope '/subscriptions/sub'
            Set-AzStorageAccount -Name 'test' -MinimumTlsVersion TLS1_2
            """;

        _service.IsSafe(script).Should().BeTrue();
    }

    [Fact]
    public void IsSafe_MultiLineWithDestructive_ReturnsFalse()
    {
        var script = """
            az storage account update --name test --min-tls-version TLS1_2
            az group delete --name myrg --yes
            """;

        _service.IsSafe(script).Should().BeFalse();
    }

    // ─── GetViolations ────────────────────────────────────────────────────────

    [Fact]
    public void GetViolations_SafeScript_ReturnsEmptyList()
    {
        var script = "az storage account update --name test --min-tls-version TLS1_2";

        var violations = _service.GetViolations(script);

        violations.Should().BeEmpty();
    }

    [Fact]
    public void GetViolations_EmptyScript_ReturnsEmptyList()
    {
        var violations = _service.GetViolations("");

        violations.Should().BeEmpty();
    }

    [Fact]
    public void GetViolations_DestructiveCommand_ReturnsSpecificViolation()
    {
        var script = "az group delete --name myrg --yes";

        var violations = _service.GetViolations(script);

        violations.Should().HaveCount(1);
        violations[0].Should().Contain("resource group deletion");
    }

    [Fact]
    public void GetViolations_MultipleDestructive_ReturnsAllViolations()
    {
        var script = """
            az group delete --name myrg
            Remove-AzResourceGroup -Name 'rg'
            terraform destroy
            """;

        var violations = _service.GetViolations(script);

        violations.Should().HaveCountGreaterThanOrEqualTo(3);
    }

    [Fact]
    public void GetViolations_RemoveAzResource_ReturnsDescriptiveViolation()
    {
        var script = "Remove-AzResource -ResourceId '/subscriptions/sub'";

        var violations = _service.GetViolations(script);

        violations.Should().ContainSingle();
        violations[0].Should().Contain("resource removal");
    }
}
