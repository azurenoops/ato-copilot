using Ato.Copilot.Agents.Common;
using Ato.Copilot.Agents.Compliance.Services;
using Ato.Copilot.Agents.Compliance.Services.KnowledgeBase;
using Ato.Copilot.Agents.Extensions;
using Ato.Copilot.Agents.KnowledgeBase.Services;
using Ato.Copilot.Core.Interfaces.Compliance;
using Ato.Copilot.Mcp.Server;
using Ato.Copilot.State.Extensions;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Ato.Copilot.Tests.Integration;

/// <summary>
/// Integration tests for orchestrator routing through the agent framework.
/// Validates that knowledge-intent messages route to KB agent via CanHandle scoring.
/// Uses lightweight DI container with only KnowledgeBase + Configuration agents.
/// </summary>
[Collection("IntegrationTests")]
public class OrchestratorRoutingIntegrationTests : IDisposable
{
    private readonly ServiceProvider _provider;
    private readonly AgentOrchestrator _orchestrator;

    public OrchestratorRoutingIntegrationTests()
    {
        var services = new ServiceCollection();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Agents:KnowledgeBaseAgent:Enabled"] = "true",
                ["Agents:KnowledgeBaseAgent:MinimumConfidenceThreshold"] = "0.3"
            })
            .Build();

        services.AddMemoryCache();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(config);
        services.AddHttpClient();
        services.AddInMemoryStateManagement();

        // Register KB service dependencies (normally from AddComplianceAgent)
        services.AddSingleton<INistControlsService, NistControlsService>();
        services.AddSingleton<IStigKnowledgeService, StigKnowledgeService>();
        services.AddSingleton<IRmfKnowledgeService, RmfKnowledgeService>();
        services.AddSingleton<IDoDInstructionService, DoDInstructionService>();
        services.AddSingleton<IDoDWorkflowService, DoDWorkflowService>();
        services.AddSingleton<IImpactLevelService, ImpactLevelService>();
        services.AddSingleton<IFedRampTemplateService, FedRampTemplateService>();

        // Register KB agent and tools
        services.AddKnowledgeBaseAgent(config);
        services.AddConfigurationAgent();
        services.AddSingleton<AgentOrchestrator>();

        _provider = services.BuildServiceProvider();
        _orchestrator = _provider.GetRequiredService<AgentOrchestrator>();
    }

    public void Dispose()
    {
        _provider.Dispose();
    }

    // ──────────────── Knowledge Intent Routing ────────────────

    [Theory]
    [InlineData("Explain NIST control AC-2")]
    [InlineData("What is STIG V-12345?")]
    [InlineData("Describe the RMF process")]
    [InlineData("What is impact level IL5?")]
    [InlineData("Show FedRAMP SSP template guidance")]
    public void KnowledgeIntent_Should_Route_To_KB_Agent(string message)
    {
        var agent = _orchestrator.SelectAgent(message);

        agent.Should().NotBeNull();
        agent!.AgentId.Should().Be("knowledgebase-agent");
    }

    // ──────────────── Non-Knowledge Intent ────────────────

    [Theory]
    [InlineData("configure my settings")]
    [InlineData("set subscription to my Azure subscription")]
    public void ConfigurationIntent_Should_Route_To_Configuration_Agent(string message)
    {
        var agent = _orchestrator.SelectAgent(message);

        agent.Should().NotBeNull();
        agent!.AgentId.Should().NotBe("knowledgebase-agent",
            "configuration keywords should route to configuration agent");
    }

    // ──────────────── Graceful Fallback ────────────────

    [Fact]
    public void UnrecognizedMessage_Should_Return_BestMatch_Or_Null()
    {
        var agent = _orchestrator.SelectAgent("hello world");

        // Either null or returns low-confidence agent — either way is acceptable
        // The key test is that it doesn't throw
        if (agent != null)
            agent.AgentId.Should().NotBeNullOrEmpty();
    }

    // ──────────────── KB Agent Scores Higher for Domain Terms ────────────────

    [Fact]
    public void KB_Agent_Should_Be_Discoverable_Via_DI()
    {
        var agents = _provider.GetServices<BaseAgent>().ToList();

        agents.Should().Contain(a => a.AgentId == "knowledgebase-agent");
    }

    [Fact]
    public void Orchestrator_Should_Resolve_Multiple_Agents()
    {
        var agents = _provider.GetServices<BaseAgent>().ToList();

        agents.Count.Should().BeGreaterThanOrEqualTo(2,
            "should have at least KB and Configuration agents");
    }
}
