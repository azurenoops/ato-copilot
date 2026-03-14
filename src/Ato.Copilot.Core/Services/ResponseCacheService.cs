using System.Security.Cryptography;
using System.Text;
using Ato.Copilot.Core.Models;
using Ato.Copilot.Core.Observability;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ato.Copilot.Core.Services;

/// <summary>
/// In-memory response cache with per-subscription scoping, TTL registry,
/// and cache hit/miss metrics (FR-016, FR-019).
/// </summary>
public class ResponseCacheService
{
    private readonly IMemoryCache _cache;
    private readonly HttpMetrics _metrics;
    private readonly CachingOptions _options;
    private readonly ILogger<ResponseCacheService> _logger;
    private readonly HashSet<string> _trackedKeys = [];
    private readonly object _keysLock = new();

    public ResponseCacheService(
        IMemoryCache cache,
        HttpMetrics metrics,
        IOptions<CachingOptions> options,
        ILogger<ResponseCacheService> logger)
    {
        _cache = cache;
        _metrics = metrics;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// Gets a cached response or executes the factory and caches the result.
    /// Composite key: SHA256(toolName:paramsJson:subscriptionId).
    /// </summary>
    public async Task<string> GetOrSetAsync(
        string toolName,
        string paramsJson,
        string subscriptionId,
        Func<Task<string>> factory,
        bool isMutation = false)
    {
        var cacheKey = ComputeKey(toolName, paramsJson, subscriptionId);

        if (isMutation)
        {
            // Mutations bypass and invalidate cache
            _cache.Remove(cacheKey);
            RemoveTrackedKey(cacheKey);
            var result = await factory();
            _logger.LogDebug("Cache bypassed for mutation: {Tool}", toolName);
            return result;
        }

        if (_cache.TryGetValue(cacheKey, out string? cached) && cached is not null)
        {
            _metrics.RecordCacheHit("response");
            _logger.LogDebug("Cache HIT for {Tool} key={Key}", toolName, cacheKey[..8]);
            return cached;
        }

        _metrics.RecordCacheMiss("response");
        var response = await factory();

        var ttl = GetTtl(toolName);
        var entryOptions = new MemoryCacheEntryOptions()
            .SetAbsoluteExpiration(TimeSpan.FromSeconds(ttl))
            .SetSize(1);

        _cache.Set(cacheKey, response, entryOptions);
        TrackKey(cacheKey, toolName, subscriptionId);

        _logger.LogDebug("Cache SET for {Tool} TTL={Ttl}s key={Key}", toolName, ttl, cacheKey[..8]);
        return response;
    }

    /// <summary>
    /// Returns the cache status for a given key: "HIT" or "MISS".
    /// </summary>
    public string GetCacheStatus(string toolName, string paramsJson, string subscriptionId)
    {
        var cacheKey = ComputeKey(toolName, paramsJson, subscriptionId);
        return _cache.TryGetValue(cacheKey, out _) ? "HIT" : "MISS";
    }

    /// <summary>
    /// Clears cache entries matching the given scope filter.
    /// </summary>
    public int ClearByScope(string? toolName = null, string? subscriptionId = null)
    {
        var keysToRemove = new List<string>();
        lock (_keysLock)
        {
            foreach (var key in _trackedKeys)
            {
                // Keys are tracked as "sha:toolName:subscriptionId"
                var match = true;
                if (toolName != null && !key.Contains($":{toolName}:"))
                    match = false;
                if (subscriptionId != null && !key.EndsWith($":{subscriptionId}"))
                    match = false;
                if (match)
                    keysToRemove.Add(key);
            }
        }

        foreach (var key in keysToRemove)
        {
            var sha = key.Split(':')[0];
            _cache.Remove(sha);
            RemoveTrackedKey(key);
        }

        _logger.LogInformation("Cleared {Count} cache entries for tool={Tool} sub={Sub}",
            keysToRemove.Count, toolName ?? "*", subscriptionId ?? "*");
        return keysToRemove.Count;
    }

    private int GetTtl(string toolName)
    {
        var lower = toolName.ToLowerInvariant();
        if (lower.Contains("lookup") || lower.Contains("search") || lower.Contains("nist"))
            return _options.ControlLookupTtlSeconds;
        if (lower.Contains("assessment") || lower.Contains("scan"))
            return _options.AssessmentTtlSeconds;
        return _options.DefaultTtlSeconds;
    }

    private static string ComputeKey(string toolName, string paramsJson, string subscriptionId)
    {
        var input = $"{toolName}:{paramsJson}:{subscriptionId}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexStringLower(hash);
    }

    private void TrackKey(string sha, string toolName, string subscriptionId)
    {
        lock (_keysLock)
        {
            _trackedKeys.Add($"{sha}:{toolName}:{subscriptionId}");
        }
    }

    private void RemoveTrackedKey(string trackingKey)
    {
        lock (_keysLock)
        {
            _trackedKeys.Remove(trackingKey);
        }
    }
}
