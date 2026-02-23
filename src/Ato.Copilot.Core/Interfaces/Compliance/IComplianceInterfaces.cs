using Ato.Copilot.Core.Models.Compliance;

namespace Ato.Copilot.Core.Interfaces.Compliance;

/// <summary>
/// Core compliance scanning engine for NIST 800-53 assessments
/// </summary>
public interface IAtoComplianceEngine
{
    Task<ComplianceAssessment> RunAssessmentAsync(
        string subscriptionId,
        string? framework = null,
        string? controlFamilies = null,
        string? resourceTypes = null,
        string? scanType = null,
        bool includePassed = false,
        CancellationToken cancellationToken = default);

    Task<List<ComplianceAssessment>> GetAssessmentHistoryAsync(
        string subscriptionId,
        int days = 30,
        CancellationToken cancellationToken = default);

    Task<ComplianceFinding?> GetFindingAsync(
        string findingId,
        CancellationToken cancellationToken = default);

    Task SaveAssessmentAsync(
        ComplianceAssessment assessment,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Remediation engine for compliance findings
/// </summary>
public interface IRemediationEngine
{
    Task<RemediationPlan> GeneratePlanAsync(
        string subscriptionId,
        string? resourceGroupName = null,
        CancellationToken cancellationToken = default);

    Task<string> ExecuteRemediationAsync(
        string findingId,
        bool applyRemediation = false,
        bool dryRun = true,
        CancellationToken cancellationToken = default);

    Task<string> ValidateRemediationAsync(
        string findingId,
        string? executionId = null,
        string? subscriptionId = null,
        CancellationToken cancellationToken = default);

    Task<string> BatchRemediateAsync(
        string? subscriptionId = null,
        string? severity = null,
        string? family = null,
        bool dryRun = true,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// NIST 800-53 controls catalog service
/// </summary>
public interface INistControlsService
{
    Task<NistControl?> GetControlAsync(
        string controlId,
        CancellationToken cancellationToken = default);

    Task<List<NistControl>> GetControlFamilyAsync(
        string familyId,
        bool includeControls = true,
        CancellationToken cancellationToken = default);

    Task<List<NistControl>> SearchControlsAsync(
        string query,
        string? controlFamily = null,
        string? impactLevel = null,
        int maxResults = 10,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Azure Policy compliance integration 
/// </summary>
public interface IAzurePolicyComplianceService
{
    Task<string> GetComplianceSummaryAsync(
        string subscriptionId,
        CancellationToken cancellationToken = default);

    Task<string> GetPolicyStatesAsync(
        string subscriptionId,
        string? policyDefinitionId = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Microsoft Defender for Cloud integration
/// </summary>
public interface IDefenderForCloudService
{
    Task<string> GetSecureScoreAsync(
        string subscriptionId,
        CancellationToken cancellationToken = default);

    Task<string> GetAssessmentsAsync(
        string subscriptionId,
        CancellationToken cancellationToken = default);

    Task<string> GetRecommendationsAsync(
        string subscriptionId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Evidence storage and collection service
/// </summary>
public interface IEvidenceStorageService
{
    Task<ComplianceEvidence> CollectEvidenceAsync(
        string controlId,
        string subscriptionId,
        string? resourceGroup = null,
        CancellationToken cancellationToken = default);

    Task<List<ComplianceEvidence>> GetEvidenceAsync(
        string controlId,
        string? subscriptionId = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Compliance monitoring for continuous compliance posture tracking
/// </summary>
public interface IComplianceMonitoringService
{
    Task<string> GetStatusAsync(
        string? subscriptionId = null,
        CancellationToken cancellationToken = default);

    Task<string> TriggerScanAsync(
        string? subscriptionId = null,
        CancellationToken cancellationToken = default);

    Task<string> GetAlertsAsync(
        string? subscriptionId = null,
        int days = 30,
        CancellationToken cancellationToken = default);

    Task<string> GetTrendAsync(
        string? subscriptionId = null,
        int days = 30,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Compliance document generation service
/// </summary>
public interface IDocumentGenerationService
{
    Task<ComplianceDocument> GenerateDocumentAsync(
        string documentType,
        string? subscriptionId = null,
        string? framework = null,
        string? systemName = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Assessment audit trail service
/// </summary>
public interface IAssessmentAuditService
{
    Task<string> GetAuditLogAsync(
        string? subscriptionId = null,
        int days = 7,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Compliance history and trending service
/// </summary>
public interface IComplianceHistoryService
{
    Task<string> GetHistoryAsync(
        string? subscriptionId = null,
        int days = 30,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Real-time compliance status summary service
/// </summary>
public interface IComplianceStatusService
{
    Task<string> GetStatusAsync(
        string? subscriptionId = null,
        string? framework = null,
        CancellationToken cancellationToken = default);
}

// ──────────────────────────────── Compliance Watch Interfaces ────────────────────────────────────

/// <summary>
/// Alert lifecycle manager — CRUD, state machine transitions, ID generation, role-based access.
/// </summary>
public interface IAlertManager
{
    /// <summary>Create a new compliance alert with auto-generated alert ID.</summary>
    Task<ComplianceAlert> CreateAlertAsync(
        ComplianceAlert alert,
        CancellationToken cancellationToken = default);

    /// <summary>Transition an alert to a new status with role-based validation.</summary>
    Task<ComplianceAlert> TransitionAlertAsync(
        Guid alertId,
        AlertStatus newStatus,
        string userId,
        string userRole,
        string? justification = null,
        CancellationToken cancellationToken = default);

    /// <summary>Get a single alert by internal ID.</summary>
    Task<ComplianceAlert?> GetAlertAsync(
        Guid alertId,
        CancellationToken cancellationToken = default);

    /// <summary>Get a single alert by human-readable alert ID.</summary>
    Task<ComplianceAlert?> GetAlertByAlertIdAsync(
        string alertId,
        CancellationToken cancellationToken = default);

    /// <summary>Get paginated list of alerts with optional filtering.</summary>
    Task<(List<ComplianceAlert> Alerts, int TotalCount)> GetAlertsAsync(
        string? subscriptionId = null,
        AlertSeverity? severity = null,
        AlertStatus? status = null,
        string? controlFamily = null,
        int? days = null,
        int page = 1,
        int pageSize = 50,
        CancellationToken cancellationToken = default);

    /// <summary>Generate a new sequential alert ID (ALT-YYYYMMDDNNNNN).</summary>
    Task<string> GenerateAlertIdAsync(
        CancellationToken cancellationToken = default);

    /// <summary>Dismiss an alert with required justification (Compliance Officer only).</summary>
    Task<ComplianceAlert> DismissAlertAsync(
        Guid alertId,
        string justification,
        string userId,
        string userRole,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Compliance Watch monitoring service — manages monitoring configurations, baselines,
/// drift detection, and scheduled compliance checks.
/// </summary>
public interface IComplianceWatchService
{
    /// <summary>Enable monitoring for a subscription or resource group scope.</summary>
    Task<MonitoringConfiguration> EnableMonitoringAsync(
        string subscriptionId,
        string? resourceGroupName = null,
        MonitoringFrequency frequency = MonitoringFrequency.Hourly,
        MonitoringMode mode = MonitoringMode.Scheduled,
        string createdBy = "system",
        CancellationToken cancellationToken = default);

    /// <summary>Disable monitoring for a scope.</summary>
    Task<MonitoringConfiguration> DisableMonitoringAsync(
        string subscriptionId,
        string? resourceGroupName = null,
        CancellationToken cancellationToken = default);

    /// <summary>Update monitoring configuration (frequency, mode).</summary>
    Task<MonitoringConfiguration> ConfigureMonitoringAsync(
        string subscriptionId,
        string? resourceGroupName = null,
        MonitoringFrequency? frequency = null,
        MonitoringMode? mode = null,
        CancellationToken cancellationToken = default);

    /// <summary>Get monitoring status for all or a specific subscription.</summary>
    Task<List<MonitoringConfiguration>> GetMonitoringStatusAsync(
        string? subscriptionId = null,
        CancellationToken cancellationToken = default);

    /// <summary>Capture a compliance baseline for all resources in a scope after assessment.</summary>
    Task<List<ComplianceBaseline>> CaptureBaselineAsync(
        string subscriptionId,
        string? resourceGroupName = null,
        Guid? assessmentId = null,
        CancellationToken cancellationToken = default);

    /// <summary>Run a monitoring check for a specific configuration.</summary>
    Task<int> RunMonitoringCheckAsync(
        MonitoringConfiguration config,
        CancellationToken cancellationToken = default);

    /// <summary>Detect drift from baselines for resources in a scope.</summary>
    Task<List<ComplianceAlert>> DetectDriftAsync(
        string subscriptionId,
        string? resourceGroupName = null,
        CancellationToken cancellationToken = default);

    /// <summary>Create a custom alert rule.</summary>
    Task<AlertRule> CreateAlertRuleAsync(AlertRule rule, CancellationToken cancellationToken = default);

    /// <summary>List alert rules, optionally filtered by subscription.</summary>
    Task<List<AlertRule>> GetAlertRulesAsync(string? subscriptionId = null, CancellationToken cancellationToken = default);

    /// <summary>Create a suppression rule.</summary>
    Task<SuppressionRule> CreateSuppressionAsync(SuppressionRule rule, CancellationToken cancellationToken = default);

    /// <summary>List active suppression rules, optionally filtered by subscription.</summary>
    Task<List<SuppressionRule>> GetSuppressionsAsync(string? subscriptionId = null, CancellationToken cancellationToken = default);

    /// <summary>Configure quiet hours on an existing suppression rule or create a global quiet-hours suppression.</summary>
    Task<SuppressionRule> ConfigureQuietHoursAsync(
        string subscriptionId,
        TimeOnly start,
        TimeOnly end,
        string createdBy = "system",
        CancellationToken cancellationToken = default);

    /// <summary>Check whether an alert should be suppressed based on active rules.</summary>
    bool IsAlertSuppressed(ComplianceAlert alert, IReadOnlyList<SuppressionRule> activeSuppressions);

    /// <summary>Match alert rules against an alert and return the best-matching rule (if any).</summary>
    AlertRule? MatchAlertRule(ComplianceAlert alert, IReadOnlyList<AlertRule> rules);

    /// <summary>Seed default alert rules for a subscription on first enable.</summary>
    Task SeedDefaultRulesAsync(string subscriptionId, string createdBy = "system", CancellationToken cancellationToken = default);

    // ─── Auto-Remediation (US9) ─────────────────────────────────────────────

    /// <summary>Create an auto-remediation rule (validates blocked families AC/IA/SC).</summary>
    Task<AutoRemediationRule> CreateAutoRemediationRuleAsync(AutoRemediationRule rule, CancellationToken cancellationToken = default);

    /// <summary>List auto-remediation rules, optionally filtered by subscription.</summary>
    Task<List<AutoRemediationRule>> GetAutoRemediationRulesAsync(string? subscriptionId = null, bool? isEnabled = null, CancellationToken cancellationToken = default);

    /// <summary>Attempt auto-remediation for an alert using matching rules.</summary>
    Task<AutoRemediationResult> TryAutoRemediateAsync(ComplianceAlert alert, CancellationToken cancellationToken = default);
}

// ─── Notification & Escalation Interfaces (US4) ─────────────────────────────

/// <summary>
/// Service for sending alert notifications across multiple channels with rate limiting.
/// </summary>
public interface IAlertNotificationService
{
    /// <summary>Send a notification for an alert through the appropriate channels.</summary>
    Task SendNotificationAsync(ComplianceAlert alert, CancellationToken cancellationToken = default);

    /// <summary>Send a daily digest of lower-severity alerts.</summary>
    Task SendDigestAsync(string subscriptionId, CancellationToken cancellationToken = default);

    /// <summary>Get the audit trail of notifications sent for a specific alert.</summary>
    Task<List<AlertNotification>> GetNotificationsForAlertAsync(Guid alertId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Service for managing automatic escalation paths and detecting SLA violations.
/// </summary>
public interface IEscalationService
{
    /// <summary>Check for alerts that have exceeded their SLA and trigger escalation.</summary>
    Task CheckEscalationsAsync(CancellationToken cancellationToken = default);

    /// <summary>Create or update an escalation path configuration.</summary>
    Task<EscalationPath> ConfigureEscalationPathAsync(EscalationPath path, CancellationToken cancellationToken = default);

    /// <summary>Get configured escalation paths, optionally filtered by severity.</summary>
    Task<List<EscalationPath>> GetEscalationPathsAsync(AlertSeverity? severity = null, CancellationToken cancellationToken = default);
}

// ─── Event-Driven Monitoring Interfaces (US5) ───────────────────────────────

/// <summary>
/// Represents a compliance-relevant platform event detected from Azure Activity Log or similar source.
/// </summary>
public class ComplianceEvent
{
    /// <summary>Unique event identifier.</summary>
    public string EventId { get; set; } = string.Empty;

    /// <summary>Type of event (e.g., ResourceWrite, ResourceDelete, PolicyAssignmentChange, RoleAssignmentChange).</summary>
    public string EventType { get; set; } = string.Empty;

    /// <summary>Azure resource ID affected by this event.</summary>
    public string ResourceId { get; set; } = string.Empty;

    /// <summary>Identity of the actor who caused the event.</summary>
    public string ActorId { get; set; } = string.Empty;

    /// <summary>UTC timestamp when the event occurred.</summary>
    public DateTimeOffset Timestamp { get; set; }

    /// <summary>Azure subscription ID where the event occurred.</summary>
    public string SubscriptionId { get; set; } = string.Empty;

    /// <summary>Resource group name (extracted from resource ID).</summary>
    public string? ResourceGroupName { get; set; }

    /// <summary>Operation name from the activity log (e.g., "Microsoft.Storage/storageAccounts/write").</summary>
    public string OperationName { get; set; } = string.Empty;

    /// <summary>Additional event properties (JSON).</summary>
    public string? Properties { get; set; }
}

/// <summary>
/// Source for compliance-relevant platform events. Polls Azure Activity Log
/// or other event sources for resource changes, policy updates, and role modifications.
/// </summary>
public interface IComplianceEventSource
{
    /// <summary>
    /// Get recent compliance-relevant events since the specified timestamp.
    /// Returns events filtered for write/delete/policy/role operations.
    /// </summary>
    Task<List<ComplianceEvent>> GetRecentEventsAsync(
        string subscriptionId,
        DateTimeOffset since,
        string? resourceGroupName = null,
        CancellationToken cancellationToken = default);
}

// ─── Alert Correlation & Noise Reduction Interfaces (US6) ───────────────────

/// <summary>
/// Tracks a sliding correlation window for grouping related alerts.
/// </summary>
public class CorrelationWindow
{
    /// <summary>Correlation key (e.g., "resource:{resourceId}").</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>Parent alert that groups correlated alerts.</summary>
    public ComplianceAlert ParentAlert { get; set; } = null!;

    /// <summary>Individual alert IDs grouped under this window.</summary>
    public List<Guid> ChildAlertIds { get; set; } = new();

    /// <summary>Timestamp when the window was first opened.</summary>
    public DateTimeOffset OpenedAt { get; set; }

    /// <summary>Timestamp of the most recent match (resets expiry).</summary>
    public DateTimeOffset LastMatchAt { get; set; }

    /// <summary>Number of alerts correlated in this window.</summary>
    public int Count => ChildAlertIds.Count;
}

/// <summary>
/// Result of an alert correlation attempt.
/// </summary>
public class CorrelationResult
{
    /// <summary>True if the alert was merged into an existing correlation group.</summary>
    public bool WasMerged { get; set; }

    /// <summary>The parent (grouped) alert — existing if merged, new if first in window.</summary>
    public ComplianceAlert Alert { get; set; } = null!;

    /// <summary>Correlation key used.</summary>
    public string CorrelationKey { get; set; } = string.Empty;
}

/// <summary>
/// Alert correlation service for grouping related alerts, anomaly detection,
/// and alert storm mitigation. Uses sliding time windows per correlation key.
/// </summary>
public interface IAlertCorrelationService
{
    /// <summary>
    /// Attempt to correlate an alert with existing alerts in active windows.
    /// Returns grouped alert if merged, or the original alert if no correlation found.
    /// </summary>
    Task<CorrelationResult> CorrelateAlertAsync(
        ComplianceAlert alert,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get the active correlation window for a specific key, if any.
    /// </summary>
    Task<CorrelationWindow?> GetCorrelationWindowAsync(
        string correlationKey,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Finalize all expired correlation windows (older than the sliding window duration).
    /// Called periodically to clean up stale windows.
    /// </summary>
    Task<int> FinalizeExpiredWindowsAsync(
        CancellationToken cancellationToken = default);
}
