using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Ato.Copilot.Agents.Common;
using Ato.Copilot.Agents.Compliance.Agents;
using Ato.Copilot.Agents.Configuration.Agents;
using Ato.Copilot.Mcp.Models;
using Ato.Copilot.Mcp.Server;
using Ato.Copilot.Mcp.Tools;

namespace Ato.Copilot.Tests.Unit.Mcp;

/// <summary>
/// Tests for response enrichment pass-through from AgentResponse → McpChatResponse (T024, FR-001/002/004/005).
/// </summary>
public class McpServerResponseEnrichmentTests
{
    private readonly Mock<ComplianceAgent> _complianceAgent;
    private readonly StubOrchestrator _orchestrator;

    public McpServerResponseEnrichmentTests()
    {
        _complianceAgent = TestMockFactory.CreateComplianceAgentMock();

        _orchestrator = TestMockFactory.CreateOrchestrator(_complianceAgent.Object);
    }

    [Fact]
    public async Task ProcessChatRequestAsync_MapsToolsExecuted()
    {
        _complianceAgent.Setup(a => a.ProcessAsync(
                It.IsAny<string>(), It.IsAny<AgentConversationContext>(),
                It.IsAny<CancellationToken>(), It.IsAny<IProgress<string>>()))
            .ReturnsAsync(new AgentResponse
            {
                Success = true,
                Response = "done",
                AgentName = "Compliance Agent",
                ToolsExecuted = new List<ToolExecutionResult>
                {
                    new() { ToolName = "assessment_summary", Success = true, ExecutionTimeMs = 100 },
                    new() { ToolName = "get_findings", Success = false, ExecutionTimeMs = 50 }
                }
            });

        var result = await CreateServer().ProcessChatRequestAsync("test");

        result.ToolsExecuted.Should().HaveCount(2);
        result.ToolsExecuted[0].ToolName.Should().Be("assessment_summary");
        result.ToolsExecuted[0].Success.Should().BeTrue();
        result.ToolsExecuted[0].ExecutionTimeMs.Should().Be(100);
        result.ToolsExecuted[1].ToolName.Should().Be("get_findings");
        result.ToolsExecuted[1].Success.Should().BeFalse();
    }

    [Fact]
    public async Task ProcessChatRequestAsync_MapsSuggestions()
    {
        _complianceAgent.Setup(a => a.ProcessAsync(
                It.IsAny<string>(), It.IsAny<AgentConversationContext>(),
                It.IsAny<CancellationToken>(), It.IsAny<IProgress<string>>()))
            .ReturnsAsync(new AgentResponse
            {
                Success = true,
                Response = "done",
                AgentName = "Compliance Agent",
                Suggestions = new List<string> { "Run full assessment", "Show findings" }
            });

        var result = await CreateServer().ProcessChatRequestAsync("test");

        result.Suggestions.Should().Contain("Run full assessment");
        result.Suggestions.Should().Contain("Show findings");
    }

    [Fact]
    public async Task ProcessChatRequestAsync_MapsFollowUpFields()
    {
        _complianceAgent.Setup(a => a.ProcessAsync(
                It.IsAny<string>(), It.IsAny<AgentConversationContext>(),
                It.IsAny<CancellationToken>(), It.IsAny<IProgress<string>>()))
            .ReturnsAsync(new AgentResponse
            {
                Success = true,
                Response = "done",
                AgentName = "Compliance Agent",
                RequiresFollowUp = true,
                FollowUpPrompt = "Which control family?",
                MissingFields = new List<string> { "controlFamily", "impactLevel" }
            });

        var result = await CreateServer().ProcessChatRequestAsync("test");

        result.RequiresFollowUp.Should().BeTrue();
        result.FollowUpPrompt.Should().Be("Which control family?");
        result.MissingFields.Should().Contain("controlFamily");
        result.MissingFields.Should().Contain("impactLevel");
    }

    [Fact]
    public async Task ProcessChatRequestAsync_MapsResponseData()
    {
        _complianceAgent.Setup(a => a.ProcessAsync(
                It.IsAny<string>(), It.IsAny<AgentConversationContext>(),
                It.IsAny<CancellationToken>(), It.IsAny<IProgress<string>>()))
            .ReturnsAsync(new AgentResponse
            {
                Success = true,
                Response = "done",
                AgentName = "Compliance Agent",
                ResponseData = new Dictionary<string, object>
                {
                    ["type"] = "assessment",
                    ["totalControls"] = 85,
                    ["passedControls"] = 70
                }
            });

        var result = await CreateServer().ProcessChatRequestAsync("test");

        result.Data.Should().NotBeNull();
        result.Data!["type"].Should().Be("assessment");
        result.Data["totalControls"].Should().Be(85);
    }

    [Fact]
    public async Task ProcessChatRequestAsync_DefaultsWhenAgentReturnsNoEnrichment()
    {
        _complianceAgent.Setup(a => a.ProcessAsync(
                It.IsAny<string>(), It.IsAny<AgentConversationContext>(),
                It.IsAny<CancellationToken>(), It.IsAny<IProgress<string>>()))
            .ReturnsAsync(new AgentResponse
            {
                Success = true,
                Response = "done",
                AgentName = "Compliance Agent"
            });

        var result = await CreateServer().ProcessChatRequestAsync("test");

        result.Suggestions.Should().BeEmpty();
        result.RequiresFollowUp.Should().BeFalse();
        result.FollowUpPrompt.Should().BeNull();
        result.MissingFields.Should().BeEmpty();
        result.Data.Should().BeNull();
        result.ToolsExecuted.Should().BeEmpty();
    }

    private McpServer CreateServer()
    {
        return new McpServer(
            (ComplianceMcpTools)null!,
            (KnowledgeBaseMcpTools)null!,
            _complianceAgent.Object,
            (ConfigurationAgent)null!,
            null!,
            _orchestrator,
            Mock.Of<IHttpContextAccessor>(),
            Mock.Of<ILogger<McpServer>>());
    }
}
