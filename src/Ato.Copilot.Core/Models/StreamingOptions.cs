using System.ComponentModel.DataAnnotations;

namespace Ato.Copilot.Core.Models;

/// <summary>
/// Configuration for SSE event buffer and keepalive behavior.
/// </summary>
public class StreamingOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Streaming";

    /// <summary>Maximum number of events buffered per streaming session for reconnection replay.</summary>
    [Range(1, 10000)]
    public int EventBufferSize { get; set; } = 256;

    /// <summary>Interval in seconds between keepalive comments sent during idle streams.</summary>
    [Range(1, 120)]
    public int KeepaliveIntervalSeconds { get; set; } = 15;

    /// <summary>Seconds after client disconnect before evicting the session buffer.</summary>
    [Range(1, 600)]
    public int InactivityTimeoutSeconds { get; set; } = 60;
}
