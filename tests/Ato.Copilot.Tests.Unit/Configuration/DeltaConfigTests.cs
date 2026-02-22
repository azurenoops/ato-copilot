using FluentAssertions;
using Xunit;
using Ato.Copilot.Core.Configuration;
using Ato.Copilot.Core.Models.Auth;

namespace Ato.Copilot.Tests.Unit.Configuration;

/// <summary>
/// Unit tests for delta configuration types: PimTier enum, RetentionPolicyOptions defaults,
/// PimServiceOptions updated defaults, and AuthTierClassification Tier 2a/2b classification.
/// </summary>
public class DeltaConfigTests
{
    // ─── PimTier Enum Tests ──────────────────────────────────────────────────

    [Fact]
    public void PimTier_None_ShouldBeZero()
    {
        ((int)PimTier.None).Should().Be(0);
    }

    [Fact]
    public void PimTier_Read_ShouldBeOne()
    {
        ((int)PimTier.Read).Should().Be(1);
    }

    [Fact]
    public void PimTier_Write_ShouldBeTwo()
    {
        ((int)PimTier.Write).Should().Be(2);
    }

    [Fact]
    public void PimTier_ShouldHaveExactlyThreeValues()
    {
        Enum.GetValues<PimTier>().Should().HaveCount(3);
    }

    [Fact]
    public void PimTier_ReadLessThanWrite()
    {
        ((int)PimTier.Read).Should().BeLessThan((int)PimTier.Write);
    }

    // ─── RetentionPolicyOptions Tests ────────────────────────────────────────

    [Fact]
    public void RetentionPolicyOptions_DefaultAssessmentRetentionDays_ShouldBe1095()
    {
        var options = new RetentionPolicyOptions();
        options.AssessmentRetentionDays.Should().Be(1095, "3-year minimum per FR-042");
    }

    [Fact]
    public void RetentionPolicyOptions_DefaultAuditLogRetentionDays_ShouldBe2555()
    {
        var options = new RetentionPolicyOptions();
        options.AuditLogRetentionDays.Should().Be(2555, "7-year minimum per FR-043");
    }

    [Fact]
    public void RetentionPolicyOptions_DefaultCleanupIntervalHours_ShouldBe24()
    {
        var options = new RetentionPolicyOptions();
        options.CleanupIntervalHours.Should().Be(24, "daily cleanup by default");
    }

    [Fact]
    public void RetentionPolicyOptions_DefaultEnableAutomaticCleanup_ShouldBeTrue()
    {
        var options = new RetentionPolicyOptions();
        options.EnableAutomaticCleanup.Should().BeTrue();
    }

    [Fact]
    public void RetentionPolicyOptions_SectionName_ShouldBeRetention()
    {
        RetentionPolicyOptions.SectionName.Should().Be("Retention");
    }

    // ─── PimServiceOptions Updated Defaults ──────────────────────────────────

    [Fact]
    public void PimServiceOptions_DefaultActivationDuration_ShouldBe4Hours()
    {
        var options = new PimServiceOptions();
        options.DefaultActivationDurationHours.Should().Be(4, "4-hour default per FR-010");
    }

    [Fact]
    public void PimServiceOptions_MaxActivationDuration_ShouldBe8Hours()
    {
        var options = new PimServiceOptions();
        options.MaxActivationDurationHours.Should().Be(8, "8-hour maximum per FR-010");
    }

    // ─── AuthTierClassification Tier 2a Tests ────────────────────────────────

    [Theory]
    [InlineData("pim_list_eligible")]
    [InlineData("pim_list_active")]
    [InlineData("pim_history")]
    [InlineData("jit_list_sessions")]
    [InlineData("run_assessment")]
    [InlineData("collect_evidence")]
    [InlineData("discover_resources")]
    [InlineData("compliance_assess")]
    [InlineData("compliance_collect_evidence")]
    [InlineData("compliance_monitoring")]
    public void AuthTierClassification_Tier2aTools_ShouldBeClassifiedAsTier2a(string toolName)
    {
        AuthTierClassification.IsTier2a(toolName).Should().BeTrue();
        AuthTierClassification.IsTier2b(toolName).Should().BeFalse();
        AuthTierClassification.IsTier2(toolName).Should().BeTrue();
        AuthTierClassification.GetRequiredPimTier(toolName).Should().Be(PimTier.Read);
    }

    // ─── AuthTierClassification Tier 2b Tests ────────────────────────────────

    [Theory]
    [InlineData("pim_activate_role")]
    [InlineData("pim_deactivate_role")]
    [InlineData("pim_extend_role")]
    [InlineData("pim_approve_request")]
    [InlineData("pim_deny_request")]
    [InlineData("jit_request_access")]
    [InlineData("jit_revoke_access")]
    [InlineData("cac_sign_out")]
    [InlineData("cac_set_timeout")]
    [InlineData("cac_map_certificate")]
    [InlineData("execute_remediation")]
    [InlineData("validate_remediation")]
    [InlineData("deploy_template")]
    [InlineData("compliance_remediate")]
    [InlineData("compliance_validate_remediation")]
    [InlineData("kanban_remediate_task")]
    [InlineData("kanban_validate_task")]
    [InlineData("kanban_collect_evidence")]
    public void AuthTierClassification_Tier2bTools_ShouldBeClassifiedAsTier2b(string toolName)
    {
        AuthTierClassification.IsTier2b(toolName).Should().BeTrue();
        AuthTierClassification.IsTier2a(toolName).Should().BeFalse();
        AuthTierClassification.IsTier2(toolName).Should().BeTrue();
        AuthTierClassification.GetRequiredPimTier(toolName).Should().Be(PimTier.Write);
    }

    // ─── AuthTierClassification Tier 1 Tests ─────────────────────────────────

    [Theory]
    [InlineData("cac_status")]
    [InlineData("compliance_chat")]
    [InlineData("unknown_tool")]
    [InlineData("")]
    public void AuthTierClassification_Tier1Tools_ShouldNotBeTier2(string toolName)
    {
        AuthTierClassification.IsTier2(toolName).Should().BeFalse();
        AuthTierClassification.IsTier2a(toolName).Should().BeFalse();
        AuthTierClassification.IsTier2b(toolName).Should().BeFalse();
        AuthTierClassification.GetRequiredPimTier(toolName).Should().Be(PimTier.None);
    }

    [Fact]
    public void AuthTierClassification_NullToolName_ShouldReturnTier1()
    {
        AuthTierClassification.IsTier2(null!).Should().BeFalse();
        AuthTierClassification.GetRequiredPimTier(null!).Should().Be(PimTier.None);
    }

    // ─── Case Insensitivity Tests ────────────────────────────────────────────

    [Theory]
    [InlineData("PIM_LIST_ELIGIBLE")]
    [InlineData("Pim_List_Eligible")]
    [InlineData("pim_list_eligible")]
    public void AuthTierClassification_ShouldBeCaseInsensitive(string toolName)
    {
        AuthTierClassification.IsTier2a(toolName).Should().BeTrue();
    }
}
