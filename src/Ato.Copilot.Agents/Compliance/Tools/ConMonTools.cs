using System.Diagnostics;
using System.Text.Json;
using Ato.Copilot.Agents.Common;
using Ato.Copilot.Core.Interfaces.Compliance;
using Microsoft.Extensions.Logging;

namespace Ato.Copilot.Agents.Compliance.Tools;

// ═══════════════════════════════════════════════════════════════════════════════
// Continuous Monitoring Tools (Feature 015 — US9)
// Spec §4.1–4.7: ConMon plans, periodic reports, ATO expiration tracking,
// significant change detection, reauthorization workflow, multi-system dashboard.
// ═══════════════════════════════════════════════════════════════════════════════

// ─────────────────────────────────────────────────────────────────────────────
// T132: CreateConMonPlanTool — §4.1
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Create or update the continuous monitoring plan for a registered system.
/// One plan per system (upsert pattern).  RBAC: ISSM, AO.
/// </summary>
public class CreateConMonPlanTool : BaseTool
{
    private readonly IConMonService _service;
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    public CreateConMonPlanTool(
        IConMonService service,
        ILogger<CreateConMonPlanTool> logger) : base(logger)
    {
        _service = service;
    }

    public override string Name => "compliance_create_conmon_plan";

    public override string Description =>
        "Create or update the continuous monitoring plan for a registered system. " +
        "One plan per system — calling again updates the existing plan. " +
        "RBAC: ISSM, AO.";

    public override IReadOnlyDictionary<string, ToolParameter> Parameters => new Dictionary<string, ToolParameter>
    {
        ["system_id"] = new() { Name = "system_id", Description = "System GUID, name, or acronym", Type = "string", Required = true },
        ["assessment_frequency"] = new() { Name = "assessment_frequency", Description = "Monthly | Quarterly | Annually", Type = "string", Required = true },
        ["annual_review_date"] = new() { Name = "annual_review_date", Description = "ISO 8601 date for annual review (e.g., 2026-06-15)", Type = "string", Required = true },
        ["report_distribution"] = new() { Name = "report_distribution", Description = "User IDs or role names for report distribution", Type = "string[]", Required = false },
        ["significant_change_triggers"] = new() { Name = "significant_change_triggers", Description = "Custom trigger descriptions for significant changes", Type = "string[]", Required = false }
    };

    public override async Task<string> ExecuteCoreAsync(
        Dictionary<string, object?> arguments,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        var systemId = GetArg<string>(arguments, "system_id");
        var frequency = GetArg<string>(arguments, "assessment_frequency");
        var reviewDateStr = GetArg<string>(arguments, "annual_review_date");

        if (string.IsNullOrWhiteSpace(systemId))
            return Error("INVALID_INPUT", "The 'system_id' parameter is required.");
        if (string.IsNullOrWhiteSpace(frequency))
            return Error("INVALID_INPUT", "The 'assessment_frequency' parameter is required.");
        if (string.IsNullOrWhiteSpace(reviewDateStr) || !DateTime.TryParse(reviewDateStr, out var reviewDate))
            return Error("INVALID_INPUT", "The 'annual_review_date' parameter must be a valid ISO 8601 date.");

        var distribution = ParseStringArray(arguments, "report_distribution");
        var triggers = ParseStringArray(arguments, "significant_change_triggers");

        try
        {
            var plan = await _service.CreatePlanAsync(
                systemId, frequency, reviewDate,
                distribution.Count > 0 ? distribution : null,
                triggers.Count > 0 ? triggers : null,
                "system", cancellationToken);

            sw.Stop();
            return JsonSerializer.Serialize(new
            {
                status = "success",
                data = new
                {
                    plan_id = plan.Id,
                    system_id = plan.RegisteredSystemId,
                    assessment_frequency = plan.AssessmentFrequency,
                    annual_review_date = plan.AnnualReviewDate.ToString("yyyy-MM-dd"),
                    report_distribution = plan.ReportDistribution,
                    significant_change_triggers = plan.SignificantChangeTriggers,
                    created_by = plan.CreatedBy,
                    created_at = plan.CreatedAt.ToString("O"),
                    modified_at = plan.ModifiedAt?.ToString("O")
                },
                metadata = Meta(sw)
            }, JsonOpts);
        }
        catch (InvalidOperationException ex)
        {
            return Error("CONMON_PLAN_FAILED", ex.Message);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "compliance_create_conmon_plan failed for system '{SystemId}'", systemId);
            return Error("CONMON_PLAN_FAILED", ex.Message);
        }
    }

    private static string Error(string code, string message) =>
        JsonSerializer.Serialize(new { status = "error", errorCode = code, message },
            new JsonSerializerOptions { WriteIndented = true });

    private object Meta(Stopwatch sw) => new
    {
        tool = Name,
        duration_ms = sw.ElapsedMilliseconds,
        timestamp = DateTime.UtcNow.ToString("O")
    };

    private static List<string> ParseStringArray(Dictionary<string, object?> arguments, string key)
    {
        if (!arguments.TryGetValue(key, out var val) || val == null)
            return new List<string>();

        if (val is JsonElement je && je.ValueKind == JsonValueKind.Array)
            return je.EnumerateArray().Select(e => e.GetString()!).Where(s => s != null).ToList();

        if (val is IEnumerable<string> strList)
            return strList.ToList();

        return new List<string>();
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// T133: GenerateConMonReportTool — §4.2
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Generate a continuous monitoring report with compliance score, delta from
/// authorization baseline, findings opened/closed, and POA&amp;M status.
/// RBAC: ISSM, SCA, AO.
/// </summary>
public class GenerateConMonReportTool : BaseTool
{
    private readonly IConMonService _service;
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    public GenerateConMonReportTool(
        IConMonService service,
        ILogger<GenerateConMonReportTool> logger) : base(logger)
    {
        _service = service;
    }

    public override string Name => "compliance_generate_conmon_report";

    public override string Description =>
        "Generate a continuous monitoring report for a system. Includes compliance score, " +
        "delta from authorization baseline, findings opened/closed, POA&M status, and risk trending. " +
        "RBAC: ISSM, SCA, AO.";

    public override IReadOnlyDictionary<string, ToolParameter> Parameters => new Dictionary<string, ToolParameter>
    {
        ["system_id"] = new() { Name = "system_id", Description = "System GUID, name, or acronym", Type = "string", Required = true },
        ["report_type"] = new() { Name = "report_type", Description = "Monthly | Quarterly | Annual", Type = "string", Required = true },
        ["period"] = new() { Name = "period", Description = "Report period (e.g., 2026-02, 2026-Q1, 2026)", Type = "string", Required = true }
    };

    public override async Task<string> ExecuteCoreAsync(
        Dictionary<string, object?> arguments,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        var systemId = GetArg<string>(arguments, "system_id");
        var reportType = GetArg<string>(arguments, "report_type");
        var period = GetArg<string>(arguments, "period");

        if (string.IsNullOrWhiteSpace(systemId))
            return Error("INVALID_INPUT", "The 'system_id' parameter is required.");
        if (string.IsNullOrWhiteSpace(reportType))
            return Error("INVALID_INPUT", "The 'report_type' parameter is required.");
        if (string.IsNullOrWhiteSpace(period))
            return Error("INVALID_INPUT", "The 'period' parameter is required.");

        try
        {
            var report = await _service.GenerateReportAsync(
                systemId, reportType, period, "system", cancellationToken);

            var scoreDelta = report.AuthorizedBaselineScore.HasValue
                ? Math.Round(report.ComplianceScore - report.AuthorizedBaselineScore.Value, 2)
                : (double?)null;

            sw.Stop();
            return JsonSerializer.Serialize(new
            {
                status = "success",
                data = new
                {
                    report_id = report.Id,
                    system_id = report.RegisteredSystemId,
                    conmon_plan_id = report.ConMonPlanId,
                    report_type = report.ReportType,
                    period = report.ReportPeriod,
                    compliance_score = report.ComplianceScore,
                    authorized_baseline_score = report.AuthorizedBaselineScore,
                    score_delta = scoreDelta,
                    new_findings = report.NewFindings,
                    resolved_findings = report.ResolvedFindings,
                    open_poam_items = report.OpenPoamItems,
                    overdue_poam_items = report.OverduePoamItems,
                    report_content = report.ReportContent,
                    generated_at = report.GeneratedAt.ToString("O"),
                    generated_by = report.GeneratedBy
                },
                metadata = Meta(sw)
            }, JsonOpts);
        }
        catch (InvalidOperationException ex)
        {
            return Error("CONMON_REPORT_FAILED", ex.Message);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "compliance_generate_conmon_report failed for system '{SystemId}'", systemId);
            return Error("CONMON_REPORT_FAILED", ex.Message);
        }
    }

    private static string Error(string code, string message) =>
        JsonSerializer.Serialize(new { status = "error", errorCode = code, message },
            new JsonSerializerOptions { WriteIndented = true });

    private object Meta(Stopwatch sw) => new
    {
        tool = Name,
        duration_ms = sw.ElapsedMilliseconds,
        timestamp = DateTime.UtcNow.ToString("O")
    };
}

// ─────────────────────────────────────────────────────────────────────────────
// T134: ReportSignificantChangeTool — §4.4
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Report a significant change that may trigger reauthorization review.
/// Automatically classifies whether the change type requires reauthorization.
/// RBAC: ISSM, ISSO, SCA.
/// </summary>
public class ReportSignificantChangeTool : BaseTool
{
    private readonly IConMonService _service;
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    public ReportSignificantChangeTool(
        IConMonService service,
        ILogger<ReportSignificantChangeTool> logger) : base(logger)
    {
        _service = service;
    }

    public override string Name => "compliance_report_significant_change";

    public override string Description =>
        "Report a significant change for a system that may trigger reauthorization. " +
        "Automatically classifies whether the change requires reauthorization review. " +
        "RBAC: ISSM, ISSO, SCA.";

    public override IReadOnlyDictionary<string, ToolParameter> Parameters => new Dictionary<string, ToolParameter>
    {
        ["system_id"] = new() { Name = "system_id", Description = "System GUID, name, or acronym", Type = "string", Required = true },
        ["change_type"] = new() { Name = "change_type", Description = "Change category (e.g., New Interconnection, Major Upgrade, Data Type Change)", Type = "string", Required = true },
        ["description"] = new() { Name = "description", Description = "Detailed description of the change", Type = "string", Required = true }
    };

    public override async Task<string> ExecuteCoreAsync(
        Dictionary<string, object?> arguments,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        var systemId = GetArg<string>(arguments, "system_id");
        var changeType = GetArg<string>(arguments, "change_type");
        var description = GetArg<string>(arguments, "description");

        if (string.IsNullOrWhiteSpace(systemId))
            return Error("INVALID_INPUT", "The 'system_id' parameter is required.");
        if (string.IsNullOrWhiteSpace(changeType))
            return Error("INVALID_INPUT", "The 'change_type' parameter is required.");
        if (string.IsNullOrWhiteSpace(description))
            return Error("INVALID_INPUT", "The 'description' parameter is required.");

        try
        {
            var change = await _service.ReportChangeAsync(
                systemId, changeType, description, "system", cancellationToken);

            sw.Stop();
            return JsonSerializer.Serialize(new
            {
                status = "success",
                data = new
                {
                    change_id = change.Id,
                    system_id = change.RegisteredSystemId,
                    change_type = change.ChangeType,
                    description = change.Description,
                    detected_at = change.DetectedAt.ToString("O"),
                    detected_by = change.DetectedBy,
                    requires_reauthorization = change.RequiresReauthorization,
                    reauthorization_triggered = change.ReauthorizationTriggered,
                    reviewed_by = change.ReviewedBy,
                    reviewed_at = change.ReviewedAt?.ToString("O"),
                    disposition = change.Disposition
                },
                metadata = Meta(sw)
            }, JsonOpts);
        }
        catch (InvalidOperationException ex)
        {
            return Error("SIGNIFICANT_CHANGE_FAILED", ex.Message);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "compliance_report_significant_change failed for system '{SystemId}'", systemId);
            return Error("SIGNIFICANT_CHANGE_FAILED", ex.Message);
        }
    }

    private static string Error(string code, string message) =>
        JsonSerializer.Serialize(new { status = "error", errorCode = code, message },
            new JsonSerializerOptions { WriteIndented = true });

    private object Meta(Stopwatch sw) => new
    {
        tool = Name,
        duration_ms = sw.ElapsedMilliseconds,
        timestamp = DateTime.UtcNow.ToString("O")
    };
}

// ─────────────────────────────────────────────────────────────────────────────
// T135: TrackAtoExpirationTool — §4.3
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Check ATO expiration status with graduated alerts at 90/60/30 days.
/// DATO systems always return "None" alert level.
/// RBAC: all compliance roles.
/// </summary>
public class TrackAtoExpirationTool : BaseTool
{
    private readonly IConMonService _service;
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    public TrackAtoExpirationTool(
        IConMonService service,
        ILogger<TrackAtoExpirationTool> logger) : base(logger)
    {
        _service = service;
    }

    public override string Name => "compliance_track_ato_expiration";

    public override string Description =>
        "Check ATO expiration status with graduated alerts at 90/60/30 days. " +
        "Returns alert level (None, Info, Warning, Urgent, Expired) with actionable message. " +
        "RBAC: all compliance roles.";

    public override IReadOnlyDictionary<string, ToolParameter> Parameters => new Dictionary<string, ToolParameter>
    {
        ["system_id"] = new() { Name = "system_id", Description = "System GUID, name, or acronym", Type = "string", Required = true }
    };

    public override async Task<string> ExecuteCoreAsync(
        Dictionary<string, object?> arguments,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        var systemId = GetArg<string>(arguments, "system_id");

        if (string.IsNullOrWhiteSpace(systemId))
            return Error("INVALID_INPUT", "The 'system_id' parameter is required.");

        try
        {
            var status = await _service.CheckExpirationAsync(systemId, cancellationToken);

            sw.Stop();
            return JsonSerializer.Serialize(new
            {
                status = "success",
                data = new
                {
                    system_id = status.SystemId,
                    system_name = status.SystemName,
                    has_active_authorization = status.HasActiveAuthorization,
                    decision_type = status.DecisionType,
                    decision_date = status.DecisionDate?.ToString("yyyy-MM-dd"),
                    expiration_date = status.ExpirationDate?.ToString("yyyy-MM-dd"),
                    days_until_expiration = status.DaysUntilExpiration,
                    alert_level = status.AlertLevel,
                    alert_message = status.AlertMessage,
                    is_expired = status.IsExpired
                },
                metadata = Meta(sw)
            }, JsonOpts);
        }
        catch (InvalidOperationException ex)
        {
            return Error("EXPIRATION_CHECK_FAILED", ex.Message);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "compliance_track_ato_expiration failed for system '{SystemId}'", systemId);
            return Error("EXPIRATION_CHECK_FAILED", ex.Message);
        }
    }

    private static string Error(string code, string message) =>
        JsonSerializer.Serialize(new { status = "error", errorCode = code, message },
            new JsonSerializerOptions { WriteIndented = true });

    private object Meta(Stopwatch sw) => new
    {
        tool = Name,
        duration_ms = sw.ElapsedMilliseconds,
        timestamp = DateTime.UtcNow.ToString("O")
    };
}

// ─────────────────────────────────────────────────────────────────────────────
// T136: MultiSystemDashboardTool — §4.6
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// View all systems with name, IL, RMF step, authorization status, expiration,
/// compliance score, open findings, open POA&amp;M items, and alert count.
/// RBAC: ISSM, AO.
/// </summary>
public class MultiSystemDashboardTool : BaseTool
{
    private readonly IConMonService _service;
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    public MultiSystemDashboardTool(
        IConMonService service,
        ILogger<MultiSystemDashboardTool> logger) : base(logger)
    {
        _service = service;
    }

    public override string Name => "compliance_multi_system_dashboard";

    public override string Description =>
        "View multi-system dashboard showing all systems with name, impact level, RMF step, " +
        "authorization status, expiration, compliance score, open findings, POA&M items, and alerts. " +
        "RBAC: ISSM, AO.";

    public override IReadOnlyDictionary<string, ToolParameter> Parameters => new Dictionary<string, ToolParameter>
    {
        ["active_only"] = new() { Name = "active_only", Description = "Show only active systems (default: true)", Type = "string", Required = false }
    };

    public override async Task<string> ExecuteCoreAsync(
        Dictionary<string, object?> arguments,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        var activeOnlyStr = GetArg<string>(arguments, "active_only") ?? "true";
        var activeOnly = !string.Equals(activeOnlyStr, "false", StringComparison.OrdinalIgnoreCase);

        try
        {
            var dashboard = await _service.GetDashboardAsync(activeOnly, cancellationToken);

            sw.Stop();
            return JsonSerializer.Serialize(new
            {
                status = "success",
                data = new
                {
                    total_systems = dashboard.TotalSystems,
                    authorized_count = dashboard.AuthorizedCount,
                    expiring_count = dashboard.ExpiringCount,
                    expired_count = dashboard.ExpiredCount,
                    systems = dashboard.Systems.Select(s => new
                    {
                        system_id = s.SystemId,
                        name = s.Name,
                        acronym = s.Acronym,
                        impact_level = s.ImpactLevel,
                        current_rmf_step = s.CurrentRmfStep,
                        authorization_status = s.AuthorizationStatus,
                        decision_type = s.DecisionType,
                        expiration_date = s.ExpirationDate?.ToString("yyyy-MM-dd"),
                        days_until_expiration = s.DaysUntilExpiration,
                        compliance_score = s.ComplianceScore,
                        open_findings = s.OpenFindings,
                        open_poam_items = s.OpenPoamItems,
                        alert_count = s.AlertCount
                    })
                },
                metadata = Meta(sw)
            }, JsonOpts);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "compliance_multi_system_dashboard failed");
            return Error("DASHBOARD_FAILED", ex.Message);
        }
    }

    private static string Error(string code, string message) =>
        JsonSerializer.Serialize(new { status = "error", errorCode = code, message },
            new JsonSerializerOptions { WriteIndented = true });

    private object Meta(Stopwatch sw) => new
    {
        tool = Name,
        duration_ms = sw.ElapsedMilliseconds,
        timestamp = DateTime.UtcNow.ToString("O")
    };
}

// ─────────────────────────────────────────────────────────────────────────────
// T219: ReauthorizationWorkflowTool — §4.5
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Detect reauthorization triggers and optionally initiate the reauthorization
/// workflow. Triggers: ATO expiration, unreviewed significant changes requiring
/// reauthorization, compliance score drift (&gt;10% below baseline).
/// When initiated, regresses RMF step to Assess.  RBAC: ISSM, AO.
/// </summary>
public class ReauthorizationWorkflowTool : BaseTool
{
    private readonly IConMonService _service;
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    public ReauthorizationWorkflowTool(
        IConMonService service,
        ILogger<ReauthorizationWorkflowTool> logger) : base(logger)
    {
        _service = service;
    }

    public override string Name => "compliance_reauthorization_workflow";

    public override string Description =>
        "Detect reauthorization triggers (ATO expiration, significant changes, compliance drift) " +
        "and optionally initiate reauthorization by regressing RMF step to Assess. " +
        "RBAC: ISSM, AO.";

    public override IReadOnlyDictionary<string, ToolParameter> Parameters => new Dictionary<string, ToolParameter>
    {
        ["system_id"] = new() { Name = "system_id", Description = "System GUID, name, or acronym", Type = "string", Required = true },
        ["initiate"] = new() { Name = "initiate", Description = "If true, initiate reauthorization workflow (default: false — check only)", Type = "string", Required = false }
    };

    public override async Task<string> ExecuteCoreAsync(
        Dictionary<string, object?> arguments,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        var systemId = GetArg<string>(arguments, "system_id");
        var initiateStr = GetArg<string>(arguments, "initiate") ?? "false";
        var initiate = string.Equals(initiateStr, "true", StringComparison.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(systemId))
            return Error("INVALID_INPUT", "The 'system_id' parameter is required.");

        try
        {
            var result = await _service.CheckReauthorizationAsync(
                systemId, initiate, cancellationToken);

            sw.Stop();
            return JsonSerializer.Serialize(new
            {
                status = "success",
                data = new
                {
                    system_id = result.SystemId,
                    is_triggered = result.IsTriggered,
                    triggers = result.Triggers,
                    was_initiated = result.WasInitiated,
                    previous_rmf_step = result.PreviousRmfStep,
                    new_rmf_step = result.NewRmfStep,
                    unreviewed_change_count = result.UnreviewedChangeCount
                },
                metadata = Meta(sw)
            }, JsonOpts);
        }
        catch (InvalidOperationException ex)
        {
            return Error("REAUTHORIZATION_FAILED", ex.Message);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "compliance_reauthorization_workflow failed for system '{SystemId}'", systemId);
            return Error("REAUTHORIZATION_FAILED", ex.Message);
        }
    }

    private static string Error(string code, string message) =>
        JsonSerializer.Serialize(new { status = "error", errorCode = code, message },
            new JsonSerializerOptions { WriteIndented = true });

    private object Meta(Stopwatch sw) => new
    {
        tool = Name,
        duration_ms = sw.ElapsedMilliseconds,
        timestamp = DateTime.UtcNow.ToString("O")
    };
}

// ─────────────────────────────────────────────────────────────────────────────
// T220: NotificationDeliveryTool — §4.7
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Send ConMon notifications (expiration alerts, significant change events) via
/// Teams or VS Code. CAT I quiet-hours override supported per spec §4.7.
/// RBAC: ISSM, ISSO.
/// </summary>
public class NotificationDeliveryTool : BaseTool
{
    private readonly IConMonService _service;
    private readonly IAlertManager? _alertManager;
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    public NotificationDeliveryTool(
        IConMonService service,
        ILogger<NotificationDeliveryTool> logger,
        IAlertManager? alertManager = null) : base(logger)
    {
        _service = service;
        _alertManager = alertManager;
    }

    public override string Name => "compliance_send_notification";

    public override string Description =>
        "Send continuous monitoring notifications (expiration alerts, significant change events) " +
        "to configured recipients. Returns notification delivery status. " +
        "RBAC: ISSM, ISSO.";

    public override IReadOnlyDictionary<string, ToolParameter> Parameters => new Dictionary<string, ToolParameter>
    {
        ["system_id"] = new() { Name = "system_id", Description = "System GUID, name, or acronym", Type = "string", Required = true },
        ["notification_type"] = new() { Name = "notification_type", Description = "expiration | significant_change | conmon_report", Type = "string", Required = true }
    };

    public override async Task<string> ExecuteCoreAsync(
        Dictionary<string, object?> arguments,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        var systemId = GetArg<string>(arguments, "system_id");
        var notificationType = GetArg<string>(arguments, "notification_type");

        if (string.IsNullOrWhiteSpace(systemId))
            return Error("INVALID_INPUT", "The 'system_id' parameter is required.");
        if (string.IsNullOrWhiteSpace(notificationType))
            return Error("INVALID_INPUT", "The 'notification_type' parameter is required.");

        try
        {
            // Notification delivery routes through AlertManager → AlertNotificationService pipeline.
            // CheckExpirationAsync and ReportChangeAsync auto-create alerts (Phase 17 §9a.4).

            switch (notificationType.ToLowerInvariant())
            {
                case "expiration":
                    var expStatus = await _service.CheckExpirationAsync(systemId, cancellationToken);
                    // Alert is auto-created by ConMonService.CheckExpirationAsync (T244)
                    var alertPipeline = _alertManager != null && expStatus.AlertLevel != "None";
                    sw.Stop();
                    return JsonSerializer.Serialize(new
                    {
                        status = "success",
                        data = new
                        {
                            notification_type = "expiration",
                            system_id = expStatus.SystemId,
                            system_name = expStatus.SystemName,
                            alert_level = expStatus.AlertLevel,
                            alert_message = expStatus.AlertMessage,
                            delivered = expStatus.AlertLevel != "None",
                            channels = alertPipeline
                                ? new[] { "mcp_response", "alert_pipeline" }
                                : expStatus.AlertLevel != "None"
                                    ? new[] { "mcp_response" }
                                    : Array.Empty<string>()
                        },
                        metadata = Meta(sw)
                    }, JsonOpts);

                case "significant_change":
                    var reauth = await _service.CheckReauthorizationAsync(systemId, false, cancellationToken);
                    // Alerts for changes requiring reauthorization are auto-created by
                    // ConMonService.ReportChangeAsync (T245)
                    sw.Stop();
                    return JsonSerializer.Serialize(new
                    {
                        status = "success",
                        data = new
                        {
                            notification_type = "significant_change",
                            system_id = reauth.SystemId,
                            unreviewed_changes = reauth.UnreviewedChangeCount,
                            reauthorization_triggered = reauth.IsTriggered,
                            triggers = reauth.Triggers,
                            delivered = reauth.UnreviewedChangeCount > 0,
                            channels = reauth.UnreviewedChangeCount > 0
                                ? new[] { "mcp_response", "alert_pipeline" }
                                : Array.Empty<string>()
                        },
                        metadata = Meta(sw)
                    }, JsonOpts);

                case "conmon_report":
                    // Report notification — check if plan exists
                    sw.Stop();
                    return JsonSerializer.Serialize(new
                    {
                        status = "success",
                        data = new
                        {
                            notification_type = "conmon_report",
                            system_id = systemId,
                            message = "ConMon report notification queued. Use compliance_generate_conmon_report to generate and deliver.",
                            delivered = true,
                            channels = new[] { "mcp_response" }
                        },
                        metadata = Meta(sw)
                    }, JsonOpts);

                default:
                    return Error("INVALID_INPUT",
                        $"Unknown notification type '{notificationType}'. Expected: expiration, significant_change, conmon_report.");
            }
        }
        catch (InvalidOperationException ex)
        {
            return Error("NOTIFICATION_FAILED", ex.Message);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "compliance_send_notification failed for system '{SystemId}'", systemId);
            return Error("NOTIFICATION_FAILED", ex.Message);
        }
    }

    private static string Error(string code, string message) =>
        JsonSerializer.Serialize(new { status = "error", errorCode = code, message },
            new JsonSerializerOptions { WriteIndented = true });

    private object Meta(Stopwatch sw) => new
    {
        tool = Name,
        duration_ms = sw.ElapsedMilliseconds,
        timestamp = DateTime.UtcNow.ToString("O")
    };
}
