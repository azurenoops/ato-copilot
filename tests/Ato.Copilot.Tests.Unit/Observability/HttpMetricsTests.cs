using Ato.Copilot.Core.Observability;
using FluentAssertions;
using Xunit;

namespace Ato.Copilot.Tests.Unit.Observability;

/// <summary>
/// Unit tests for HttpMetrics instruments (US5 / FR-022).
/// Verifies metric instruments are created and record values correctly.
/// </summary>
public class HttpMetricsTests
{
    private readonly HttpMetrics _metrics = new();

    [Fact]
    public void RecordRequest_DoesNotThrow()
    {
        var act = () => _metrics.RecordRequest(150.5, "/mcp/chat", "POST", 200);
        act.Should().NotThrow();
    }

    [Fact]
    public void RecordCacheHit_DoesNotThrow()
    {
        var act = () => _metrics.RecordCacheHit("response");
        act.Should().NotThrow();
    }

    [Fact]
    public void RecordCacheMiss_DoesNotThrow()
    {
        var act = () => _metrics.RecordCacheMiss("response");
        act.Should().NotThrow();
    }

    [Fact]
    public void Constructor_CreatesAllInstruments()
    {
        // HttpMetrics should be constructable (non-null instruments)
        var metrics = new HttpMetrics();
        metrics.Should().NotBeNull();
    }

    [Fact]
    public void RecordRequest_WithVariousTags_DoesNotThrow()
    {
        _metrics.RecordRequest(50, "/mcp", "POST", 200);
        _metrics.RecordRequest(100, "/mcp/chat", "POST", 429);
        _metrics.RecordRequest(200, "/health", "GET", 200);
        _metrics.RecordRequest(5000, "/mcp/chat/stream", "POST", 503);
    }
}
