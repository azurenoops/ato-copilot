using System.Collections.Concurrent;
using Ato.Copilot.Core.Models;
using Microsoft.Extensions.Options;

namespace Ato.Copilot.Mcp.Resilience;

/// <summary>
/// Represents an individual SSE event with a monotonically assigned ID.
/// </summary>
public record SseEvent(long Id, string Data, DateTimeOffset Timestamp);

/// <summary>
/// Per-session buffer holding events for reconnection replay.
/// </summary>
public class SessionBuffer
{
    private readonly int _maxSize;
    private readonly ConcurrentQueue<SseEvent> _events = new();
    private long _nextId;

    public SessionBuffer(int maxSize)
    {
        _maxSize = maxSize;
        LastActivity = DateTimeOffset.UtcNow;
    }

    /// <summary>Timestamp of the last event added or replayed.</summary>
    public DateTimeOffset LastActivity { get; private set; }

    /// <summary>Whether the session has completed (no more events expected).</summary>
    public bool IsComplete { get; set; }

    /// <summary>Current event count in the buffer.</summary>
    public int Count => _events.Count;

    /// <summary>Adds an event to the buffer, assigning a monotonic ID. Evicts oldest if at capacity.</summary>
    public SseEvent Add(string data)
    {
        var evt = new SseEvent(Interlocked.Increment(ref _nextId), data, DateTimeOffset.UtcNow);
        _events.Enqueue(evt);
        LastActivity = DateTimeOffset.UtcNow;

        // Evict oldest events when buffer is full
        while (_events.Count > _maxSize)
            _events.TryDequeue(out _);

        return evt;
    }

    /// <summary>Returns all events with an ID greater than the specified lastEventId (for replay).</summary>
    public IReadOnlyList<SseEvent> GetEventsAfter(long lastEventId)
    {
        LastActivity = DateTimeOffset.UtcNow;
        return _events.Where(e => e.Id > lastEventId).OrderBy(e => e.Id).ToList().AsReadOnly();
    }

    /// <summary>Returns all buffered events in order.</summary>
    public IReadOnlyList<SseEvent> GetAllEvents()
        => _events.OrderBy(e => e.Id).ToList().AsReadOnly();
}

/// <summary>
/// Thread-safe SSE event buffer with per-conversation session management (FR-041, FR-043).
/// Supports reconnection replay via Last-Event-ID and automatic session cleanup.
/// </summary>
public class SseEventBuffer : IDisposable
{
    private readonly ConcurrentDictionary<string, SessionBuffer> _sessions = new();
    private readonly StreamingOptions _options;
    private readonly Timer _cleanupTimer;

    public SseEventBuffer(IOptions<StreamingOptions> options)
    {
        _options = options.Value;
        // Periodic cleanup of stale sessions
        _cleanupTimer = new Timer(CleanupStaleSessions, null,
            TimeSpan.FromSeconds(_options.InactivityTimeoutSeconds),
            TimeSpan.FromSeconds(_options.InactivityTimeoutSeconds));
    }

    /// <summary>Gets or creates a session buffer for the given conversation.</summary>
    public SessionBuffer GetOrCreateSession(string conversationId)
        => _sessions.GetOrAdd(conversationId, _ => new SessionBuffer(_options.EventBufferSize));

    /// <summary>Adds an event to the specified session and returns it with an assigned ID.</summary>
    public SseEvent AddEvent(string conversationId, string data)
        => GetOrCreateSession(conversationId).Add(data);

    /// <summary>Returns events for replay after the given last event ID.</summary>
    public IReadOnlyList<SseEvent> GetEventsForReplay(string conversationId, long lastEventId)
    {
        if (_sessions.TryGetValue(conversationId, out var session))
            return session.GetEventsAfter(lastEventId);
        return Array.Empty<SseEvent>();
    }

    /// <summary>Marks a session as complete and schedules eviction.</summary>
    public void CompleteSession(string conversationId)
    {
        if (_sessions.TryGetValue(conversationId, out var session))
            session.IsComplete = true;
    }

    /// <summary>Removes a session immediately.</summary>
    public bool RemoveSession(string conversationId)
        => _sessions.TryRemove(conversationId, out _);

    /// <summary>Returns the number of active sessions.</summary>
    public int SessionCount => _sessions.Count;

    /// <summary>Returns the configured keepalive interval.</summary>
    public TimeSpan KeepaliveInterval => TimeSpan.FromSeconds(_options.KeepaliveIntervalSeconds);

    private void CleanupStaleSessions(object? state)
    {
        var cutoff = DateTimeOffset.UtcNow.AddSeconds(-_options.InactivityTimeoutSeconds);
        foreach (var kvp in _sessions)
        {
            if (kvp.Value.IsComplete || kvp.Value.LastActivity < cutoff)
                _sessions.TryRemove(kvp.Key, out _);
        }
    }

    public void Dispose()
    {
        _cleanupTimer.Dispose();
        GC.SuppressFinalize(this);
    }
}
