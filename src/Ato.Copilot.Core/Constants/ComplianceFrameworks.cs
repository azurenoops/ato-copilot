namespace Ato.Copilot.Core.Constants;

using Ato.Copilot.Core.Models.Compliance;

/// <summary>
/// Compliance framework identifiers and Azure Policy initiative GUIDs.
/// Supports NIST 800-53 Rev 5, FedRAMP High/Moderate, and DoD IL5.
/// </summary>
public static class ComplianceFrameworks
{
    /// <summary>NIST 800-53 Rev 5 framework identifier.</summary>
    public const string Nist80053 = "NIST80053";

    /// <summary>FedRAMP High baseline framework identifier.</summary>
    public const string FedRampHigh = "FedRAMPHigh";

    /// <summary>FedRAMP Moderate baseline framework identifier.</summary>
    public const string FedRampModerate = "FedRAMPModerate";

    /// <summary>DoD Impact Level 5 framework identifier.</summary>
    public const string DoDIL5 = "DoDIL5";

    /// <summary>
    /// All supported framework identifiers.
    /// </summary>
    public static readonly HashSet<string> AllFrameworks = new(StringComparer.OrdinalIgnoreCase)
    {
        Nist80053,
        FedRampHigh,
        FedRampModerate,
        DoDIL5
    };

    /// <summary>
    /// Azure Policy regulatory compliance initiative GUIDs per framework.
    /// Note: Azure Government may have different IDs — query programmatically as fallback.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> PolicyInitiativeIds =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [Nist80053] = "179d1daa-458f-4e47-8086-2a68d0d6c38f",
            [FedRampHigh] = "d5264498-16f4-418a-b659-fa7ef418175f",
            [FedRampModerate] = "e95f5a9f-57ad-4d03-bb0b-b1d16db93693",
            [DoDIL5] = "f15e86d0-8189-4e81-9999-30e5547f5fac"
        };

    /// <summary>
    /// Maps framework identifier to human-readable display name.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> DisplayNames =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [Nist80053] = "NIST 800-53 Rev 5",
            [FedRampHigh] = "FedRAMP High",
            [FedRampModerate] = "FedRAMP Moderate",
            [DoDIL5] = "DoD Impact Level 5"
        };

    /// <summary>
    /// Valid baseline levels for FedRAMP and NIST frameworks.
    /// </summary>
    public static readonly HashSet<string> ValidBaselines = new(StringComparer.OrdinalIgnoreCase)
    {
        "High",
        "Moderate",
        "Low"
    };

    /// <summary>
    /// Returns true if the given framework identifier is recognized.
    /// </summary>
    /// <param name="framework">Framework identifier (e.g., "NIST80053").</param>
    /// <returns>True if valid.</returns>
    public static bool IsValidFramework(string framework) =>
        AllFrameworks.Contains(framework);

    /// <summary>
    /// Returns true if the given baseline level is recognized.
    /// </summary>
    /// <param name="baseline">Baseline level (e.g., "High").</param>
    /// <returns>True if valid.</returns>
    public static bool IsValidBaseline(string baseline) =>
        ValidBaselines.Contains(baseline);

    /// <summary>
    /// Normalizes a framework string to canonical form (case-insensitive match).
    /// Returns null if not recognized.
    /// </summary>
    /// <param name="framework">Framework identifier to normalize.</param>
    /// <returns>Canonical framework string, or null if invalid.</returns>
    public static string? Normalize(string framework)
    {
        foreach (var valid in AllFrameworks)
        {
            if (string.Equals(valid, framework, StringComparison.OrdinalIgnoreCase))
                return valid;
        }
        return null;
    }

    // ──────────────────────── NIST Baseline Control Counts ────────────────────────

    /// <summary>Number of controls in the NIST 800-53 Low baseline.</summary>
    public const int LowBaselineControlCount = 131;

    /// <summary>Number of controls in the NIST 800-53 Moderate baseline.</summary>
    public const int ModerateBaselineControlCount = 325;

    /// <summary>Number of controls in the NIST 800-53 High baseline.</summary>
    public const int HighBaselineControlCount = 421;

    /// <summary>
    /// Gets the expected control count for a NIST baseline level.
    /// </summary>
    /// <param name="baselineLevel">Baseline level: "Low", "Moderate", or "High".</param>
    /// <returns>Expected control count, or 0 for unknown levels.</returns>
    public static int GetBaselineControlCount(string baselineLevel) =>
        baselineLevel.ToLowerInvariant() switch
        {
            "low" => LowBaselineControlCount,
            "moderate" => ModerateBaselineControlCount,
            "high" => HighBaselineControlCount,
            _ => 0
        };

    // ──────────────────────── RMF Step Display Names ────────────────────────

    /// <summary>
    /// Human-readable display names for each RMF step.
    /// </summary>
    public static readonly IReadOnlyDictionary<RmfPhase, string> RmfPhaseDisplayNames =
        new Dictionary<RmfPhase, string>
        {
            [RmfPhase.Prepare] = "Step 0 — Prepare",
            [RmfPhase.Categorize] = "Step 1 — Categorize",
            [RmfPhase.Select] = "Step 2 — Select",
            [RmfPhase.Implement] = "Step 3 — Implement",
            [RmfPhase.Assess] = "Step 4 — Assess",
            [RmfPhase.Authorize] = "Step 5 — Authorize",
            [RmfPhase.Monitor] = "Step 6 — Monitor"
        };

    /// <summary>
    /// Gets the display name for an RMF step.
    /// </summary>
    public static string GetStepDisplayName(RmfPhase step) =>
        RmfPhaseDisplayNames.TryGetValue(step, out var name) ? name : step.ToString();

    /// <summary>
    /// Gets the numeric step number (0–6) for an RMF step.
    /// </summary>
    public static int GetStepNumber(RmfPhase step) => (int)step;

    // ──────────────────────── FIPS 199 Notation ────────────────────────

    /// <summary>
    /// Formats a FIPS 199 security categorization notation string.
    /// Example: "SC information system = {(confidentiality, HIGH), (integrity, MODERATE), (availability, LOW)}"
    /// </summary>
    /// <param name="systemName">System name for the notation.</param>
    /// <param name="confidentiality">Confidentiality impact value.</param>
    /// <param name="integrity">Integrity impact value.</param>
    /// <param name="availability">Availability impact value.</param>
    /// <returns>Formal FIPS 199 security categorization notation.</returns>
    public static string FormatFips199Notation(
        string systemName,
        ImpactValue confidentiality,
        ImpactValue integrity,
        ImpactValue availability)
    {
        return $"SC {systemName} = {{(confidentiality, {confidentiality.ToString().ToUpperInvariant()}), " +
               $"(integrity, {integrity.ToString().ToUpperInvariant()}), " +
               $"(availability, {availability.ToString().ToUpperInvariant()})}}";
    }

    // ──────────────────────── DoD Impact Level Derivation ────────────────────────

    /// <summary>
    /// Derives the DoD Impact Level (IL) from FIPS 199 overall categorization and NSS status.
    /// IL2 = Low, IL4 = Moderate, IL5 = High, IL6 = classified (NSS with Secret/TopSecret designation).
    /// </summary>
    /// <param name="overallCategorization">The FIPS 199 high-water-mark impact value.</param>
    /// <param name="isNationalSecuritySystem">Whether the system is designated NSS.</param>
    /// <param name="classifiedDesignation">Classified designation string (e.g., "Secret", "TopSecret"), or null.</param>
    /// <returns>DoD Impact Level string (e.g., "IL2", "IL4", "IL5", "IL6").</returns>
    public static string DeriveImpactLevel(
        ImpactValue overallCategorization,
        bool isNationalSecuritySystem,
        string? classifiedDesignation)
    {
        if (isNationalSecuritySystem && !string.IsNullOrEmpty(classifiedDesignation))
            return "IL6";

        return overallCategorization switch
        {
            ImpactValue.Low => "IL2",
            ImpactValue.Moderate => "IL4",
            ImpactValue.High => "IL5",
            _ => "IL2"
        };
    }

    /// <summary>
    /// Derives the NIST baseline level from the FIPS 199 overall categorization.
    /// </summary>
    public static string DeriveBaselineLevel(ImpactValue overallCategorization) =>
        overallCategorization switch
        {
            ImpactValue.Low => "Low",
            ImpactValue.Moderate => "Moderate",
            ImpactValue.High => "High",
            _ => "Low"
        };

    // ──────────────────────── NIST Control Family Names ────────────────────────

    /// <summary>
    /// NIST 800-53 control family abbreviations and full names.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> ControlFamilyNames =
        new Dictionary<string, string>
        {
            ["AC"] = "Access Control",
            ["AT"] = "Awareness and Training",
            ["AU"] = "Audit and Accountability",
            ["CA"] = "Assessment, Authorization, and Monitoring",
            ["CM"] = "Configuration Management",
            ["CP"] = "Contingency Planning",
            ["IA"] = "Identification and Authentication",
            ["IR"] = "Incident Response",
            ["MA"] = "Maintenance",
            ["MP"] = "Media Protection",
            ["PE"] = "Physical and Environmental Protection",
            ["PL"] = "Planning",
            ["PM"] = "Program Management",
            ["PS"] = "Personnel Security",
            ["PT"] = "Personally Identifiable Information Processing and Transparency",
            ["RA"] = "Risk Assessment",
            ["SA"] = "System and Services Acquisition",
            ["SC"] = "System and Communications Protection",
            ["SI"] = "System and Information Integrity",
            ["SR"] = "Supply Chain Risk Management"
        };

    /// <summary>
    /// Extracts the control family prefix from a NIST control ID.
    /// Example: "AC-2(1)" → "AC", "SI-4" → "SI"
    /// </summary>
    public static string ExtractControlFamily(string controlId)
    {
        if (string.IsNullOrEmpty(controlId)) return string.Empty;
        var dashIndex = controlId.IndexOf('-');
        return dashIndex > 0 ? controlId[..dashIndex] : controlId;
    }
}
