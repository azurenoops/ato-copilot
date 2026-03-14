using Ato.Copilot.Agents.Common;
using Ato.Copilot.Core.Services;
using Microsoft.Extensions.Logging;

namespace Ato.Copilot.Agents.Tools;

/// <summary>
/// Administrative tool that clears cached responses by scope (FR-020).
/// Accepts optional scope filters: subscription ID and/or tool name.
/// </summary>
public class ClearCacheTool : BaseTool
{
    private readonly ResponseCacheService _cacheService;

    public ClearCacheTool(
        ResponseCacheService cacheService,
        ILogger<ClearCacheTool> logger) : base(logger)
    {
        _cacheService = cacheService;
    }

    public override string Name => "cache_clear";

    public override string Description =>
        "Clear cached responses. Optionally filter by tool name and/or subscription ID.";

    public override IReadOnlyDictionary<string, ToolParameter> Parameters =>
        new Dictionary<string, ToolParameter>
        {
            ["tool_name"] = new()
            {
                Type = "string",
                Description = "Optional: clear cache only for this tool name",
                Required = false
            },
            ["subscription_id"] = new()
            {
                Type = "string",
                Description = "Optional: clear cache only for this subscription",
                Required = false
            }
        };

    public override Task<string> ExecuteCoreAsync(
        Dictionary<string, object?> arguments,
        CancellationToken cancellationToken = default)
    {
        var toolName = GetArg<string>(arguments, "tool_name");
        var subscriptionId = GetArg<string>(arguments, "subscription_id");

        var evictedCount = _cacheService.ClearByScope(toolName, subscriptionId);

        var scope = toolName ?? subscriptionId ?? "all";
        Logger.LogInformation("Cache cleared for scope={Scope}: {Count} entries evicted", scope, evictedCount);

        return Task.FromResult($"Cache cleared: {evictedCount} entries evicted for scope '{scope}'.");
    }
}
