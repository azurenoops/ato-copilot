using Ato.Copilot.Channels.Models;

namespace Ato.Copilot.Channels.Abstractions;

/// <summary>
/// Processes incoming messages and produces channel responses.
/// </summary>
public interface IMessageHandler
{
    /// <summary>Handle an incoming message and return a response.</summary>
    /// <param name="message">Incoming message to process.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Response message.</returns>
    Task<ChannelMessage> HandleMessageAsync(IncomingMessage message, CancellationToken ct = default);
}
