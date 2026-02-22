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

public class AzureOpenAIGatewayOptions
{
    public string ApiKey { get; set; } = string.Empty;
    public string Endpoint { get; set; } = string.Empty;
    public string DeploymentName { get; set; } = "gpt-4o";
    public bool UseManagedIdentity { get; set; }
    public string ChatDeploymentName { get; set; } = "gpt-4o";
    public string EmbeddingDeploymentName { get; set; } = "text-embedding-ada-002";
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
}
