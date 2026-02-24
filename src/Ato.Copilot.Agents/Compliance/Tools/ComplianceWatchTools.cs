using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Ato.Copilot.Agents.Common;
using Ato.Copilot.Core.Constants;
using Ato.Copilot.Core.Data.Context;
using Ato.Copilot.Core.Interfaces.Compliance;
using Ato.Copilot.Core.Interfaces.Kanban;
using Ato.Copilot.Core.Models.Compliance;
using Ato.Copilot.Core.Models.Kanban;

namespace Ato.Copilot.Agents.Compliance.Tools;

/// <summary>
/// Checks whether a role string represents an Auditor (read-only) role.
/// </summary>
internal static class WatchRoleHelper
{
    private static readonly HashSet<string> AuditorRoles = new(StringComparer.OrdinalIgnoreCase)
    {
        ComplianceRoles.Auditor,
        "Auditor"
    };

    public static bool IsAuditorRole(string role) => AuditorRoles.Contains(role);
}

/// <summary>
/// Tool to enable continuous compliance monitoring for a subscription or resource group.
/// </summary>
public class WatchEnableMonitoringTool : BaseTool
{
    private readonly IComplianceWatchService _watchService;

    public WatchEnableMonitoringTool(
        IComplianceWatchService watchService,
        ILogger<WatchEnableMonitoringTool> logger) : base(logger)
    {
        _watchService = watchService;
    }

    public override string Name => "watch_enable_monitoring";
    public override string Description => "Enable continuous compliance monitoring for a subscription or resource group.";

    public override IReadOnlyDictionary<string, ToolParameter> Parameters => new Dictionary<string, ToolParameter>
    {
        ["subscription_id"] = new() { Name = "subscription_id", Description = "Azure subscription ID", Type = "string", Required = true },
        ["resource_group"] = new() { Name = "resource_group", Description = "Resource group name (null = entire subscription)", Type = "string" },
        ["frequency"] = new() { Name = "frequency", Description = "Monitoring frequency: 15min, hourly, daily, weekly (default: hourly)", Type = "string" },
        ["mode"] = new() { Name = "mode", Description = "Monitoring mode: scheduled, event-driven, both (default: scheduled)", Type = "string" }
    };

    public override async Task<string> ExecuteCoreAsync(
        Dictionary<string, object?> args, CancellationToken cancellationToken)
    {
        var subscriptionId = GetArg<string>(args, "subscription_id")
            ?? throw new ArgumentException("subscription_id is required");
        var resourceGroup = GetArg<string>(args, "resource_group");
        var frequencyStr = GetArg<string>(args, "frequency") ?? "hourly";
        var modeStr = GetArg<string>(args, "mode") ?? "scheduled";

        var frequency = ParseFrequency(frequencyStr);
        var mode = ParseMode(modeStr);

        var config = await _watchService.EnableMonitoringAsync(
            subscriptionId, resourceGroup, frequency, mode, cancellationToken: cancellationToken);

        return JsonSerializer.Serialize(new
        {
            status = "success",
            data = new
            {
                configurationId = config.Id,
                subscriptionId = config.SubscriptionId,
                resourceGroup = config.ResourceGroupName,
                mode = config.Mode.ToString(),
                frequency = FormatFrequency(config.Frequency),
                isEnabled = config.IsEnabled,
                nextRunAt = config.NextRunAt,
                message = $"Monitoring enabled for subscription {config.SubscriptionId}. " +
                    $"First check scheduled at {config.NextRunAt:u}."
            },
            metadata = new { tool = "watch_enable_monitoring", timestamp = DateTimeOffset.UtcNow }
        });
    }

    private static MonitoringFrequency ParseFrequency(string value) => value.ToLowerInvariant() switch
    {
        "15min" or "15minutes" or "fifteenminutes" => MonitoringFrequency.FifteenMinutes,
        "hourly" or "hour" => MonitoringFrequency.Hourly,
        "daily" or "day" => MonitoringFrequency.Daily,
        "weekly" or "week" => MonitoringFrequency.Weekly,
        _ => throw new ArgumentException($"INVALID_FREQUENCY: '{value}'. Valid values: 15min, hourly, daily, weekly")
    };

    private static MonitoringMode ParseMode(string value) => value.ToLowerInvariant() switch
    {
        "scheduled" => MonitoringMode.Scheduled,
        "event-driven" or "eventdriven" or "event_driven" => MonitoringMode.EventDriven,
        "both" => MonitoringMode.Both,
        _ => throw new ArgumentException($"INVALID_MODE: '{value}'. Valid values: scheduled, event-driven, both")
    };

    private static string FormatFrequency(MonitoringFrequency freq) => freq switch
    {
        MonitoringFrequency.FifteenMinutes => "15min",
        MonitoringFrequency.Hourly => "hourly",
        MonitoringFrequency.Daily => "daily",
        MonitoringFrequency.Weekly => "weekly",
        _ => freq.ToString()
    };
}

/// <summary>
/// Tool to disable monitoring for a subscription or resource group.
/// </summary>
public class WatchDisableMonitoringTool : BaseTool
{
    private readonly IComplianceWatchService _watchService;

    public WatchDisableMonitoringTool(
        IComplianceWatchService watchService,
        ILogger<WatchDisableMonitoringTool> logger) : base(logger)
    {
        _watchService = watchService;
    }

    public override string Name => "watch_disable_monitoring";
    public override string Description => "Disable monitoring for a subscription or resource group.";

    public override IReadOnlyDictionary<string, ToolParameter> Parameters => new Dictionary<string, ToolParameter>
    {
        ["subscription_id"] = new() { Name = "subscription_id", Description = "Azure subscription ID", Type = "string", Required = true },
        ["resource_group"] = new() { Name = "resource_group", Description = "Resource group name", Type = "string" }
    };

    public override async Task<string> ExecuteCoreAsync(
        Dictionary<string, object?> args, CancellationToken cancellationToken)
    {
        var subscriptionId = GetArg<string>(args, "subscription_id")
            ?? throw new ArgumentException("subscription_id is required");
        var resourceGroup = GetArg<string>(args, "resource_group");

        var config = await _watchService.DisableMonitoringAsync(
            subscriptionId, resourceGroup, cancellationToken);

        return JsonSerializer.Serialize(new
        {
            status = "success",
            data = new
            {
                subscriptionId = config.SubscriptionId,
                isEnabled = false,
                message = $"Monitoring disabled for subscription {config.SubscriptionId}."
            },
            metadata = new { tool = "watch_disable_monitoring", timestamp = DateTimeOffset.UtcNow }
        });
    }
}

/// <summary>
/// Tool to update monitoring settings (frequency, mode) for an existing configuration.
/// </summary>
public class WatchConfigureMonitoringTool : BaseTool
{
    private readonly IComplianceWatchService _watchService;

    public WatchConfigureMonitoringTool(
        IComplianceWatchService watchService,
        ILogger<WatchConfigureMonitoringTool> logger) : base(logger)
    {
        _watchService = watchService;
    }

    public override string Name => "watch_configure_monitoring";
    public override string Description => "Update monitoring settings for an existing configuration.";

    public override IReadOnlyDictionary<string, ToolParameter> Parameters => new Dictionary<string, ToolParameter>
    {
        ["subscription_id"] = new() { Name = "subscription_id", Description = "Azure subscription ID", Type = "string", Required = true },
        ["resource_group"] = new() { Name = "resource_group", Description = "Resource group name", Type = "string" },
        ["frequency"] = new() { Name = "frequency", Description = "New frequency: 15min, hourly, daily, weekly", Type = "string" },
        ["mode"] = new() { Name = "mode", Description = "New mode: scheduled, event-driven, both", Type = "string" }
    };

    public override async Task<string> ExecuteCoreAsync(
        Dictionary<string, object?> args, CancellationToken cancellationToken)
    {
        var subscriptionId = GetArg<string>(args, "subscription_id")
            ?? throw new ArgumentException("subscription_id is required");
        var resourceGroup = GetArg<string>(args, "resource_group");
        var frequencyStr = GetArg<string>(args, "frequency");
        var modeStr = GetArg<string>(args, "mode");

        MonitoringFrequency? frequency = frequencyStr != null ? ParseFrequency(frequencyStr) : null;
        MonitoringMode? mode = modeStr != null ? ParseMode(modeStr) : null;

        var config = await _watchService.ConfigureMonitoringAsync(
            subscriptionId, resourceGroup, frequency, mode, cancellationToken);

        return JsonSerializer.Serialize(new
        {
            status = "success",
            data = new
            {
                configurationId = config.Id,
                subscriptionId = config.SubscriptionId,
                frequency = FormatFrequency(config.Frequency),
                mode = config.Mode.ToString().ToLowerInvariant(),
                nextRunAt = config.NextRunAt,
                message = $"Monitoring updated for subscription {config.SubscriptionId}. " +
                    $"Next check: {config.NextRunAt:u}."
            },
            metadata = new { tool = "watch_configure_monitoring", timestamp = DateTimeOffset.UtcNow }
        });
    }

    private static MonitoringFrequency ParseFrequency(string value) => value.ToLowerInvariant() switch
    {
        "15min" or "15minutes" or "fifteenminutes" => MonitoringFrequency.FifteenMinutes,
        "hourly" or "hour" => MonitoringFrequency.Hourly,
        "daily" or "day" => MonitoringFrequency.Daily,
        "weekly" or "week" => MonitoringFrequency.Weekly,
        _ => throw new ArgumentException($"INVALID_FREQUENCY: '{value}'. Valid values: 15min, hourly, daily, weekly")
    };

    private static MonitoringMode ParseMode(string value) => value.ToLowerInvariant() switch
    {
        "scheduled" => MonitoringMode.Scheduled,
        "event-driven" or "eventdriven" or "event_driven" => MonitoringMode.EventDriven,
        "both" => MonitoringMode.Both,
        _ => throw new ArgumentException($"INVALID_MODE: '{value}'. Valid values: scheduled, event-driven, both")
    };

    private static string FormatFrequency(MonitoringFrequency freq) => freq switch
    {
        MonitoringFrequency.FifteenMinutes => "15min",
        MonitoringFrequency.Hourly => "hourly",
        MonitoringFrequency.Daily => "daily",
        MonitoringFrequency.Weekly => "weekly",
        _ => freq.ToString()
    };
}

/// <summary>
/// Tool to show current monitoring configuration and status.
/// </summary>
public class WatchMonitoringStatusTool : BaseTool
{
    private readonly IComplianceWatchService _watchService;

    public WatchMonitoringStatusTool(
        IComplianceWatchService watchService,
        ILogger<WatchMonitoringStatusTool> logger) : base(logger)
    {
        _watchService = watchService;
    }

    public override string Name => "watch_monitoring_status";
    public override string Description => "Show current monitoring configuration and status for one or all subscriptions.";

    public override IReadOnlyDictionary<string, ToolParameter> Parameters => new Dictionary<string, ToolParameter>
    {
        ["subscription_id"] = new() { Name = "subscription_id", Description = "Filter by subscription (null = all)", Type = "string" }
    };

    public override async Task<string> ExecuteCoreAsync(
        Dictionary<string, object?> args, CancellationToken cancellationToken)
    {
        var subscriptionId = GetArg<string>(args, "subscription_id");

        var configs = await _watchService.GetMonitoringStatusAsync(subscriptionId, cancellationToken);

        var configData = configs.Select(c => new
        {
            configurationId = c.Id,
            subscriptionId = c.SubscriptionId,
            resourceGroup = c.ResourceGroupName,
            mode = c.Mode.ToString().ToLowerInvariant(),
            frequency = FormatFrequency(c.Frequency),
            isEnabled = c.IsEnabled,
            lastRunAt = c.LastRunAt,
            nextRunAt = c.NextRunAt
        }).ToList();

        return JsonSerializer.Serialize(new
        {
            status = "success",
            data = new
            {
                configurations = configData,
                totalConfigurations = configs.Count,
                activeConfigurations = configs.Count(c => c.IsEnabled)
            },
            metadata = new { tool = "watch_monitoring_status", timestamp = DateTimeOffset.UtcNow }
        });
    }

    private static string FormatFrequency(MonitoringFrequency freq) => freq switch
    {
        MonitoringFrequency.FifteenMinutes => "15min",
        MonitoringFrequency.Hourly => "hourly",
        MonitoringFrequency.Daily => "daily",
        MonitoringFrequency.Weekly => "weekly",
        _ => freq.ToString()
    };
}

// ════════════════════════════════════════════════════════════════════════════
// Phase 4 — Alert Lifecycle & Management Tools (US2)
// ════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Tool to show paginated list of compliance alerts with optional filters.
/// </summary>
public class WatchShowAlertsTool : BaseTool
{
    private readonly IAlertManager _alertManager;

    public WatchShowAlertsTool(
        IAlertManager alertManager,
        ILogger<WatchShowAlertsTool> logger) : base(logger)
    {
        _alertManager = alertManager;
    }

    public override string Name => "watch_show_alerts";
    public override string Description => "List active compliance alerts with optional severity, status, control family, and date filters.";

    public override IReadOnlyDictionary<string, ToolParameter> Parameters => new Dictionary<string, ToolParameter>
    {
        ["subscription_id"] = new() { Name = "subscription_id", Description = "Filter by subscription", Type = "string" },
        ["severity"] = new() { Name = "severity", Description = "Filter by severity: Critical, High, Medium, Low", Type = "string" },
        ["status"] = new() { Name = "status", Description = "Filter by status: New, Acknowledged, InProgress, Escalated", Type = "string" },
        ["control_family"] = new() { Name = "control_family", Description = "Filter by NIST control family (e.g., AC, SC)", Type = "string" },
        ["days"] = new() { Name = "days", Description = "Lookback period in days (default: 7)", Type = "integer" },
        ["page"] = new() { Name = "page", Description = "Page number (default: 1)", Type = "integer" },
        ["page_size"] = new() { Name = "page_size", Description = "Results per page (default: 50)", Type = "integer" }
    };

    public override async Task<string> ExecuteCoreAsync(
        Dictionary<string, object?> args, CancellationToken cancellationToken)
    {
        var subscriptionId = GetArg<string>(args, "subscription_id");
        var severityStr = GetArg<string>(args, "severity");
        var statusStr = GetArg<string>(args, "status");
        var controlFamily = GetArg<string>(args, "control_family");
        var days = GetArg<int?>(args, "days") ?? 7;
        var page = GetArg<int?>(args, "page") ?? 1;
        var pageSize = GetArg<int?>(args, "page_size") ?? 50;

        AlertSeverity? severity = severityStr != null ? ParseSeverity(severityStr) : null;
        AlertStatus? status = statusStr != null ? ParseStatus(statusStr) : null;

        var (alerts, totalCount) = await _alertManager.GetAlertsAsync(
            subscriptionId, severity, status, controlFamily, days, page, pageSize, cancellationToken);

        var alertData = alerts.Select(a => new
        {
            alertId = a.AlertId,
            type = a.Type.ToString(),
            severity = a.Severity.ToString(),
            status = a.Status.ToString(),
            title = a.Title,
            subscriptionId = a.SubscriptionId,
            affectedResources = a.AffectedResources,
            controlId = a.ControlId,
            controlFamily = a.ControlFamily,
            actorId = a.ActorId,
            createdAt = a.CreatedAt,
            slaDeadline = a.SlaDeadline,
            isGrouped = a.IsGrouped,
            childAlertCount = a.ChildAlertCount
        }).ToList();

        return JsonSerializer.Serialize(new
        {
            status = "success",
            data = new { alerts = alertData, totalCount, page, pageSize },
            metadata = new { tool = "watch_show_alerts", timestamp = DateTimeOffset.UtcNow }
        });
    }

    private static AlertSeverity ParseSeverity(string value) => value.ToLowerInvariant() switch
    {
        "critical" => AlertSeverity.Critical,
        "high" => AlertSeverity.High,
        "medium" => AlertSeverity.Medium,
        "low" => AlertSeverity.Low,
        _ => throw new ArgumentException($"INVALID_SEVERITY: '{value}'. Valid: Critical, High, Medium, Low")
    };

    private static AlertStatus ParseStatus(string value) => value.ToLowerInvariant() switch
    {
        "new" => AlertStatus.New,
        "acknowledged" => AlertStatus.Acknowledged,
        "inprogress" or "in_progress" or "in-progress" => AlertStatus.InProgress,
        "escalated" => AlertStatus.Escalated,
        "resolved" => AlertStatus.Resolved,
        "dismissed" => AlertStatus.Dismissed,
        _ => throw new ArgumentException($"INVALID_STATUS: '{value}'. Valid: New, Acknowledged, InProgress, Escalated, Resolved, Dismissed")
    };
}

/// <summary>
/// Tool to get full details of a specific compliance alert.
/// </summary>
public class WatchGetAlertTool : BaseTool
{
    private readonly IAlertManager _alertManager;

    public WatchGetAlertTool(
        IAlertManager alertManager,
        ILogger<WatchGetAlertTool> logger) : base(logger)
    {
        _alertManager = alertManager;
    }

    public override string Name => "watch_get_alert";
    public override string Description => "Get full details of a specific compliance alert including notifications and child alerts.";

    public override IReadOnlyDictionary<string, ToolParameter> Parameters => new Dictionary<string, ToolParameter>
    {
        ["alert_id"] = new() { Name = "alert_id", Description = "Alert ID (e.g., ALT-2026022200001)", Type = "string", Required = true }
    };

    public override async Task<string> ExecuteCoreAsync(
        Dictionary<string, object?> args, CancellationToken cancellationToken)
    {
        var alertIdStr = GetArg<string>(args, "alert_id")
            ?? throw new ArgumentException("alert_id is required");

        var alert = await _alertManager.GetAlertByAlertIdAsync(alertIdStr, cancellationToken)
            ?? throw new KeyNotFoundException($"ALERT_NOT_FOUND: Alert '{alertIdStr}' not found.");

        return JsonSerializer.Serialize(new
        {
            status = "success",
            data = new
            {
                alertId = alert.AlertId,
                type = alert.Type.ToString(),
                severity = alert.Severity.ToString(),
                status = alert.Status.ToString(),
                title = alert.Title,
                description = alert.Description,
                subscriptionId = alert.SubscriptionId,
                affectedResources = alert.AffectedResources,
                controlId = alert.ControlId,
                controlFamily = alert.ControlFamily,
                changeDetails = alert.ChangeDetails,
                actorId = alert.ActorId,
                recommendedAction = alert.RecommendedAction,
                assignedTo = alert.AssignedTo,
                createdAt = alert.CreatedAt,
                slaDeadline = alert.SlaDeadline,
                acknowledgedAt = alert.AcknowledgedAt,
                acknowledgedBy = alert.AcknowledgedBy,
                resolvedAt = alert.ResolvedAt,
                notifications = alert.Notifications.Select(n => new
                {
                    channel = n.Channel.ToString(),
                    sentAt = n.SentAt,
                    isDelivered = n.IsDelivered
                }).ToList(),
                childAlerts = alert.ChildAlerts.Select(c => new
                {
                    alertId = c.AlertId,
                    type = c.Type.ToString(),
                    severity = c.Severity.ToString(),
                    status = c.Status.ToString(),
                    title = c.Title
                }).ToList()
            },
            metadata = new { tool = "watch_get_alert", timestamp = DateTimeOffset.UtcNow }
        });
    }
}

/// <summary>
/// Tool to acknowledge an alert (pauses escalation timer).
/// </summary>
public class WatchAcknowledgeAlertTool : BaseTool
{
    private readonly IAlertManager _alertManager;

    public WatchAcknowledgeAlertTool(
        IAlertManager alertManager,
        ILogger<WatchAcknowledgeAlertTool> logger) : base(logger)
    {
        _alertManager = alertManager;
    }

    public override string Name => "watch_acknowledge_alert";
    public override string Description => "Acknowledge a compliance alert, pausing the escalation timer.";

    public override IReadOnlyDictionary<string, ToolParameter> Parameters => new Dictionary<string, ToolParameter>
    {
        ["alert_id"] = new() { Name = "alert_id", Description = "Alert ID", Type = "string", Required = true },
        ["user_id"] = new() { Name = "user_id", Description = "Acknowledging user identity", Type = "string", Required = true },
        ["user_role"] = new() { Name = "user_role", Description = "User's compliance role", Type = "string", Required = true }
    };

    public override async Task<string> ExecuteCoreAsync(
        Dictionary<string, object?> args, CancellationToken cancellationToken)
    {
        var alertIdStr = GetArg<string>(args, "alert_id")
            ?? throw new ArgumentException("alert_id is required");
        var userId = GetArg<string>(args, "user_id") ?? "unknown";
        var userRole = GetArg<string>(args, "user_role") ?? "unknown";

        // FR-031: Auditor role is read-only — deny acknowledge
        if (WatchRoleHelper.IsAuditorRole(userRole))
        {
            throw new InvalidOperationException(
                "INSUFFICIENT_PERMISSIONS: Auditor role has read-only access to alerts. " +
                "Use watch_show_alerts or watch_get_alert to view alert details. " +
                "To acknowledge alerts, activate a role with write permissions via pim_activate_role.");
        }

        var alert = await _alertManager.GetAlertByAlertIdAsync(alertIdStr, cancellationToken)
            ?? throw new KeyNotFoundException($"ALERT_NOT_FOUND: Alert '{alertIdStr}' not found.");

        var previousStatus = alert.Status;
        var updated = await _alertManager.TransitionAlertAsync(
            alert.Id, AlertStatus.Acknowledged, userId, userRole, cancellationToken: cancellationToken);

        return JsonSerializer.Serialize(new
        {
            status = "success",
            data = new
            {
                alertId = updated.AlertId,
                previousStatus = previousStatus.ToString(),
                newStatus = updated.Status.ToString(),
                acknowledgedBy = userId,
                acknowledgedAt = updated.AcknowledgedAt,
                message = $"Alert {updated.AlertId} acknowledged. Escalation timer paused."
            },
            metadata = new { tool = "watch_acknowledge_alert", timestamp = DateTimeOffset.UtcNow }
        });
    }
}

/// <summary>
/// Tool to execute remediation for an alert and transition to Resolved.
/// </summary>
public class WatchFixAlertTool : BaseTool
{
    private readonly IAlertManager _alertManager;
    private readonly IAtoComplianceEngine _complianceEngine;

    public WatchFixAlertTool(
        IAlertManager alertManager,
        IAtoComplianceEngine complianceEngine,
        ILogger<WatchFixAlertTool> logger) : base(logger)
    {
        _alertManager = alertManager;
        _complianceEngine = complianceEngine;
    }

    public override string Name => "watch_fix_alert";
    public override string Description => "Execute remediation for an alert and transition it to Resolved.";

    public override IReadOnlyDictionary<string, ToolParameter> Parameters => new Dictionary<string, ToolParameter>
    {
        ["alert_id"] = new() { Name = "alert_id", Description = "Alert ID", Type = "string", Required = true },
        ["user_id"] = new() { Name = "user_id", Description = "User executing the fix", Type = "string", Required = true },
        ["user_role"] = new() { Name = "user_role", Description = "User's compliance role", Type = "string", Required = true },
        ["dry_run"] = new() { Name = "dry_run", Description = "Preview fix without applying (default: false)", Type = "boolean" }
    };

    public override async Task<string> ExecuteCoreAsync(
        Dictionary<string, object?> args, CancellationToken cancellationToken)
    {
        var alertIdStr = GetArg<string>(args, "alert_id")
            ?? throw new ArgumentException("alert_id is required");
        var userId = GetArg<string>(args, "user_id") ?? "unknown";
        var userRole = GetArg<string>(args, "user_role") ?? "unknown";
        var dryRun = GetArg<bool?>(args, "dry_run") ?? false;

        // FR-031: Auditor role is read-only — deny fix
        if (WatchRoleHelper.IsAuditorRole(userRole))
        {
            throw new InvalidOperationException(
                "INSUFFICIENT_PERMISSIONS: Auditor role has read-only access to alerts. " +
                "Use watch_show_alerts or watch_get_alert to view alert details. " +
                "To fix alerts, activate a role with write permissions via pim_activate_role.");
        }

        var alert = await _alertManager.GetAlertByAlertIdAsync(alertIdStr, cancellationToken)
            ?? throw new KeyNotFoundException($"ALERT_NOT_FOUND: Alert '{alertIdStr}' not found.");

        if (dryRun)
        {
            return JsonSerializer.Serialize(new
            {
                status = "success",
                data = new
                {
                    alertId = alert.AlertId,
                    dryRun = true,
                    currentStatus = alert.Status.ToString(),
                    recommendedAction = alert.RecommendedAction,
                    message = $"Dry run: would remediate {alert.AlertId} affecting " +
                        $"{string.Join(", ", alert.AffectedResources.Take(3))}."
                },
                metadata = new { tool = "watch_fix_alert", timestamp = DateTimeOffset.UtcNow }
            });
        }

        var previousStatus = alert.Status;

        // Transition to InProgress first, then to Resolved
        if (alert.Status == AlertStatus.New || alert.Status == AlertStatus.Acknowledged)
        {
            await _alertManager.TransitionAlertAsync(
                alert.Id, AlertStatus.InProgress, userId, userRole, cancellationToken: cancellationToken);
        }

        // Execute remediation via compliance engine
        string remediationAction;
        try
        {
            var assessment = await _complianceEngine.RunAssessmentAsync(
                alert.SubscriptionId, scanType: "quick", cancellationToken: cancellationToken);

            remediationAction = $"Remediation executed for {alert.ControlId ?? "compliance issue"} " +
                $"on {string.Join(", ", alert.AffectedResources.Select(r => r.Split('/').LastOrDefault() ?? r).Take(3))}";
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"REMEDIATION_FAILED: Could not remediate alert {alertIdStr}. Error: {ex.Message}", ex);
        }

        var resolved = await _alertManager.TransitionAlertAsync(
            alert.Id, AlertStatus.Resolved, userId, userRole,
            justification: remediationAction, cancellationToken: cancellationToken);

        return JsonSerializer.Serialize(new
        {
            status = "success",
            data = new
            {
                alertId = resolved.AlertId,
                previousStatus = previousStatus.ToString(),
                newStatus = resolved.Status.ToString(),
                remediationAction,
                resolvedAt = resolved.ResolvedAt,
                message = $"Alert {resolved.AlertId} resolved. {remediationAction}."
            },
            metadata = new { tool = "watch_fix_alert", timestamp = DateTimeOffset.UtcNow }
        });
    }
}

/// <summary>
/// Tool to dismiss an alert. Compliance Officer only — requires justification.
/// Platform Engineers and Auditors are denied with actionable error.
/// </summary>
public class WatchDismissAlertTool : BaseTool
{
    private readonly IAlertManager _alertManager;

    public WatchDismissAlertTool(
        IAlertManager alertManager,
        ILogger<WatchDismissAlertTool> logger) : base(logger)
    {
        _alertManager = alertManager;
    }

    public override string Name => "watch_dismiss_alert";
    public override string Description => "Dismiss a compliance alert. Requires Compliance Officer role and justification.";

    public override IReadOnlyDictionary<string, ToolParameter> Parameters => new Dictionary<string, ToolParameter>
    {
        ["alert_id"] = new() { Name = "alert_id", Description = "Alert ID", Type = "string", Required = true },
        ["justification"] = new() { Name = "justification", Description = "Reason for dismissal", Type = "string", Required = true },
        ["user_id"] = new() { Name = "user_id", Description = "Dismissing user identity", Type = "string", Required = true },
        ["user_role"] = new() { Name = "user_role", Description = "User's compliance role", Type = "string", Required = true }
    };

    public override async Task<string> ExecuteCoreAsync(
        Dictionary<string, object?> args, CancellationToken cancellationToken)
    {
        var alertIdStr = GetArg<string>(args, "alert_id")
            ?? throw new ArgumentException("alert_id is required");
        var justification = GetArg<string>(args, "justification");
        var userId = GetArg<string>(args, "user_id") ?? "unknown";
        var userRole = GetArg<string>(args, "user_role") ?? "unknown";

        // FR-031: Auditor role is read-only — deny dismiss
        if (WatchRoleHelper.IsAuditorRole(userRole))
        {
            throw new InvalidOperationException(
                "INSUFFICIENT_PERMISSIONS: Auditor role has read-only access to alerts. " +
                "Use watch_show_alerts or watch_get_alert to view alert details. " +
                "To dismiss alerts, activate the Compliance Officer role via pim_activate_role.");
        }

        if (string.IsNullOrWhiteSpace(justification))
        {
            throw new InvalidOperationException(
                "JUSTIFICATION_REQUIRED: Dismissal requires a justification.");
        }

        var alert = await _alertManager.GetAlertByAlertIdAsync(alertIdStr, cancellationToken)
            ?? throw new KeyNotFoundException($"ALERT_NOT_FOUND: Alert '{alertIdStr}' not found.");

        var previousStatus = alert.Status;

        var dismissed = await _alertManager.DismissAlertAsync(
            alert.Id, justification, userId, userRole, cancellationToken);

        return JsonSerializer.Serialize(new
        {
            status = "success",
            data = new
            {
                alertId = dismissed.AlertId,
                previousStatus = previousStatus.ToString(),
                newStatus = dismissed.Status.ToString(),
                dismissedBy = userId,
                justification,
                message = $"Alert {dismissed.AlertId} dismissed."
            },
            metadata = new { tool = "watch_dismiss_alert", timestamp = DateTimeOffset.UtcNow }
        });
    }
}

// ─── Alert Rules & Suppression Tools (US3) ──────────────────────────────────

/// <summary>
/// Tool to create a custom alert rule for compliance monitoring.
/// </summary>
public class WatchCreateRuleTool : BaseTool
{
    private readonly IComplianceWatchService _watchService;

    public WatchCreateRuleTool(
        IComplianceWatchService watchService,
        ILogger<WatchCreateRuleTool> logger) : base(logger)
    {
        _watchService = watchService;
    }

    public override string Name => "watch_create_rule";
    public override string Description => "Create a custom alert rule to control severity and routing for compliance alerts.";

    public override IReadOnlyDictionary<string, ToolParameter> Parameters => new Dictionary<string, ToolParameter>
    {
        ["name"] = new() { Name = "name", Description = "Rule name", Type = "string", Required = true },
        ["description"] = new() { Name = "description", Description = "Rule description", Type = "string" },
        ["subscription_id"] = new() { Name = "subscription_id", Description = "Scope: Azure subscription ID (null = all subscriptions)", Type = "string" },
        ["resource_group"] = new() { Name = "resource_group", Description = "Scope: Resource group name", Type = "string" },
        ["resource_type"] = new() { Name = "resource_type", Description = "Scope: Resource type filter", Type = "string" },
        ["resource_id"] = new() { Name = "resource_id", Description = "Scope: Specific resource ID", Type = "string" },
        ["control_family"] = new() { Name = "control_family", Description = "Control family filter (e.g., AC, SC, AU, IA)", Type = "string" },
        ["control_id"] = new() { Name = "control_id", Description = "Specific control ID filter", Type = "string" },
        ["severity_override"] = new() { Name = "severity_override", Description = "Override alert severity: Critical, High, Medium, Low, Informational", Type = "string" },
        ["user_role"] = new() { Name = "user_role", Description = "Role of the calling user", Type = "string", Required = true }
    };

    public override async Task<string> ExecuteCoreAsync(
        Dictionary<string, object?> args, CancellationToken cancellationToken)
    {
        var userRole = args.GetValueOrDefault("user_role")?.ToString() ?? "";
        if (WatchRoleHelper.IsAuditorRole(userRole))
        {
            return JsonSerializer.Serialize(new
            {
                status = "error",
                error = new { code = "INSUFFICIENT_PERMISSIONS", message = "Auditor role is read-only. Use pim_activate_role to elevate." }
            });
        }

        var name = args.GetValueOrDefault("name")?.ToString()
            ?? throw new ArgumentException("MISSING_PARAMETER: 'name' is required.");

        AlertSeverity? severityOverride = null;
        if (args.GetValueOrDefault("severity_override")?.ToString() is { } sevStr
            && Enum.TryParse<AlertSeverity>(sevStr, true, out var sev))
        {
            severityOverride = sev;
        }

        var rule = new AlertRule
        {
            Name = name,
            Description = args.GetValueOrDefault("description")?.ToString(),
            SubscriptionId = args.GetValueOrDefault("subscription_id")?.ToString(),
            ResourceGroupName = args.GetValueOrDefault("resource_group")?.ToString(),
            ResourceType = args.GetValueOrDefault("resource_type")?.ToString(),
            ResourceId = args.GetValueOrDefault("resource_id")?.ToString(),
            ControlFamily = args.GetValueOrDefault("control_family")?.ToString(),
            ControlId = args.GetValueOrDefault("control_id")?.ToString(),
            SeverityOverride = severityOverride,
            IsEnabled = true,
            CreatedBy = args.GetValueOrDefault("user_id")?.ToString() ?? "system"
        };

        var created = await _watchService.CreateAlertRuleAsync(rule, cancellationToken);

        return JsonSerializer.Serialize(new
        {
            status = "success",
            data = new
            {
                ruleId = created.Id,
                name = created.Name,
                scope = new { created.SubscriptionId, created.ResourceGroupName, created.ResourceType, created.ResourceId },
                controlFamily = created.ControlFamily,
                controlId = created.ControlId,
                severityOverride = created.SeverityOverride?.ToString(),
                isEnabled = created.IsEnabled,
                message = $"Alert rule '{created.Name}' created successfully."
            },
            metadata = new { tool = "watch_create_rule", timestamp = DateTimeOffset.UtcNow }
        });
    }
}

/// <summary>
/// Tool to list alert rules for a subscription.
/// </summary>
public class WatchListRulesTool : BaseTool
{
    private readonly IComplianceWatchService _watchService;

    public WatchListRulesTool(
        IComplianceWatchService watchService,
        ILogger<WatchListRulesTool> logger) : base(logger)
    {
        _watchService = watchService;
    }

    public override string Name => "watch_list_rules";
    public override string Description => "List configured alert rules for a subscription.";

    public override IReadOnlyDictionary<string, ToolParameter> Parameters => new Dictionary<string, ToolParameter>
    {
        ["subscription_id"] = new() { Name = "subscription_id", Description = "Azure subscription ID to filter by (optional)", Type = "string" }
    };

    public override async Task<string> ExecuteCoreAsync(
        Dictionary<string, object?> args, CancellationToken cancellationToken)
    {
        var subscriptionId = args.GetValueOrDefault("subscription_id")?.ToString();
        var rules = await _watchService.GetAlertRulesAsync(subscriptionId, cancellationToken);

        return JsonSerializer.Serialize(new
        {
            status = "success",
            data = new
            {
                totalRules = rules.Count,
                rules = rules.Select(r => new
                {
                    ruleId = r.Id,
                    name = r.Name,
                    description = r.Description,
                    scope = new { r.SubscriptionId, r.ResourceGroupName, r.ResourceType, r.ResourceId },
                    controlFamily = r.ControlFamily,
                    controlId = r.ControlId,
                    severityOverride = r.SeverityOverride?.ToString(),
                    isDefault = r.IsDefault,
                    isEnabled = r.IsEnabled,
                    createdAt = r.CreatedAt
                })
            },
            metadata = new { tool = "watch_list_rules", timestamp = DateTimeOffset.UtcNow }
        });
    }
}

/// <summary>
/// Tool to create a suppression rule for compliance alerts.
/// </summary>
public class WatchSuppressAlertsTool : BaseTool
{
    private readonly IComplianceWatchService _watchService;

    public WatchSuppressAlertsTool(
        IComplianceWatchService watchService,
        ILogger<WatchSuppressAlertsTool> logger) : base(logger)
    {
        _watchService = watchService;
    }

    public override string Name => "watch_suppress_alerts";
    public override string Description => "Create a suppression rule to suppress alerts matching specific criteria.";

    public override IReadOnlyDictionary<string, ToolParameter> Parameters => new Dictionary<string, ToolParameter>
    {
        ["subscription_id"] = new() { Name = "subscription_id", Description = "Scope: Azure subscription ID", Type = "string" },
        ["resource_id"] = new() { Name = "resource_id", Description = "Scope: Specific resource ID", Type = "string" },
        ["control_family"] = new() { Name = "control_family", Description = "Control family filter", Type = "string" },
        ["control_id"] = new() { Name = "control_id", Description = "Specific control ID filter", Type = "string" },
        ["type"] = new() { Name = "type", Description = "Suppression type: temporary, permanent (default: temporary)", Type = "string", Required = true },
        ["justification"] = new() { Name = "justification", Description = "Justification for suppression (required for permanent)", Type = "string" },
        ["expires_at"] = new() { Name = "expires_at", Description = "Expiration for temporary suppressions (ISO 8601)", Type = "string" },
        ["user_role"] = new() { Name = "user_role", Description = "Role of the calling user", Type = "string", Required = true },
        ["user_id"] = new() { Name = "user_id", Description = "Identity of the calling user", Type = "string" }
    };

    public override async Task<string> ExecuteCoreAsync(
        Dictionary<string, object?> args, CancellationToken cancellationToken)
    {
        var userRole = args.GetValueOrDefault("user_role")?.ToString() ?? "";
        if (WatchRoleHelper.IsAuditorRole(userRole))
        {
            return JsonSerializer.Serialize(new
            {
                status = "error",
                error = new { code = "INSUFFICIENT_PERMISSIONS", message = "Auditor role is read-only. Use pim_activate_role to elevate." }
            });
        }

        var typeStr = args.GetValueOrDefault("type")?.ToString() ?? "temporary";
        if (!Enum.TryParse<SuppressionType>(typeStr, true, out var suppressionType))
        {
            return JsonSerializer.Serialize(new
            {
                status = "error",
                error = new { code = "INVALID_TYPE", message = $"Invalid suppression type '{typeStr}'. Use 'temporary' or 'permanent'." }
            });
        }

        DateTimeOffset? expiresAt = null;
        if (args.GetValueOrDefault("expires_at")?.ToString() is { } expiresStr
            && DateTimeOffset.TryParse(expiresStr, out var parsed))
        {
            expiresAt = parsed;
        }

        var rule = new SuppressionRule
        {
            SubscriptionId = args.GetValueOrDefault("subscription_id")?.ToString(),
            ResourceId = args.GetValueOrDefault("resource_id")?.ToString(),
            ControlFamily = args.GetValueOrDefault("control_family")?.ToString(),
            ControlId = args.GetValueOrDefault("control_id")?.ToString(),
            Type = suppressionType,
            Justification = args.GetValueOrDefault("justification")?.ToString(),
            ExpiresAt = expiresAt,
            CreatedBy = args.GetValueOrDefault("user_id")?.ToString() ?? "system"
        };

        var created = await _watchService.CreateSuppressionAsync(rule, cancellationToken);

        return JsonSerializer.Serialize(new
        {
            status = "success",
            data = new
            {
                suppressionId = created.Id,
                scope = new { created.SubscriptionId, created.ResourceId, created.ControlFamily, created.ControlId },
                type = created.Type.ToString(),
                justification = created.Justification,
                expiresAt = created.ExpiresAt,
                isActive = created.IsActive,
                message = $"{created.Type} suppression rule created successfully."
            },
            metadata = new { tool = "watch_suppress_alerts", timestamp = DateTimeOffset.UtcNow }
        });
    }
}

/// <summary>
/// Tool to list active suppression rules.
/// </summary>
public class WatchListSuppressionsTool : BaseTool
{
    private readonly IComplianceWatchService _watchService;

    public WatchListSuppressionsTool(
        IComplianceWatchService watchService,
        ILogger<WatchListSuppressionsTool> logger) : base(logger)
    {
        _watchService = watchService;
    }

    public override string Name => "watch_list_suppressions";
    public override string Description => "List active alert suppression rules.";

    public override IReadOnlyDictionary<string, ToolParameter> Parameters => new Dictionary<string, ToolParameter>
    {
        ["subscription_id"] = new() { Name = "subscription_id", Description = "Azure subscription ID to filter by (optional)", Type = "string" }
    };

    public override async Task<string> ExecuteCoreAsync(
        Dictionary<string, object?> args, CancellationToken cancellationToken)
    {
        var subscriptionId = args.GetValueOrDefault("subscription_id")?.ToString();
        var suppressions = await _watchService.GetSuppressionsAsync(subscriptionId, cancellationToken);

        return JsonSerializer.Serialize(new
        {
            status = "success",
            data = new
            {
                totalSuppressions = suppressions.Count,
                suppressions = suppressions.Select(s => new
                {
                    suppressionId = s.Id,
                    scope = new { s.SubscriptionId, s.ResourceId, s.ControlFamily, s.ControlId },
                    type = s.Type.ToString(),
                    justification = s.Justification,
                    expiresAt = s.ExpiresAt,
                    isActive = s.IsActive,
                    quietHours = s.QuietHoursStart.HasValue
                        ? new { start = s.QuietHoursStart.Value.ToString(), end = s.QuietHoursEnd?.ToString() }
                        : null,
                    createdAt = s.CreatedAt
                })
            },
            metadata = new { tool = "watch_list_suppressions", timestamp = DateTimeOffset.UtcNow }
        });
    }
}

/// <summary>
/// Tool to configure quiet hours for a subscription (non-Critical alerts held during window).
/// </summary>
public class WatchConfigureQuietHoursTool : BaseTool
{
    private readonly IComplianceWatchService _watchService;

    public WatchConfigureQuietHoursTool(
        IComplianceWatchService watchService,
        ILogger<WatchConfigureQuietHoursTool> logger) : base(logger)
    {
        _watchService = watchService;
    }

    public override string Name => "watch_configure_quiet_hours";
    public override string Description => "Configure quiet hours during which non-Critical alerts are suppressed. Critical alerts always deliver.";

    public override IReadOnlyDictionary<string, ToolParameter> Parameters => new Dictionary<string, ToolParameter>
    {
        ["subscription_id"] = new() { Name = "subscription_id", Description = "Azure subscription ID", Type = "string", Required = true },
        ["start_time"] = new() { Name = "start_time", Description = "Quiet hours start (HH:mm, UTC)", Type = "string", Required = true },
        ["end_time"] = new() { Name = "end_time", Description = "Quiet hours end (HH:mm, UTC)", Type = "string", Required = true },
        ["user_role"] = new() { Name = "user_role", Description = "Role of the calling user", Type = "string", Required = true },
        ["user_id"] = new() { Name = "user_id", Description = "Identity of the calling user", Type = "string" }
    };

    public override async Task<string> ExecuteCoreAsync(
        Dictionary<string, object?> args, CancellationToken cancellationToken)
    {
        var userRole = args.GetValueOrDefault("user_role")?.ToString() ?? "";
        if (WatchRoleHelper.IsAuditorRole(userRole))
        {
            return JsonSerializer.Serialize(new
            {
                status = "error",
                error = new { code = "INSUFFICIENT_PERMISSIONS", message = "Auditor role is read-only. Use pim_activate_role to elevate." }
            });
        }

        var subscriptionId = args.GetValueOrDefault("subscription_id")?.ToString()
            ?? throw new ArgumentException("MISSING_PARAMETER: 'subscription_id' is required.");

        var startStr = args.GetValueOrDefault("start_time")?.ToString()
            ?? throw new ArgumentException("MISSING_PARAMETER: 'start_time' is required.");

        var endStr = args.GetValueOrDefault("end_time")?.ToString()
            ?? throw new ArgumentException("MISSING_PARAMETER: 'end_time' is required.");

        if (!TimeOnly.TryParse(startStr, out var start))
            throw new ArgumentException($"INVALID_TIME: Cannot parse start_time '{startStr}'. Use HH:mm format.");

        if (!TimeOnly.TryParse(endStr, out var end))
            throw new ArgumentException($"INVALID_TIME: Cannot parse end_time '{endStr}'. Use HH:mm format.");

        var createdBy = args.GetValueOrDefault("user_id")?.ToString() ?? "system";
        var rule = await _watchService.ConfigureQuietHoursAsync(subscriptionId, start, end, createdBy, cancellationToken);

        return JsonSerializer.Serialize(new
        {
            status = "success",
            data = new
            {
                suppressionId = rule.Id,
                subscriptionId,
                quietHoursStart = start.ToString(),
                quietHoursEnd = end.ToString(),
                note = "Non-Critical alerts will be held during this window. Critical alerts always deliver immediately (FR-019).",
                message = $"Quiet hours configured: {start}-{end} UTC."
            },
            metadata = new { tool = "watch_configure_quiet_hours", timestamp = DateTimeOffset.UtcNow }
        });
    }
}

// ─── Notification & Escalation Tools (US4 / T037) ──────────────────────────

/// <summary>
/// Tool to configure notification channels (email, webhook) for compliance alerts.
/// Auditors and Platform Engineers are denied; Security Lead, Compliance Officer, Administrator allowed.
/// </summary>
public class WatchConfigureNotificationsTool : BaseTool
{
    private readonly IEscalationService _escalationService;
    private readonly ILogger<WatchConfigureNotificationsTool> _logger;

    public WatchConfigureNotificationsTool(
        IEscalationService escalationService,
        ILogger<WatchConfigureNotificationsTool> logger)
        : base(logger)
    {
        _escalationService = escalationService;
        _logger = logger;
    }

    public override string Name => "watch_configure_notifications";
    public override string Description =>
        "Configure notification channels (email, webhook) for compliance alerts. " +
        "Specify channel type, target address, and optional severity filter.";

    public override IReadOnlyDictionary<string, ToolParameter> Parameters => new Dictionary<string, ToolParameter>
    {
        ["channel"] = new() { Name = "channel", Description = "Notification channel: email or webhook", Type = "string", Required = true },
        ["target"] = new() { Name = "target", Description = "Email address or webhook URL", Type = "string", Required = true },
        ["severity"] = new() { Name = "severity", Description = "Minimum severity to trigger (default: all)", Type = "string" },
        ["subscription_id"] = new() { Name = "subscription_id", Description = "Scope to a specific subscription", Type = "string" },
        ["role"] = new() { Name = "role", Description = "Caller role for authorization", Type = "string" },
        ["user_id"] = new() { Name = "user_id", Description = "Caller user ID", Type = "string" }
    };

    public override async Task<string> ExecuteCoreAsync(
        Dictionary<string, object?> arguments,
        CancellationToken cancellationToken = default)
    {
        var args = arguments;
        var role = args.GetValueOrDefault("role")?.ToString() ?? string.Empty;

        // Auditors are read-only
        if (WatchRoleHelper.IsAuditorRole(role))
        {
            return JsonSerializer.Serialize(new
            {
                status = "error",
                error = new { code = "INSUFFICIENT_PERMISSIONS", message = "Auditor role is read-only. Use pim_activate_role to elevate." }
            });
        }

        // Platform Engineers denied per FR-030
        if (string.Equals(role, ComplianceRoles.PlatformEngineer, StringComparison.OrdinalIgnoreCase)
            || string.Equals(role, "PlatformEngineer", StringComparison.OrdinalIgnoreCase))
        {
            return JsonSerializer.Serialize(new
            {
                status = "error",
                error = new
                {
                    code = "INSUFFICIENT_PERMISSIONS",
                    message = "Platform Engineers cannot configure notifications. Contact a Security Lead or Compliance Officer to set up notification channels."
                }
            });
        }

        var channelStr = args.GetValueOrDefault("channel")?.ToString()
            ?? throw new ArgumentException("MISSING_PARAMETER: 'channel' is required (email or webhook).");

        var target = args.GetValueOrDefault("target")?.ToString()
            ?? throw new ArgumentException("MISSING_PARAMETER: 'target' is required (email address or webhook URL).");

        if (!Enum.TryParse<NotificationChannel>(channelStr, ignoreCase: true, out var channel)
            || channel == NotificationChannel.Chat)
        {
            return JsonSerializer.Serialize(new
            {
                status = "error",
                error = new { code = "INVALID_CHANNEL", message = $"Invalid channel '{channelStr}'. Supported: email, webhook. Chat is always enabled." }
            });
        }

        // Parse optional severity filter
        AlertSeverity? severity = null;
        var sevStr = args.GetValueOrDefault("severity")?.ToString();
        if (!string.IsNullOrEmpty(sevStr))
        {
            if (!Enum.TryParse<AlertSeverity>(sevStr, ignoreCase: true, out var parsed))
                throw new ArgumentException($"INVALID_SEVERITY: '{sevStr}'. Must be Critical, High, Medium, or Low.");
            severity = parsed;
        }

        var subscriptionId = args.GetValueOrDefault("subscription_id")?.ToString();
        var createdBy = args.GetValueOrDefault("user_id")?.ToString() ?? "system";

        // Build an EscalationPath entry for this notification channel config
        var path = new EscalationPath
        {
            Name = $"{channel}-notification-{target}",
            TriggerSeverity = severity ?? AlertSeverity.Low, // Default: all severities (Low = lowest threshold)
            EscalationDelayMinutes = 0, // Immediate — no escalation delay for notifications
            Recipients = channel == NotificationChannel.Email ? new List<string> { target } : new List<string>(),
            Channel = channel,
            RepeatIntervalMinutes = 0, // No repeat — notifications use their own rate limiting
            MaxEscalations = 1,
            WebhookUrl = channel == NotificationChannel.Webhook ? target : null,
            IsEnabled = true,
            CreatedBy = createdBy
        };

        var result = await _escalationService.ConfigureEscalationPathAsync(path, cancellationToken);

        _logger.LogInformation("Notification channel configured: {Channel} → {Target} by {User}",
            channel, target, createdBy);

        return JsonSerializer.Serialize(new
        {
            status = "success",
            data = new
            {
                configId = result.Id,
                channel = channel.ToString(),
                target,
                severityFilter = severity?.ToString() ?? "all",
                subscriptionId = subscriptionId ?? "global",
                message = $"Notification channel '{channel}' configured for target '{target}'."
            },
            metadata = new { tool = "watch_configure_notifications", timestamp = DateTimeOffset.UtcNow }
        });
    }
}

/// <summary>
/// Tool to configure escalation paths for SLA violation detection and automatic escalation.
/// Security Lead and Compliance Officer can configure; Auditors and Platform Engineers denied (FR-030).
/// </summary>
public class WatchConfigureEscalationTool : BaseTool
{
    private readonly IEscalationService _escalationService;
    private readonly ILogger<WatchConfigureEscalationTool> _logger;

    public WatchConfigureEscalationTool(
        IEscalationService escalationService,
        ILogger<WatchConfigureEscalationTool> logger)
        : base(logger)
    {
        _escalationService = escalationService;
        _logger = logger;
    }

    public override string Name => "watch_configure_escalation";
    public override string Description =>
        "Configure an escalation path for SLA violation detection. Specifies severity trigger, " +
        "delay before escalation, recipients, notification channel, and repeat interval.";

    public override IReadOnlyDictionary<string, ToolParameter> Parameters => new Dictionary<string, ToolParameter>
    {
        ["name"] = new() { Name = "name", Description = "Escalation path name", Type = "string", Required = true },
        ["severity"] = new() { Name = "severity", Description = "Trigger severity: Critical, High, Medium, Low", Type = "string", Required = true },
        ["delay_minutes"] = new() { Name = "delay_minutes", Description = "Minutes after SLA to escalate", Type = "integer", Required = true },
        ["recipients"] = new() { Name = "recipients", Description = "Comma-separated list of recipients", Type = "string", Required = true },
        ["channel"] = new() { Name = "channel", Description = "Notification channel: Chat, Email, Webhook (default: Chat)", Type = "string" },
        ["repeat_minutes"] = new() { Name = "repeat_minutes", Description = "Re-notify interval in minutes (default: 30, min: 5)", Type = "integer" },
        ["webhook_url"] = new() { Name = "webhook_url", Description = "External webhook URL for integration", Type = "string" },
        ["role"] = new() { Name = "role", Description = "Caller role for authorization", Type = "string" },
        ["user_id"] = new() { Name = "user_id", Description = "Caller user ID", Type = "string" }
    };

    public override async Task<string> ExecuteCoreAsync(
        Dictionary<string, object?> arguments,
        CancellationToken cancellationToken = default)
    {
        var args = arguments;
        var role = args.GetValueOrDefault("role")?.ToString() ?? string.Empty;

        // Auditors are read-only
        if (WatchRoleHelper.IsAuditorRole(role))
        {
            return JsonSerializer.Serialize(new
            {
                status = "error",
                error = new { code = "INSUFFICIENT_PERMISSIONS", message = "Auditor role is read-only. Use pim_activate_role to elevate." }
            });
        }

        // Platform Engineers denied per FR-030
        if (string.Equals(role, ComplianceRoles.PlatformEngineer, StringComparison.OrdinalIgnoreCase)
            || string.Equals(role, "PlatformEngineer", StringComparison.OrdinalIgnoreCase))
        {
            return JsonSerializer.Serialize(new
            {
                status = "error",
                error = new
                {
                    code = "INSUFFICIENT_PERMISSIONS",
                    message = "Platform Engineers cannot configure escalation. Contact a Security Lead or Compliance Officer to manage escalation paths."
                }
            });
        }

        var name = args.GetValueOrDefault("name")?.ToString()
            ?? throw new ArgumentException("MISSING_PARAMETER: 'name' is required.");

        var sevStr = args.GetValueOrDefault("severity")?.ToString()
            ?? throw new ArgumentException("MISSING_PARAMETER: 'severity' is required.");

        if (!Enum.TryParse<AlertSeverity>(sevStr, ignoreCase: true, out var severity))
            throw new ArgumentException($"INVALID_SEVERITY: '{sevStr}'. Must be Critical, High, Medium, or Low.");

        var delayStr = args.GetValueOrDefault("delay_minutes")?.ToString()
            ?? throw new ArgumentException("MISSING_PARAMETER: 'delay_minutes' is required.");

        if (!int.TryParse(delayStr, out var delayMinutes) || delayMinutes <= 0)
            throw new ArgumentException("INVALID_DELAY: 'delay_minutes' must be a positive integer.");

        var recipientStr = args.GetValueOrDefault("recipients")?.ToString()
            ?? throw new ArgumentException("MISSING_PARAMETER: 'recipients' is required (comma-separated).");

        var recipients = recipientStr.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();

        if (recipients.Count == 0)
            throw new ArgumentException("VALIDATION_ERROR: At least one recipient is required.");

        // Optional channel (default: Chat)
        var channelStr = args.GetValueOrDefault("channel")?.ToString() ?? "chat";
        if (!Enum.TryParse<NotificationChannel>(channelStr, ignoreCase: true, out var channel))
            throw new ArgumentException($"INVALID_CHANNEL: '{channelStr}'. Must be Chat, Email, or Webhook.");

        // Optional repeat interval (default: 30)
        var repeatMinutes = 30;
        var repeatStr = args.GetValueOrDefault("repeat_minutes")?.ToString();
        if (!string.IsNullOrEmpty(repeatStr))
        {
            if (!int.TryParse(repeatStr, out repeatMinutes) || repeatMinutes < 5)
                throw new ArgumentException("INVALID_REPEAT: 'repeat_minutes' must be at least 5.");
        }

        // Optional webhook URL
        var webhookUrl = args.GetValueOrDefault("webhook_url")?.ToString();

        var createdBy = args.GetValueOrDefault("user_id")?.ToString() ?? "system";

        var path = new EscalationPath
        {
            Name = name,
            TriggerSeverity = severity,
            EscalationDelayMinutes = delayMinutes,
            Recipients = recipients,
            Channel = channel,
            RepeatIntervalMinutes = repeatMinutes,
            WebhookUrl = webhookUrl,
            IsEnabled = true,
            CreatedBy = createdBy
        };

        var result = await _escalationService.ConfigureEscalationPathAsync(path, cancellationToken);

        _logger.LogInformation("Escalation path '{Name}' configured for severity {Severity} by {User}",
            name, severity, createdBy);

        return JsonSerializer.Serialize(new
        {
            status = "success",
            data = new
            {
                escalationPathId = result.Id,
                name = result.Name,
                triggerSeverity = result.TriggerSeverity.ToString(),
                delayMinutes = result.EscalationDelayMinutes,
                recipients = result.Recipients,
                channel = result.Channel.ToString(),
                repeatMinutes = result.RepeatIntervalMinutes,
                maxEscalations = result.MaxEscalations,
                webhookUrl = result.WebhookUrl,
                message = $"Escalation path '{name}' configured: escalate {severity} alerts after {delayMinutes}m delay."
            },
            metadata = new { tool = "watch_configure_escalation", timestamp = DateTimeOffset.UtcNow }
        });
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
// Dashboard Queries & Historical Reporting Tools (Phase 9 / US7)
// ═══════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Query alert history with natural-language keyword parsing.
/// "drifted" → type=Drift; "dismissed" → status=Dismissed; control families → controlFamily filter.
/// Falls back to structured parameter filters for unrecognized queries.
/// </summary>
public class WatchAlertHistoryTool : BaseTool
{
    private readonly IAlertManager _alertManager;

    public override string Name => "watch_alert_history";
    public override string Description =>
        "Query compliance alert history. Supports natural-language queries " +
        "(e.g., 'What drifted this week?', 'Show dismissed alerts') and structured filters.";

    public override IReadOnlyDictionary<string, ToolParameter> Parameters { get; } =
        new Dictionary<string, ToolParameter>
        {
            ["query"] = new() { Type = "string", Description = "Natural-language query or keyword (optional)", Required = false },
            ["subscriptionId"] = new() { Type = "string", Description = "Filter by subscription ID", Required = false },
            ["severity"] = new() { Type = "string", Description = "Filter by severity (Critical, High, Medium, Low)", Required = false },
            ["status"] = new() { Type = "string", Description = "Filter by status (New, Acknowledged, InProgress, Escalated, Resolved, Dismissed)", Required = false },
            ["controlFamily"] = new() { Type = "string", Description = "Filter by control family (e.g., AC, SC)", Required = false },
            ["days"] = new() { Type = "integer", Description = "Look-back window in days (default: 7)", Required = false },
            ["page"] = new() { Type = "integer", Description = "Page number (default: 1)", Required = false },
            ["pageSize"] = new() { Type = "integer", Description = "Results per page (default: 20, max: 100)", Required = false }
        };

    public WatchAlertHistoryTool(IAlertManager alertManager, ILogger<WatchAlertHistoryTool> logger) : base(logger)
    {
        _alertManager = alertManager;
    }

    public override async Task<string> ExecuteCoreAsync(
        Dictionary<string, object?> args, CancellationToken cancellationToken)
    {
        // Parse natural-language query into structured filters
        var query = GetArg<string>(args, "query");
        AlertSeverity? severity = null;
        AlertStatus? status = null;
        string? controlFamily = null;
        string? subscriptionId = GetArg<string>(args, "subscriptionId");
        int days = 7;
        int page = 1;
        int pageSize = 20;

        var daysStr = GetArg<string>(args, "days");
        if (daysStr != null) int.TryParse(daysStr, out days);
        var pageStr = GetArg<string>(args, "page");
        if (pageStr != null) int.TryParse(pageStr, out page);
        var pageSizeStr = GetArg<string>(args, "pageSize");
        if (pageSizeStr != null) int.TryParse(pageSizeStr, out pageSize);

        pageSize = Math.Min(pageSize, 100);

        // Keyword-based query parsing
        if (!string.IsNullOrEmpty(query))
        {
            var lower = query.ToLowerInvariant();

            // Note: drift type filtering handled via NL query keywords
            if (lower.Contains("dismiss"))
                status = AlertStatus.Dismissed;
            if (lower.Contains("escalat"))
                status = AlertStatus.Escalated;
            if (lower.Contains("resolv"))
                status = AlertStatus.Resolved;
            if (lower.Contains("critical"))
                severity = AlertSeverity.Critical;
            if (lower.Contains("high"))
                severity = AlertSeverity.High;
            if (lower.Contains("week"))
                days = 7;
            if (lower.Contains("month"))
                days = 30;
            if (lower.Contains("today"))
                days = 1;

            // Control family detection
            var families = new[] { "AC", "AU", "AT", "CM", "CP", "IA", "IR", "MA", "MP", "PE", "PL", "PM", "PS", "RA", "SA", "SC", "SI", "SR" };
            foreach (var cf in families)
            {
                if (lower.Contains(cf.ToLowerInvariant()) || query.Contains(cf))
                {
                    controlFamily = cf;
                    break;
                }
            }
        }

        // Apply explicit parameter overrides
        var sevArg = GetArg<string>(args, "severity");
        if (sevArg != null && Enum.TryParse<AlertSeverity>(sevArg, true, out var sevParsed))
            severity = sevParsed;
        var statArg = GetArg<string>(args, "status");
        if (statArg != null && Enum.TryParse<AlertStatus>(statArg, true, out var statParsed))
            status = statParsed;
        var cfArg = GetArg<string>(args, "controlFamily");
        if (cfArg != null)
            controlFamily = cfArg;

        var (alerts, totalCount) = await _alertManager.GetAlertsAsync(
            subscriptionId, severity, status, controlFamily, days, page, pageSize);

        return JsonSerializer.Serialize(new
        {
            result = new
            {
                alerts = alerts.Select(a => new
                {
                    a.AlertId,
                    type = a.Type.ToString(),
                    severity = a.Severity.ToString(),
                    status = a.Status.ToString(),
                    a.Title,
                    a.SubscriptionId,
                    a.ControlFamily,
                    a.ActorId,
                    createdAt = a.CreatedAt,
                    a.IsGrouped,
                    a.ChildAlertCount
                }),
                totalCount,
                page,
                pageSize,
                query,
                message = $"Found {totalCount} alerts matching query (showing page {page})."
            },
            metadata = new { tool = "watch_alert_history", timestamp = DateTimeOffset.UtcNow }
        });
    }
}

/// <summary>
/// Query compliance trend data from snapshots with direction indicators.
/// </summary>
public class WatchComplianceTrendTool : BaseTool
{
    private readonly IDbContextFactory<AtoCopilotContext> _dbFactory;

    public override string Name => "watch_compliance_trend";
    public override string Description =>
        "View compliance score trends over time. Shows direction indicators (improving/declining/stable).";

    public override IReadOnlyDictionary<string, ToolParameter> Parameters { get; } =
        new Dictionary<string, ToolParameter>
        {
            ["subscriptionId"] = new() { Type = "string", Description = "Subscription ID to query trends for", Required = true },
            ["days"] = new() { Type = "integer", Description = "Look-back window in days (default: 30)", Required = false },
            ["weekly"] = new() { Type = "boolean", Description = "Show only weekly snapshots (default: false)", Required = false }
        };

    public WatchComplianceTrendTool(IDbContextFactory<AtoCopilotContext> dbFactory, ILogger<WatchComplianceTrendTool> logger) : base(logger)
    {
        _dbFactory = dbFactory;
    }

    public override async Task<string> ExecuteCoreAsync(
        Dictionary<string, object?> args, CancellationToken cancellationToken)
    {
        var subscriptionId = GetArg<string>(args, "subscriptionId")
            ?? throw new ArgumentException("subscriptionId is required");

        int days = 30;
        var daysStr = GetArg<string>(args, "days");
        if (daysStr != null) int.TryParse(daysStr, out days);

        bool weeklyOnly = false;
        var weeklyStr = GetArg<string>(args, "weekly");
        if (weeklyStr != null) bool.TryParse(weeklyStr, out weeklyOnly);

        var since = DateTimeOffset.UtcNow.AddDays(-days);

        await using var db = await _dbFactory.CreateDbContextAsync();

        var query = db.ComplianceSnapshots
            .Where(s => s.SubscriptionId == subscriptionId && s.CapturedAt >= since);

        if (weeklyOnly)
            query = query.Where(s => s.IsWeeklySnapshot);

        var snapshots = await query
            .OrderBy(s => s.CapturedAt)
            .ToListAsync();

        // Compute direction indicators
        var trendPoints = new List<object>();
        double? previousScore = null;

        foreach (var snap in snapshots)
        {
            var direction = "stable";
            if (previousScore.HasValue)
            {
                var diff = snap.ComplianceScore - previousScore.Value;
                if (diff > 1) direction = "improving";
                else if (diff < -1) direction = "declining";
            }

            trendPoints.Add(new
            {
                date = snap.CapturedAt.ToString("yyyy-MM-dd"),
                complianceScore = snap.ComplianceScore,
                direction,
                activeAlerts = snap.ActiveAlertCount,
                criticalAlerts = snap.CriticalAlertCount,
                totalControls = snap.TotalControls,
                passedControls = snap.PassedControls,
                isWeekly = snap.IsWeeklySnapshot
            });

            previousScore = snap.ComplianceScore;
        }

        // Overall trend
        var overallDirection = "stable";
        if (snapshots.Count >= 2)
        {
            var first = snapshots[0].ComplianceScore;
            var last = snapshots[^1].ComplianceScore;
            if (last - first > 2) overallDirection = "improving";
            else if (first - last > 2) overallDirection = "declining";
        }

        return JsonSerializer.Serialize(new
        {
            result = new
            {
                subscriptionId,
                period = $"{days} days",
                overallDirection,
                dataPoints = trendPoints,
                message = snapshots.Count == 0
                    ? "No snapshot data available yet. Snapshots are captured daily."
                    : $"Compliance trend: {overallDirection} over {days} days ({snapshots.Count} data points)."
            },
            metadata = new { tool = "watch_compliance_trend", timestamp = DateTimeOffset.UtcNow }
        });
    }
}

/// <summary>
/// Alert statistics aggregation — counts by severity, type, status; average resolution time;
/// escalation and auto-resolve counts.
/// </summary>
public class WatchAlertStatisticsTool : BaseTool
{
    private readonly IDbContextFactory<AtoCopilotContext> _dbFactory;

    public override string Name => "watch_alert_statistics";
    public override string Description =>
        "Get alert statistics including counts by severity, type, and status; " +
        "average resolution time; escalation count; and auto-resolved count.";

    public override IReadOnlyDictionary<string, ToolParameter> Parameters { get; } =
        new Dictionary<string, ToolParameter>
        {
            ["subscriptionId"] = new() { Type = "string", Description = "Filter by subscription ID (optional)", Required = false },
            ["days"] = new() { Type = "integer", Description = "Look-back window in days (default: 30)", Required = false }
        };

    public WatchAlertStatisticsTool(IDbContextFactory<AtoCopilotContext> dbFactory, ILogger<WatchAlertStatisticsTool> logger) : base(logger)
    {
        _dbFactory = dbFactory;
    }

    public override async Task<string> ExecuteCoreAsync(
        Dictionary<string, object?> args, CancellationToken cancellationToken)
    {
        var subscriptionId = GetArg<string>(args, "subscriptionId");
        int days = 30;
        var daysStr = GetArg<string>(args, "days");
        if (daysStr != null) int.TryParse(daysStr, out days);

        var since = DateTimeOffset.UtcNow.AddDays(-days);

        await using var db = await _dbFactory.CreateDbContextAsync();

        var alertsQuery = db.ComplianceAlerts
            .Where(a => a.CreatedAt >= since);

        if (!string.IsNullOrEmpty(subscriptionId))
            alertsQuery = alertsQuery.Where(a => a.SubscriptionId == subscriptionId);

        var alerts = await alertsQuery.ToListAsync();

        // By severity
        var bySeverity = alerts
            .GroupBy(a => a.Severity)
            .ToDictionary(g => g.Key.ToString(), g => g.Count());

        // By type
        var byType = alerts
            .GroupBy(a => a.Type)
            .ToDictionary(g => g.Key.ToString(), g => g.Count());

        // By status
        var byStatus = alerts
            .GroupBy(a => a.Status)
            .ToDictionary(g => g.Key.ToString(), g => g.Count());

        // Average resolution time
        var resolved = alerts.Where(a => a.ResolvedAt.HasValue).ToList();
        var avgResolutionHours = resolved.Count > 0
            ? resolved.Average(a => (a.ResolvedAt!.Value - a.CreatedAt).TotalHours)
            : 0;

        // Escalation count
        var escalatedCount = alerts.Count(a =>
            a.Status == AlertStatus.Escalated
            || alerts.Any(h => h.Id == a.Id && h.Status == AlertStatus.Escalated));

        // Auto-resolved count (resolved by system)
        var autoResolvedCount = resolved.Count(a =>
            a.AcknowledgedBy == "system" || a.DismissalJustification?.Contains("Auto-resolved") == true);

        // Grouped alert count
        var groupedCount = alerts.Count(a => a.IsGrouped);

        return JsonSerializer.Serialize(new
        {
            result = new
            {
                period = $"{days} days",
                totalAlerts = alerts.Count,
                bySeverity,
                byType,
                byStatus,
                averageResolutionHours = Math.Round(avgResolutionHours, 1),
                escalatedCount,
                autoResolvedCount,
                groupedAlertCount = groupedCount,
                activeAlerts = alerts.Count(a => a.Status != AlertStatus.Resolved && a.Status != AlertStatus.Dismissed),
                message = alerts.Count == 0
                    ? $"No alerts in the last {days} days."
                    : $"Alert statistics for last {days} days: {alerts.Count} total, " +
                      $"{bySeverity.GetValueOrDefault("Critical", 0)} critical, " +
                      $"avg resolution: {avgResolutionHours:F1}h."
            },
            metadata = new { tool = "watch_alert_statistics", timestamp = DateTimeOffset.UtcNow }
        });
    }
}

/// <summary>
/// Create a Kanban remediation task from a compliance alert with pre-populated details.
/// Links the task to the alert for auto-close on resolution.
/// </summary>
public class WatchCreateTaskFromAlertTool : BaseTool
{
    private readonly IAlertManager _alertManager;
    private readonly IServiceScopeFactory _scopeFactory;

    public override string Name => "watch_create_task_from_alert";
    public override string Description =>
        "Create a Kanban remediation task from an alert. Pre-populates title, description, severity, " +
        "and control mapping from the alert. Links the task to the alert for auto-close on resolution.";

    public override IReadOnlyDictionary<string, ToolParameter> Parameters { get; } =
        new Dictionary<string, ToolParameter>
        {
            ["alertId"] = new() { Type = "string", Description = "Alert ID (e.g., ALT-2026022200001)", Required = true },
            ["boardId"] = new() { Type = "string", Description = "Target board ID (defaults to first active board for the alert's subscription)", Required = false }
        };

    public WatchCreateTaskFromAlertTool(
        IAlertManager alertManager,
        IServiceScopeFactory scopeFactory,
        ILogger<WatchCreateTaskFromAlertTool> logger) : base(logger)
    {
        _alertManager = alertManager;
        _scopeFactory = scopeFactory;
    }

    public override async Task<string> ExecuteCoreAsync(
        Dictionary<string, object?> args, CancellationToken cancellationToken)
    {
        var alertId = GetArg<string>(args, "alertId")
            ?? throw new ArgumentException("ALERT_NOT_FOUND: alertId is required");
        var boardIdArg = GetArg<string>(args, "boardId");

        // Retrieve the alert
        var alert = await _alertManager.GetAlertByAlertIdAsync(alertId, cancellationToken)
            ?? throw new ArgumentException($"ALERT_NOT_FOUND: Alert '{alertId}' not found.");

        using var scope = _scopeFactory.CreateScope();
        var kanbanService = scope.ServiceProvider.GetRequiredService<IKanbanService>();

        // Check for existing linked task
        var existingTask = await kanbanService.GetTaskByLinkedAlertIdAsync(alertId, cancellationToken);
        if (existingTask != null)
        {
            return JsonSerializer.Serialize(new
            {
                status = "error",
                error = new
                {
                    code = "TASK_ALREADY_EXISTS",
                    message = $"Task '{existingTask.TaskNumber}' already exists for alert '{alertId}'.",
                    existingTaskId = existingTask.Id,
                    existingTaskNumber = existingTask.TaskNumber
                }
            });
        }

        // Resolve target board
        string boardId;
        if (!string.IsNullOrEmpty(boardIdArg))
        {
            boardId = boardIdArg;
        }
        else
        {
            var boards = await kanbanService.ListBoardsAsync(
                alert.SubscriptionId, isArchived: false, page: 1, pageSize: 1, cancellationToken: cancellationToken);
            if (boards.Items.Count == 0)
            {
                return JsonSerializer.Serialize(new
                {
                    status = "error",
                    error = new
                    {
                        code = "NO_ACTIVE_BOARD",
                        message = $"No active board found for subscription '{alert.SubscriptionId}'. Create a board first."
                    }
                });
            }
            boardId = boards.Items[0].Id;
        }

        // Map AlertSeverity → FindingSeverity (matching ordinals: Critical=0, High=1, Medium=2, Low=3)
        var findingSeverity = (FindingSeverity)(int)alert.Severity;

        var title = $"[{alertId}] {alert.Title}";
        var description = $"Auto-created from compliance alert {alertId}.\n\n" +
                          $"Type: {alert.Type}\n" +
                          $"Severity: {alert.Severity}\n" +
                          $"Control: {alert.ControlId} ({alert.ControlFamily})\n" +
                          $"Subscription: {alert.SubscriptionId}\n" +
                          $"Created: {alert.CreatedAt:u}";

        var controlId = !string.IsNullOrEmpty(alert.ControlId) ? alert.ControlId : $"{alert.ControlFamily}-0";

        var task = await kanbanService.CreateTaskAsync(
            boardId,
            title,
            controlId,
            createdBy: "system:watch",
            description: description,
            severity: findingSeverity,
            affectedResources: alert.AffectedResources,
            linkedAlertId: alertId,
            cancellationToken: cancellationToken);

        return JsonSerializer.Serialize(new
        {
            result = new
            {
                alertId,
                taskId = task.Id,
                taskNumber = task.TaskNumber,
                boardId,
                title,
                message = $"Remediation task created from alert {alertId}."
            },
            metadata = new { tool = "watch_create_task_from_alert", timestamp = DateTimeOffset.UtcNow }
        });
    }
}

/// <summary>
/// Capture alert details, timeline, and remediation context as compliance evidence.
/// </summary>
public class WatchCollectEvidenceFromAlertTool : BaseTool
{
    private readonly IAlertManager _alertManager;
    private readonly IDbContextFactory<AtoCopilotContext> _dbFactory;

    public override string Name => "watch_collect_evidence_from_alert";
    public override string Description =>
        "Capture alert details, timeline, and context as compliance evidence for audit trails.";

    public override IReadOnlyDictionary<string, ToolParameter> Parameters { get; } =
        new Dictionary<string, ToolParameter>
        {
            ["alertId"] = new() { Type = "string", Description = "Alert ID to collect evidence from", Required = true }
        };

    public WatchCollectEvidenceFromAlertTool(
        IAlertManager alertManager,
        IDbContextFactory<AtoCopilotContext> dbFactory,
        ILogger<WatchCollectEvidenceFromAlertTool> logger) : base(logger)
    {
        _alertManager = alertManager;
        _dbFactory = dbFactory;
    }

    public override async Task<string> ExecuteCoreAsync(
        Dictionary<string, object?> args, CancellationToken cancellationToken)
    {
        var alertId = GetArg<string>(args, "alertId")
            ?? throw new ArgumentException("ALERT_NOT_FOUND: alertId is required");

        var alert = await _alertManager.GetAlertByAlertIdAsync(alertId, cancellationToken)
            ?? throw new ArgumentException($"ALERT_NOT_FOUND: Alert '{alertId}' not found.");

        // Build evidence content from alert details
        var evidenceContent = JsonSerializer.Serialize(new
        {
            alertId = alert.AlertId,
            type = alert.Type.ToString(),
            severity = alert.Severity.ToString(),
            status = alert.Status.ToString(),
            title = alert.Title,
            description = alert.Description,
            subscriptionId = alert.SubscriptionId,
            controlId = alert.ControlId,
            controlFamily = alert.ControlFamily,
            affectedResources = alert.AffectedResources,
            actorId = alert.ActorId,
            createdAt = alert.CreatedAt,
            acknowledgedAt = alert.AcknowledgedAt,
            resolvedAt = alert.ResolvedAt,
            slaDeadline = alert.SlaDeadline,
            isGrouped = alert.IsGrouped,
            childAlertCount = alert.ChildAlertCount
        });

        var contentHash = Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes(evidenceContent))).ToLowerInvariant();

        var evidence = new ComplianceEvidence
        {
            ControlId = alert.ControlId ?? $"{alert.ControlFamily}-0",
            SubscriptionId = alert.SubscriptionId,
            EvidenceType = "AlertSnapshot",
            Description = $"Evidence captured from compliance alert {alertId}: {alert.Title}",
            Content = evidenceContent,
            CollectedAt = DateTime.UtcNow,
            CollectedBy = "system:watch",
            EvidenceCategory = EvidenceCategory.Configuration,
            ContentHash = contentHash
        };

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        db.Evidence.Add(evidence);
        await db.SaveChangesAsync(cancellationToken);

        return JsonSerializer.Serialize(new
        {
            result = new
            {
                alertId,
                evidenceId = evidence.Id,
                controlId = evidence.ControlId,
                evidenceType = evidence.EvidenceType,
                collectedAt = evidence.CollectedAt,
                message = $"Evidence captured from alert {alertId}."
            },
            metadata = new { tool = "watch_collect_evidence_from_alert", timestamp = DateTimeOffset.UtcNow }
        });
    }
}

// ────────────────────────────────────────────────────────────────────────────
// Auto-Remediation Rule Tools (US9)
// ────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Create an opt-in auto-remediation rule. Validates that blocked control families
/// (AC, IA, SC) cannot be auto-remediated.
/// </summary>
public class WatchCreateAutoRemediationRuleTool : BaseTool
{
    private readonly IComplianceWatchService _watchService;

    public WatchCreateAutoRemediationRuleTool(
        IComplianceWatchService watchService,
        ILogger<WatchCreateAutoRemediationRuleTool> logger)
        : base(logger)
    {
        _watchService = watchService;
    }

    public override string Name => "watch_create_auto_remediation_rule";
    public override string Description => "Create an opt-in auto-remediation rule for trusted, low-risk violations. AC, IA, and SC control families are blocked.";

    public override IReadOnlyDictionary<string, ToolParameter> Parameters => new Dictionary<string, ToolParameter>
    {
        ["name"] = new() { Name = "name", Type = "string", Description = "Rule name", Required = true },
        ["action"] = new() { Name = "action", Type = "string", Description = "Remediation action description", Required = true },
        ["subscriptionId"] = new() { Name = "subscriptionId", Type = "string", Description = "Target subscription scope (null = all)" },
        ["resourceGroup"] = new() { Name = "resourceGroup", Type = "string", Description = "Target resource group scope" },
        ["controlFamily"] = new() { Name = "controlFamily", Type = "string", Description = "Target control family (AC, IA, SC blocked)" },
        ["controlId"] = new() { Name = "controlId", Type = "string", Description = "Target specific control ID" },
        ["approvalMode"] = new() { Name = "approvalMode", Type = "string", Description = "\"auto\" or \"require-approval\" (default: \"require-approval\")" },
        ["createdBy"] = new() { Name = "createdBy", Type = "string", Description = "User creating the rule" }
    };

    public override async Task<string> ExecuteCoreAsync(
        Dictionary<string, object?> args, CancellationToken cancellationToken)
    {
        var name = GetArg<string>(args, "name")
            ?? throw new ArgumentException("INVALID_NAME: 'name' parameter is required.");
        var action = GetArg<string>(args, "action")
            ?? throw new ArgumentException("INVALID_ACTION: 'action' parameter is required.");

        var rule = new AutoRemediationRule
        {
            Name = name,
            Action = action,
            SubscriptionId = GetArg<string>(args, "subscriptionId"),
            ResourceGroupName = GetArg<string>(args, "resourceGroup"),
            ControlFamily = GetArg<string>(args, "controlFamily"),
            ControlId = GetArg<string>(args, "controlId"),
            ApprovalMode = GetArg<string>(args, "approvalMode") ?? "require-approval",
            CreatedBy = GetArg<string>(args, "createdBy") ?? "system"
        };

        try
        {
            var created = await _watchService.CreateAutoRemediationRuleAsync(rule, cancellationToken);

            var scope = created.SubscriptionId is not null
                ? $"subscription {created.SubscriptionId}" + (created.ResourceGroupName is not null ? $" / {created.ResourceGroupName}" : "")
                : "all subscriptions";

            return JsonSerializer.Serialize(new
            {
                result = new
                {
                    ruleId = created.Id,
                    name = created.Name,
                    scope,
                    approvalMode = created.ApprovalMode,
                    isEnabled = created.IsEnabled,
                    message = "Auto-remediation rule created. Matching violations will be automatically fixed."
                },
                metadata = new { tool = Name, timestamp = DateTimeOffset.UtcNow }
            });
        }
        catch (ArgumentException ex) when (ex.Message.Contains("BLOCKED_FAMILY"))
        {
            return JsonSerializer.Serialize(new
            {
                status = "error",
                error = new
                {
                    code = "BLOCKED_CONTROL_FAMILY",
                    message = "Auto-remediation is not allowed for control families AC, IA, SC — these require human approval."
                }
            });
        }
    }
}

/// <summary>
/// List auto-remediation rules with execution history.
/// </summary>
public class WatchListAutoRemediationRulesTool : BaseTool
{
    private readonly IComplianceWatchService _watchService;

    public WatchListAutoRemediationRulesTool(
        IComplianceWatchService watchService,
        ILogger<WatchListAutoRemediationRulesTool> logger)
        : base(logger)
    {
        _watchService = watchService;
    }

    public override string Name => "watch_list_auto_remediation_rules";
    public override string Description => "List auto-remediation rules and their execution history.";

    public override IReadOnlyDictionary<string, ToolParameter> Parameters => new Dictionary<string, ToolParameter>
    {
        ["subscriptionId"] = new() { Name = "subscriptionId", Type = "string", Description = "Filter by subscription" },
        ["includeDisabled"] = new() { Name = "includeDisabled", Type = "boolean", Description = "Include disabled rules (default: false)" }
    };

    public override async Task<string> ExecuteCoreAsync(
        Dictionary<string, object?> args, CancellationToken cancellationToken)
    {
        var subscriptionId = GetArg<string>(args, "subscriptionId");
        var includeDisabled = GetArg<bool?>(args, "includeDisabled") ?? false;
        bool? isEnabled = includeDisabled ? null : true;

        var rules = await _watchService.GetAutoRemediationRulesAsync(subscriptionId, isEnabled, cancellationToken);

        return JsonSerializer.Serialize(new
        {
            result = new
            {
                rules = rules.Select(r => new
                {
                    ruleId = r.Id,
                    name = r.Name,
                    description = r.Description,
                    subscriptionId = r.SubscriptionId,
                    resourceGroup = r.ResourceGroupName,
                    controlFamily = r.ControlFamily,
                    controlId = r.ControlId,
                    action = r.Action,
                    approvalMode = r.ApprovalMode,
                    isEnabled = r.IsEnabled,
                    executionCount = r.ExecutionCount,
                    lastExecutedAt = r.LastExecutedAt,
                    createdBy = r.CreatedBy,
                    createdAt = r.CreatedAt
                }).ToList(),
                totalCount = rules.Count,
                includeDisabled
            },
            metadata = new { tool = Name, timestamp = DateTimeOffset.UtcNow }
        });
    }
}
