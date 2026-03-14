using Ato.Copilot.Core.Models;
using Ato.Copilot.Mcp.Resilience;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Ato.Copilot.Tests.Unit.Resilience;

public class SseEventBufferTests : IDisposable
{
    private readonly SseEventBuffer _buffer;

    public SseEventBufferTests()
    {
        var options = Options.Create(new StreamingOptions
        {
            EventBufferSize = 5,
            KeepaliveIntervalSeconds = 15,
            InactivityTimeoutSeconds = 60
        });
        _buffer = new SseEventBuffer(options);
    }

    [Fact]
    public void AddEvent_AssignsMonotonicIds()
    {
        var evt1 = _buffer.AddEvent("conv-1", "data1");
        var evt2 = _buffer.AddEvent("conv-1", "data2");
        var evt3 = _buffer.AddEvent("conv-1", "data3");

        evt1.Id.Should().Be(1);
        evt2.Id.Should().Be(2);
        evt3.Id.Should().Be(3);
    }

    [Fact]
    public void SessionBuffer_StoresUpToMaxSize()
    {
        for (int i = 0; i < 5; i++)
            _buffer.AddEvent("conv-1", $"event-{i}");

        var session = _buffer.GetOrCreateSession("conv-1");
        session.Count.Should().Be(5);
    }

    [Fact]
    public void SessionBuffer_EvictsOldestWhenFull()
    {
        // Buffer size is 5, add 7 events
        for (int i = 1; i <= 7; i++)
            _buffer.AddEvent("conv-1", $"event-{i}");

        var session = _buffer.GetOrCreateSession("conv-1");
        session.Count.Should().Be(5);

        var events = session.GetAllEvents();
        events.First().Id.Should().Be(3); // Events 1-2 evicted
        events.Last().Id.Should().Be(7);
    }

    [Fact]
    public void GetEventsForReplay_ReturnsEventsAfterSpecifiedId()
    {
        for (int i = 0; i < 5; i++)
            _buffer.AddEvent("conv-1", $"event-{i}");

        var events = _buffer.GetEventsForReplay("conv-1", 3);

        events.Should().HaveCount(2);
        events[0].Id.Should().Be(4);
        events[1].Id.Should().Be(5);
    }

    [Fact]
    public void GetEventsForReplay_ReturnsEmptyForUnknownSession()
    {
        var events = _buffer.GetEventsForReplay("unknown", 0);
        events.Should().BeEmpty();
    }

    [Fact]
    public void CompleteSession_MarksSessionComplete()
    {
        _buffer.AddEvent("conv-1", "data");
        _buffer.CompleteSession("conv-1");

        var session = _buffer.GetOrCreateSession("conv-1");
        session.IsComplete.Should().BeTrue();
    }

    [Fact]
    public void KeepaliveInterval_ReturnsConfiguredValue()
    {
        _buffer.KeepaliveInterval.Should().Be(TimeSpan.FromSeconds(15));
    }

    public void Dispose() => _buffer.Dispose();
}
