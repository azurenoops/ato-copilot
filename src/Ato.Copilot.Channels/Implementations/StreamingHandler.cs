using Ato.Copilot.Channels.Abstractions;
using Microsoft.Extensions.Logging;

namespace Ato.Copilot.Channels.Implementations;

/// <summary>
/// Factory for creating StreamContext instances. Implements IStreamingHandler
/// by delegating to the underlying IChannel for message delivery.
/// </summary>
public class StreamingHandler : IStreamingHandler
{
    private readonly IChannel _channel;
    private readonly ILogger<StreamingHandler> _logger;

    /// <summary>
    /// Initializes a new instance of StreamingHandler.
    /// </summary>
    public StreamingHandler(IChannel channel, ILogger<StreamingHandler> logger)
    {
        _channel = channel;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<IStreamContext> BeginStreamAsync(string conversationId, string? agentType = null, CancellationToken ct = default)
    {
        var context = new StreamContext(conversationId, agentType, _channel, _logger);
        _logger.LogInformation("Started stream {StreamId} for conversation {ConversationId}", context.StreamId, conversationId);
        return Task.FromResult<IStreamContext>(context);
    }
}
