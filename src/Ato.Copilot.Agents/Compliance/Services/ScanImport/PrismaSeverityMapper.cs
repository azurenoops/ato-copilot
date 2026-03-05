// ═══════════════════════════════════════════════════════════════════════════
// Feature 019 — Prisma Cloud Scan Import: Severity Mapper
// Maps Prisma Cloud severity strings to DoD CAT severity and FindingSeverity.
// See specs/019-prisma-cloud-import/data-model.md §Severity Mapping.
// ═══════════════════════════════════════════════════════════════════════════

using Ato.Copilot.Core.Models.Compliance;

namespace Ato.Copilot.Agents.Compliance.Services.ScanImport;

/// <summary>
/// Static helper for mapping Prisma Cloud severity strings to DoD/RMF severity enums.
/// Prisma uses 5 levels: critical, high, medium, low, informational.
/// DoD CAT has 3 levels: CAT I, CAT II, CAT III.
/// </summary>
public static class PrismaSeverityMapper
{
    /// <summary>
    /// Map a Prisma severity string to DoD CAT severity.
    /// Returns <c>null</c> for informational (no CAT equivalent).
    /// Defaults to <see cref="CatSeverity.CatII"/> for unknown/null values.
    /// </summary>
    /// <param name="prismaSeverity">Prisma severity string (case-insensitive).</param>
    /// <returns>CAT severity, or null for informational findings.</returns>
    public static CatSeverity? MapToCatSeverity(string? prismaSeverity)
    {
        var normalized = prismaSeverity?.Trim().ToLowerInvariant();

        return normalized switch
        {
            "critical" => CatSeverity.CatI,
            "high"     => CatSeverity.CatI,
            "medium"   => CatSeverity.CatII,
            "low"      => CatSeverity.CatIII,
            "informational" => null,
            _ => CatSeverity.CatII  // Default: unknown/null → medium equivalent
        };
    }

    /// <summary>
    /// Map a Prisma severity string to <see cref="FindingSeverity"/>.
    /// Defaults to <see cref="FindingSeverity.Medium"/> for unknown/null values.
    /// </summary>
    /// <param name="prismaSeverity">Prisma severity string (case-insensitive).</param>
    /// <returns>Finding severity enum value.</returns>
    public static FindingSeverity MapToFindingSeverity(string? prismaSeverity)
    {
        var normalized = prismaSeverity?.Trim().ToLowerInvariant();

        return normalized switch
        {
            "critical"      => FindingSeverity.Critical,
            "high"          => FindingSeverity.High,
            "medium"        => FindingSeverity.Medium,
            "low"           => FindingSeverity.Low,
            "informational" => FindingSeverity.Informational,
            _ => FindingSeverity.Medium  // Default: unknown/null → medium
        };
    }
}
