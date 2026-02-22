using Azure.Core;
using Azure.Identity;
using Azure.ResourceManager;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Ato.Copilot.Core.Configuration;
using Ato.Copilot.Core.Data.Context;

namespace Ato.Copilot.Core.Extensions;

/// <summary>
/// Extension methods for registering ATO Copilot Core services including
/// database context, Azure clients, and configuration bindings.
/// </summary>
public static class CoreServiceExtensions
{
    /// <summary>
    /// Adds core compliance services to the DI container including
    /// DbContext, ArmClient, configuration bindings, and HTTP client factory.
    /// </summary>
    /// <param name="services">The service collection to register services in.</param>
    /// <param name="configuration">Application configuration.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddAtoCopilotCore(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Bind configuration sections
        services.Configure<GatewayOptions>(configuration.GetSection(GatewayOptions.SectionName));
        services.Configure<AzureAdOptions>(configuration.GetSection(AzureAdOptions.SectionName));
        services.Configure<PimServiceOptions>(configuration.GetSection(PimServiceOptions.SectionName));
        services.Configure<CacAuthOptions>(configuration.GetSection(CacAuthOptions.SectionName));
        services.Configure<RetentionPolicyOptions>(configuration.GetSection(RetentionPolicyOptions.SectionName));

        // Register HTTP client factory
        services.AddHttpClient();

        // Register database context with provider selection
        RegisterDbContext(services, configuration);

        // Register Azure ARM client
        RegisterArmClient(services, configuration);

        return services;
    }

    /// <summary>
    /// Registers <see cref="AtoCopilotContext"/> with SQLite (development) or
    /// SQL Server (production) based on Database:Provider configuration.
    /// Defaults to SQLite with "Data Source=ato-copilot.db".
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">Application configuration.</param>
    private static void RegisterDbContext(IServiceCollection services, IConfiguration configuration)
    {
        var provider = configuration.GetValue<string>("Database:Provider") ?? "SQLite";
        var connectionString = configuration.GetConnectionString("DefaultConnection")
                               ?? "Data Source=ato-copilot.db";

        services.AddDbContext<AtoCopilotContext>(options =>
        {
            if (provider.Equals("SqlServer", StringComparison.OrdinalIgnoreCase))
            {
                options.UseSqlServer(connectionString, sqlOptions =>
                {
                    sqlOptions.EnableRetryOnFailure(
                        maxRetryCount: 5,
                        maxRetryDelay: TimeSpan.FromSeconds(30),
                        errorNumbersToAdd: null);
                    sqlOptions.CommandTimeout(30);
                });
            }
            else
            {
                options.UseSqlite(connectionString, sqliteOptions =>
                {
                    sqliteOptions.CommandTimeout(30);
                });
            }
        });
    }

    /// <summary>
    /// Registers <see cref="ArmClient"/> as a singleton with dual-cloud support
    /// (AzureGovernment / AzureCloud). Uses <see cref="DefaultAzureCredential"/>
    /// configured for the target cloud environment.
    /// Thread-safe: ArmClient is designed for concurrent use.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">Application configuration.</param>
    private static void RegisterArmClient(IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton(sp =>
        {
            var cloudEnv = configuration.GetValue<string>("Gateway:Azure:CloudEnvironment")
                           ?? "AzureGovernment";

            var authorityHost = cloudEnv.Equals("AzureCloud", StringComparison.OrdinalIgnoreCase)
                ? AzureAuthorityHosts.AzurePublicCloud
                : AzureAuthorityHosts.AzureGovernment;

            var credential = new DefaultAzureCredential(new DefaultAzureCredentialOptions
            {
                AuthorityHost = authorityHost
            });

            var armEnvironment = cloudEnv.Equals("AzureCloud", StringComparison.OrdinalIgnoreCase)
                ? ArmEnvironment.AzurePublicCloud
                : ArmEnvironment.AzureGovernment;

            var logger = sp.GetService<ILogger<ArmClient>>();
            logger?.LogInformation(
                "Initializing ArmClient for {CloudEnvironment} ({ArmEndpoint})",
                cloudEnv, armEnvironment.Endpoint);

            return new ArmClient(credential, default, new ArmClientOptions
            {
                Environment = armEnvironment
            });
        });
    }
}
