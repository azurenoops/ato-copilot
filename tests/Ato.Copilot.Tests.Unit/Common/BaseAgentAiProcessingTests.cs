using Xunit;
using Moq;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Ato.Copilot.Agents.Common;
using Ato.Copilot.Core.Configuration;

namespace Ato.Copilot.Tests.Unit.Common;

/// <summary>
/// Unit tests for BaseAgent AI processing - TryProcessWithAiAsync,
/// BuildChatContext, BuildToolDefinitions, and degraded mode behavior.
/// </summary>
public class BaseAgentAiProcessingTests
{
    private readonly Mock<ILogger> _loggerMock = new();

    // ─── Test helpers ────────────────────────────────────────────────────────

    private class TestAgent : BaseAgent
    {
        private readonly string _systemPrompt;

        public TestAgent(ILogger logger, IChatClient? chatClient = null,
            AzureAiOptions? aiOptions = null)
            : base(logger, chatClient, null, aiOptions)
        {
            _systemPrompt = "You are a test agent for compliance.";
        }

        public TestAgent(ILogger logger) : base(logger)
        {
            _systemPrompt = "You are a test agent.";
        }

        public override string AgentId => "test-agent";
        public override string AgentName => "Test Agent";
        public override string Description => "A test agent";
        public override string GetSystemPrompt() => _systemPrompt;
        public override double CanHandle(string message) => 0.5;

        public override Task<AgentResponse> ProcessAsync(
            string message, AgentConversationContext context,
            CancellationToken cancellationToken = default,
            IProgress<string>? progress = null)
        {
            return Task.FromResult(new AgentResponse
            {
                Success = true,
                Response = "Deterministic response",
                AgentName = AgentName
            });
        }

        /// <summary>Expose protected method for testing.</summary>
        public Task<AgentResponse?> TestTryProcessWithAiAsync(
            string message, AgentConversationContext context,
            CancellationToken cancellationToken = default)
            => TryProcessWithAiAsync(message, context, cancellationToken);

        /// <summary>Expose tool registration for testing.</summary>
        public void AddTool(BaseTool tool) => RegisterTool(tool);

        /// <summary>Get tools list for testing.</summary>
        public List<BaseTool> GetTools() => Tools;
    }

    private class TestTool : BaseTool
    {
        private readonly Func<Dictionary<string, object?>, CancellationToken, Task<string>> _handler;

        public TestTool(string name, string description,
            Func<Dictionary<string, object?>, CancellationToken, Task<string>>? handler = null)
            : base(new Mock<ILogger>().Object)
        {
            Name = name;
            Description = description;
            _handler = handler ?? ((_, _) => Task.FromResult($"Result from {name}"));
        }

        public override string Name { get; }
        public override string Description { get; }
        public override IReadOnlyDictionary<string, ToolParameter> Parameters =>
            new Dictionary<string, ToolParameter>
            {
                ["query"] = new ToolParameter { Name = "query", Description = "Search query", Type = "string", Required = true }
            };

        public override Task<string> ExecuteCoreAsync(
            Dictionary<string, object?> arguments, CancellationToken cancellationToken)
            => _handler(arguments, cancellationToken);
    }

    private AzureAiOptions CreateEnabledOptions(int maxRounds = 5) =>
        new()
        {
            Enabled = true,
            MaxToolIterations = maxRounds,
            Temperature = 0.3,
            Endpoint = "https://test.openai.azure.us/"
        };

    private AgentConversationContext CreateContext() =>
        new()
        {
            ConversationId = "test-conv-1",
            UserId = "test-user"
        };

    // ─── T014: TryProcessWithAiAsync tests ───────────────────────────────────

    [Fact]
    public async Task TryProcessWithAiAsync_WhenChatClientNull_ReturnsNull()
    {
        var agent = new TestAgent(_loggerMock.Object);

        var result = await agent.TestTryProcessWithAiAsync("hello", CreateContext());

        result.Should().BeNull("AI processing should be skipped when IChatClient is null");
    }

    [Fact]
    public async Task TryProcessWithAiAsync_WhenAgentAIDisabled_ReturnsNull()
    {
        var chatClient = new Mock<IChatClient>();
        var options = new AzureAiOptions { Enabled = false };
        var agent = new TestAgent(_loggerMock.Object, chatClient.Object, options);

        var result = await agent.TestTryProcessWithAiAsync("hello", CreateContext());

        result.Should().BeNull("AI processing should be skipped when AgentAIEnabled is false");
    }

    [Fact]
    public async Task TryProcessWithAiAsync_WithTextResponse_ReturnsAgentResponse()
    {
        var chatClient = new Mock<IChatClient>();
        var textMessage = new ChatMessage(ChatRole.Assistant, "Here is your compliance report.");
        var chatResponse = new ChatResponse([textMessage]);

        chatClient.Setup(c => c.GetResponseAsync(
                It.IsAny<IList<ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(chatResponse);

        var agent = new TestAgent(_loggerMock.Object, chatClient.Object, CreateEnabledOptions());

        var result = await agent.TestTryProcessWithAiAsync("scan my subscription", CreateContext());

        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.Response.Should().Contain("compliance report");
        result.AgentName.Should().Be("Test Agent");
        result.ProcessingTimeMs.Should().BeGreaterOrEqualTo(0);
    }

    [Fact]
    public async Task TryProcessWithAiAsync_WithToolCall_ExecutesTool()
    {
        var toolExecuted = false;
        var testTool = new TestTool("compliance_scan", "Scan compliance", (args, ct) =>
        {
            toolExecuted = true;
            return Task.FromResult("Scan complete: 5 findings");
        });

        var chatClient = new Mock<IChatClient>();

        // First call: Tool call response
        var toolCallContent = new FunctionCallContent("call-1", "compliance_scan",
            new Dictionary<string, object?> { ["query"] = "scan" });
        var toolCallMessage = new ChatMessage(ChatRole.Assistant, [toolCallContent]);
        var firstResponse = new ChatResponse([toolCallMessage]);

        // Second call: Final text response
        var textMessage = new ChatMessage(ChatRole.Assistant, "Scan found 5 findings.");
        var secondResponse = new ChatResponse([textMessage]);

        var callCount = 0;
        chatClient.Setup(c => c.GetResponseAsync(
                It.IsAny<IList<ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => ++callCount == 1 ? firstResponse : secondResponse);

        var agent = new TestAgent(_loggerMock.Object, chatClient.Object, CreateEnabledOptions());
        agent.AddTool(testTool);

        var result = await agent.TestTryProcessWithAiAsync("scan compliance", CreateContext());

        toolExecuted.Should().BeTrue("Tool should have been executed");
        result.Should().NotBeNull();
        result!.ToolsExecuted.Should().HaveCount(1);
        result.ToolsExecuted[0].ToolName.Should().Be("compliance_scan");
        result.ToolsExecuted[0].Success.Should().BeTrue();
    }

    [Fact]
    public async Task TryProcessWithAiAsync_WithMultiToolChaining_ExecutesMultipleRounds()
    {
        var toolACalled = false;
        var toolBCalled = false;
        var toolA = new TestTool("tool_a", "Tool A", (_, _) =>
        {
            toolACalled = true;
            return Task.FromResult("Result A");
        });
        var toolB = new TestTool("tool_b", "Tool B", (_, _) =>
        {
            toolBCalled = true;
            return Task.FromResult("Result B");
        });

        var chatClient = new Mock<IChatClient>();

        // Round 1: Call tool A
        var round1 = new ChatResponse([new ChatMessage(ChatRole.Assistant,
            [new FunctionCallContent("c1", "tool_a")])]);
        // Round 2: Call tool B
        var round2 = new ChatResponse([new ChatMessage(ChatRole.Assistant,
            [new FunctionCallContent("c2", "tool_b")])]);
        // Round 3: Final text
        var round3 = new ChatResponse([new ChatMessage(ChatRole.Assistant, "Done with both tools.")]);

        var callCount = 0;
        chatClient.Setup(c => c.GetResponseAsync(
                It.IsAny<IList<ChatMessage>>(), It.IsAny<ChatOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                callCount++;
                return callCount switch { 1 => round1, 2 => round2, _ => round3 };
            });

        var agent = new TestAgent(_loggerMock.Object, chatClient.Object, CreateEnabledOptions());
        agent.AddTool(toolA);
        agent.AddTool(toolB);

        var result = await agent.TestTryProcessWithAiAsync("use both tools", CreateContext());

        toolACalled.Should().BeTrue();
        toolBCalled.Should().BeTrue();
        result!.ToolsExecuted.Should().HaveCount(2);
    }

    [Fact]
    public async Task TryProcessWithAiAsync_MaxRoundsExceeded_ReturnsSummary()
    {
        var chatClient = new Mock<IChatClient>();
        var testTool = new TestTool("loop_tool", "A tool that keeps being called");

        // Always return tool calls — will exceed max rounds
        chatClient.Setup(c => c.GetResponseAsync(
                It.IsAny<IList<ChatMessage>>(), It.IsAny<ChatOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatResponse([new ChatMessage(ChatRole.Assistant,
                [new FunctionCallContent("call-x", "loop_tool")])]));

        var agent = new TestAgent(_loggerMock.Object, chatClient.Object, CreateEnabledOptions(maxRounds: 2));
        agent.AddTool(testTool);

        var result = await agent.TestTryProcessWithAiAsync("do something", CreateContext());

        result.Should().NotBeNull();
        result!.Response.Should().Contain("maximum processing rounds");
        result.ToolsExecuted.Should().HaveCount(2);
    }

    [Fact]
    public async Task TryProcessWithAiAsync_UnknownTool_SendsErrorToLLM()
    {
        var chatClient = new Mock<IChatClient>();

        // First call: request unknown tool
        var round1 = new ChatResponse([new ChatMessage(ChatRole.Assistant,
            [new FunctionCallContent("c1", "nonexistent_tool")])]);
        // Second call: final text
        var round2 = new ChatResponse([new ChatMessage(ChatRole.Assistant, "OK, that tool doesn't exist.")]);

        var callCount = 0;
        chatClient.Setup(c => c.GetResponseAsync(
                It.IsAny<IList<ChatMessage>>(), It.IsAny<ChatOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => ++callCount == 1 ? round1 : round2);

        var agent = new TestAgent(_loggerMock.Object, chatClient.Object, CreateEnabledOptions());

        var result = await agent.TestTryProcessWithAiAsync("use unknown tool", CreateContext());

        result.Should().NotBeNull();
        result!.ToolsExecuted.Should().HaveCount(1);
        result.ToolsExecuted[0].ToolName.Should().Be("nonexistent_tool");
        result.ToolsExecuted[0].Success.Should().BeFalse();
    }

    [Fact]
    public async Task TryProcessWithAiAsync_ToolException_SendsErrorToLLM()
    {
        var failingTool = new TestTool("failing_tool", "A tool that throws", (_, _) =>
            throw new InvalidOperationException("Tool crashed"));

        var chatClient = new Mock<IChatClient>();

        var round1 = new ChatResponse([new ChatMessage(ChatRole.Assistant,
            [new FunctionCallContent("c1", "failing_tool")])]);
        var round2 = new ChatResponse([new ChatMessage(ChatRole.Assistant, "The tool failed.")]);

        var callCount = 0;
        chatClient.Setup(c => c.GetResponseAsync(
                It.IsAny<IList<ChatMessage>>(), It.IsAny<ChatOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => ++callCount == 1 ? round1 : round2);

        var agent = new TestAgent(_loggerMock.Object, chatClient.Object, CreateEnabledOptions());
        agent.AddTool(failingTool);

        var result = await agent.TestTryProcessWithAiAsync("use failing tool", CreateContext());

        result.Should().NotBeNull();
        result!.ToolsExecuted.Should().HaveCount(1);
        result.ToolsExecuted[0].Success.Should().BeFalse();
        result.ToolsExecuted[0].Result.Should().Contain("Tool crashed");
    }

    [Fact]
    public async Task TryProcessWithAiAsync_EmptyResponse_ReturnsUserFriendlyMessage()
    {
        var chatClient = new Mock<IChatClient>();
        var emptyResponse = new ChatResponse([new ChatMessage(ChatRole.Assistant, "")]);

        chatClient.Setup(c => c.GetResponseAsync(
                It.IsAny<IList<ChatMessage>>(), It.IsAny<ChatOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(emptyResponse);

        var agent = new TestAgent(_loggerMock.Object, chatClient.Object, CreateEnabledOptions());

        var result = await agent.TestTryProcessWithAiAsync("hello", CreateContext());

        result.Should().NotBeNull();
        result!.Response.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task TryProcessWithAiAsync_PopulatesProcessingTimeMs()
    {
        var chatClient = new Mock<IChatClient>();
        chatClient.Setup(c => c.GetResponseAsync(
                It.IsAny<IList<ChatMessage>>(), It.IsAny<ChatOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatResponse([new ChatMessage(ChatRole.Assistant, "Response")]));

        var agent = new TestAgent(_loggerMock.Object, chatClient.Object, CreateEnabledOptions());

        var result = await agent.TestTryProcessWithAiAsync("hello", CreateContext());

        result!.ProcessingTimeMs.Should().BeGreaterOrEqualTo(0);
    }

    // ─── T015: BuildChatContext tests ────────────────────────────────────────

    [Fact]
    public async Task BuildChatContext_SystemPromptIsFirst()
    {
        var chatClient = new Mock<IChatClient>();
        IList<ChatMessage>? capturedMessages = null;

        chatClient.Setup(c => c.GetResponseAsync(
                It.IsAny<IList<ChatMessage>>(), It.IsAny<ChatOptions>(), It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<ChatMessage>, ChatOptions?, CancellationToken>((msgs, _, _) =>
                capturedMessages = msgs.ToList())
            .ReturnsAsync(new ChatResponse([new ChatMessage(ChatRole.Assistant, "OK")]));

        var agent = new TestAgent(_loggerMock.Object, chatClient.Object, CreateEnabledOptions());

        await agent.TestTryProcessWithAiAsync("test", CreateContext());

        capturedMessages.Should().NotBeNull();
        capturedMessages![0].Role.Should().Be(ChatRole.System);
        capturedMessages[0].Text.Should().Contain("test agent");
    }

    [Fact]
    public async Task BuildChatContext_HistoryMappedCorrectly()
    {
        var chatClient = new Mock<IChatClient>();
        IList<ChatMessage>? capturedMessages = null;

        chatClient.Setup(c => c.GetResponseAsync(
                It.IsAny<IList<ChatMessage>>(), It.IsAny<ChatOptions>(), It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<ChatMessage>, ChatOptions?, CancellationToken>((msgs, _, _) =>
                capturedMessages = msgs.ToList())
            .ReturnsAsync(new ChatResponse([new ChatMessage(ChatRole.Assistant, "OK")]));

        var context = CreateContext();
        context.MessageHistory.Add(("user", "What is NIST?"));
        context.MessageHistory.Add(("assistant", "NIST is the National Institute of Standards."));

        var agent = new TestAgent(_loggerMock.Object, chatClient.Object, CreateEnabledOptions());
        await agent.TestTryProcessWithAiAsync("Tell me more", CreateContext());

        capturedMessages.Should().NotBeNull();
        // System prompt (index 0) + user message (index 1)
        capturedMessages!.Count.Should().BeGreaterOrEqualTo(2);
        capturedMessages[0].Role.Should().Be(ChatRole.System);
        capturedMessages.Last().Role.Should().Be(ChatRole.User);
        capturedMessages.Last().Text.Should().Be("Tell me more");
    }

    [Fact]
    public async Task BuildChatContext_EmptyHistory_OnlySystemAndUser()
    {
        var chatClient = new Mock<IChatClient>();
        IList<ChatMessage>? capturedMessages = null;

        chatClient.Setup(c => c.GetResponseAsync(
                It.IsAny<IList<ChatMessage>>(), It.IsAny<ChatOptions>(), It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<ChatMessage>, ChatOptions?, CancellationToken>((msgs, _, _) =>
                capturedMessages = msgs.ToList())
            .ReturnsAsync(new ChatResponse([new ChatMessage(ChatRole.Assistant, "OK")]));

        var agent = new TestAgent(_loggerMock.Object, chatClient.Object, CreateEnabledOptions());
        await agent.TestTryProcessWithAiAsync("hello", CreateContext());

        capturedMessages.Should().HaveCount(2); // System + User
        capturedMessages![0].Role.Should().Be(ChatRole.System);
        capturedMessages[1].Role.Should().Be(ChatRole.User);
    }

    [Fact]
    public async Task BuildChatContext_UserMessageIsLast()
    {
        var chatClient = new Mock<IChatClient>();
        IList<ChatMessage>? capturedMessages = null;

        chatClient.Setup(c => c.GetResponseAsync(
                It.IsAny<IList<ChatMessage>>(), It.IsAny<ChatOptions>(), It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<ChatMessage>, ChatOptions?, CancellationToken>((msgs, _, _) =>
                capturedMessages = msgs.ToList())
            .ReturnsAsync(new ChatResponse([new ChatMessage(ChatRole.Assistant, "OK")]));

        var context = CreateContext();
        context.MessageHistory.Add(("user", "First msg"));
        context.MessageHistory.Add(("assistant", "Reply"));

        var agent = new TestAgent(_loggerMock.Object, chatClient.Object, CreateEnabledOptions());
        await agent.TestTryProcessWithAiAsync("Second msg", context);

        capturedMessages.Should().NotBeNull();
        capturedMessages!.Last().Role.Should().Be(ChatRole.User);
        capturedMessages.Last().Text.Should().Be("Second msg");
    }

    // ─── T016: BuildToolDefinitions tests ────────────────────────────────────

    [Fact]
    public async Task BuildToolDefinitions_RegisteredToolsProduceAITools()
    {
        var chatClient = new Mock<IChatClient>();
        ChatOptions? capturedOptions = null;

        chatClient.Setup(c => c.GetResponseAsync(
                It.IsAny<IList<ChatMessage>>(), It.IsAny<ChatOptions>(), It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<ChatMessage>, ChatOptions?, CancellationToken>((_, opts, _) =>
                capturedOptions = opts)
            .ReturnsAsync(new ChatResponse([new ChatMessage(ChatRole.Assistant, "OK")]));

        var agent = new TestAgent(_loggerMock.Object, chatClient.Object, CreateEnabledOptions());
        agent.AddTool(new TestTool("scan_tool", "Run compliance scan"));
        agent.AddTool(new TestTool("report_tool", "Generate report"));

        await agent.TestTryProcessWithAiAsync("test", CreateContext());

        capturedOptions.Should().NotBeNull();
        capturedOptions!.Tools.Should().HaveCount(2);
    }

    [Fact]
    public async Task BuildToolDefinitions_EmptyToolsList_ProducesEmptyAIToolList()
    {
        var chatClient = new Mock<IChatClient>();
        ChatOptions? capturedOptions = null;

        chatClient.Setup(c => c.GetResponseAsync(
                It.IsAny<IList<ChatMessage>>(), It.IsAny<ChatOptions>(), It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<ChatMessage>, ChatOptions?, CancellationToken>((_, opts, _) =>
                capturedOptions = opts)
            .ReturnsAsync(new ChatResponse([new ChatMessage(ChatRole.Assistant, "OK")]));

        var agent = new TestAgent(_loggerMock.Object, chatClient.Object, CreateEnabledOptions());
        // No tools registered

        await agent.TestTryProcessWithAiAsync("test", CreateContext());

        capturedOptions!.Tools.Should().BeEmpty();
    }

    [Fact]
    public async Task BuildToolDefinitions_TemperatureSetFromOptions()
    {
        var chatClient = new Mock<IChatClient>();
        ChatOptions? capturedOptions = null;

        chatClient.Setup(c => c.GetResponseAsync(
                It.IsAny<IList<ChatMessage>>(), It.IsAny<ChatOptions>(), It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<ChatMessage>, ChatOptions?, CancellationToken>((_, opts, _) =>
                capturedOptions = opts)
            .ReturnsAsync(new ChatResponse([new ChatMessage(ChatRole.Assistant, "OK")]));

        var options = CreateEnabledOptions();
        options.Temperature = 0.7;
        var agent = new TestAgent(_loggerMock.Object, chatClient.Object, options);

        await agent.TestTryProcessWithAiAsync("test", CreateContext());

        capturedOptions!.Temperature.Should().Be(0.7f);
    }

    // ─── T017: Degraded mode tests ───────────────────────────────────────────

    [Fact]
    public async Task DegradedMode_LLMTimeoutException_ReturnsNull()
    {
        var chatClient = new Mock<IChatClient>();
        chatClient.Setup(c => c.GetResponseAsync(
                It.IsAny<IList<ChatMessage>>(), It.IsAny<ChatOptions>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TaskCanceledException("LLM request timed out"));

        var agent = new TestAgent(_loggerMock.Object, chatClient.Object, CreateEnabledOptions());

        var result = await agent.TestTryProcessWithAiAsync("test", CreateContext());

        result.Should().BeNull("LLM timeout should trigger fallback (return null)");
    }

    [Fact]
    public async Task DegradedMode_LLMRateLimitException_ReturnsNull()
    {
        var chatClient = new Mock<IChatClient>();
        chatClient.Setup(c => c.GetResponseAsync(
                It.IsAny<IList<ChatMessage>>(), It.IsAny<ChatOptions>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("429 Too Many Requests"));

        var agent = new TestAgent(_loggerMock.Object, chatClient.Object, CreateEnabledOptions());

        var result = await agent.TestTryProcessWithAiAsync("test", CreateContext());

        result.Should().BeNull("Rate limit should trigger fallback");
    }

    [Fact]
    public async Task DegradedMode_NullChatClient_ProcessesLikePreAI()
    {
        var agent = new TestAgent(_loggerMock.Object);

        // TryProcessWithAiAsync returns null → caller falls back to deterministic
        var aiResult = await agent.TestTryProcessWithAiAsync("test", CreateContext());
        aiResult.Should().BeNull();

        // ProcessAsync still works deterministically
        var result = await agent.ProcessAsync("test", CreateContext());
        result.Success.Should().BeTrue();
        result.Response.Should().Be("Deterministic response");
    }

    [Fact]
    public async Task DegradedMode_DisabledFlag_ProcessesLikePreAI()
    {
        var chatClient = new Mock<IChatClient>();
        var options = new AzureAiOptions { Enabled = false };
        var agent = new TestAgent(_loggerMock.Object, chatClient.Object, options);

        var aiResult = await agent.TestTryProcessWithAiAsync("test", CreateContext());
        aiResult.Should().BeNull();

        var result = await agent.ProcessAsync("test", CreateContext());
        result.Success.Should().BeTrue();
        result.Response.Should().Be("Deterministic response");
    }

    [Fact]
    public async Task DegradedMode_MidRequestFailure_FallsBackForThatRequest()
    {
        var chatClient = new Mock<IChatClient>();

        // First call succeeds, second throws
        var callCount = 0;
        chatClient.Setup(c => c.GetResponseAsync(
                It.IsAny<IList<ChatMessage>>(), It.IsAny<ChatOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                callCount++;
                if (callCount == 1)
                    return new ChatResponse([new ChatMessage(ChatRole.Assistant, "Success")]);
                throw new HttpRequestException("Connection lost");
            });

        var agent = new TestAgent(_loggerMock.Object, chatClient.Object, CreateEnabledOptions());

        // First request succeeds
        var result1 = await agent.TestTryProcessWithAiAsync("first", CreateContext());
        result1.Should().NotBeNull();

        // Second request fails → falls back
        var result2 = await agent.TestTryProcessWithAiAsync("second", CreateContext());
        result2.Should().BeNull("Mid-request failure should trigger fallback for that request");
    }

    [Fact]
    public async Task TryProcessWithAiAsync_SetsTemperatureInChatOptions()
    {
        var chatClient = new Mock<IChatClient>();
        ChatOptions? capturedOptions = null;

        chatClient.Setup(c => c.GetResponseAsync(
                It.IsAny<IList<ChatMessage>>(), It.IsAny<ChatOptions>(), It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<ChatMessage>, ChatOptions?, CancellationToken>((_, opts, _) =>
                capturedOptions = opts)
            .ReturnsAsync(new ChatResponse([new ChatMessage(ChatRole.Assistant, "OK")]));

        var options = CreateEnabledOptions();
        options.Temperature = 0.5;
        var agent = new TestAgent(_loggerMock.Object, chatClient.Object, options);

        await agent.TestTryProcessWithAiAsync("test", CreateContext());

        capturedOptions!.Temperature.Should().Be(0.5f);
    }
}
