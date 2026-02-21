using Microsoft.Extensions.DependencyInjection;
using Ato.Copilot.State.Abstractions;
using Ato.Copilot.State.Implementations;

namespace Ato.Copilot.State.Extensions;

/// <summary>
/// Extension methods for state management registration
/// </summary>
public static class StateServiceExtensions
{
    /// <summary>
    /// Adds in-memory state management services
    /// </summary>
    public static IServiceCollection AddInMemoryStateManagement(this IServiceCollection services)
    {
        services.AddSingleton<IAgentStateManager, InMemoryAgentStateManager>();
        services.AddSingleton<IConversationStateManager, InMemoryConversationStateManager>();
        return services;
    }
}
