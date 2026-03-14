using Ato.Copilot.Core.Data.Context;
using Ato.Copilot.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Ato.Copilot.Core.Services;

/// <summary>
/// Persistent cache repository using EF Core for offline mode (FR-036, FR-039).
/// Provides database-backed storage for CachedResponse entries.
/// </summary>
public class CacheRepository
{
    private readonly IDbContextFactory<AtoCopilotContext> _dbFactory;
    private readonly ILogger<CacheRepository> _logger;

    public CacheRepository(IDbContextFactory<AtoCopilotContext> dbFactory, ILogger<CacheRepository> logger)
    {
        _dbFactory = dbFactory;
        _logger = logger;
    }

    /// <summary>Saves or updates a cached response entry.</summary>
    public async Task SaveAsync(CachedResponse entry, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var existing = await db.CachedResponses
            .FirstOrDefaultAsync(c => c.CacheKey == entry.CacheKey, cancellationToken);

        if (existing != null)
        {
            existing.Response = entry.Response;
            existing.CachedAt = entry.CachedAt;
            existing.TtlSeconds = entry.TtlSeconds;
            existing.Source = entry.Source;
            existing.HitCount = entry.HitCount;
        }
        else
        {
            db.CachedResponses.Add(entry);
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Retrieves a cached response by its cache key.</summary>
    public async Task<CachedResponse?> GetByKeyAsync(string cacheKey, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var entry = await db.CachedResponses
            .FirstOrDefaultAsync(c => c.CacheKey == cacheKey, cancellationToken);

        if (entry != null)
        {
            entry.HitCount++;
            await db.SaveChangesAsync(cancellationToken);
        }

        return entry;
    }

    /// <summary>Returns entries that have exceeded their TTL.</summary>
    public async Task<List<CachedResponse>> GetStaleEntriesAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var now = DateTimeOffset.UtcNow;
        return await db.CachedResponses
            .Where(c => now > c.CachedAt.AddSeconds(c.TtlSeconds))
            .ToListAsync(cancellationToken);
    }

    /// <summary>Deletes all cached entries for a given subscription scope.</summary>
    public async Task<int> DeleteByScopeAsync(string subscriptionId, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var count = await db.CachedResponses
            .Where(c => c.SubscriptionId == subscriptionId)
            .ExecuteDeleteAsync(cancellationToken);

        _logger.LogInformation("Deleted {Count} cached entries for subscription {SubscriptionId}",
            count, subscriptionId);
        return count;
    }
}
