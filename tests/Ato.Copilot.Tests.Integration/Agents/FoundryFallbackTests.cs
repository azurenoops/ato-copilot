using Xunit;
using FluentAssertions;
using Moq;
using Microsoft.Extensions.Logging;
using Ato.Copilot.Agents.Common;
using Ato.Copilot.Core.Configuration;

namespace Ato.Copilot.Tests.Integration.Agents;

/// <summary>
/// Fallback chain integration tests for Azure AI Foundry Agent integration (Feature 028).
/// Validates graceful degradation: Foundry → IChatClient → deterministic (SC-009).
/// </summary>
public class FoundryFallbackTests
{
    private readonly Mock<ILogger<TestFallbackAgent>> _loggerMock = new();

    /// <summary>
    /// SC-009: Foundry failure triggers IChatClient fallback, then deterministic.
    /// Simulates Foundry returning null (client unavailable) and verifies fallback chain.
    /// </summary>
    [Fact]
    public async Task FallbackChain_FoundryFails_FallsBackToIchatClient_ThenDeterministic()
    {
        // Foundry provider with no client — Foundry returns null, IChatClient also null → deterministic null
        var agent = new TestFallbackAgent(
            _loggerMock.Object,
            azureAiOptions: new AzureAiOptions { Enabled = true, Provider = AiProvider.Foundry },
            foundryClient: null);

        var context = new AgentConversationContext { ConversationId = "fallback-1" };
        var result = await agent.InvokeTryProcessWithBackendAsync("test", context);

        result.Should().BeNull("when both Foundry and IChatClient are unavailable, result should be null for deterministic fallback");
        agent.FoundryAttempted.Should().BeTrue("Foundry path should be attempted first");
    }

    /// <summary>
    /// SC-009: Provider=OpenAi (default) with no IChatClient returns null — zero regressions.
    /// </summary>
    [Fact]
    public async Task FallbackChain_DefaultProvider_NoChatClient_ReturnsNull()
    {
        var agent = new TestFallbackAgent(
            _loggerMock.Object);

        var context = new AgentConversationContext { ConversationId = "fallback-2" };
        var result = await agent.InvokeTryProcessWithBackendAsync("test", context);

        result.Should().BeNull("default provider with no IChatClient should fall through to deterministic");
        agent.FoundryAttempted.Should().BeFalse("OpenAi provider should not attempt Foundry path");
    }

    /// <summary>
    /// SC-009: Provider=OpenAi routes directly to IChatClient without Foundry.
    /// </summary>
    [Fact]
    public async Task FallbackChain_OpenAi_SkipsFoundry()
    {
        var agent = new TestFallbackAgent(
            _loggerMock.Object,
            azureAiOptions: new AzureAiOptions { Enabled = true, Provider = AiProvider.OpenAi });

        var context = new AgentConversationContext { ConversationId = "fallback-3" };
        var result = await agent.InvokeTryProcessWithBackendAsync("test", context);

        result.Should().BeNull("IChatClient is null so TryProcessWithAiAsync returns null");
        agent.FoundryAttempted.Should().BeFalse("OpenAi should not attempt Foundry path");
    }

    /// <summary>
    /// SC-009: When Foundry exception is thrown, fallback to IChatClient still works.
    /// </summary>
    [Fact]
    public async Task FallbackChain_FoundryThrowsException_FallsBackGracefully()
    {
        var agent = new TestFallbackAgent(
            _loggerMock.Object,
            azureAiOptions: new AzureAiOptions { Enabled = true, Provider = AiProvider.Foundry },
            foundryClient: null,
            throwOnFoundry: true);

        var context = new AgentConversationContext { ConversationId = "fallback-4" };
        var result = await agent.InvokeTryProcessWithBackendAsync("test", context);

        // Foundry throws, IChatClient is null → deterministic fallback
        result.Should().BeNull();
        agent.FoundryAttempted.Should().BeTrue("Foundry should be attempted even though it will throw");
    }
}

/// <summary>
/// Test agent with controllable Foundry behavior for fallback testing.
/// </summary>
public class TestFallbackAgent : BaseAgent
{
    public bool FoundryAttempted { get; private set; }
    private readonly bool _throwOnFoundry;

    public TestFallbackAgent(
        ILogger logger,
        AzureAiOptions? azureAiOptions = null,
        Azure.AI.Agents.Persistent.PersistentAgentsClient? foundryClient = null,
        bool throwOnFoundry = false)
        : base(logger, null, foundryClient, azureAiOptions)
    {
        _throwOnFoundry = throwOnFoundry;
    }

    public override string AgentId => "test-fallback-agent";
    public override string AgentName => "Test Fallback Agent";
    public override string Description => "A test agent for fallback testing";
    public override double CanHandle(string message) => 0.5;
    public override string GetSystemPrompt() => "You are a test agent.";

    public override Task<AgentResponse> ProcessAsync(
        string message,
        AgentConversationContext context,
        CancellationToken cancellationToken = default,
        IProgress<string>? progress = null)
    {
        return Task.FromResult(new AgentResponse
        {
            Success = true,
            Response = "test",
            AgentName = AgentName
        });
    }

    protected override Task<AgentResponse?> TryProcessWithFoundryAsync(
        string message,
        AgentConversationContext context,
        CancellationToken cancellationToken = default,
        IProgress<string>? progress = null)
    {
        FoundryAttempted = true;

        if (_throwOnFoundry)
            throw new InvalidOperationException("Simulated Foundry failure");

        return base.TryProcessWithFoundryAsync(message, context, cancellationToken, progress);
    }

    public Task<AgentResponse?> InvokeTryProcessWithBackendAsync(
        string message, AgentConversationContext context, CancellationToken ct = default)
        => TryProcessWithBackendAsync(message, context, ct);
}
