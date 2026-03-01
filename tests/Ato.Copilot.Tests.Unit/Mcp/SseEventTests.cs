using System.Text.Json;
using FluentAssertions;
using Xunit;
using Ato.Copilot.Mcp.Models;

namespace Ato.Copilot.Tests.Unit.Mcp;

/// <summary>
/// Tests for SSE event model serialization and type discriminators (T028, FR-029a).
/// </summary>
public class SseEventTests
{
    private static readonly JsonSerializerOptions CamelCase = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    [Fact]
    public void SseAgentRoutedEvent_TypeIsAgentRouted()
    {
        var evt = new SseAgentRoutedEvent { AgentName = "compliance-agent", Confidence = 0.95 };
        evt.Type.Should().Be("agentRouted");
        evt.AgentName.Should().Be("compliance-agent");
        evt.Confidence.Should().Be(0.95);
    }

    [Fact]
    public void SseThinkingEvent_TypeIsThinking()
    {
        var evt = new SseThinkingEvent { Message = "Analyzing request…" };
        evt.Type.Should().Be("thinking");
        evt.Message.Should().Be("Analyzing request…");
    }

    [Fact]
    public void SseToolStartEvent_TypeIsToolStart()
    {
        var evt = new SseToolStartEvent { ToolName = "assessment_summary", ToolIndex = 0 };
        evt.Type.Should().Be("toolStart");
        evt.ToolName.Should().Be("assessment_summary");
        evt.ToolIndex.Should().Be(0);
    }

    [Fact]
    public void SseToolProgressEvent_TypeIsToolProgress()
    {
        var evt = new SseToolProgressEvent { ToolName = "assessment_summary", PercentComplete = 50 };
        evt.Type.Should().Be("toolProgress");
        evt.PercentComplete.Should().Be(50);
    }

    [Fact]
    public void SseToolCompleteEvent_TypeIsToolComplete()
    {
        var evt = new SseToolCompleteEvent
        {
            ToolName = "assessment_summary",
            Success = true,
            ExecutionTimeMs = 123.45
        };
        evt.Type.Should().Be("toolComplete");
        evt.Success.Should().BeTrue();
        evt.ExecutionTimeMs.Should().Be(123.45);
    }

    [Fact]
    public void SsePartialEvent_TypeIsPartial()
    {
        var evt = new SsePartialEvent { Text = "Here are the finding…" };
        evt.Type.Should().Be("partial");
        evt.Text.Should().Be("Here are the finding…");
    }

    [Fact]
    public void SseValidatingEvent_TypeIsValidating()
    {
        var evt = new SseValidatingEvent { Message = "Checking NIST compliance…" };
        evt.Type.Should().Be("validating");
        evt.Message.Should().Be("Checking NIST compliance…");
    }

    [Fact]
    public void SseCompleteEvent_TypeIsComplete_WithEmbeddedResult()
    {
        var response = new McpChatResponse
        {
            Response = "Assessment complete.",
            AgentName = "compliance-agent",
            IntentType = "compliance"
        };

        var evt = new SseCompleteEvent { Result = response };
        evt.Type.Should().Be("complete");
        evt.Result.Should().NotBeNull();
        evt.Result!.IntentType.Should().Be("compliance");
    }

    [Fact]
    public void SseEvent_Timestamp_DefaultsToUtcNow()
    {
        var before = DateTime.UtcNow.AddSeconds(-1);
        var evt = new SseThinkingEvent { Message = "test" };
        var after = DateTime.UtcNow.AddSeconds(1);

        evt.Timestamp.Should().BeAfter(before).And.BeBefore(after);
    }

    [Fact]
    public void AllSseEvents_SerializeWithTypeDiscriminator()
    {
        SseEvent[] events =
        [
            new SseAgentRoutedEvent { AgentName = "a" },
            new SseThinkingEvent { Message = "m" },
            new SseToolStartEvent { ToolName = "t" },
            new SseToolProgressEvent { ToolName = "t", PercentComplete = 10 },
            new SseToolCompleteEvent { ToolName = "t", Success = true },
            new SsePartialEvent { Text = "chunk" },
            new SseValidatingEvent { Message = "v" },
            new SseCompleteEvent()
        ];

        string[] expectedTypes =
            ["agentRouted", "thinking", "toolStart", "toolProgress", "toolComplete", "partial", "validating", "complete"];

        for (var i = 0; i < events.Length; i++)
        {
            var json = JsonSerializer.Serialize(events[i], events[i].GetType(), CamelCase);
            var doc = JsonDocument.Parse(json);
            doc.RootElement.GetProperty("type").GetString().Should().Be(expectedTypes[i],
                $"event index {i} should serialize type={expectedTypes[i]}");
            doc.RootElement.TryGetProperty("timestamp", out _).Should().BeTrue(
                "every SSE event should include a timestamp");
        }
    }
}
