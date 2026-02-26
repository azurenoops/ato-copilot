using Ato.Copilot.Channels.Models;

namespace Ato.Copilot.Channels.Abstractions;

/// <summary>
/// Initiates streaming responses through a channel.
/// </summary>
public interface IStreamingHandler
{
    /// <summary>Begin a new streaming response for a conversation.</summary>
    /// <param name="conversationId">Target conversation.</param>
    /// <param name="agentType">Optional agent type attribution.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Stream context for writing chunks.</returns>
    Task<IStreamContext> BeginStreamAsync(string conversationId, string? agentType = null, CancellationToken ct = default);
}

/// <summary>
/// Context for an active streaming response. Must be disposed to finalize the stream.
/// </summary>
public interface IStreamContext : IAsyncDisposable
{
    /// <summary>Current stream ID.</summary>
    string StreamId { get; }

    /// <summary>Write a text chunk to the stream.</summary>
    /// <param name="content">Text content to write.</param>
    /// <param name="ct">Cancellation token.</param>
    Task WriteAsync(string content, CancellationToken ct = default);

    /// <summary>Write a typed chunk to the stream.</summary>
    /// <param name="chunk">Typed chunk to write.</param>
    /// <param name="ct">Cancellation token.</param>
    Task WriteAsync(StreamChunk chunk, CancellationToken ct = default);

    /// <summary>Complete the stream and deliver aggregated content.</summary>
    /// <param name="ct">Cancellation token.</param>
    Task CompleteAsync(CancellationToken ct = default);

    /// <summary>Abort the stream and deliver an error message.</summary>
    /// <param name="error">Error description.</param>
    /// <param name="ct">Cancellation token.</param>
    Task AbortAsync(string error, CancellationToken ct = default);
}
