using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Ato.Copilot.Core.Interfaces.Compliance;
using Ato.Copilot.Core.Models.Compliance;

namespace Ato.Copilot.Agents.Compliance.Services;

/// <summary>
/// Alert correlation service that groups related alerts using sliding time windows.
/// Uses in-memory ConcurrentDictionary for correlation state.
/// 
/// Correlation keys:
///   - "resource:{resourceId}" — same resource producing multiple alerts
///   - "control:{controlId}:{subscriptionId}" — same control failing across resources
///   - "actor:{actorId}" — same actor triggering multiple alerts (anomaly at 10+)
///
/// Features:
///   - 5-minute sliding window with expiry reset on new matches
///   - Anomaly detection at 10+ actor events
///   - Alert storm detection at 50+ alerts in 5 minutes (summary alert)
///   - Periodic sweep to finalize expired windows
/// </summary>
public class AlertCorrelationService : IAlertCorrelationService
{
    private readonly ConcurrentDictionary<string, CorrelationWindow> _windows = new();
    private readonly ILogger<AlertCorrelationService> _logger;

    /// <summary>Duration of the sliding correlation window.</summary>
    internal static readonly TimeSpan WindowDuration = TimeSpan.FromMinutes(5);

    /// <summary>Threshold for actor anomaly detection.</summary>
    internal const int ActorAnomalyThreshold = 10;

    /// <summary>Threshold for alert storm detection (within a single window).</summary>
    internal const int AlertStormThreshold = 50;

    /// <summary>Global counter for detecting alert storms across all windows.</summary>
    private int _recentAlertCount;
    private DateTimeOffset _stormWindowStart = DateTimeOffset.UtcNow;
    private readonly object _stormLock = new();

    public AlertCorrelationService(ILogger<AlertCorrelationService> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<CorrelationResult> CorrelateAlertAsync(
        ComplianceAlert alert,
        CancellationToken cancellationToken = default)
    {
        // Track alert storm detection
        TrackAlertStorm();

        var correlationKeys = BuildCorrelationKeys(alert);
        var now = DateTimeOffset.UtcNow;

        // Try each correlation key to find an active window
        foreach (var key in correlationKeys)
        {
            if (_windows.TryGetValue(key, out var window) && !IsExpired(window, now))
            {
                // Merge into existing window
                window.ChildAlertIds.Add(alert.Id);
                window.LastMatchAt = now;
                window.ParentAlert.ChildAlertCount = window.Count;

                // Check for actor anomaly
                if (key.StartsWith("actor:", StringComparison.OrdinalIgnoreCase)
                    && window.Count >= ActorAnomalyThreshold)
                {
                    _logger.LogWarning(
                        "Actor anomaly detected: {Key} has {Count} alerts in correlation window",
                        key, window.Count);
                    window.ParentAlert.Title = $"[ANOMALY] {window.ParentAlert.Title}";
                }

                _logger.LogDebug(
                    "Alert {AlertId} merged into correlation window {Key} (count: {Count})",
                    alert.AlertId, key, window.Count);

                return Task.FromResult(new CorrelationResult
                {
                    WasMerged = true,
                    Alert = window.ParentAlert,
                    CorrelationKey = key
                });
            }
        }

        // No existing window — check for alert storm before creating new window
        bool isStorm = IsAlertStorm();
        if (isStorm)
        {
            _logger.LogWarning(
                "Alert storm detected: {Count} alerts in {Window} minutes. Creating summary alert.",
                _recentAlertCount, WindowDuration.TotalMinutes);

            alert.Title = $"[ALERT STORM] {alert.Title}";
            alert.Description = $"Alert storm detected: {_recentAlertCount}+ alerts in " +
                $"{WindowDuration.TotalMinutes} minutes. {alert.Description}";
        }

        // Create new correlation window with the first key
        if (correlationKeys.Count > 0)
        {
            var primaryKey = correlationKeys[0];
            alert.IsGrouped = true;
            alert.ChildAlertCount = 0;

            var newWindow = new CorrelationWindow
            {
                Key = primaryKey,
                ParentAlert = alert,
                OpenedAt = now,
                LastMatchAt = now
            };

            _windows[primaryKey] = newWindow;

            // Also register secondary keys pointing to the same window
            for (int i = 1; i < correlationKeys.Count; i++)
            {
                _windows[correlationKeys[i]] = newWindow;
            }

            _logger.LogDebug(
                "New correlation window created for {Key} with alert {AlertId}",
                primaryKey, alert.AlertId);

            return Task.FromResult(new CorrelationResult
            {
                WasMerged = false,
                Alert = alert,
                CorrelationKey = primaryKey
            });
        }

        // No correlation keys available — return alert unchanged
        return Task.FromResult(new CorrelationResult
        {
            WasMerged = false,
            Alert = alert,
            CorrelationKey = string.Empty
        });
    }

    /// <inheritdoc />
    public Task<CorrelationWindow?> GetCorrelationWindowAsync(
        string correlationKey,
        CancellationToken cancellationToken = default)
    {
        if (_windows.TryGetValue(correlationKey, out var window) && !IsExpired(window, DateTimeOffset.UtcNow))
        {
            return Task.FromResult<CorrelationWindow?>(window);
        }

        return Task.FromResult<CorrelationWindow?>(null);
    }

    /// <inheritdoc />
    public Task<int> FinalizeExpiredWindowsAsync(
        CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var finalized = 0;
        var keysToRemove = new List<string>();

        foreach (var kvp in _windows)
        {
            if (IsExpired(kvp.Value, now))
            {
                keysToRemove.Add(kvp.Key);
            }
        }

        // Use HashSet to avoid double-counting windows shared by multiple keys
        var finalizedWindows = new HashSet<CorrelationWindow>(ReferenceEqualityComparer.Instance);

        foreach (var key in keysToRemove)
        {
            if (_windows.TryRemove(key, out var window))
            {
                if (finalizedWindows.Add(window))
                {
                    finalized++;
                    _logger.LogDebug(
                        "Finalized correlation window {Key} with {Count} child alerts",
                        key, window.Count);
                }
            }
        }

        return Task.FromResult(finalized);
    }

    /// <summary>
    /// Build correlation keys for an alert. Returns the list in priority order:
    /// resource, control, actor.
    /// </summary>
    internal static List<string> BuildCorrelationKeys(ComplianceAlert alert)
    {
        var keys = new List<string>();

        // Resource-based correlation (highest priority)
        if (alert.AffectedResources.Count > 0)
        {
            var primaryResource = alert.AffectedResources[0];
            if (!string.IsNullOrEmpty(primaryResource))
            {
                keys.Add($"resource:{primaryResource}");
            }
        }

        // Control-based correlation
        if (!string.IsNullOrEmpty(alert.ControlId) && !string.IsNullOrEmpty(alert.SubscriptionId))
        {
            keys.Add($"control:{alert.ControlId}:{alert.SubscriptionId}");
        }

        // Actor-based correlation
        if (!string.IsNullOrEmpty(alert.ActorId))
        {
            keys.Add($"actor:{alert.ActorId}");
        }

        return keys;
    }

    /// <summary>
    /// Check if a correlation window has expired (last match older than window duration).
    /// </summary>
    private static bool IsExpired(CorrelationWindow window, DateTimeOffset now)
    {
        return (now - window.LastMatchAt) > WindowDuration;
    }

    /// <summary>
    /// Track global alert count for storm detection.
    /// </summary>
    private void TrackAlertStorm()
    {
        lock (_stormLock)
        {
            var now = DateTimeOffset.UtcNow;
            if ((now - _stormWindowStart) > WindowDuration)
            {
                // Reset storm window
                _stormWindowStart = now;
                _recentAlertCount = 0;
            }

            _recentAlertCount++;
        }
    }

    /// <summary>
    /// Check if we are in an alert storm condition (50+ alerts in window).
    /// </summary>
    internal bool IsAlertStorm()
    {
        lock (_stormLock)
        {
            return _recentAlertCount >= AlertStormThreshold;
        }
    }

    /// <summary>
    /// Get the count of active (non-expired) correlation windows.
    /// For testing/diagnostics.
    /// </summary>
    internal int ActiveWindowCount
    {
        get
        {
            var now = DateTimeOffset.UtcNow;
            var uniqueWindows = new HashSet<CorrelationWindow>(ReferenceEqualityComparer.Instance);
            foreach (var kvp in _windows)
            {
                if (!IsExpired(kvp.Value, now))
                    uniqueWindows.Add(kvp.Value);
            }
            return uniqueWindows.Count;
        }
    }
}
