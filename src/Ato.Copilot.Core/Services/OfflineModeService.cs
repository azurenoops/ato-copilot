using Ato.Copilot.Core.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Ato.Copilot.Core.Services;

/// <summary>
/// Manages IL6 offline mode behavior for air-gapped environments (FR-034, FR-035).
/// Determines which capabilities are available without network connectivity.
/// </summary>
public class OfflineModeService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<OfflineModeService> _logger;
    private readonly bool _isOffline;

    private static readonly List<OfflineCapability> Capabilities = new()
    {
        new() { CapabilityName = "NIST Control Lookups", RequiresNetwork = false, FallbackDescription = "Uses embedded OSCAL catalog data" },
        new() { CapabilityName = "Cached Assessments", RequiresNetwork = false, FallbackDescription = "Returns previously cached assessment results" },
        new() { CapabilityName = "Document Generation", RequiresNetwork = false, FallbackDescription = "Generates documents from local templates and cached data" },
        new() { CapabilityName = "STIG Lookups", RequiresNetwork = false, FallbackDescription = "Uses embedded STIG control data" },
        new() { CapabilityName = "RMF Guidance", RequiresNetwork = false, FallbackDescription = "Uses embedded RMF process data" },
        new() { CapabilityName = "AI Chat", RequiresNetwork = true, FallbackDescription = "Requires Azure OpenAI connectivity" },
        new() { CapabilityName = "ARM Resource Scan", RequiresNetwork = true, FallbackDescription = "Requires Azure Resource Manager API access" },
        new() { CapabilityName = "Live Assessment", RequiresNetwork = true, FallbackDescription = "Requires live Azure subscription connectivity" },
        new() { CapabilityName = "Prisma Cloud Import", RequiresNetwork = true, FallbackDescription = "Requires Prisma Cloud API connectivity" },
    };

    public OfflineModeService(IConfiguration configuration, ILogger<OfflineModeService> logger)
    {
        _configuration = configuration;
        _logger = logger;
        _isOffline = configuration.GetValue<bool>("Server:OfflineMode");

        if (_isOffline)
        {
            _logger.LogWarning("Server starting in OFFLINE mode — network-dependent capabilities disabled");
            // FR-035: Force NistControls:EnableOfflineFallback when offline
            configuration["NistControls:EnableOfflineFallback"] = "true";
        }
    }

    /// <summary>Whether the server is operating in offline mode.</summary>
    public bool IsOffline => _isOffline;

    /// <summary>Returns capabilities that work without network connectivity.</summary>
    public IReadOnlyList<OfflineCapability> GetAvailableCapabilities()
        => Capabilities.Where(c => !c.RequiresNetwork).ToList().AsReadOnly();

    /// <summary>Returns capabilities that require network connectivity.</summary>
    public IReadOnlyList<OfflineCapability> GetUnavailableCapabilities()
        => Capabilities.Where(c => c.RequiresNetwork).ToList().AsReadOnly();

    /// <summary>
    /// Performs background sync when transitioning from offline to online (FR-037).
    /// Refreshes stale cached data using the provided CacheRepository.
    /// </summary>
    public async Task SyncOnReconnectAsync(CacheRepository cacheRepository, CancellationToken cancellationToken = default)
    {
        if (_isOffline)
        {
            _logger.LogDebug("Skipping sync — server is still in offline mode");
            return;
        }

        var staleEntries = await cacheRepository.GetStaleEntriesAsync(cancellationToken);
        var refreshed = 0;
        var failed = 0;

        foreach (var entry in staleEntries)
        {
            try
            {
                // Mark stale entries for refresh — actual refresh happens on next cache miss
                entry.Source = "stale";
                await cacheRepository.SaveAsync(entry, cancellationToken);
                refreshed++;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to mark stale cache entry {CacheKey} for refresh", entry.CacheKey);
                failed++;
            }
        }

        _logger.LogInformation(
            "Offline-to-online sync complete: {Refreshed} marked for refresh, {Unchanged} unchanged, {Failed} failed",
            refreshed, staleEntries.Count - refreshed - failed, failed);
    }
}
