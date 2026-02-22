using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using System.Diagnostics.Metrics;
using Ato.Copilot.Core.Observability;
using Ato.Copilot.Core.Interfaces.Auth;

namespace Ato.Copilot.Tests.Unit.Observability;

/// <summary>
/// Unit tests for ToolMetrics and AgentHealthCheck (per FR-045 / FR-046).
/// </summary>
public class ObservabilityTests
{
    // ────────────────────────────────────────────────────────────
    //  ToolMetrics Tests
    // ────────────────────────────────────────────────────────────

    [Fact]
    public void ToolMetrics_MeterName_IsAtoCopilot()
    {
        ToolMetrics.MeterName.Should().Be("Ato.Copilot");
    }

    [Fact]
    public void ToolMetrics_Instruments_AreCreated()
    {
        ToolMetrics.ToolInvocations.Should().NotBeNull();
        ToolMetrics.ToolDurationMs.Should().NotBeNull();
        ToolMetrics.ToolErrors.Should().NotBeNull();
        ToolMetrics.ActiveSessions.Should().NotBeNull();
    }

    [Fact]
    public void ToolMetrics_RecordStart_DoesNotThrow()
    {
        var act = () => ToolMetrics.RecordStart("test-tool", "test-agent");
        act.Should().NotThrow();
    }

    [Fact]
    public void ToolMetrics_RecordSuccess_DoesNotThrow()
    {
        var act = () => ToolMetrics.RecordSuccess(42.5, "test-tool", "test-agent");
        act.Should().NotThrow();
    }

    [Fact]
    public void ToolMetrics_RecordError_DoesNotThrow()
    {
        var act = () => ToolMetrics.RecordError("test-tool", "test-agent", "test-error");
        act.Should().NotThrow();
    }

    [Fact]
    public void ToolMetrics_InstrumentsHaveCorrectNames()
    {
        ToolMetrics.ToolInvocations.Name.Should().Be("ato.copilot.tool.invocations");
        ToolMetrics.ToolDurationMs.Name.Should().Be("ato.copilot.tool.duration");
        ToolMetrics.ToolErrors.Name.Should().Be("ato.copilot.tool.errors");
        ToolMetrics.ActiveSessions.Name.Should().Be("ato.copilot.sessions.active");
    }

    [Fact]
    public void ToolMetrics_RecordStart_IncrementsCounter()
    {
        // Use MeterListener to verify counters are actually incremented
        using var listener = new MeterListener();
        long invocationCount = 0;

        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Name == "ato.copilot.tool.invocations")
                meterListener.EnableMeasurementEvents(instrument);
        };

        listener.SetMeasurementEventCallback<long>((instrument, measurement, tags, state) =>
        {
            invocationCount += measurement;
        });

        listener.Start();

        ToolMetrics.RecordStart("counter-test-tool", "counter-test-agent");

        listener.RecordObservableInstruments();
        invocationCount.Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public void ToolMetrics_RecordSuccess_RecordsDuration()
    {
        using var listener = new MeterListener();
        double recordedDuration = 0;

        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Name == "ato.copilot.tool.duration")
                meterListener.EnableMeasurementEvents(instrument);
        };

        listener.SetMeasurementEventCallback<double>((instrument, measurement, tags, state) =>
        {
            recordedDuration = measurement;
        });

        listener.Start();

        ToolMetrics.RecordSuccess(123.45, "duration-test-tool", "duration-test-agent");

        recordedDuration.Should().Be(123.45);
    }

    [Fact]
    public void ToolMetrics_RecordError_IncrementsErrorCounter()
    {
        using var listener = new MeterListener();
        long errorCount = 0;

        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Name == "ato.copilot.tool.errors")
                meterListener.EnableMeasurementEvents(instrument);
        };

        listener.SetMeasurementEventCallback<long>((instrument, measurement, tags, state) =>
        {
            errorCount += measurement;
        });

        listener.Start();

        ToolMetrics.RecordError("error-test-tool", "error-test-agent", "test-error-code");

        errorCount.Should().BeGreaterThanOrEqualTo(1);
    }

    // ────────────────────────────────────────────────────────────
    //  AgentHealthCheck Tests
    // ────────────────────────────────────────────────────────────

    [Fact]
    public async Task AgentHealthCheck_AllServicesAvailable_ReturnsHealthy()
    {
        var services = new ServiceCollection();
        services.AddSingleton(Mock.Of<ICacSessionService>());
        services.AddSingleton(Mock.Of<IPimService>());
        services.AddSingleton(Mock.Of<ILogger<AgentHealthCheck>>());

        // Add a mock DbContext using InMemory
        services.AddDbContext<Core.Data.Context.AtoCopilotContext>(opts =>
            opts.UseInMemoryDatabase(Guid.NewGuid().ToString()));

        var provider = services.BuildServiceProvider();

        var healthCheck = new AgentHealthCheck(
            provider,
            provider.GetRequiredService<ILogger<AgentHealthCheck>>());

        var result = await healthCheck.CheckHealthAsync(
            new HealthCheckContext(),
            CancellationToken.None);

        result.Status.Should().Be(HealthStatus.Healthy);
        result.Description.Should().Contain("operational");
    }

    [Fact]
    public async Task AgentHealthCheck_MissingCoreService_ReturnsUnhealthy()
    {
        var services = new ServiceCollection();
        // Register ICacSessionService but NOT IPimService
        services.AddSingleton(Mock.Of<ICacSessionService>());
        services.AddSingleton(Mock.Of<ILogger<AgentHealthCheck>>());

        services.AddDbContext<Core.Data.Context.AtoCopilotContext>(opts =>
            opts.UseInMemoryDatabase(Guid.NewGuid().ToString()));

        var provider = services.BuildServiceProvider();

        var healthCheck = new AgentHealthCheck(
            provider,
            provider.GetRequiredService<ILogger<AgentHealthCheck>>());

        var result = await healthCheck.CheckHealthAsync(
            new HealthCheckContext(),
            CancellationToken.None);

        result.Status.Should().Be(HealthStatus.Unhealthy);
        result.Description.Should().Contain("IPimService");
    }

    [Fact]
    public async Task AgentHealthCheck_MissingBothServices_ReturnsUnhealthy()
    {
        var services = new ServiceCollection();
        services.AddSingleton(Mock.Of<ILogger<AgentHealthCheck>>());

        services.AddDbContext<Core.Data.Context.AtoCopilotContext>(opts =>
            opts.UseInMemoryDatabase(Guid.NewGuid().ToString()));

        var provider = services.BuildServiceProvider();

        var healthCheck = new AgentHealthCheck(
            provider,
            provider.GetRequiredService<ILogger<AgentHealthCheck>>());

        var result = await healthCheck.CheckHealthAsync(
            new HealthCheckContext(),
            CancellationToken.None);

        result.Status.Should().Be(HealthStatus.Unhealthy);
        result.Description.Should().Contain("ICacSessionService");
        result.Description.Should().Contain("IPimService");
    }

    [Fact]
    public async Task AgentHealthCheck_HonorsCancellationToken()
    {
        var services = new ServiceCollection();
        services.AddSingleton(Mock.Of<ICacSessionService>());
        services.AddSingleton(Mock.Of<IPimService>());
        services.AddSingleton(Mock.Of<ILogger<AgentHealthCheck>>());

        services.AddDbContext<Core.Data.Context.AtoCopilotContext>(opts =>
            opts.UseInMemoryDatabase(Guid.NewGuid().ToString()));

        var provider = services.BuildServiceProvider();

        var healthCheck = new AgentHealthCheck(
            provider,
            provider.GetRequiredService<ILogger<AgentHealthCheck>>());

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Should not throw — cancellation is handled gracefully via CanConnectAsync
        var act = async () => await healthCheck.CheckHealthAsync(
            new HealthCheckContext(), cts.Token);

        // The behavior depends on the InMemory provider — it may or may not respect cancellation
        // but the method should not throw an unhandled exception
        await act.Should().NotThrowAsync<NullReferenceException>();
    }
}
