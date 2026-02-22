using FluentAssertions;
using Xunit;
using Ato.Copilot.Agents.Compliance.Tools;
using Ato.Copilot.Core.Models.Auth;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;

namespace Ato.Copilot.Tests.Unit.Tools;

/// <summary>
/// Unit tests verifying every Auth/PIM tool class declares the correct RequiredPimTier
/// per contracts PIM Tier Classification table.
/// </summary>
public class PimTierDeclarationTests
{
    private readonly IServiceScopeFactory _scopeFactory;

    public PimTierDeclarationTests()
    {
        var services = new ServiceCollection();
        services.AddScoped<IServiceProvider>(sp => sp);
        var provider = services.BuildServiceProvider();
        _scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();
    }

    // ─── Tier 1 (PimTier.None) ───────────────────────────────────────────────

    [Fact]
    public void CacStatusTool_ShouldHavePimTierNone()
    {
        var tool = new CacStatusTool(_scopeFactory, new Mock<ILogger<CacStatusTool>>().Object);
        tool.RequiredPimTier.Should().Be(PimTier.None);
    }

    // ─── Tier 2a (PimTier.Read) ──────────────────────────────────────────────

    [Fact]
    public void PimListEligibleTool_ShouldHavePimTierRead()
    {
        var tool = new PimListEligibleTool(_scopeFactory, new Mock<ILogger<PimListEligibleTool>>().Object);
        tool.RequiredPimTier.Should().Be(PimTier.Read);
    }

    [Fact]
    public void PimListActiveTool_ShouldHavePimTierRead()
    {
        var tool = new PimListActiveTool(_scopeFactory, new Mock<ILogger<PimListActiveTool>>().Object);
        tool.RequiredPimTier.Should().Be(PimTier.Read);
    }

    [Fact]
    public void PimHistoryTool_ShouldHavePimTierRead()
    {
        var tool = new PimHistoryTool(_scopeFactory, new Mock<ILogger<PimHistoryTool>>().Object);
        tool.RequiredPimTier.Should().Be(PimTier.Read);
    }

    [Fact]
    public void JitListSessionsTool_ShouldHavePimTierRead()
    {
        var tool = new JitListSessionsTool(_scopeFactory, new Mock<ILogger<JitListSessionsTool>>().Object);
        tool.RequiredPimTier.Should().Be(PimTier.Read);
    }

    // ─── Tier 2b (PimTier.Write) ─────────────────────────────────────────────

    [Fact]
    public void PimActivateRoleTool_ShouldHavePimTierWrite()
    {
        var tool = new PimActivateRoleTool(_scopeFactory, new Mock<ILogger<PimActivateRoleTool>>().Object);
        tool.RequiredPimTier.Should().Be(PimTier.Write);
    }

    [Fact]
    public void PimDeactivateRoleTool_ShouldHavePimTierWrite()
    {
        var tool = new PimDeactivateRoleTool(_scopeFactory, new Mock<ILogger<PimDeactivateRoleTool>>().Object);
        tool.RequiredPimTier.Should().Be(PimTier.Write);
    }

    [Fact]
    public void PimExtendRoleTool_ShouldHavePimTierWrite()
    {
        var tool = new PimExtendRoleTool(_scopeFactory, new Mock<ILogger<PimExtendRoleTool>>().Object);
        tool.RequiredPimTier.Should().Be(PimTier.Write);
    }

    [Fact]
    public void PimApproveRequestTool_ShouldHavePimTierWrite()
    {
        var tool = new PimApproveRequestTool(_scopeFactory, new Mock<ILogger<PimApproveRequestTool>>().Object);
        tool.RequiredPimTier.Should().Be(PimTier.Write);
    }

    [Fact]
    public void PimDenyRequestTool_ShouldHavePimTierWrite()
    {
        var tool = new PimDenyRequestTool(_scopeFactory, new Mock<ILogger<PimDenyRequestTool>>().Object);
        tool.RequiredPimTier.Should().Be(PimTier.Write);
    }

    [Fact]
    public void JitRequestAccessTool_ShouldHavePimTierWrite()
    {
        var tool = new JitRequestAccessTool(_scopeFactory, new Mock<ILogger<JitRequestAccessTool>>().Object);
        tool.RequiredPimTier.Should().Be(PimTier.Write);
    }

    [Fact]
    public void JitRevokeAccessTool_ShouldHavePimTierWrite()
    {
        var tool = new JitRevokeAccessTool(_scopeFactory, new Mock<ILogger<JitRevokeAccessTool>>().Object);
        tool.RequiredPimTier.Should().Be(PimTier.Write);
    }

    [Fact]
    public void CacSignOutTool_ShouldHavePimTierWrite()
    {
        var tool = new CacSignOutTool(_scopeFactory, new Mock<ILogger<CacSignOutTool>>().Object);
        tool.RequiredPimTier.Should().Be(PimTier.Write);
    }

    [Fact]
    public void CacSetTimeoutTool_ShouldHavePimTierWrite()
    {
        var tool = new CacSetTimeoutTool(_scopeFactory, new Mock<ILogger<CacSetTimeoutTool>>().Object);
        tool.RequiredPimTier.Should().Be(PimTier.Write);
    }

    [Fact]
    public void CacMapCertificateTool_ShouldHavePimTierWrite()
    {
        var tool = new CacMapCertificateTool(_scopeFactory, new Mock<ILogger<CacMapCertificateTool>>().Object);
        tool.RequiredPimTier.Should().Be(PimTier.Write);
    }
}
