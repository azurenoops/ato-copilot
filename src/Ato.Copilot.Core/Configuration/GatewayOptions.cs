namespace Ato.Copilot.Core.Configuration;

/// <summary>
/// Azure AD authentication configuration.
/// </summary>
public class AzureAdOptions
{
    public const string SectionName = "AzureAd";

    public string Instance { get; set; } = string.Empty;
    public string TenantId { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public string Authority => $"{Instance}{TenantId}/v2.0";
    public bool RequireMfa { get; set; }
    public bool RequireCac { get; set; }
    public List<string> ValidIssuers { get; set; } = new();
    public bool EnableUserTokenPassthrough { get; set; }
}

/// <summary>
/// Gateway connection configuration for Azure, OpenAI, and GitHub
/// </summary>
public class GatewayOptions
{
    public const string SectionName = "Gateway";

    public AzureGatewayOptions Azure { get; set; } = new();
    public AzureOpenAIGatewayOptions AzureOpenAI { get; set; } = new();
    public GitHubGatewayOptions GitHub { get; set; } = new();
    public int ConnectionTimeoutSeconds { get; set; } = 60;
    public int RequestTimeoutSeconds { get; set; } = 300;
}

public class AzureGatewayOptions
{
    public string TenantId { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string SubscriptionId { get; set; } = string.Empty;
    public bool UseManagedIdentity { get; set; }
    public string CloudEnvironment { get; set; } = "AzureGovernment";
    public bool Enabled { get; set; } = true;
    public bool EnableUserTokenPassthrough { get; set; }
}

/// <summary>
/// Azure OpenAI service configuration for agent AI processing.
/// Bound from the "Gateway:AzureOpenAI" configuration section.
/// </summary>
public class AzureOpenAIGatewayOptions
{
    /// <summary>API key for Azure OpenAI authentication (used when UseManagedIdentity is false).</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Azure OpenAI service endpoint URL (e.g., https://my-service.openai.azure.us/).</summary>
    public string Endpoint { get; set; } = string.Empty;

    /// <summary>Default deployment name for backward compatibility.</summary>
    public string DeploymentName { get; set; } = "gpt-4o";

    /// <summary>Whether to use DefaultAzureCredential instead of API key.</summary>
    public bool UseManagedIdentity { get; set; }

    /// <summary>Chat completion deployment name used for agent AI processing.</summary>
    public string ChatDeploymentName { get; set; } = "gpt-4o";

    /// <summary>Embedding deployment name for vector operations.</summary>
    public string EmbeddingDeploymentName { get; set; } = "text-embedding-ada-002";

    /// <summary>Master feature flag — when false, all agents skip AI processing and use deterministic tool routing.</summary>
    public bool AgentAIEnabled { get; set; }

    /// <summary>Maximum number of LLM ↔ tool-call round-trips before terminating with a summary response.</summary>
    public int MaxToolCallRounds { get; set; } = 5;

    /// <summary>LLM sampling temperature (0.0–1.0). Lower values produce more deterministic responses.</summary>
    public double Temperature { get; set; } = 0.3;
}

public class GitHubGatewayOptions
{
    public string AccessToken { get; set; } = string.Empty;
    public string ApiBaseUrl { get; set; } = "https://api.github.com";
    public string DefaultOwner { get; set; } = string.Empty;
    public bool Enabled { get; set; }
}

/// <summary>
/// PIM service configuration for role activation, JIT access, and ticket validation.
/// Bound from the "Pim" configuration section.
/// </summary>
public class PimServiceOptions
{
    /// <summary>Configuration section name for binding.</summary>
    public const string SectionName = "Pim";

    /// <summary>Default PIM role activation duration in hours (FR-010).</summary>
    public int DefaultActivationDurationHours { get; set; } = 4;

    /// <summary>Maximum allowed PIM role activation duration in hours (FR-010).</summary>
    public int MaxActivationDurationHours { get; set; } = 8;

    /// <summary>Default JIT VM access duration in hours.</summary>
    public int DefaultJitDurationHours { get; set; } = 3;

    /// <summary>Maximum allowed JIT VM access duration in hours.</summary>
    public int MaxJitDurationHours { get; set; } = 24;

    /// <summary>Whether ticket number is required for PIM activation (opt-in, default false).</summary>
    public bool RequireTicketNumber { get; set; }

    /// <summary>Minimum character length for justification text.</summary>
    public int MinJustificationLength { get; set; } = 20;

    /// <summary>Maximum character length for justification text.</summary>
    public int MaxJustificationLength { get; set; } = 500;

    /// <summary>Role names classified as high-privilege requiring approval.</summary>
    public List<string> HighPrivilegeRoles { get; set; } = new()
    {
        "Owner",
        "User Access Administrator",
        "Security Administrator",
        "Global Administrator",
        "Privileged Role Administrator"
    };

    /// <summary>
    /// Approved ticketing systems mapped to regex validation patterns.
    /// Key = system name, Value = regex pattern for ticket number format.
    /// </summary>
    public Dictionary<string, string> ApprovedTicketSystems { get; set; } = new()
    {
        ["ServiceNow"] = @"^SNOW-[A-Z]+-\d+$",
        ["Jira"] = @"^[A-Z]{2,10}-\d+$",
        ["Remedy"] = @"^HD-\d+$",
        ["AzureDevOps"] = @"^AB#\d+$"
    };

    /// <summary>Timeout in minutes for approval requests before they auto-expire.</summary>
    public int ApprovalTimeoutMinutes { get; set; } = 1440;

    /// <summary>Warning threshold in minutes before session expiration.</summary>
    public int SessionExpirationWarningMinutes { get; set; } = 15;

    /// <summary>Whether to auto-deactivate PIM roles after remediation completes.</summary>
    public bool AutoDeactivateAfterRemediation { get; set; }
}

/// <summary>
/// CAC/PIV authentication session configuration.
/// Bound from the "CacAuth" configuration section.
/// </summary>
public class CacAuthOptions
{
    /// <summary>Configuration section name for binding.</summary>
    public const string SectionName = "CacAuth";

    /// <summary>Default session timeout in hours when not specified by user.</summary>
    public int DefaultSessionTimeoutHours { get; set; } = 8;

    /// <summary>Maximum allowed session timeout in hours.</summary>
    public int MaxSessionTimeoutHours { get; set; } = 24;
}

/// <summary>
/// Configuration for data retention policies per federal compliance requirements.
/// Assessment data retained minimum 3 years (1095 days per FR-042).
/// Audit logs retained minimum 7 years (2555 days per FR-043), immutable and append-only.
/// Bound from the "Retention" configuration section.
/// </summary>
public class RetentionPolicyOptions
{
    /// <summary>Configuration section name for binding.</summary>
    public const string SectionName = "Retention";

    /// <summary>
    /// Number of days to retain assessment results before cleanup.
    /// Minimum 365 days (1 year); default 1095 days (3 years) per FR-042.
    /// </summary>
    public int AssessmentRetentionDays { get; set; } = 1095;

    /// <summary>
    /// Number of days to retain audit log entries (immutable, append-only).
    /// Minimum 2555 days (7 years); default 2555 days per FR-043.
    /// Audit logs are never deleted by automated cleanup.
    /// </summary>
    public int AuditLogRetentionDays { get; set; } = 2555;

    /// <summary>
    /// Interval in hours between automated cleanup runs.
    /// Minimum 1 hour; default 24 hours (daily cleanup).
    /// </summary>
    public int CleanupIntervalHours { get; set; } = 24;

    /// <summary>
    /// Whether automated cleanup is enabled.
    /// When false, the RetentionCleanupHostedService will not be registered.
    /// </summary>
    public bool EnableAutomaticCleanup { get; set; } = true;

    /// <summary>Number of days to retain compliance alerts (2–7 years). Default 730 (2 years).</summary>
    public int AlertRetentionDays { get; set; } = 730;

    /// <summary>Number of days to retain daily compliance snapshots. Default 90.</summary>
    public int DailySnapshotRetentionDays { get; set; } = 90;

    /// <summary>Number of days to retain weekly compliance snapshots. Default 730 (2 years).</summary>
    public int WeeklySnapshotRetentionDays { get; set; } = 730;
}

/// <summary>
/// Configuration for compliance monitoring schedules and behavior.
/// Bound from the "Monitoring" configuration section.
/// </summary>
public class MonitoringOptions
{
    /// <summary>Configuration section name for binding.</summary>
    public const string SectionName = "Monitoring";

    /// <summary>Default monitoring frequency for new configurations.</summary>
    public string DefaultFrequency { get; set; } = "Hourly";

    /// <summary>Default monitoring mode for new configurations.</summary>
    public string DefaultMode { get; set; } = "Scheduled";

    /// <summary>Background service tick interval in seconds. Default 60 (1 minute).</summary>
    public int TickIntervalSeconds { get; set; } = 60;

    /// <summary>Activity Log polling interval in seconds for event-driven mode. Default 120 (2 minutes).</summary>
    public int EventPollIntervalSeconds { get; set; } = 120;

    /// <summary>Maximum number of concurrent monitoring checks. Default 5.</summary>
    public int MaxConcurrentChecks { get; set; } = 5;

    /// <summary>Whether to enable monitoring on startup. Default true.</summary>
    public bool EnableOnStartup { get; set; } = true;

    /// <summary>Azure cloud environment: AzureGovernment or AzureCloud. Default AzureGovernment.</summary>
    public string CloudEnvironment { get; set; } = "AzureGovernment";

    // ─── Phase 17 §9a.5 — Drift-to-SignificantChange thresholds ─────────────

    /// <summary>
    /// Minimum number of drift detections within a monitoring cycle that qualifies
    /// as a <em>significant change</em> and triggers ConMon alert creation.
    /// Default: 5 (per spec §9a.5).
    /// </summary>
    public int SignificantDriftThreshold { get; set; } = 5;

    /// <summary>
    /// When <c>true</c>, the system automatically creates <c>SignificantChange</c>
    /// records when drift exceeds <see cref="SignificantDriftThreshold"/>.
    /// </summary>
    public bool AutoCreateSignificantChanges { get; set; } = true;

    /// <summary>
    /// Maximum number of drift alerts to include in a single ConMon report enrichment snapshot.
    /// Prevents runaway queries on large environments.
    /// </summary>
    public int MaxDriftAlertsPerReport { get; set; } = 500;
}

/// <summary>
/// Configuration for compliance alert behavior and SLA deadlines.
/// Bound from the "Alerts" configuration section.
/// </summary>
public class AlertOptions
{
    /// <summary>Configuration section name for binding.</summary>
    public const string SectionName = "Alerts";

    /// <summary>SLA deadline in minutes for Critical alerts. Default 60 (1 hour).</summary>
    public int CriticalSlaMinutes { get; set; } = 60;

    /// <summary>SLA deadline in minutes for High alerts. Default 240 (4 hours).</summary>
    public int HighSlaMinutes { get; set; } = 240;

    /// <summary>SLA deadline in minutes for Medium alerts. Default 1440 (24 hours).</summary>
    public int MediumSlaMinutes { get; set; } = 1440;

    /// <summary>SLA deadline in minutes for Low alerts. Default 10080 (7 days).</summary>
    public int LowSlaMinutes { get; set; } = 10080;

    /// <summary>Default page size for alert queries. Default 50.</summary>
    public int DefaultPageSize { get; set; } = 50;

    /// <summary>Maximum page size for alert queries. Default 200.</summary>
    public int MaxPageSize { get; set; } = 200;

    /// <summary>Secure score threshold percentage below which DEGRADATION alerts fire. Default 80.0.</summary>
    public double SecureScoreThreshold { get; set; } = 80.0;
}

/// <summary>
/// Configuration for alert notification delivery.
/// Bound from the "Notifications" configuration section.
/// </summary>
public class NotificationOptions
{
    /// <summary>Configuration section name for binding.</summary>
    public const string SectionName = "Notifications";

    /// <summary>Maximum notifications per minute per channel. Default 10.</summary>
    public int MaxPerMinutePerChannel { get; set; } = 10;

    /// <summary>Whether to enable email notifications. Default false.</summary>
    public bool EnableEmail { get; set; }

    /// <summary>Whether to enable webhook notifications. Default false.</summary>
    public bool EnableWebhook { get; set; }

    /// <summary>Daily digest delivery hour (UTC 0–23). Default 8 (08:00 UTC).</summary>
    public int DigestHourUtc { get; set; } = 8;

    /// <summary>Bounded channel capacity for async notification dispatch. Default 500.</summary>
    public int ChannelCapacity { get; set; } = 500;
}

/// <summary>
/// Configuration for alert escalation behavior.
/// Bound from the "Escalation" configuration section.
/// </summary>
public class EscalationOptions
{
    /// <summary>Configuration section name for binding.</summary>
    public const string SectionName = "Escalation";

    /// <summary>Escalation check interval in seconds. Default 300 (5 minutes).</summary>
    public int CheckIntervalSeconds { get; set; } = 300;

    /// <summary>Default escalation delay in minutes after SLA expiry. Default 15.</summary>
    public int DefaultDelayMinutes { get; set; } = 15;

    /// <summary>Default maximum escalation attempts. Default 3.</summary>
    public int DefaultMaxEscalations { get; set; } = 3;

    /// <summary>Default repeat interval in minutes between escalation notifications. Default 30.</summary>
    public int DefaultRepeatIntervalMinutes { get; set; } = 30;
}
