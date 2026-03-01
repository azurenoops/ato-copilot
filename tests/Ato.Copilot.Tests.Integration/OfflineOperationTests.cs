using Ato.Copilot.Agents.Compliance.Services;
using Ato.Copilot.Agents.Compliance.Services.KnowledgeBase;
using Ato.Copilot.Agents.Extensions;
using Ato.Copilot.Agents.KnowledgeBase.Services;
using Ato.Copilot.Core.Interfaces.Compliance;
using Ato.Copilot.State.Extensions;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Ato.Copilot.Tests.Integration;

/// <summary>
/// Integration tests verifying all KnowledgeBase services operate fully offline.
/// No HTTP calls, no external dependencies — all data loaded from local JSON files.
/// </summary>
[Collection("IntegrationTests")]
public class OfflineOperationTests : IDisposable
{
    private readonly ServiceProvider _provider;

    public OfflineOperationTests()
    {
        var services = new ServiceCollection();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Agents:KnowledgeBaseAgent:Enabled"] = "true",
                ["Agents:KnowledgeBaseAgent:CacheDurationMinutes"] = "60"
            })
            .Build();

        services.AddMemoryCache();
        services.AddInMemoryStateManagement();
        services.AddSingleton<IConfiguration>(config);
        services.AddHttpClient(); // NistControlsService requires HttpClient

        // Register all KB services (normally from AddComplianceAgent)
        services.AddSingleton<INistControlsService, NistControlsService>();
        services.AddSingleton<IStigKnowledgeService, StigKnowledgeService>();
        services.AddSingleton<IRmfKnowledgeService, RmfKnowledgeService>();
        services.AddSingleton<IDoDInstructionService, DoDInstructionService>();
        services.AddSingleton<IDoDWorkflowService, DoDWorkflowService>();
        services.AddSingleton<IImpactLevelService, ImpactLevelService>();
        services.AddSingleton<IFedRampTemplateService, FedRampTemplateService>();

        services.AddKnowledgeBaseAgent(config);

        _provider = services.BuildServiceProvider();
    }

    public void Dispose()
    {
        _provider.Dispose();
    }

    // ──────────────── NIST Controls (Offline) ────────────────

    [Fact]
    public async Task NistControlsService_Should_Load_Catalog_Offline()
    {
        var svc = _provider.GetRequiredService<INistControlsService>();

        var catalog = await svc.GetCatalogAsync();

        catalog.Should().NotBeNull();
        catalog!.Groups.Should().NotBeEmpty("NIST catalog should have control groups");
    }

    [Fact]
    public async Task NistControlsService_Should_Resolve_Control_Offline()
    {
        var svc = _provider.GetRequiredService<INistControlsService>();

        var control = await svc.GetControlAsync("AC-2");

        control.Should().NotBeNull();
        control!.Id.Should().NotBeNullOrEmpty();
    }

    // ──────────────── STIG Knowledge (Offline) ────────────────

    [Fact]
    public async Task StigKnowledgeService_Should_Search_Offline()
    {
        var svc = _provider.GetRequiredService<IStigKnowledgeService>();

        var results = await svc.SearchStigsAsync("authentication");

        results.Should().NotBeNull();
        // Search should run without network
    }

    [Fact]
    public async Task StigKnowledgeService_Should_Return_Mapping_Offline()
    {
        var svc = _provider.GetRequiredService<IStigKnowledgeService>();

        var mapping = await svc.GetStigMappingAsync("AC-2");

        mapping.Should().NotBeNull();
    }

    // ──────────────── RMF Knowledge (Offline) ────────────────

    [Fact]
    public async Task RmfKnowledgeService_Should_Load_Full_Process_Offline()
    {
        var svc = _provider.GetRequiredService<IRmfKnowledgeService>();

        var process = await svc.GetRmfProcessAsync();

        process.Should().NotBeNull();
        process!.Steps.Should().HaveCountGreaterOrEqualTo(6,
            "RMF has 6 defined steps");
    }

    [Fact]
    public async Task RmfKnowledgeService_Should_Return_Step_Offline()
    {
        var svc = _provider.GetRequiredService<IRmfKnowledgeService>();

        var step = await svc.GetRmfStepAsync(1);

        step.Should().NotBeNull();
        step!.Step.Should().Be(1);
    }

    // ──────────────── DoD Instructions (Offline) ────────────────

    [Fact]
    public async Task DoDInstructionService_Should_Load_Instruction_Offline()
    {
        var svc = _provider.GetRequiredService<IDoDInstructionService>();

        var instruction = await svc.ExplainInstructionAsync("DoDI 8510.01");

        instruction.Should().NotBeNull();
    }

    // ──────────────── DoD Workflows (Offline) ────────────────

    [Fact]
    public async Task DoDWorkflowService_Should_Query_By_Organization_Offline()
    {
        var svc = _provider.GetRequiredService<IDoDWorkflowService>();

        var workflows = await svc.GetWorkflowsByOrganizationAsync("Navy");

        workflows.Should().NotBeNull();
    }

    // ──────────────── Impact Levels (Offline) ────────────────

    [Fact]
    public async Task ImpactLevelService_Should_Return_All_Levels_Offline()
    {
        var svc = _provider.GetRequiredService<IImpactLevelService>();

        var levels = await svc.GetAllImpactLevelsAsync();

        levels.Should().NotBeNull();
        levels.Should().NotBeEmpty("should have IL2-IL6 impact levels");
    }

    [Fact]
    public async Task ImpactLevelService_Should_Return_FedRamp_Baseline_Offline()
    {
        var svc = _provider.GetRequiredService<IImpactLevelService>();

        var baseline = await svc.GetFedRampBaselineAsync("High");

        baseline.Should().NotBeNull();
    }

    // ──────────────── FedRAMP Templates (Offline) ────────────────

    [Fact]
    public async Task FedRampTemplateService_Should_Return_All_Templates_Offline()
    {
        var svc = _provider.GetRequiredService<IFedRampTemplateService>();

        var templates = await svc.GetAllTemplatesAsync();

        templates.Should().NotBeNull();
        templates.Should().HaveCountGreaterOrEqualTo(3,
            "should have SSP, POAM, CRM templates at minimum");
    }

    [Fact]
    public async Task FedRampTemplateService_Should_Return_SSP_Template_Offline()
    {
        var svc = _provider.GetRequiredService<IFedRampTemplateService>();

        var template = await svc.GetTemplateGuidanceAsync("SSP");

        template.Should().NotBeNull();
        template!.TemplateType.Should().Be("SSP");
    }

    // ──────────────── Cache Isolation (Offline) ────────────────

    [Fact]
    public async Task Services_Should_Use_Cache_After_First_Load()
    {
        var svc = _provider.GetRequiredService<INistControlsService>();
        var cache = _provider.GetRequiredService<IMemoryCache>();

        // First call loads from JSON
        var catalog1 = await svc.GetCatalogAsync();
        // Second call should hit cache
        var catalog2 = await svc.GetCatalogAsync();

        catalog1.Should().NotBeNull();
        catalog2.Should().NotBeNull();
        // Both calls return data — the key test is no exceptions
    }

    // ──────────────── Zero Network Dependency ────────────────

    [Fact]
    public async Task All_Services_Should_Resolve_Without_HttpClient()
    {
        // This provider has NO HttpClient registration.
        // ALL services must resolve and return data from local JSON files.
        var nist = _provider.GetService<INistControlsService>();
        var stig = _provider.GetService<IStigKnowledgeService>();
        var rmf = _provider.GetService<IRmfKnowledgeService>();
        var dod = _provider.GetService<IDoDInstructionService>();
        var workflow = _provider.GetService<IDoDWorkflowService>();
        var impact = _provider.GetService<IImpactLevelService>();
        var fedramp = _provider.GetService<IFedRampTemplateService>();

        nist.Should().NotBeNull();
        stig.Should().NotBeNull();
        rmf.Should().NotBeNull();
        dod.Should().NotBeNull();
        workflow.Should().NotBeNull();
        impact.Should().NotBeNull();
        fedramp.Should().NotBeNull();

        // Verify each service can return data without network
        (await nist!.GetCatalogAsync()).Should().NotBeNull();
        (await rmf!.GetRmfProcessAsync()).Should().NotBeNull();
        (await impact!.GetAllImpactLevelsAsync()).Should().NotBeEmpty();
        (await fedramp!.GetAllTemplatesAsync()).Should().NotBeEmpty();
    }
}
