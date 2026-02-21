namespace Ato.Copilot.Core.Constants;

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
}
