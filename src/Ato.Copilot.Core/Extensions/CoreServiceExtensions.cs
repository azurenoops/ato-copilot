using Azure.Core;
using Azure.Identity;
using Azure.ResourceManager;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenAI;
using Ato.Copilot.Core.Configuration;
using Ato.Copilot.Core.Data.Context;
using System.ClientModel;

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
        services.Configure<MonitoringOptions>(configuration.GetSection(MonitoringOptions.SectionName));
        services.Configure<AlertOptions>(configuration.GetSection(AlertOptions.SectionName));
        services.Configure<NotificationOptions>(configuration.GetSection(NotificationOptions.SectionName));
        services.Configure<EscalationOptions>(configuration.GetSection(EscalationOptions.SectionName));
        services.Configure<AzureOpenAIGatewayOptions>(configuration.GetSection("Gateway:AzureOpenAI"));

        // Register HTTP client factory
        services.AddHttpClient();

        // Register IChatClient from Azure OpenAI when configured
        RegisterChatClient(services, configuration);

        // Register database context with provider selection
        RegisterDbContext(services, configuration);

        // Register Azure ARM client
        RegisterArmClient(services, configuration);

        return services;
    }

    /// <summary>
    /// Registers <see cref="IChatClient"/> as a singleton when Gateway:AzureOpenAI:Endpoint
    /// is configured. Uses API key or DefaultAzureCredential based on UseManagedIdentity.
    /// When Endpoint is empty or missing, registration is silently skipped — agents fall back
    /// to deterministic tool routing (Constitution Principle: graceful degradation).
    /// </summary>
    private static void RegisterChatClient(IServiceCollection services, IConfiguration configuration)
    {
        var endpoint = configuration.GetValue<string>("Gateway:AzureOpenAI:Endpoint");
        if (string.IsNullOrWhiteSpace(endpoint))
            return;

        var useManagedIdentity = configuration.GetValue<bool>("Gateway:AzureOpenAI:UseManagedIdentity");
        var chatDeploymentName = configuration.GetValue<string>("Gateway:AzureOpenAI:ChatDeploymentName") ?? "gpt-4o";

        services.AddSingleton<IChatClient>(sp =>
        {
            var logger = sp.GetService<ILogger<IChatClient>>();

            Azure.AI.OpenAI.AzureOpenAIClient azureClient;
            if (useManagedIdentity)
            {
                var cloudEnv = configuration.GetValue<string>("Gateway:Azure:CloudEnvironment") ?? "AzureGovernment";
                var authorityHost = cloudEnv.Equals("AzureCloud", StringComparison.OrdinalIgnoreCase)
                    ? AzureAuthorityHosts.AzurePublicCloud
                    : AzureAuthorityHosts.AzureGovernment;

                var credential = new DefaultAzureCredential(new DefaultAzureCredentialOptions
                {
                    AuthorityHost = authorityHost
                });

                azureClient = new Azure.AI.OpenAI.AzureOpenAIClient(new Uri(endpoint), credential);
                logger?.LogInformation(
                    "Registered IChatClient with DefaultAzureCredential for {Endpoint}, deployment {Deployment}",
                    endpoint, chatDeploymentName);
            }
            else
            {
                var apiKey = configuration.GetValue<string>("Gateway:AzureOpenAI:ApiKey") ?? string.Empty;
                azureClient = new Azure.AI.OpenAI.AzureOpenAIClient(
                    new Uri(endpoint),
                    new ApiKeyCredential(apiKey));
                logger?.LogInformation(
                    "Registered IChatClient with API key for {Endpoint}, deployment {Deployment}",
                    endpoint, chatDeploymentName);
            }

            return azureClient.AsChatClient(chatDeploymentName);
        });
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

        services.AddDbContextFactory<AtoCopilotContext>(options =>
        {
            // Suppress PendingModelChangesWarning — model snapshot may lag behind
            // code-first changes during active development. EnsureCreated/Migrate
            // will apply the correct schema.
            options.ConfigureWarnings(w =>
                w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));

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
