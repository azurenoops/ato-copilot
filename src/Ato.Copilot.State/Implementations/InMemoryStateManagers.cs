using System.Collections.Concurrent;
using Ato.Copilot.State.Abstractions;

namespace Ato.Copilot.State.Implementations;

/// <summary>
/// In-memory implementation of agent state management
/// </summary>
public class InMemoryAgentStateManager : IAgentStateManager
{
    private readonly ConcurrentDictionary<string, object> _state = new();

    /// <inheritdoc />
    public Task<T?> GetStateAsync<T>(string agentId, string key, CancellationToken cancellationToken = default)
    {
        var compositeKey = $"{agentId}:{key}";
        if (_state.TryGetValue(compositeKey, out var value) && value is T typedValue)
        {
            return Task.FromResult<T?>(typedValue);
        }
        return Task.FromResult<T?>(default);
    }

    /// <inheritdoc />
    public Task SetStateAsync<T>(string agentId, string key, T value, CancellationToken cancellationToken = default)
    {
        var compositeKey = $"{agentId}:{key}";
        _state[compositeKey] = value!;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task ClearStateAsync(string agentId, CancellationToken cancellationToken = default)
    {
        var prefix = $"{agentId}:";
        var keysToRemove = _state.Keys.Where(k => k.StartsWith(prefix)).ToList();
        foreach (var key in keysToRemove)
        {
            _state.TryRemove(key, out _);
        }
        return Task.CompletedTask;
    }
}

/// <summary>
/// In-memory implementation of conversation state management
/// </summary>
public class InMemoryConversationStateManager : IConversationStateManager
{
    private readonly ConcurrentDictionary<string, ConversationState> _conversations = new();

    /// <inheritdoc />
    public Task<ConversationState?> GetConversationAsync(string conversationId, CancellationToken cancellationToken = default)
    {
        _conversations.TryGetValue(conversationId, out var state);
        return Task.FromResult(state);
    }

    /// <inheritdoc />
    public Task SaveConversationAsync(ConversationState state, CancellationToken cancellationToken = default)
    {
        state.LastActivityAt = DateTime.UtcNow;
        _conversations[state.Id] = state;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<string> CreateConversationAsync(CancellationToken cancellationToken = default)
    {
        var state = new ConversationState();
        _conversations[state.Id] = state;
        return Task.FromResult(state.Id);
    }
}
