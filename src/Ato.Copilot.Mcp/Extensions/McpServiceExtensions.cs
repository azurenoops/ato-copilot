using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Ato.Copilot.Mcp.Server;
using Ato.Copilot.Mcp.Tools;

namespace Ato.Copilot.Mcp.Extensions;

public static class McpServiceExtensions
{
    /// <summary>
    /// Register MCP server services (tools, server, HTTP bridge)
    /// </summary>
    public static IServiceCollection AddMcpServer(this IServiceCollection services, IConfiguration configuration)
    {
        // MCP Tools
        services.AddSingleton<ComplianceMcpTools>();

        // MCP Server
        services.AddSingleton<McpServer>();
        services.AddSingleton<McpHttpBridge>();

        return services;
    }

    /// <summary>
    /// Register MCP stdio background service for CLI mode
    /// </summary>
    public static IServiceCollection AddMcpStdioService(this IServiceCollection services)
    {
        services.AddHostedService<McpStdioService>();
        return services;
    }
}
