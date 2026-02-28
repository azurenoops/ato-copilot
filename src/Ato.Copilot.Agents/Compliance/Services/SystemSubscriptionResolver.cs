using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Ato.Copilot.Core.Data.Context;

namespace Ato.Copilot.Agents.Compliance.Services;

/// <summary>
/// Resolves a subscription ID to its owning <c>RegisteredSystem.Id</c> by querying
/// <c>AzureEnvironmentProfile.SubscriptionIds</c> across all active registered systems.
/// Uses an in-memory cache (invalidated on a configurable TTL) to avoid repeated DB queries.
/// Phase 17 §9a.1.
/// </summary>
public interface ISystemSubscriptionResolver
{
    /// <summary>
    /// Returns the <c>RegisteredSystem.Id</c> whose <c>AzureProfile.SubscriptionIds</c>
    /// contains <paramref name="subscriptionId"/>, or <c>null</c> if no match is found.
    /// For multiple matches the first active system is returned (deterministic via OrderBy Id).
    /// </summary>
    Task<string?> ResolveAsync(string subscriptionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Clears the internal cache, forcing the next <see cref="ResolveAsync"/> call
    /// to reload mappings from the database.
    /// </summary>
    void InvalidateCache();
}

/// <inheritdoc />
public class SystemSubscriptionResolver : ISystemSubscriptionResolver
{
    private readonly IDbContextFactory<AtoCopilotContext> _dbFactory;
    private readonly ILogger<SystemSubscriptionResolver> _logger;

    /// <summary>subscription-id → registered-system-id cache.</summary>
    private Dictionary<string, string> _cache = new(StringComparer.OrdinalIgnoreCase);
    private DateTime _cacheLoadedAt = DateTime.MinValue;
    private readonly object _cacheLock = new();

    /// <summary>Cache TTL — 5 minutes before auto-reload.</summary>
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Initializes a new instance of the <see cref="SystemSubscriptionResolver"/> class.
    /// </summary>
    public SystemSubscriptionResolver(
        IDbContextFactory<AtoCopilotContext> dbFactory,
        ILogger<SystemSubscriptionResolver> logger)
    {
        _dbFactory = dbFactory;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<string?> ResolveAsync(string subscriptionId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(subscriptionId))
            return null;

        // Fast path — check cache
        lock (_cacheLock)
        {
            if (_cacheLoadedAt.Add(CacheTtl) > DateTime.UtcNow && _cache.TryGetValue(subscriptionId, out var cached))
                return cached;
        }

        // Cache miss or stale — reload from DB
        await RefreshCacheAsync(cancellationToken);

        lock (_cacheLock)
        {
            return _cache.TryGetValue(subscriptionId, out var resolved) ? resolved : null;
        }
    }

    /// <inheritdoc />
    public void InvalidateCache()
    {
        lock (_cacheLock)
        {
            _cache = new(StringComparer.OrdinalIgnoreCase);
            _cacheLoadedAt = DateTime.MinValue;
        }

        _logger.LogDebug("SystemSubscriptionResolver cache invalidated");
    }

    /// <summary>Reload subscription→system mapping from the database.</summary>
    private async Task RefreshCacheAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

            // Load all active systems with their AzureProfile (owned entity — loaded automatically)
            var systems = await db.RegisteredSystems
                .AsNoTracking()
                .Where(s => s.IsActive)
                .OrderBy(s => s.Id) // deterministic for multiple-match scenario
                .Select(s => new { s.Id, s.AzureProfile })
                .ToListAsync(cancellationToken);

            var newCache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var mappedCount = 0;

            foreach (var system in systems)
            {
                if (system.AzureProfile?.SubscriptionIds == null)
                    continue;

                foreach (var subId in system.AzureProfile.SubscriptionIds)
                {
                    if (string.IsNullOrWhiteSpace(subId))
                        continue;

                    // First writer wins — if a subscription appears in multiple systems,
                    // the deterministic OrderBy(Id) ensures stable resolution.
                    if (newCache.TryAdd(subId, system.Id))
                        mappedCount++;
                }
            }

            lock (_cacheLock)
            {
                _cache = newCache;
                _cacheLoadedAt = DateTime.UtcNow;
            }

            _logger.LogDebug(
                "SystemSubscriptionResolver cache refreshed: {MappedCount} subscriptions across {SystemCount} systems",
                mappedCount, systems.Count);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to refresh SystemSubscriptionResolver cache; stale mappings may be used");
        }
    }
}
