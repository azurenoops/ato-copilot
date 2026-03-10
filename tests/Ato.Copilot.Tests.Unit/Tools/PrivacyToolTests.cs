using Ato.Copilot.Agents.Compliance.Tools;
using Ato.Copilot.Core.Interfaces.Compliance;
using Ato.Copilot.Core.Models.Auth;
using Ato.Copilot.Core.Models.Compliance;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Ato.Copilot.Tests.Unit.Tools;

/// <summary>
/// Unit tests for Privacy MCP tools — tool metadata, RBAC tier validation, and argument handling.
/// Feature 021 Task: T014.
/// </summary>
public class PrivacyToolTests
{
    private readonly Mock<IPrivacyService> _privacyService = new();

    // ─── CreatePtaTool ───────────────────────────────────────────────────────

    [Fact]
    public void CreatePtaTool_Name_IsCorrect()
    {
        var tool = new CreatePtaTool(_privacyService.Object, Mock.Of<ILogger<CreatePtaTool>>());
        tool.Name.Should().Be("compliance_create_pta");
    }

    [Fact]
    public void CreatePtaTool_Parameters_ContainsSystemId()
    {
        var tool = new CreatePtaTool(_privacyService.Object, Mock.Of<ILogger<CreatePtaTool>>());
        tool.Parameters.Should().ContainKey("system_id");
        tool.Parameters["system_id"].Required.Should().BeTrue();
    }

    [Fact]
    public void CreatePtaTool_RequiredPimTier_IsWrite()
    {
        var tool = new CreatePtaTool(_privacyService.Object, Mock.Of<ILogger<CreatePtaTool>>());
        tool.RequiredPimTier.Should().Be(PimTier.Write);
    }

    [Fact]
    public void CreatePtaTool_Parameters_IncludesManualModeFlags()
    {
        var tool = new CreatePtaTool(_privacyService.Object, Mock.Of<ILogger<CreatePtaTool>>());
        var keys = tool.Parameters.Keys;
        keys.Should().Contain("manual_mode");
        keys.Should().Contain("collects_pii");
        keys.Should().Contain("maintains_pii");
        keys.Should().Contain("disseminates_pii");
        keys.Should().Contain("pii_categories");
        keys.Should().Contain("estimated_record_count");
        keys.Should().Contain("exemption_rationale");
    }

    // ─── GeneratePiaTool ─────────────────────────────────────────────────────

    [Fact]
    public void GeneratePiaTool_Name_IsCorrect()
    {
        var tool = new GeneratePiaTool(_privacyService.Object, Mock.Of<ILogger<GeneratePiaTool>>());
        tool.Name.Should().Be("compliance_generate_pia");
    }

    [Fact]
    public void GeneratePiaTool_RequiredPimTier_IsWrite()
    {
        var tool = new GeneratePiaTool(_privacyService.Object, Mock.Of<ILogger<GeneratePiaTool>>());
        tool.RequiredPimTier.Should().Be(PimTier.Write);
    }

    [Fact]
    public void GeneratePiaTool_Parameters_RequiresOnlySystemId()
    {
        var tool = new GeneratePiaTool(_privacyService.Object, Mock.Of<ILogger<GeneratePiaTool>>());
        tool.Parameters.Should().ContainKey("system_id");
        tool.Parameters["system_id"].Required.Should().BeTrue();
        tool.Parameters.Should().HaveCount(1);
    }

    // ─── ReviewPiaTool ───────────────────────────────────────────────────────

    [Fact]
    public void ReviewPiaTool_Name_IsCorrect()
    {
        var tool = new ReviewPiaTool(_privacyService.Object, Mock.Of<ILogger<ReviewPiaTool>>());
        tool.Name.Should().Be("compliance_review_pia");
    }

    [Fact]
    public void ReviewPiaTool_RequiredPimTier_IsWrite()
    {
        var tool = new ReviewPiaTool(_privacyService.Object, Mock.Of<ILogger<ReviewPiaTool>>());
        tool.RequiredPimTier.Should().Be(PimTier.Write);
    }

    [Fact]
    public void ReviewPiaTool_Parameters_ContainsDecisionAndComments()
    {
        var tool = new ReviewPiaTool(_privacyService.Object, Mock.Of<ILogger<ReviewPiaTool>>());
        tool.Parameters.Should().ContainKey("system_id");
        tool.Parameters.Should().ContainKey("decision");
        tool.Parameters.Should().ContainKey("reviewer_comments");
        tool.Parameters["decision"].Required.Should().BeTrue();
        tool.Parameters["reviewer_comments"].Required.Should().BeTrue();
    }

    [Fact]
    public void ReviewPiaTool_Parameters_DeficienciesOptional()
    {
        var tool = new ReviewPiaTool(_privacyService.Object, Mock.Of<ILogger<ReviewPiaTool>>());
        tool.Parameters.Should().ContainKey("deficiencies");
        tool.Parameters["deficiencies"].Required.Should().BeFalse();
    }

    // ─── CheckPrivacyComplianceTool ──────────────────────────────────────────

    [Fact]
    public void CheckPrivacyComplianceTool_Name_IsCorrect()
    {
        var tool = new CheckPrivacyComplianceTool(_privacyService.Object, Mock.Of<ILogger<CheckPrivacyComplianceTool>>());
        tool.Name.Should().Be("compliance_check_privacy_compliance");
    }

    [Fact]
    public void CheckPrivacyComplianceTool_RequiredPimTier_IsRead()
    {
        var tool = new CheckPrivacyComplianceTool(_privacyService.Object, Mock.Of<ILogger<CheckPrivacyComplianceTool>>());
        tool.RequiredPimTier.Should().Be(PimTier.Read);
    }

    [Fact]
    public void CheckPrivacyComplianceTool_Parameters_RequiresOnlySystemId()
    {
        var tool = new CheckPrivacyComplianceTool(_privacyService.Object, Mock.Of<ILogger<CheckPrivacyComplianceTool>>());
        tool.Parameters.Should().ContainKey("system_id");
        tool.Parameters.Should().HaveCount(1);
    }

    // ─── RBAC Tier Validation (Cross-Tool) ───────────────────────────────────

    [Fact]
    public void WriteTools_RequireWritePimTier()
    {
        // PTA/PIA modification tools must require Write tier
        var createPta = new CreatePtaTool(_privacyService.Object, Mock.Of<ILogger<CreatePtaTool>>());
        var generatePia = new GeneratePiaTool(_privacyService.Object, Mock.Of<ILogger<GeneratePiaTool>>());
        var reviewPia = new ReviewPiaTool(_privacyService.Object, Mock.Of<ILogger<ReviewPiaTool>>());

        createPta.RequiredPimTier.Should().Be(PimTier.Write);
        generatePia.RequiredPimTier.Should().Be(PimTier.Write);
        reviewPia.RequiredPimTier.Should().Be(PimTier.Write);
    }

    [Fact]
    public void ReadOnlyTools_RequireReadPimTier()
    {
        // Compliance check is read-only — should require Read tier (not Write)
        var check = new CheckPrivacyComplianceTool(_privacyService.Object, Mock.Of<ILogger<CheckPrivacyComplianceTool>>());
        check.RequiredPimTier.Should().Be(PimTier.Read);
    }
}
