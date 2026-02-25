using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Ato.Copilot.Agents.Compliance.Services;
using Ato.Copilot.Agents.Compliance.Services.KnowledgeBase;
using Ato.Copilot.Agents.Extensions;
using Ato.Copilot.Agents.KnowledgeBase.Services;
using Ato.Copilot.Core.Interfaces.Compliance;
using Ato.Copilot.Mcp.Tools;
using Ato.Copilot.State.Extensions;
using FluentAssertions;
using Xunit;

namespace Ato.Copilot.Tests.Integration;

/// <summary>
/// Integration tests for KnowledgeBase MCP tools.
/// Tests the MCP tool → KB agent tool → KB service pipeline end-to-end
/// using a lightweight DI container with real services (no HTTP, no mocks).
/// </summary>
[Collection("IntegrationTests")]
public class KnowledgeBaseMcpToolEndpointTests
{
    private readonly KnowledgeBaseMcpTools _mcpTools;

    public KnowledgeBaseMcpToolEndpointTests()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Agents:Compliance:NistControls:CatalogUrl"] = "https://raw.githubusercontent.com/usnistgov/oscal-content/main/nist.gov/SP800-53/rev5/json/NIST_SP-800-53_rev5_catalog.json",
                ["Agents:Compliance:NistControls:CacheExpirationMinutes"] = "60"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddMemoryCache();
        services.AddLogging(b => b.AddConsole().SetMinimumLevel(LogLevel.Warning));
        services.AddSingleton<IConfiguration>(config);
        services.AddHttpClient();
        services.AddInMemoryStateManagement();

        // Register KB services (same registrations as AddComplianceAgent)
        services.AddSingleton<INistControlsService, NistControlsService>();
        services.AddSingleton<IStigKnowledgeService, StigKnowledgeService>();
        services.AddSingleton<IRmfKnowledgeService, RmfKnowledgeService>();
        services.AddSingleton<IDoDInstructionService, DoDInstructionService>();
        services.AddSingleton<IDoDWorkflowService, DoDWorkflowService>();
        services.AddSingleton<IImpactLevelService, ImpactLevelService>();
        services.AddSingleton<IFedRampTemplateService, FedRampTemplateService>();

        // Register KB tools + MCP wrapper
        services.AddKnowledgeBaseAgent(config);
        services.AddSingleton<KnowledgeBaseMcpTools>();

        var sp = services.BuildServiceProvider();
        _mcpTools = sp.GetRequiredService<KnowledgeBaseMcpTools>();
    }

    // ──────────────── MCP Tool Resolution ────────────────

    [Fact]
    public void KnowledgeBaseMcpTools_Should_Resolve_From_DI()
    {
        _mcpTools.Should().NotBeNull();
    }

    // ──────────────── NIST Control Tools ────────────────

    [Fact]
    public async Task ExplainNistControl_Should_Return_Control_Details()
    {
        var result = await _mcpTools.ExplainNistControlAsync("AC-2");

        result.Should().NotBeNull();
        result.Should().Contain("AC-2");
    }

    [Fact]
    public async Task SearchNistControls_Should_Return_Matching_Controls()
    {
        var result = await _mcpTools.SearchNistControlsAsync("access", null, null);

        result.Should().NotBeNull();
        result.Should().NotBeEmpty();
    }

    // ──────────────── STIG Tools ────────────────

    [Fact]
    public async Task ExplainStig_Should_Return_Rule_Details()
    {
        var result = await _mcpTools.ExplainStigAsync("V-220706");

        result.Should().NotBeNull();
    }

    [Fact]
    public async Task SearchStigs_Should_Return_Matching_Rules()
    {
        var result = await _mcpTools.SearchStigsAsync("password", null, null);

        result.Should().NotBeNull();
    }

    // ──────────────── RMF Tool ────────────────

    [Fact]
    public async Task ExplainRmf_Overview_Should_Return_Process()
    {
        var result = await _mcpTools.ExplainRmfAsync("overview", null, null, null);

        result.Should().NotBeNull();
        result.Should().Contain("RMF");
    }

    // ──────────────── Impact Level Tool ────────────────

    [Fact]
    public async Task ExplainImpactLevel_Should_Return_Level_Details()
    {
        var result = await _mcpTools.ExplainImpactLevelAsync("IL5");

        result.Should().NotBeNull();
        result.Should().Contain("IL5");
    }

    // ──────────────── FedRAMP Template Tool ────────────────

    [Fact]
    public async Task GetFedRampTemplateGuidance_SSP_Should_Return_Template()
    {
        var result = await _mcpTools.GetFedRampTemplateGuidanceAsync("SSP", null);

        result.Should().NotBeNull();
        result.Should().Contain("SSP");
    }

    [Fact]
    public async Task GetFedRampTemplateGuidance_NoType_Should_Return_Overview()
    {
        var result = await _mcpTools.GetFedRampTemplateGuidanceAsync(null, null);

        result.Should().NotBeNull();
        result.Should().Contain("Package Overview");
    }
}
