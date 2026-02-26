using System.Text;
using Ato.Copilot.Channels.Abstractions;
using Ato.Copilot.Channels.Models;
using Microsoft.Extensions.Logging;

namespace Ato.Copilot.Channels.Implementations;

/// <summary>
/// Context for an active streaming response. Implements IAsyncDisposable with CompareExchange
/// guard for exactly-once completion (R4). Uses Interlocked.Increment for lock-free sequence
/// numbers (R3) and StringBuilder for content aggregation.
/// </summary>
public class StreamContext : IStreamContext
{
    private readonly string _conversationId;
    private readonly string? _agentType;
    private readonly IChannel _channel;
    private readonly ILogger _logger;
    private readonly StringBuilder _buffer = new();
    private long _sequenceNumber;
    private int _completed; // 0 = active, 1 = completed/aborted

    /// <summary>
    /// Initializes a new StreamContext.
    /// </summary>
    public StreamContext(string conversationId, string? agentType, IChannel channel, ILogger logger)
    {
        StreamId = Guid.NewGuid().ToString();
        _conversationId = conversationId;
        _agentType = agentType;
        _channel = channel;
        _logger = logger;

        _logger.LogDebug("Stream {StreamId} started for conversation {ConversationId}", StreamId, conversationId);
    }

    /// <inheritdoc />
    public string StreamId { get; }

    /// <inheritdoc />
    public async Task WriteAsync(string content, CancellationToken ct = default)
    {
        if (Volatile.Read(ref _completed) == 1)
        {
            _logger.LogWarning("Attempted to write to completed/aborted stream {StreamId}", StreamId);
            return;
        }

        var sequenceNumber = Interlocked.Increment(ref _sequenceNumber);
        _buffer.Append(content);

        var message = new ChannelMessage
        {
            ConversationId = _conversationId,
            Type = MessageType.StreamChunk,
            Content = content,
            AgentType = _agentType,
            IsStreaming = true,
            IsComplete = false,
            SequenceNumber = sequenceNumber,
            Metadata = new Dictionary<string, object> { ["streamId"] = StreamId }
        };

        await _channel.SendToConversationAsync(_conversationId, message, ct);
    }

    /// <inheritdoc />
    public async Task WriteAsync(StreamChunk chunk, CancellationToken ct = default)
    {
        if (Volatile.Read(ref _completed) == 1)
        {
            _logger.LogWarning("Attempted to write to completed/aborted stream {StreamId}", StreamId);
            return;
        }

        var sequenceNumber = Interlocked.Increment(ref _sequenceNumber);
        chunk.SequenceNumber = sequenceNumber;
        _buffer.Append(chunk.Content);

        var message = new ChannelMessage
        {
            ConversationId = _conversationId,
            Type = MessageType.StreamChunk,
            Content = chunk.Content,
            AgentType = _agentType,
            IsStreaming = true,
            IsComplete = false,
            SequenceNumber = sequenceNumber,
            Metadata = new Dictionary<string, object>
            {
                ["streamId"] = StreamId,
                ["contentType"] = chunk.ContentType.ToString()
            }
        };

        await _channel.SendToConversationAsync(_conversationId, message, ct);
    }

    /// <inheritdoc />
    public async Task CompleteAsync(CancellationToken ct = default)
    {
        // CompareExchange guard: exactly-once completion (R4)
        if (Interlocked.CompareExchange(ref _completed, 1, 0) != 0)
        {
            _logger.LogDebug("Stream {StreamId} already completed/aborted, skipping duplicate CompleteAsync", StreamId);
            return;
        }

        var aggregatedContent = _buffer.ToString();

        var message = new ChannelMessage
        {
            ConversationId = _conversationId,
            Type = MessageType.AgentResponse,
            Content = aggregatedContent,
            AgentType = _agentType,
            IsStreaming = false,
            IsComplete = true,
            Metadata = new Dictionary<string, object>
            {
                ["streamId"] = StreamId,
                ["totalChunks"] = _sequenceNumber
            }
        };

        await _channel.SendToConversationAsync(_conversationId, message, ct);

        _logger.LogInformation("Stream {StreamId} completed with {ChunkCount} chunks, {ContentLength} chars",
            StreamId, _sequenceNumber, aggregatedContent.Length);
    }

    /// <inheritdoc />
    public async Task AbortAsync(string error, CancellationToken ct = default)
    {
        // CompareExchange guard: exactly-once completion (R4)
        if (Interlocked.CompareExchange(ref _completed, 1, 0) != 0)
        {
            _logger.LogDebug("Stream {StreamId} already completed/aborted, skipping duplicate AbortAsync", StreamId);
            return;
        }

        var message = new ChannelMessage
        {
            ConversationId = _conversationId,
            Type = MessageType.Error,
            Content = error,
            AgentType = _agentType,
            IsStreaming = false,
            IsComplete = true,
            Metadata = new Dictionary<string, object>
            {
                ["streamId"] = StreamId,
                ["aborted"] = true
            }
        };

        await _channel.SendToConversationAsync(_conversationId, message, ct);

        _logger.LogWarning("Stream {StreamId} aborted: {Error}", StreamId, error);
    }

    /// <summary>
    /// Disposes the stream context. Calls CompleteAsync if not already completed/aborted (FR-016 safety net).
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        try
        {
            await CompleteAsync();
        }
        catch (Exception ex)
        {
            // Async disposal must not throw (R4)
            _logger.LogWarning(ex, "Error during stream {StreamId} auto-complete on disposal", StreamId);
        }
    }
}
