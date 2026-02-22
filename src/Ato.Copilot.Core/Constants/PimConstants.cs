namespace Ato.Copilot.Core.Constants;

/// <summary>
/// Constants for PIM operations including ticket patterns, high-privilege roles,
/// NIST control mappings, and default durations.
/// </summary>
public static class PimConstants
{
    // ─── Default Ticket System Patterns (per R-009) ──────────────────────────

    /// <summary>ServiceNow ticket pattern (e.g., SNOW-INC-1234).</summary>
    public const string ServiceNowPattern = @"^SNOW-[A-Z]+-\d+$";

    /// <summary>Jira ticket pattern (e.g., PROJ-1234).</summary>
    public const string JiraPattern = @"^[A-Z]{2,10}-\d+$";

    /// <summary>Remedy ticket pattern (e.g., HD-1234).</summary>
    public const string RemedyPattern = @"^HD-\d+$";

    /// <summary>Azure DevOps work item pattern (e.g., AB#1234).</summary>
    public const string AzureDevOpsPattern = @"^AB#\d+$";

    // ─── Default High-Privilege Roles (per R-008) ────────────────────────────

    /// <summary>Roles requiring approval workflow before activation.</summary>
    public static readonly IReadOnlyList<string> DefaultHighPrivilegeRoles = new[]
    {
        "Owner",
        "User Access Administrator",
        "Security Administrator",
        "Global Administrator",
        "Privileged Role Administrator"
    };

    // ─── NIST 800-53 Control Mappings ────────────────────────────────────────

    /// <summary>NIST controls for PIM activation audit events (AC-2: Account Management).</summary>
    public const string NistAC2 = "AC-2";

    /// <summary>NIST controls for least privilege enforcement (AC-6: Least Privilege).</summary>
    public const string NistAC6 = "AC-6";

    /// <summary>NIST controls for authorization to security functions (AC-6(1)).</summary>
    public const string NistAC6_1 = "AC-6(1)";

    /// <summary>NIST controls for least privilege deactivation (AC-6(5)).</summary>
    public const string NistAC6_5 = "AC-6(5)";

    /// <summary>NIST controls for audit events (AU-2: Event Logging).</summary>
    public const string NistAU2 = "AU-2";

    /// <summary>NIST controls for audit record content (AU-3: Content of Audit Records).</summary>
    public const string NistAU3 = "AU-3";

    /// <summary>Standard NIST control mapping array for PIM actions.</summary>
    public static readonly IReadOnlyList<string> PimNistControlMapping = new[]
    {
        NistAC2, NistAC6, NistAU2, NistAU3
    };

    /// <summary>NIST control mapping for approval workflow actions.</summary>
    public static readonly IReadOnlyList<string> ApprovalNistControlMapping = new[]
    {
        NistAC2, NistAC6, NistAC6_1, NistAU2, NistAU3
    };

    /// <summary>NIST control mapping for deactivation actions.</summary>
    public static readonly IReadOnlyList<string> DeactivationNistControlMapping = new[]
    {
        NistAC6, NistAC6_5, NistAU2, NistAU3
    };

    // ─── Default Durations ───────────────────────────────────────────────────

    /// <summary>Default PIM role activation duration in hours.</summary>
    public const int DefaultActivationDurationHours = 8;

    /// <summary>Maximum PIM role activation duration in hours.</summary>
    public const int MaxActivationDurationHours = 24;

    /// <summary>Default JIT VM access duration in hours.</summary>
    public const int DefaultJitDurationHours = 3;

    /// <summary>Maximum JIT VM access duration in hours.</summary>
    public const int MaxJitDurationHours = 24;

    /// <summary>Default CAC session timeout in hours.</summary>
    public const int DefaultSessionTimeoutHours = 8;

    /// <summary>Maximum CAC session timeout in hours.</summary>
    public const int MaxSessionTimeoutHours = 24;

    /// <summary>Minimum justification length in characters.</summary>
    public const int MinJustificationLength = 20;

    /// <summary>Maximum justification length in characters.</summary>
    public const int MaxJustificationLength = 500;
}
