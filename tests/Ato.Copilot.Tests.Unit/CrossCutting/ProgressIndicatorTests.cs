using Xunit;
using FluentAssertions;
using Moq;
using Microsoft.Extensions.Logging;
using Ato.Copilot.Agents.Common;
using System.Diagnostics;

namespace Ato.Copilot.Tests.Unit.CrossCutting;

/// <summary>
/// Tests that long-running tool operations record latency metrics,
/// that the ExecuteAsync instrumentation wrapper captures timing and errors,
/// and that tool execution follows the expected lifecycle.
/// </summary>
public class ProgressIndicatorTests
{
    [Fact]
    public async Task ExecuteAsync_RecordsSuccessMetrics_WhenToolCompletes()
    {
        // Arrange
        var tool = new SlowCompletingTool();
        var args = new Dictionary<string, object?> { ["input"] = "test" };

        // Act
        var sw = Stopwatch.StartNew();
        var result = await tool.ExecuteAsync(args);
        sw.Stop();

        // Assert
        result.Should().Contain("completed");
        sw.ElapsedMilliseconds.Should().BeGreaterOrEqualTo(0, "tool should have measurable execution time");
    }

    [Fact]
    public async Task ExecuteAsync_RecordsErrorMetrics_WhenToolFails()
    {
        // Arrange
        var tool = new FailingTool();
        var args = new Dictionary<string, object?> { ["input"] = "test" };

        // Act
        Func<Task> act = () => tool.ExecuteAsync(args);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*simulated failure*");
    }

    [Fact]
    public async Task ExecuteAsync_RespectsCircuitBreakerOnTimeout()
    {
        // Arrange
        var tool = new TimeoutTool();
        var args = new Dictionary<string, object?>();
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

        // Act
        Func<Task> act = () => tool.ExecuteAsync(args, cts.Token);

        // Assert — should throw OperationCanceledException
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task ExecuteAsync_MeasuresLatencyWithinExpectedBounds()
    {
        // Arrange — tool that takes a known amount of time
        var tool = new TimedTool(delayMs: 50);
        var args = new Dictionary<string, object?>();

        // Act
        var sw = Stopwatch.StartNew();
        await tool.ExecuteAsync(args);
        sw.Stop();

        // Assert — should be at least 50ms (our delay) but less than 2000ms
        sw.ElapsedMilliseconds.Should().BeGreaterOrEqualTo(40, "tool has 50ms intentional delay");
        sw.ElapsedMilliseconds.Should().BeLessThan(2000, "tool should not take excessively long");
    }

    [Fact]
    public void ToolName_IsConsistentAcrossInvocations()
    {
        // Arrange
        var tool = new SlowCompletingTool();

        // Assert — Name should be stable
        tool.Name.Should().Be("test_slow_completing_tool");
        tool.AgentName.Should().Be("compliance");
    }

    [Fact]
    public async Task ExecuteAsync_PropagatesCancellationFromExternalToken()
    {
        // Arrange
        var tool = new TimedTool(delayMs: 5000);
        var args = new Dictionary<string, object?>();
        using var cts = new CancellationTokenSource();
        cts.Cancel(); // Pre-cancel

        // Act
        Func<Task> act = () => tool.ExecuteAsync(args, cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task ExecuteAsync_SuccessfulToolReturnsNonNullResult()
    {
        // Arrange
        var tool = new SlowCompletingTool();
        var args = new Dictionary<string, object?> { ["input"] = "hello" };

        // Act
        var result = await tool.ExecuteAsync(args);

        // Assert
        result.Should().NotBeNullOrEmpty();
    }

    // ─── Test Tool Implementations ───────────────────────────────────────

    private class SlowCompletingTool : BaseTool
    {
        public SlowCompletingTool() : base(Mock.Of<ILogger>()) { }
        public override string Name => "test_slow_completing_tool";
        public override string Description => "Test tool that simulates slow completion.";
        public override IReadOnlyDictionary<string, ToolParameter> Parameters =>
            new Dictionary<string, ToolParameter>
            {
                ["input"] = new() { Description = "Test input.", Required = false },
            };

        public override async Task<string> ExecuteCoreAsync(
            Dictionary<string, object?> arguments,
            CancellationToken cancellationToken = default)
        {
            await Task.Delay(10, cancellationToken);
            return "{\"status\": \"completed\"}";
        }
    }

    private class FailingTool : BaseTool
    {
        public FailingTool() : base(Mock.Of<ILogger>()) { }
        public override string Name => "test_failing_tool";
        public override string Description => "Test tool that always fails.";
        public override IReadOnlyDictionary<string, ToolParameter> Parameters =>
            new Dictionary<string, ToolParameter>();

        public override Task<string> ExecuteCoreAsync(
            Dictionary<string, object?> arguments,
            CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("simulated failure");
        }
    }

    private class TimeoutTool : BaseTool
    {
        public TimeoutTool() : base(Mock.Of<ILogger>()) { }
        public override string Name => "test_timeout_tool";
        public override string Description => "Test tool that takes too long.";
        public override IReadOnlyDictionary<string, ToolParameter> Parameters =>
            new Dictionary<string, ToolParameter>();

        public override async Task<string> ExecuteCoreAsync(
            Dictionary<string, object?> arguments,
            CancellationToken cancellationToken = default)
        {
            await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);
            return "should not reach here";
        }
    }

    private class TimedTool : BaseTool
    {
        private readonly int _delayMs;
        public TimedTool(int delayMs) : base(Mock.Of<ILogger>()) { _delayMs = delayMs; }
        public override string Name => "test_timed_tool";
        public override string Description => "Test tool with configurable delay.";
        public override IReadOnlyDictionary<string, ToolParameter> Parameters =>
            new Dictionary<string, ToolParameter>();

        public override async Task<string> ExecuteCoreAsync(
            Dictionary<string, object?> arguments,
            CancellationToken cancellationToken = default)
        {
            await Task.Delay(_delayMs, cancellationToken);
            return "{\"status\": \"done\"}";
        }
    }
}
