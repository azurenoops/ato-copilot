using Xunit;
using FluentAssertions;
using Moq;
using Microsoft.Extensions.Logging;
using Ato.Copilot.Agents.Configuration.Agents;
using Ato.Copilot.Agents.Configuration.Tools;
using Ato.Copilot.Agents.Common;
using Ato.Copilot.Core.Models.Compliance;
using Ato.Copilot.State.Abstractions;

namespace Ato.Copilot.Tests.Unit.Agents;

public class ConfigurationAgentTests
{
    private readonly ConfigurationAgent _agent;
    private readonly ConfigurationTool _tool;
    private readonly Mock<IAgentStateManager> _stateMock;

    public ConfigurationAgentTests()
    {
        _stateMock = new Mock<IAgentStateManager>();
        _stateMock.Setup(s => s.GetStateAsync<string>(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);
        _stateMock.Setup(s => s.GetStateAsync<ConfigurationSettings>(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ConfigurationSettings?)null);
        _stateMock.Setup(s => s.SetStateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<ConfigurationSettings>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _stateMock.Setup(s => s.SetStateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _tool = new ConfigurationTool(
            _stateMock.Object,
            Mock.Of<ILogger<ConfigurationTool>>());

        _agent = new ConfigurationAgent(
            _tool,
            Mock.Of<ILogger<ConfigurationAgent>>());
    }

    [Fact]
    public void Agent_Should_Have_Correct_Identity()
    {
        _agent.AgentId.Should().Be("configuration-agent");
        _agent.AgentName.Should().Be("Configuration Agent");
    }

    [Fact]
    public void Agent_Should_Have_Description()
    {
        _agent.Description.Should().NotBeNullOrEmpty();
        _agent.Description.Should().Contain("settings", because: "agent manages configuration settings");
    }

    [Fact]
    public void Agent_Should_Have_System_Prompt()
    {
        var prompt = _agent.GetSystemPrompt();
        prompt.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task ProcessAsync_ShowSettings_ShouldReturnResponse()
    {
        var context = new AgentConversationContext
        {
            ConversationId = "test-config-1",
            UserId = "test-user"
        };

        var result = await _agent.ProcessAsync("show settings", context);

        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.AgentName.Should().Be("Configuration Agent");
        result.ToolsExecuted.Should().HaveCount(1);
        result.ToolsExecuted[0].ToolName.Should().Be("configuration_manage");
    }

    [Fact]
    public async Task ProcessAsync_SetSubscription_ShouldRouteCorrectly()
    {
        var context = new AgentConversationContext
        {
            ConversationId = "test-config-2",
            UserId = "test-user"
        };

        var result = await _agent.ProcessAsync(
            "set subscription 00000000-0000-0000-0000-000000000001", context);

        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Response.Should().Contain("subscriptionId");
    }

    [Fact]
    public async Task ProcessAsync_SetFramework_ShouldRouteCorrectly()
    {
        var context = new AgentConversationContext
        {
            ConversationId = "test-config-3",
            UserId = "test-user"
        };

        var result = await _agent.ProcessAsync("use fedramp high", context);

        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
    }

    [Fact]
    public async Task ProcessAsync_ShouldTrackProcessingTime()
    {
        var context = new AgentConversationContext
        {
            ConversationId = "test-config-4",
            UserId = "test-user"
        };

        var result = await _agent.ProcessAsync("show settings", context);

        result.ProcessingTimeMs.Should().BeGreaterOrEqualTo(0);
    }
}
