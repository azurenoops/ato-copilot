using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Ato.Copilot.Core.Interfaces.Compliance;

namespace Ato.Copilot.Agents.Common;

/// <summary>
/// One-shot hosted service that injects <see cref="ISystemIdResolver"/> into every
/// <see cref="BaseTool"/> singleton at application startup.
/// This ensures all tools — whether invoked via the ComplianceAgent, MCP layer,
/// or any other entry point — automatically resolve system names/acronyms to GUIDs.
/// </summary>
public class SystemIdResolverInitializer : IHostedService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<SystemIdResolverInitializer> _logger;

    public SystemIdResolverInitializer(
        IServiceProvider serviceProvider,
        ILogger<SystemIdResolverInitializer> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        var resolver = _serviceProvider.GetRequiredService<ISystemIdResolver>();
        var tools = _serviceProvider.GetServices<BaseTool>();

        var count = 0;
        foreach (var tool in tools)
        {
            tool.SystemIdResolver = resolver;
            count++;
        }

        _logger.LogInformation(
            "SystemIdResolverInitializer: injected resolver into {Count} tools", count);

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
