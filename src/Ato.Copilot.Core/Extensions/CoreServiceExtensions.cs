using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Ato.Copilot.Core.Configuration;

namespace Ato.Copilot.Core.Extensions;

/// <summary>
/// Extension methods for registering ATO Copilot Core services
/// </summary>
public static class CoreServiceExtensions
{
    /// <summary>
    /// Adds core compliance services to the DI container
    /// </summary>
    public static IServiceCollection AddAtoCopilotCore(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Bind configuration sections
        services.Configure<GatewayOptions>(configuration.GetSection(GatewayOptions.SectionName));
        services.Configure<AzureAdOptions>(configuration.GetSection(AzureAdOptions.SectionName));

        // Register HTTP client factory
        services.AddHttpClient();

        return services;
    }
}
