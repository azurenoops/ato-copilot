using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Ato.Copilot.Agents.Compliance.Configuration;
using Ato.Copilot.Core.Interfaces.Compliance;

namespace Ato.Copilot.Agents.Compliance.Services;

/// <summary>
/// Background service that pre-loads the NIST SP 800-53 Rev 5 catalog into <c>IMemoryCache</c>
/// at application startup and refreshes it periodically at 90% of the configured TTL.
/// </summary>
public sealed class NistControlsCacheWarmupService : BackgroundService
{
    private readonly INistControlsService _nistControlsService;
    private readonly IOptions<NistControlsOptions> _options;
    private readonly ILogger<NistControlsCacheWarmupService> _logger;
    private readonly ComplianceValidationService? _validationService;

    /// <summary>Initializes a new instance of the <see cref="NistControlsCacheWarmupService"/> class.</summary>
    /// <param name="nistControlsService">NIST controls service to warm up.</param>
    /// <param name="options">NIST controls configuration options.</param>
    /// <param name="logger">Logger instance.</param>
    /// <param name="validationService">Optional validation service for post-warmup checks.</param>
    public NistControlsCacheWarmupService(
        INistControlsService nistControlsService,
        IOptions<NistControlsOptions> options,
        ILogger<NistControlsCacheWarmupService> logger,
        ComplianceValidationService? validationService = null)
    {
        _nistControlsService = nistControlsService;
        _options = options;
        _logger = logger;
        _validationService = validationService;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var warmupDelay = TimeSpan.FromSeconds(_options.Value.WarmupDelaySeconds);
        _logger.LogInformation("NIST Controls cache warmup scheduled after {DelaySeconds}s initial delay", _options.Value.WarmupDelaySeconds);

        await Task.Delay(warmupDelay, stoppingToken);

        // Calculate refresh interval at 90% of cache TTL
        var refreshInterval = TimeSpan.FromHours(_options.Value.CacheDurationHours * 0.9);
        using var timer = new PeriodicTimer(refreshInterval);

        // Initial warmup
        await WarmupCacheAsync(stoppingToken);

        // Periodic refresh loop
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await WarmupCacheAsync(stoppingToken);
        }
    }

    private async Task WarmupCacheAsync(CancellationToken stoppingToken)
    {
        const int maxRetries = 3;
        var retryDelay = TimeSpan.FromMinutes(5);

        for (var attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                _logger.LogInformation("Starting NIST catalog cache warmup (attempt {Attempt}/{MaxRetries})", attempt, maxRetries);

                var catalog = await _nistControlsService.GetCatalogAsync(stoppingToken);
                if (catalog is null)
                {
                    _logger.LogWarning("NIST catalog warmup returned null on attempt {Attempt}/{MaxRetries}", attempt, maxRetries);
                    if (attempt < maxRetries)
                    {
                        await Task.Delay(retryDelay, stoppingToken);
                        continue;
                    }
                    return;
                }

                var version = await _nistControlsService.GetVersionAsync(stoppingToken);
                _logger.LogInformation(
                    "NIST catalog cache warmup succeeded: version {Version}, {GroupCount} control families",
                    version,
                    catalog.Groups.Count);

                // Run validation if the service is registered
                if (_validationService is not null)
                {
                    try
                    {
                        await _validationService.ValidateControlMappingsAsync(stoppingToken);
                        _logger.LogInformation("Post-warmup control mapping validation completed");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Post-warmup validation failed (non-fatal)");
                    }
                }

                return; // Success — exit retry loop
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("NIST catalog cache warmup cancelled");
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "NIST catalog cache warmup failed on attempt {Attempt}/{MaxRetries}", attempt, maxRetries);
                if (attempt < maxRetries)
                {
                    await Task.Delay(retryDelay, stoppingToken);
                }
            }
        }
    }
}
