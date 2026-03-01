using Xunit;
using FluentAssertions;
using Moq;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Ato.Copilot.Agents.Common;
using Ato.Copilot.Agents.KnowledgeBase.Agents;
using Ato.Copilot.Agents.KnowledgeBase.Configuration;
using Ato.Copilot.Agents.KnowledgeBase.Tools;
using Ato.Copilot.Core.Configuration;
using Ato.Copilot.Core.Interfaces.Compliance;
using Ato.Copilot.State.Abstractions;

namespace Ato.Copilot.Tests.Unit.Agents;

/// <summary>
/// Tests for KnowledgeBaseAgent AI-powered processing path (Feature 011).
/// Validates TryProcessWithAiAsync integration and degraded fallback.
/// </summary>
public class KnowledgeBaseAgentAiTests
{
    private readonly Mock<IChatClient> _chatClientMock = new();
    private readonly Mock<IAgentStateManager> _stateManagerMock = new();

    public KnowledgeBaseAgentAiTests()
    {
        _stateManagerMock.Setup(s => s.SetStateAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }

    private KnowledgeBaseAgent CreateAgent(IChatClient? chatClient = null,
        AzureOpenAIGatewayOptions? aiOptions = null)
    {
        var options = Options.Create(new KnowledgeBaseAgentOptions());
        var cache = new MemoryCache(new MemoryCacheOptions());
        var toolOptions = Options.Create(new KnowledgeBaseAgentOptions());
        var nistService = Mock.Of<INistControlsService>();
        var stigService = Mock.Of<IStigKnowledgeService>();
        var rmfService = Mock.Of<IRmfKnowledgeService>();
        var dodInstructionService = Mock.Of<IDoDInstructionService>();
        var dodWorkflowService = Mock.Of<IDoDWorkflowService>();
        var impactLevelService = Mock.Of<IImpactLevelService>();
        var fedRampTemplateService = Mock.Of<IFedRampTemplateService>();

        var explainNist = new ExplainNistControlTool(nistService, cache, toolOptions, Mock.Of<ILogger<ExplainNistControlTool>>());
        var searchNist = new SearchNistControlsTool(nistService, cache, toolOptions, Mock.Of<ILogger<SearchNistControlsTool>>());
        var explainStig = new ExplainStigTool(stigService, cache, toolOptions, Mock.Of<ILogger<ExplainStigTool>>());
        var searchStigs = new SearchStigsTool(stigService, cache, toolOptions, Mock.Of<ILogger<SearchStigsTool>>());
        var explainRmf = new ExplainRmfTool(rmfService, dodInstructionService, dodWorkflowService, cache, toolOptions, Mock.Of<ILogger<ExplainRmfTool>>());
        var explainImpactLevel = new ExplainImpactLevelTool(impactLevelService, cache, toolOptions, Mock.Of<ILogger<ExplainImpactLevelTool>>());
        var getFedRampTemplate = new GetFedRampTemplateGuidanceTool(fedRampTemplateService, cache, toolOptions, Mock.Of<ILogger<GetFedRampTemplateGuidanceTool>>());

        IOptions<AzureOpenAIGatewayOptions>? optionsWrapper =
            aiOptions != null ? Options.Create(aiOptions) : null;

        return new KnowledgeBaseAgent(
            options,
            _stateManagerMock.Object,
            explainNist, searchNist,
            explainStig, searchStigs,
            explainRmf, explainImpactLevel,
            getFedRampTemplate,
            Mock.Of<ILogger<KnowledgeBaseAgent>>(),
            chatClient,
            optionsWrapper);
    }

    private static AzureOpenAIGatewayOptions CreateEnabledOptions() => new()
    {
        AgentAIEnabled = true,
        MaxToolCallRounds = 5,
        Temperature = 0.3,
        Endpoint = "https://test.openai.azure.us/"
    };

    private static AgentConversationContext CreateContext() => new()
    {
        ConversationId = "test-kb-ai",
        UserId = "test-user"
    };

    // ── AI-enabled path tests ────────────────────────────────────────────────

    [Fact]
    public async Task ProcessAsync_WithAiEnabled_ReturnsAiResponse()
    {
        _chatClientMock.Setup(c => c.GetResponseAsync(
                It.IsAny<IList<ChatMessage>>(), It.IsAny<ChatOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatResponse([new ChatMessage(ChatRole.Assistant, "NIST AC-2 covers Account Management.")]));

        var agent = CreateAgent(_chatClientMock.Object, CreateEnabledOptions());

        var result = await agent.ProcessAsync("explain NIST AC-2", CreateContext());

        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Response.Should().Contain("AC-2");
        result.AgentName.Should().Be("KnowledgeBase Agent");
    }

    [Fact]
    public async Task ProcessAsync_WithAiDisabled_FallsBackToAnalyzeQueryType()
    {
        var agent = CreateAgent(); // No IChatClient

        var result = await agent.ProcessAsync("explain NIST AC-2", CreateContext());

        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.AgentName.Should().Be("KnowledgeBase Agent");
        // AI path not invoked
        _chatClientMock.Verify(c => c.GetResponseAsync(
            It.IsAny<IList<ChatMessage>>(), It.IsAny<ChatOptions>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ProcessAsync_AiFailure_FallsBackToAnalyzeQueryType()
    {
        _chatClientMock.Setup(c => c.GetResponseAsync(
                It.IsAny<IList<ChatMessage>>(), It.IsAny<ChatOptions>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Azure OpenAI unavailable"));

        var agent = CreateAgent(_chatClientMock.Object, CreateEnabledOptions());

        var result = await agent.ProcessAsync("explain NIST AC-2", CreateContext());

        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.AgentName.Should().Be("KnowledgeBase Agent");
        // Should have fallen back to deterministic AnalyzeQueryType path
    }

    [Fact]
    public async Task ProcessAsync_AiResponse_StoresQueryState()
    {
        _chatClientMock.Setup(c => c.GetResponseAsync(
                It.IsAny<IList<ChatMessage>>(), It.IsAny<ChatOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatResponse([new ChatMessage(ChatRole.Assistant, "AI-generated explanation")]));

        var agent = CreateAgent(_chatClientMock.Object, CreateEnabledOptions());

        await agent.ProcessAsync("what is NIST 800-53", CreateContext());

        // Verify that StoreQueryStateAsync was called (state manager interaction)
        _stateManagerMock.Verify(s => s.SetStateAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object>(), It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);
    }
}
