using Xunit;
using FluentAssertions;
using Moq;
using Microsoft.Extensions.Logging;
using Ato.Copilot.Agents.Common;
using Ato.Copilot.Mcp.Server;

namespace Ato.Copilot.Tests.Unit.Orchestrator;

public class AgentOrchestratorTests
{
    private readonly Mock<ILogger<AgentOrchestrator>> _loggerMock = new();

    /// <summary>
    /// Creates a mock BaseAgent with the specified CanHandle return value.
    /// </summary>
    private static BaseAgent CreateMockAgent(string agentId, string agentName, double canHandleScore)
    {
        var mock = new Mock<BaseAgent>(MockBehavior.Loose, Mock.Of<ILogger>()) { CallBase = false };
        mock.SetupGet(a => a.AgentId).Returns(agentId);
        mock.SetupGet(a => a.AgentName).Returns(agentName);
        mock.SetupGet(a => a.Description).Returns($"Mock {agentName}");
        mock.Setup(a => a.CanHandle(It.IsAny<string>())).Returns(canHandleScore);
        mock.Setup(a => a.GetSystemPrompt()).Returns("Mock prompt");
        mock.Setup(a => a.ProcessAsync(It.IsAny<string>(), It.IsAny<AgentConversationContext>(), It.IsAny<CancellationToken>(), It.IsAny<IProgress<string>?>()))
            .ReturnsAsync(new AgentResponse { Success = true, Response = "OK", AgentName = agentName });
        return mock.Object;
    }

    [Fact]
    public void SelectAgent_WithHighestScore_ReturnsTopScoringAgent()
    {
        // Arrange
        var agentA = CreateMockAgent("agent-a", "Agent A", 0.4);
        var agentB = CreateMockAgent("agent-b", "Agent B", 0.9);
        var agentC = CreateMockAgent("agent-c", "Agent C", 0.6);
        var orchestrator = new AgentOrchestrator(
            new[] { agentA, agentB, agentC }, _loggerMock.Object);

        // Act
        var selected = orchestrator.SelectAgent("test message");

        // Assert
        selected.Should().NotBeNull();
        selected!.AgentId.Should().Be("agent-b");
    }

    [Fact]
    public void SelectAgent_WithAllBelowThreshold_ReturnsNull()
    {
        // Arrange
        var agentA = CreateMockAgent("agent-a", "Agent A", 0.1);
        var agentB = CreateMockAgent("agent-b", "Agent B", 0.2);
        var orchestrator = new AgentOrchestrator(
            new[] { agentA, agentB }, _loggerMock.Object);

        // Act
        var selected = orchestrator.SelectAgent("test message");

        // Assert
        selected.Should().BeNull();
    }

    [Fact]
    public void SelectAgent_WithCustomThreshold_RespectsThreshold()
    {
        // Arrange
        var agentA = CreateMockAgent("agent-a", "Agent A", 0.5);
        var orchestrator = new AgentOrchestrator(
            new[] { agentA }, _loggerMock.Object, minimumThreshold: 0.6);

        // Act
        var selected = orchestrator.SelectAgent("test message");

        // Assert — 0.5 is below the 0.6 threshold
        selected.Should().BeNull();
    }

    [Fact]
    public void SelectAgent_WithExactThreshold_ReturnsAgent()
    {
        // Arrange
        var agentA = CreateMockAgent("agent-a", "Agent A", 0.3);
        var orchestrator = new AgentOrchestrator(
            new[] { agentA }, _loggerMock.Object, minimumThreshold: 0.3);

        // Act
        var selected = orchestrator.SelectAgent("test message");

        // Assert
        selected.Should().NotBeNull();
        selected!.AgentId.Should().Be("agent-a");
    }

    [Fact]
    public void SelectAgent_WithTiedScores_ReturnsFirstRegistered()
    {
        // Arrange — both score 0.7, first one wins (stable sort)
        var agentA = CreateMockAgent("agent-a", "Agent A", 0.7);
        var agentB = CreateMockAgent("agent-b", "Agent B", 0.7);
        var orchestrator = new AgentOrchestrator(
            new[] { agentA, agentB }, _loggerMock.Object);

        // Act
        var selected = orchestrator.SelectAgent("test message");

        // Assert — first registered agent wins the tie
        selected.Should().NotBeNull();
        selected!.AgentId.Should().Be("agent-a");
    }

    [Fact]
    public void SelectAgent_WithThreeAgents_SelectsCorrectly()
    {
        // Arrange — simulate real scenario: compliance=0.2, config=0.0, kb=0.8
        var compliance = CreateMockAgent("compliance-agent", "Compliance Agent", 0.2);
        var config = CreateMockAgent("configuration-agent", "Configuration Agent", 0.0);
        var kb = CreateMockAgent("knowledgebase-agent", "KnowledgeBase Agent", 0.8);
        var orchestrator = new AgentOrchestrator(
            new[] { compliance, config, kb }, _loggerMock.Object);

        // Act
        var selected = orchestrator.SelectAgent("What is AC-2?");

        // Assert
        selected.Should().NotBeNull();
        selected!.AgentId.Should().Be("knowledgebase-agent");
    }

    [Fact]
    public void SelectAgent_WithNoAgents_ReturnsNull()
    {
        // Arrange
        var orchestrator = new AgentOrchestrator(
            Enumerable.Empty<BaseAgent>(), _loggerMock.Object);

        // Act
        var selected = orchestrator.SelectAgent("test message");

        // Assert
        selected.Should().BeNull();
    }

    [Fact]
    public void SelectAgent_WithEmptyMessage_PassesMessageToAgents()
    {
        // Arrange
        var agentMock = new Mock<BaseAgent>(MockBehavior.Loose, Mock.Of<ILogger>()) { CallBase = false };
        agentMock.SetupGet(a => a.AgentId).Returns("test-agent");
        agentMock.SetupGet(a => a.AgentName).Returns("Test Agent");
        agentMock.SetupGet(a => a.Description).Returns("Test");
        agentMock.Setup(a => a.CanHandle("")).Returns(0.0);
        agentMock.Setup(a => a.GetSystemPrompt()).Returns("prompt");

        var orchestrator = new AgentOrchestrator(
            new[] { agentMock.Object }, _loggerMock.Object);

        // Act
        var selected = orchestrator.SelectAgent("");

        // Assert
        agentMock.Verify(a => a.CanHandle(""), Times.Once);
        selected.Should().BeNull();
    }

    [Fact]
    public void SelectAgent_LogsSelectedAgent()
    {
        // Arrange
        var agent = CreateMockAgent("agent-a", "Agent A", 0.9);
        var orchestrator = new AgentOrchestrator(
            new[] { agent }, _loggerMock.Object);

        // Act
        orchestrator.SelectAgent("test message");

        // Assert — verify logging was called
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public void SelectAgent_LogsWhenNoAgentSelected()
    {
        // Arrange
        var agent = CreateMockAgent("agent-a", "Agent A", 0.1);
        var orchestrator = new AgentOrchestrator(
            new[] { agent }, _loggerMock.Object);

        // Act
        orchestrator.SelectAgent("test message");

        // Assert — verify warning logged
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public void Constructor_WithDefaultThreshold_Uses0Point3()
    {
        // Arrange — agent at exactly 0.3 should be selected with default threshold
        var agent = CreateMockAgent("agent-a", "Agent A", 0.3);
        var orchestrator = new AgentOrchestrator(
            new[] { agent }, _loggerMock.Object);

        // Act
        var selected = orchestrator.SelectAgent("test");

        // Assert
        selected.Should().NotBeNull();
    }
}
