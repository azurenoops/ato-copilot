using Xunit;
using FluentAssertions;
using Moq;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Ato.Copilot.Agents.Common;
using Ato.Copilot.Agents.Configuration.Agents;
using Ato.Copilot.Agents.Configuration.Tools;
using Ato.Copilot.Core.Configuration;
using Ato.Copilot.Core.Models.Compliance;
using Ato.Copilot.State.Abstractions;

namespace Ato.Copilot.Tests.Unit.Agents;

/// <summary>
/// Tests for ConfigurationAgent AI-powered processing path (Feature 011).
/// Validates TryProcessWithAiAsync integration and degraded fallback.
/// </summary>
public class ConfigurationAgentAiTests
{
    private readonly Mock<IChatClient> _chatClientMock = new();
    private readonly Mock<IAgentStateManager> _stateMock = new();

    public ConfigurationAgentAiTests()
    {
        _stateMock.Setup(s => s.GetStateAsync<string>(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);
        _stateMock.Setup(s => s.GetStateAsync<ConfigurationSettings>(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ConfigurationSettings?)null);
        _stateMock.Setup(s => s.SetStateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<ConfigurationSettings>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _stateMock.Setup(s => s.SetStateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }

    private ConfigurationAgent CreateAgent(IChatClient? chatClient = null,
        AzureOpenAIGatewayOptions? aiOptions = null)
    {
        var tool = new ConfigurationTool(
            _stateMock.Object,
            Mock.Of<ILogger<ConfigurationTool>>());

        IOptions<AzureOpenAIGatewayOptions>? optionsWrapper =
            aiOptions != null ? Options.Create(aiOptions) : null;

        return new ConfigurationAgent(
            tool,
            Mock.Of<ILogger<ConfigurationAgent>>(),
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
        ConversationId = "test-config-ai",
        UserId = "test-user"
    };

    // ── AI-enabled path tests ────────────────────────────────────────────────

    [Fact]
    public async Task ProcessAsync_WithAiEnabled_ReturnsAiResponse()
    {
        _chatClientMock.Setup(c => c.GetResponseAsync(
                It.IsAny<IList<ChatMessage>>(), It.IsAny<ChatOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatResponse([new ChatMessage(ChatRole.Assistant, "Your subscription is set to sub-123.")]));

        var agent = CreateAgent(_chatClientMock.Object, CreateEnabledOptions());

        var result = await agent.ProcessAsync("show my settings", CreateContext());

        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Response.Should().Contain("subscription");
        result.AgentName.Should().Be("Configuration Agent");
    }

    [Fact]
    public async Task ProcessAsync_WithAiDisabled_FallsBackToClassifyIntent()
    {
        var agent = CreateAgent(); // No IChatClient

        var result = await agent.ProcessAsync("show settings", CreateContext());

        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.AgentName.Should().Be("Configuration Agent");
        // AI path not invoked
        _chatClientMock.Verify(c => c.GetResponseAsync(
            It.IsAny<IList<ChatMessage>>(), It.IsAny<ChatOptions>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ProcessAsync_AiFailure_FallsBackToClassifyIntent()
    {
        _chatClientMock.Setup(c => c.GetResponseAsync(
                It.IsAny<IList<ChatMessage>>(), It.IsAny<ChatOptions>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("503 Service Unavailable"));

        var agent = CreateAgent(_chatClientMock.Object, CreateEnabledOptions());

        var result = await agent.ProcessAsync("show settings", CreateContext());

        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.AgentName.Should().Be("Configuration Agent");
        // Should have fallen back to ClassifyIntent path
    }

    [Fact]
    public async Task ProcessAsync_AiDisabledFlag_FallsBackToClassifyIntent()
    {
        var options = new AzureOpenAIGatewayOptions { AgentAIEnabled = false };
        var agent = CreateAgent(_chatClientMock.Object, options);

        var result = await agent.ProcessAsync("show settings", CreateContext());

        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        // IChatClient provided but AgentAIEnabled=false, so AI path skipped
        _chatClientMock.Verify(c => c.GetResponseAsync(
            It.IsAny<IList<ChatMessage>>(), It.IsAny<ChatOptions>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
