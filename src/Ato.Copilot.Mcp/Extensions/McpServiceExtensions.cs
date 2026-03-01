using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Ato.Copilot.Core.Interfaces.Auth;
using Ato.Copilot.Mcp.Models;
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
        // User context — IHttpContextAccessor enables cross-scope access to request identity
        services.AddHttpContextAccessor();
        services.AddScoped<IUserContext, HttpUserContext>();

        // MCP Tools
        services.AddSingleton<ComplianceMcpTools>();
        services.AddSingleton<KnowledgeBaseMcpTools>();

        // Agent Orchestrator — confidence-scored multi-agent routing
        services.AddSingleton<AgentOrchestrator>();

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
