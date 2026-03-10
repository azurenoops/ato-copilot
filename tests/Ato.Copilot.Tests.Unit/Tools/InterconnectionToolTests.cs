using Ato.Copilot.Agents.Compliance.Tools;
using Ato.Copilot.Core.Interfaces.Compliance;
using Ato.Copilot.Core.Models.Auth;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Ato.Copilot.Tests.Unit.Tools;

/// <summary>
/// Unit tests for Interconnection MCP tools — tool metadata, RBAC tier validation, and parameter keys.
/// Feature 021 Task: T027.
/// </summary>
public class InterconnectionToolTests
{
    private readonly Mock<IInterconnectionService> _interconnectionService = new();

    // ─── AddInterconnectionTool ──────────────────────────────────────────────

    [Fact]
    public void AddInterconnectionTool_Name_IsCorrect()
    {
        var tool = new AddInterconnectionTool(_interconnectionService.Object, Mock.Of<ILogger<AddInterconnectionTool>>());
        tool.Name.Should().Be("compliance_add_interconnection");
    }

    [Fact]
    public void AddInterconnectionTool_RequiredPimTier_IsWrite()
    {
        var tool = new AddInterconnectionTool(_interconnectionService.Object, Mock.Of<ILogger<AddInterconnectionTool>>());
        tool.RequiredPimTier.Should().Be(PimTier.Write);
    }

    [Fact]
    public void AddInterconnectionTool_Parameters_ContainsRequiredKeys()
    {
        var tool = new AddInterconnectionTool(_interconnectionService.Object, Mock.Of<ILogger<AddInterconnectionTool>>());
        var keys = tool.Parameters.Keys;
        keys.Should().Contain("system_id");
        keys.Should().Contain("target_system_name");
        keys.Should().Contain("connection_type");
        keys.Should().Contain("data_flow_direction");
        keys.Should().Contain("data_classification");
        tool.Parameters["system_id"].Required.Should().BeTrue();
        tool.Parameters["target_system_name"].Required.Should().BeTrue();
        tool.Parameters["connection_type"].Required.Should().BeTrue();
        tool.Parameters["data_flow_direction"].Required.Should().BeTrue();
        tool.Parameters["data_classification"].Required.Should().BeTrue();
    }

    [Fact]
    public void AddInterconnectionTool_Parameters_ContainsOptionalKeys()
    {
        var tool = new AddInterconnectionTool(_interconnectionService.Object, Mock.Of<ILogger<AddInterconnectionTool>>());
        var keys = tool.Parameters.Keys;
        keys.Should().Contain("target_system_owner");
        keys.Should().Contain("target_system_acronym");
        keys.Should().Contain("data_description");
        keys.Should().Contain("protocols");
        keys.Should().Contain("ports");
        keys.Should().Contain("security_measures");
        keys.Should().Contain("authentication_method");
    }

    // ─── ListInterconnectionsTool ────────────────────────────────────────────

    [Fact]
    public void ListInterconnectionsTool_Name_IsCorrect()
    {
        var tool = new ListInterconnectionsTool(_interconnectionService.Object, Mock.Of<ILogger<ListInterconnectionsTool>>());
        tool.Name.Should().Be("compliance_list_interconnections");
    }

    [Fact]
    public void ListInterconnectionsTool_RequiredPimTier_IsRead()
    {
        var tool = new ListInterconnectionsTool(_interconnectionService.Object, Mock.Of<ILogger<ListInterconnectionsTool>>());
        tool.RequiredPimTier.Should().Be(PimTier.Read);
    }

    [Fact]
    public void ListInterconnectionsTool_Parameters_ContainsExpectedKeys()
    {
        var tool = new ListInterconnectionsTool(_interconnectionService.Object, Mock.Of<ILogger<ListInterconnectionsTool>>());
        tool.Parameters.Should().ContainKey("system_id");
        tool.Parameters.Should().ContainKey("status_filter");
        tool.Parameters["system_id"].Required.Should().BeTrue();
        tool.Parameters["status_filter"].Required.Should().BeFalse();
    }

    // ─── UpdateInterconnectionTool ───────────────────────────────────────────

    [Fact]
    public void UpdateInterconnectionTool_Name_IsCorrect()
    {
        var tool = new UpdateInterconnectionTool(_interconnectionService.Object, Mock.Of<ILogger<UpdateInterconnectionTool>>());
        tool.Name.Should().Be("compliance_update_interconnection");
    }

    [Fact]
    public void UpdateInterconnectionTool_RequiredPimTier_IsWrite()
    {
        var tool = new UpdateInterconnectionTool(_interconnectionService.Object, Mock.Of<ILogger<UpdateInterconnectionTool>>());
        tool.RequiredPimTier.Should().Be(PimTier.Write);
    }

    [Fact]
    public void UpdateInterconnectionTool_Parameters_ContainsExpectedKeys()
    {
        var tool = new UpdateInterconnectionTool(_interconnectionService.Object, Mock.Of<ILogger<UpdateInterconnectionTool>>());
        var keys = tool.Parameters.Keys;
        keys.Should().Contain("interconnection_id");
        keys.Should().Contain("status");
        keys.Should().Contain("status_reason");
        keys.Should().Contain("connection_type");
        keys.Should().Contain("data_classification");
        keys.Should().Contain("protocols");
        keys.Should().Contain("ports");
        keys.Should().Contain("security_measures");
        tool.Parameters["interconnection_id"].Required.Should().BeTrue();
    }

    // ─── Cross-tool RBAC ─────────────────────────────────────────────────────

    [Fact]
    public void AllInterconnectionTools_RBAC_CorrectTiers()
    {
        var add = new AddInterconnectionTool(_interconnectionService.Object, Mock.Of<ILogger<AddInterconnectionTool>>());
        var list = new ListInterconnectionsTool(_interconnectionService.Object, Mock.Of<ILogger<ListInterconnectionsTool>>());
        var update = new UpdateInterconnectionTool(_interconnectionService.Object, Mock.Of<ILogger<UpdateInterconnectionTool>>());

        add.RequiredPimTier.Should().Be(PimTier.Write);
        list.RequiredPimTier.Should().Be(PimTier.Read);
        update.RequiredPimTier.Should().Be(PimTier.Write);
    }

    // ─── GenerateIsaTool (T037) ──────────────────────────────────────────────

    [Fact]
    public void GenerateIsaTool_Name_IsCorrect()
    {
        var tool = new GenerateIsaTool(_interconnectionService.Object, Mock.Of<ILogger<GenerateIsaTool>>());
        tool.Name.Should().Be("compliance_generate_isa");
    }

    [Fact]
    public void GenerateIsaTool_RequiredPimTier_IsWrite()
    {
        var tool = new GenerateIsaTool(_interconnectionService.Object, Mock.Of<ILogger<GenerateIsaTool>>());
        tool.RequiredPimTier.Should().Be(PimTier.Write);
    }

    [Fact]
    public void GenerateIsaTool_Parameters_ContainsInterconnectionId()
    {
        var tool = new GenerateIsaTool(_interconnectionService.Object, Mock.Of<ILogger<GenerateIsaTool>>());
        tool.Parameters.Should().ContainKey("interconnection_id");
        tool.Parameters["interconnection_id"].Required.Should().BeTrue();
    }

    // ─── RegisterAgreementTool ───────────────────────────────────────────────

    [Fact]
    public void RegisterAgreementTool_Name_IsCorrect()
    {
        var tool = new RegisterAgreementTool(_interconnectionService.Object, Mock.Of<ILogger<RegisterAgreementTool>>());
        tool.Name.Should().Be("compliance_register_agreement");
    }

    [Fact]
    public void RegisterAgreementTool_RequiredPimTier_IsWrite()
    {
        var tool = new RegisterAgreementTool(_interconnectionService.Object, Mock.Of<ILogger<RegisterAgreementTool>>());
        tool.RequiredPimTier.Should().Be(PimTier.Write);
    }

    [Fact]
    public void RegisterAgreementTool_Parameters_ContainsRequiredKeys()
    {
        var tool = new RegisterAgreementTool(_interconnectionService.Object, Mock.Of<ILogger<RegisterAgreementTool>>());
        var keys = tool.Parameters.Keys;
        keys.Should().Contain("interconnection_id");
        keys.Should().Contain("agreement_type");
        keys.Should().Contain("title");
        tool.Parameters["interconnection_id"].Required.Should().BeTrue();
        tool.Parameters["agreement_type"].Required.Should().BeTrue();
        tool.Parameters["title"].Required.Should().BeTrue();
    }

    // ─── UpdateAgreementTool ─────────────────────────────────────────────────

    [Fact]
    public void UpdateAgreementTool_Name_IsCorrect()
    {
        var tool = new UpdateAgreementTool(_interconnectionService.Object, Mock.Of<ILogger<UpdateAgreementTool>>());
        tool.Name.Should().Be("compliance_update_agreement");
    }

    [Fact]
    public void UpdateAgreementTool_RequiredPimTier_IsWrite()
    {
        var tool = new UpdateAgreementTool(_interconnectionService.Object, Mock.Of<ILogger<UpdateAgreementTool>>());
        tool.RequiredPimTier.Should().Be(PimTier.Write);
    }

    // ─── CertifyNoInterconnectionsTool ───────────────────────────────────────

    [Fact]
    public void CertifyNoInterconnectionsTool_Name_IsCorrect()
    {
        var tool = new CertifyNoInterconnectionsTool(_interconnectionService.Object, Mock.Of<ILogger<CertifyNoInterconnectionsTool>>());
        tool.Name.Should().Be("compliance_certify_no_interconnections");
    }

    [Fact]
    public void CertifyNoInterconnectionsTool_RequiredPimTier_IsWrite()
    {
        var tool = new CertifyNoInterconnectionsTool(_interconnectionService.Object, Mock.Of<ILogger<CertifyNoInterconnectionsTool>>());
        tool.RequiredPimTier.Should().Be(PimTier.Write);
    }

    [Fact]
    public void CertifyNoInterconnectionsTool_Parameters_ContainsExpectedKeys()
    {
        var tool = new CertifyNoInterconnectionsTool(_interconnectionService.Object, Mock.Of<ILogger<CertifyNoInterconnectionsTool>>());
        tool.Parameters.Should().ContainKey("system_id");
        tool.Parameters.Should().ContainKey("certify");
        tool.Parameters["system_id"].Required.Should().BeTrue();
        tool.Parameters["certify"].Required.Should().BeTrue();
    }

    // ─── ValidateAgreementsTool ──────────────────────────────────────────────

    [Fact]
    public void ValidateAgreementsTool_Name_IsCorrect()
    {
        var tool = new ValidateAgreementsTool(_interconnectionService.Object, Mock.Of<ILogger<ValidateAgreementsTool>>());
        tool.Name.Should().Be("compliance_validate_agreements");
    }

    [Fact]
    public void ValidateAgreementsTool_RequiredPimTier_IsRead()
    {
        var tool = new ValidateAgreementsTool(_interconnectionService.Object, Mock.Of<ILogger<ValidateAgreementsTool>>());
        tool.RequiredPimTier.Should().Be(PimTier.Read);
    }

    // ─── Cross-tool RBAC (all Phase 5 tools) ────────────────────────────────

    [Fact]
    public void AllAgreementTools_RBAC_CorrectTiers()
    {
        var genIsa = new GenerateIsaTool(_interconnectionService.Object, Mock.Of<ILogger<GenerateIsaTool>>());
        var regAgreement = new RegisterAgreementTool(_interconnectionService.Object, Mock.Of<ILogger<RegisterAgreementTool>>());
        var updAgreement = new UpdateAgreementTool(_interconnectionService.Object, Mock.Of<ILogger<UpdateAgreementTool>>());
        var certify = new CertifyNoInterconnectionsTool(_interconnectionService.Object, Mock.Of<ILogger<CertifyNoInterconnectionsTool>>());
        var validate = new ValidateAgreementsTool(_interconnectionService.Object, Mock.Of<ILogger<ValidateAgreementsTool>>());

        genIsa.RequiredPimTier.Should().Be(PimTier.Write);
        regAgreement.RequiredPimTier.Should().Be(PimTier.Write);
        updAgreement.RequiredPimTier.Should().Be(PimTier.Write);
        certify.RequiredPimTier.Should().Be(PimTier.Write);
        validate.RequiredPimTier.Should().Be(PimTier.Read);
    }
}
