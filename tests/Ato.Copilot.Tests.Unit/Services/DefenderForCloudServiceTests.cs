using Xunit;
using FluentAssertions;
using Ato.Copilot.Agents.Compliance.Services;

namespace Ato.Copilot.Tests.Unit.Services;

/// <summary>
/// Unit tests for DefenderForCloudService: NIST control mapping from recommendation names.
/// Note: ArmClient-based SDK calls require integration tests; we test the static mapper.
/// </summary>
public class DefenderForCloudServiceTests
{
    // ─── MapRecommendationToNistControls ────────────────────────────────────

    [Fact]
    public void MapRecommendation_EncryptionRelated_MapsToSC8()
    {
        var result = DefenderForCloudService.MapRecommendationToNistControls(
            "Transparent Data Encryption on SQL databases should be enabled");
        result.Should().Contain("SC-8");
    }

    [Fact]
    public void MapRecommendation_TlsRelated_MapsToSC8()
    {
        var result = DefenderForCloudService.MapRecommendationToNistControls(
            "Latest TLS version should be used in your web app");
        result.Should().Contain("SC-8");
    }

    [Fact]
    public void MapRecommendation_HttpsRelated_MapsToSC8()
    {
        var result = DefenderForCloudService.MapRecommendationToNistControls(
            "HTTPS should be enforced on function apps");
        result.Should().Contain("SC-8");
    }

    [Fact]
    public void MapRecommendation_MfaRelated_MapsToIA2()
    {
        var result = DefenderForCloudService.MapRecommendationToNistControls(
            "MFA should be enabled on accounts with write permissions");
        result.Should().Contain("IA-2");
    }

    [Fact]
    public void MapRecommendation_MultiFactor_MapsToIA2()
    {
        var result = DefenderForCloudService.MapRecommendationToNistControls(
            "Enable multi-factor authentication for all privileged accounts");
        result.Should().Contain("IA-2");
    }

    [Fact]
    public void MapRecommendation_AccessRbac_MapsToAC2()
    {
        var result = DefenderForCloudService.MapRecommendationToNistControls(
            "RBAC should be used to restrict access to Kubernetes clusters");
        result.Should().Contain("AC-2");
    }

    [Fact]
    public void MapRecommendation_AuditLog_MapsToAU3()
    {
        var result = DefenderForCloudService.MapRecommendationToNistControls(
            "Auditing on SQL server should be enabled");
        result.Should().Contain("AU-3");
    }

    [Fact]
    public void MapRecommendation_DiagnosticLog_MapsToAU3()
    {
        var result = DefenderForCloudService.MapRecommendationToNistControls(
            "Diagnostic logs should be enabled in Azure resources");
        result.Should().Contain("AU-3");
    }

    [Fact]
    public void MapRecommendation_Network_MapsToSC7()
    {
        var result = DefenderForCloudService.MapRecommendationToNistControls(
            "Network Security Groups should restrict traffic");
        result.Should().Contain("SC-7");
    }

    [Fact]
    public void MapRecommendation_Firewall_MapsToSC7()
    {
        var result = DefenderForCloudService.MapRecommendationToNistControls(
            "Azure Firewall should be deployed to protect virtual networks");
        result.Should().Contain("SC-7");
    }

    [Fact]
    public void MapRecommendation_Nsg_MapsToSC7()
    {
        var result = DefenderForCloudService.MapRecommendationToNistControls(
            "NSG rules should be hardened for internet facing VMs");
        result.Should().Contain("SC-7");
    }

    [Fact]
    public void MapRecommendation_Vulnerability_MapsToSI2()
    {
        var result = DefenderForCloudService.MapRecommendationToNistControls(
            "Vulnerability assessment solution should be installed on VMs");
        result.Should().Contain("SI-2");
    }

    [Fact]
    public void MapRecommendation_Patch_MapsToSI2()
    {
        var result = DefenderForCloudService.MapRecommendationToNistControls(
            "System updates should be installed on your machines");
        result.Should().Contain("SI-2");
    }

    [Fact]
    public void MapRecommendation_Backup_MapsToCP9()
    {
        var result = DefenderForCloudService.MapRecommendationToNistControls(
            "Azure Backup should be enabled for virtual machines");
        result.Should().Contain("CP-9");
    }

    [Fact]
    public void MapRecommendation_KeyVault_MapsToSC12()
    {
        var result = DefenderForCloudService.MapRecommendationToNistControls(
            "Key Vault keys should have an expiration date");
        result.Should().Contain("SC-12");
    }

    [Fact]
    public void MapRecommendation_Secret_MapsToSC12()
    {
        var result = DefenderForCloudService.MapRecommendationToNistControls(
            "Key Vault secrets should have an expiration date");
        result.Should().Contain("SC-12");
    }

    [Fact]
    public void MapRecommendation_MultipleMatches_ReturnAll()
    {
        // Contains both "encryption" and "access"
        var result = DefenderForCloudService.MapRecommendationToNistControls(
            "Require encryption for access to storage accounts");
        result.Should().Contain("SC-8"); // encryption
        result.Should().Contain("AC-2"); // access
    }

    [Fact]
    public void MapRecommendation_NoMatch_ReturnsEmpty()
    {
        var result = DefenderForCloudService.MapRecommendationToNistControls(
            "Some generic orphaned recommendation");
        result.Should().BeEmpty();
    }

    [Fact]
    public void MapRecommendation_CaseInsensitive()
    {
        var result = DefenderForCloudService.MapRecommendationToNistControls(
            "ENCRYPTION SHOULD BE ENABLED");
        result.Should().Contain("SC-8");
    }

    [Fact]
    public void MapRecommendation_EmptyString_ReturnsEmpty()
    {
        var result = DefenderForCloudService.MapRecommendationToNistControls("");
        result.Should().BeEmpty();
    }
}
