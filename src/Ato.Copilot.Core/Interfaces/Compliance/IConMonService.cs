using Ato.Copilot.Core.Models.Compliance;

namespace Ato.Copilot.Core.Interfaces.Compliance;

// ═══════════════════════════════════════════════════════════════════════════════
// Continuous Monitoring Service Interface (Feature 015 — US9)
// Spec §4.1–4.7: ConMon plans, reports, expiration, significant changes,
// reauthorization triggers, multi-system dashboard.
// ═══════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Service for continuous monitoring lifecycle management.
/// </summary>
public interface IConMonService
{
    /// <summary>Create or update a ConMon plan for a system (one plan per system).</summary>
    Task<ConMonPlan> CreatePlanAsync(
        string systemId, string assessmentFrequency, DateTime annualReviewDate,
        List<string>? reportDistribution, List<string>? significantChangeTriggers,
        string createdBy, CancellationToken cancellationToken = default);

    /// <summary>Generate a periodic ConMon report with compliance score, delta, findings, POA&amp;M status.</summary>
    Task<ConMonReport> GenerateReportAsync(
        string systemId, string reportType, string period,
        string generatedBy, CancellationToken cancellationToken = default);

    /// <summary>Report a significant change that may trigger reauthorization.</summary>
    Task<SignificantChange> ReportChangeAsync(
        string systemId, string changeType, string description,
        string detectedBy, CancellationToken cancellationToken = default);

    /// <summary>Check ATO expiration status with graduated alerts at 90/60/30 days.</summary>
    Task<ExpirationStatus> CheckExpirationAsync(
        string systemId, CancellationToken cancellationToken = default);

    /// <summary>Multi-system dashboard: all systems with name, IL, RMF step, auth status, score, alerts.</summary>
    Task<DashboardResult> GetDashboardAsync(
        bool activeOnly, CancellationToken cancellationToken = default);

    /// <summary>Detect reauthorization triggers and optionally initiate reauthorization workflow.</summary>
    Task<ReauthorizationResult> CheckReauthorizationAsync(
        string systemId, bool initiateIfTriggered,
        CancellationToken cancellationToken = default);
}

// ─────────────────────────────────────────────────────────────────────────────
// DTOs
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>ATO expiration status with graduated alerts.</summary>
public class ExpirationStatus
{
    /// <summary>System ID.</summary>
    public string SystemId { get; set; } = string.Empty;

    /// <summary>System name.</summary>
    public string SystemName { get; set; } = string.Empty;

    /// <summary>Whether the system has an active authorization.</summary>
    public bool HasActiveAuthorization { get; set; }

    /// <summary>Authorization decision type (ATO, ATOwC, IATT, DATO).</summary>
    public string? DecisionType { get; set; }

    /// <summary>Authorization decision date.</summary>
    public DateTime? DecisionDate { get; set; }

    /// <summary>Expiration date.</summary>
    public DateTime? ExpirationDate { get; set; }

    /// <summary>Days until expiration (negative = past due).</summary>
    public int? DaysUntilExpiration { get; set; }

    /// <summary>Alert level: None, Info, Warning, Urgent, Expired.</summary>
    public string AlertLevel { get; set; } = "None";

    /// <summary>Alert message for the user.</summary>
    public string AlertMessage { get; set; } = string.Empty;

    /// <summary>Whether the system is operating without authorization.</summary>
    public bool IsExpired { get; set; }
}

/// <summary>Multi-system dashboard result.</summary>
public class DashboardResult
{
    /// <summary>Total systems in dashboard.</summary>
    public int TotalSystems { get; set; }

    /// <summary>Count of systems with active authorization.</summary>
    public int AuthorizedCount { get; set; }

    /// <summary>Count of systems with expiring authorization (&lt;90 days).</summary>
    public int ExpiringCount { get; set; }

    /// <summary>Count of systems with expired authorization.</summary>
    public int ExpiredCount { get; set; }

    /// <summary>Per-system summary rows.</summary>
    public List<DashboardSystemRow> Systems { get; set; } = new();
}

/// <summary>Single system row in the multi-system dashboard.</summary>
public class DashboardSystemRow
{
    /// <summary>System ID.</summary>
    public string SystemId { get; set; } = string.Empty;

    /// <summary>System name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>System acronym.</summary>
    public string? Acronym { get; set; }

    /// <summary>Impact level derived from security categorization.</summary>
    public string ImpactLevel { get; set; } = "Unknown";

    /// <summary>Current RMF step.</summary>
    public string CurrentRmfStep { get; set; } = string.Empty;

    /// <summary>Authorization status: Authorized, Expired, Pending, None.</summary>
    public string AuthorizationStatus { get; set; } = "None";

    /// <summary>Authorization decision type.</summary>
    public string? DecisionType { get; set; }

    /// <summary>Authorization expiration date.</summary>
    public DateTime? ExpirationDate { get; set; }

    /// <summary>Days until expiration (negative = past due).</summary>
    public int? DaysUntilExpiration { get; set; }

    /// <summary>Current compliance score.</summary>
    public double? ComplianceScore { get; set; }

    /// <summary>Open finding count.</summary>
    public int OpenFindings { get; set; }

    /// <summary>Open POA&amp;M count.</summary>
    public int OpenPoamItems { get; set; }

    /// <summary>Alert count (expiration + significant changes).</summary>
    public int AlertCount { get; set; }
}

/// <summary>Reauthorization trigger check result.</summary>
public class ReauthorizationResult
{
    /// <summary>System ID.</summary>
    public string SystemId { get; set; } = string.Empty;

    /// <summary>Whether reauthorization is triggered.</summary>
    public bool IsTriggered { get; set; }

    /// <summary>Reasons for reauthorization.</summary>
    public List<string> Triggers { get; set; } = new();

    /// <summary>Whether reauthorization was initiated (RMF step regressed to Assess).</summary>
    public bool WasInitiated { get; set; }

    /// <summary>Previous RMF step (before regression).</summary>
    public string? PreviousRmfStep { get; set; }

    /// <summary>New RMF step (after regression, if initiated).</summary>
    public string? NewRmfStep { get; set; }

    /// <summary>Active significant changes requiring review.</summary>
    public int UnreviewedChangeCount { get; set; }
}
