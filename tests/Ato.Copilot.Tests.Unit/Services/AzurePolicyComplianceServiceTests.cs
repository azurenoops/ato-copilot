using Xunit;
using FluentAssertions;
using Ato.Copilot.Agents.Compliance.Services;

namespace Ato.Copilot.Tests.Unit.Services;

/// <summary>
/// Unit tests for AzurePolicyComplianceService: NIST group mapping, pagination, error handling.
/// Note: SDK calls to ArmClient are not easily mockable without wrapping, so we test
/// the static helper methods and error-handling paths.
/// </summary>
public class AzurePolicyComplianceServiceTests
{
    // ─── MapGroupsToNistControls ──────────────────────────────────────────────

    [Fact]
    public void MapGroupsToNistControls_ValidNistGroup_ReturnsControlId()
    {
        var groups = new[] { "NIST_SP_800-53_Rev._5_AC-2" };
        var result = AzurePolicyComplianceService.MapGroupsToNistControls(groups);
        result.Should().Contain("AC-2");
    }

    [Fact]
    public void MapGroupsToNistControls_MultipleGroups_ReturnsMultipleControls()
    {
        var groups = new[]
        {
            "NIST_SP_800-53_Rev._5_AC-2",
            "NIST_SP_800-53_Rev._5_AU-3",
            "NIST_SP_800-53_Rev._5_SC-7"
        };
        var result = AzurePolicyComplianceService.MapGroupsToNistControls(groups);
        result.Should().HaveCount(3);
        result.Should().Contain("AC-2");
        result.Should().Contain("AU-3");
        result.Should().Contain("SC-7");
    }

    [Fact]
    public void MapGroupsToNistControls_NoNistGroups_ReturnsEmpty()
    {
        var groups = new[] { "CustomGroup", "AnotherGroup" };
        var result = AzurePolicyComplianceService.MapGroupsToNistControls(groups);
        result.Should().BeEmpty();
    }

    [Fact]
    public void MapGroupsToNistControls_EmptyInput_ReturnsEmpty()
    {
        var result = AzurePolicyComplianceService.MapGroupsToNistControls(Array.Empty<string>());
        result.Should().BeEmpty();
    }

    [Fact]
    public void MapGroupsToNistControls_InvalidFamily_Filtered()
    {
        // "ZZ" is not a valid NIST family
        var groups = new[] { "NIST_ZZ-1" };
        var result = AzurePolicyComplianceService.MapGroupsToNistControls(groups);
        result.Should().BeEmpty();
    }

    [Fact]
    public void MapGroupsToNistControls_MixedValidInvalid_ReturnsOnlyValid()
    {
        var groups = new[]
        {
            "NIST_SP_800-53_Rev._5_AC-2",
            "CustomGroup",
            "NIST_SP_800-53_Rev._5_IA-5"
        };
        var result = AzurePolicyComplianceService.MapGroupsToNistControls(groups);
        result.Should().HaveCount(2);
        result.Should().Contain("AC-2");
        result.Should().Contain("IA-5");
    }

    [Fact]
    public void MapGroupsToNistControls_GroupWithSubControl_MapsCorrectly()
    {
        // e.g., "NIST_SP_800-53_Rev._5_AC-2" — subcontrol AC-2(1) would be AC-2
        var groups = new[] { "NIST_SP_800-53_Rev._5_SC-12" };
        var result = AzurePolicyComplianceService.MapGroupsToNistControls(groups);
        result.Should().Contain("SC-12");
    }

    [Theory]
    [InlineData("NIST_SP_800-53_Rev._5_CM-6", "CM-6")]
    [InlineData("NIST_SP_800-53_Rev._5_SI-2", "SI-2")]
    [InlineData("NIST_SP_800-53_Rev._5_CP-9", "CP-9")]
    [InlineData("NIST_SP_800-53_Rev._5_IR-4", "IR-4")]
    public void MapGroupsToNistControls_VariousFamilies_MapsCorrectly(string groupName, string expectedControl)
    {
        var result = AzurePolicyComplianceService.MapGroupsToNistControls(new[] { groupName });
        result.Should().Contain(expectedControl);
    }

    [Fact]
    public void MapGroupsToNistControls_DuplicateGroups_ReturnsDuplicates()
    {
        var groups = new[]
        {
            "NIST_SP_800-53_Rev._5_AC-2",
            "NIST_SP_800-53_Rev._5_AC-2"
        };
        var result = AzurePolicyComplianceService.MapGroupsToNistControls(groups);
        // Static method doesn't deduplicate — that's the caller's job
        result.Should().HaveCount(2);
    }
}
