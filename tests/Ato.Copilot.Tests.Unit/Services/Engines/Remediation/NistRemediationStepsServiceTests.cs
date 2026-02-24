using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using FluentAssertions;
using Ato.Copilot.Agents.Compliance.Services.Engines.Remediation;

namespace Ato.Copilot.Tests.Unit.Services.Engines.Remediation;

/// <summary>
/// Unit tests for NistRemediationStepsService: curated steps lookup,
/// regex-based step parsing, action verb extraction, skill level mapping.
/// </summary>
public class NistRemediationStepsServiceTests
{
    private readonly NistRemediationStepsService _service;

    public NistRemediationStepsServiceTests()
    {
        var logger = new Mock<ILogger<NistRemediationStepsService>>();
        _service = new NistRemediationStepsService(logger.Object);
    }

    // ─── GetRemediationSteps ──────────────────────────────────────────────────

    [Theory]
    [InlineData("AC")]
    [InlineData("AU")]
    [InlineData("CM")]
    [InlineData("CP")]
    [InlineData("IA")]
    [InlineData("SC")]
    public void GetRemediationSteps_KnownFamilies_ReturnsCuratedSteps(string family)
    {
        var steps = _service.GetRemediationSteps(family, $"{family}-1");

        steps.Should().NotBeEmpty();
        steps.Should().HaveCountGreaterThanOrEqualTo(4);
    }

    [Fact]
    public void GetRemediationSteps_AcFamily_ReturnsAccessControlSteps()
    {
        var steps = _service.GetRemediationSteps("AC", "AC-2");

        steps.Should().Contain(s => s.Contains("RBAC") || s.Contains("access"));
        steps.Should().Contain(s => s.Contains("multi-factor") || s.Contains("MFA"));
    }

    [Fact]
    public void GetRemediationSteps_ScFamily_ReturnsCommunicationProtectionSteps()
    {
        var steps = _service.GetRemediationSteps("SC", "SC-8");

        steps.Should().Contain(s => s.Contains("TLS") || s.Contains("encryption"));
    }

    [Fact]
    public void GetRemediationSteps_CaseInsensitive_ReturnsSteps()
    {
        var upper = _service.GetRemediationSteps("AC", "AC-1");
        var lower = _service.GetRemediationSteps("ac", "ac-1");

        upper.Should().BeEquivalentTo(lower);
    }

    [Fact]
    public void GetRemediationSteps_UnknownFamily_ReturnsGenericFallback()
    {
        var steps = _service.GetRemediationSteps("ZZ", "ZZ-1");

        steps.Should().NotBeEmpty();
        steps.Should().HaveCount(4);
        steps.Should().Contain(s => s.Contains("ZZ-1"));
    }

    [Theory]
    [InlineData("IR")]
    [InlineData("MA")]
    [InlineData("MP")]
    [InlineData("PE")]
    [InlineData("PL")]
    [InlineData("PS")]
    [InlineData("RA")]
    [InlineData("SA")]
    [InlineData("SI")]
    public void GetRemediationSteps_AllFifteenFamilies_ReturnsCuratedSteps(string family)
    {
        var steps = _service.GetRemediationSteps(family, $"{family}-1");

        steps.Should().NotBeEmpty();
    }

    [Fact]
    public void GetRemediationSteps_ReturnsCopy_NotReference()
    {
        var steps1 = _service.GetRemediationSteps("AC", "AC-1");
        var steps2 = _service.GetRemediationSteps("AC", "AC-1");

        steps1.Should().BeEquivalentTo(steps2);
        steps1.Should().NotBeSameAs(steps2);
    }

    // ─── ParseStepsFromGuidance ───────────────────────────────────────────────

    [Fact]
    public void ParseStepsFromGuidance_NumberedSteps_ParsesCorrectly()
    {
        var guidance = "1. Enable MFA for all users\n2. Configure RBAC\n3. Review access policies";

        var steps = _service.ParseStepsFromGuidance(guidance);

        steps.Should().HaveCount(3);
        steps[0].Should().Contain("Enable MFA");
        steps[1].Should().Contain("Configure RBAC");
        steps[2].Should().Contain("Review access policies");
    }

    [Fact]
    public void ParseStepsFromGuidance_NumberedStepsWithParenthesis_ParsesCorrectly()
    {
        var guidance = "1) Enable logging\n2) Configure retention";

        var steps = _service.ParseStepsFromGuidance(guidance);

        steps.Should().HaveCount(2);
        steps[0].Should().Contain("Enable logging");
    }

    [Fact]
    public void ParseStepsFromGuidance_BulletedList_ParsesCorrectly()
    {
        var guidance = "- Enable TLS 1.2\n- Configure encryption\n* Deploy monitoring";

        var steps = _service.ParseStepsFromGuidance(guidance);

        steps.Should().HaveCountGreaterThanOrEqualTo(3);
    }

    [Fact]
    public void ParseStepsFromGuidance_ActionVerbs_ExtractsSteps()
    {
        var guidance = "Enable diagnostic logging on all resources. Configure log retention to 90 days. Verify audit collection.";

        var steps = _service.ParseStepsFromGuidance(guidance);

        steps.Should().NotBeEmpty();
        steps.Should().Contain(s => s.Contains("Enable"));
    }

    [Fact]
    public void ParseStepsFromGuidance_EmptyInput_ReturnsEmptyList()
    {
        var steps = _service.ParseStepsFromGuidance("");

        steps.Should().BeEmpty();
    }

    [Fact]
    public void ParseStepsFromGuidance_NullInput_ReturnsEmptyList()
    {
        var steps = _service.ParseStepsFromGuidance(null!);

        steps.Should().BeEmpty();
    }

    [Fact]
    public void ParseStepsFromGuidance_WhitespaceOnly_ReturnsEmptyList()
    {
        var steps = _service.ParseStepsFromGuidance("   \n  \t  ");

        steps.Should().BeEmpty();
    }

    [Fact]
    public void ParseStepsFromGuidance_NoDuplicates_DeduplicatesSteps()
    {
        var guidance = "1. Enable TLS\n- Enable TLS";

        var steps = _service.ParseStepsFromGuidance(guidance);

        steps.Should().NotBeEmpty();
        steps.Distinct().Should().HaveCount(steps.Count);
    }

    // ─── GetSkillLevel ────────────────────────────────────────────────────────

    [Theory]
    [InlineData("AC", "Intermediate")]
    [InlineData("AU", "Intermediate")]
    [InlineData("CM", "Intermediate")]
    [InlineData("CP", "Intermediate")]
    [InlineData("IA", "Advanced")]
    [InlineData("IR", "Advanced")]
    [InlineData("SC", "Advanced")]
    [InlineData("SA", "Advanced")]
    [InlineData("MA", "Beginner")]
    [InlineData("PE", "Beginner")]
    [InlineData("PL", "Beginner")]
    [InlineData("PS", "Beginner")]
    public void GetSkillLevel_KnownFamilies_ReturnsExpectedLevel(string family, string expected)
    {
        var level = _service.GetSkillLevel(family);

        level.Should().Be(expected);
    }

    [Fact]
    public void GetSkillLevel_CaseInsensitive_ReturnsCorrectLevel()
    {
        var upper = _service.GetSkillLevel("SC");
        var lower = _service.GetSkillLevel("sc");

        upper.Should().Be("Advanced");
        lower.Should().Be("Advanced");
    }

    [Fact]
    public void GetSkillLevel_UnknownFamily_ReturnsIntermediate()
    {
        var level = _service.GetSkillLevel("ZZ");

        level.Should().Be("Intermediate");
    }
}
