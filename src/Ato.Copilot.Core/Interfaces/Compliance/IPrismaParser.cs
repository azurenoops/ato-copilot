// ═══════════════════════════════════════════════════════════════════════════
// Feature 019 — Prisma Cloud Scan Import: Parser Interface
// Defines the contract for Prisma Cloud file parsers (CSV and API JSON).
// ═══════════════════════════════════════════════════════════════════════════

using Ato.Copilot.Core.Models.Compliance;

namespace Ato.Copilot.Core.Interfaces.Compliance;

/// <summary>
/// Parses raw Prisma Cloud export file bytes into a structured <see cref="ParsedPrismaFile"/>.
/// Implementations exist for CSV (<c>PrismaCsvParser</c>) and API JSON (<c>PrismaApiJsonParser</c>).
/// </summary>
public interface IPrismaParser
{
    /// <summary>
    /// Parse raw file content into consolidated Prisma alerts.
    /// CSV files are grouped by Alert ID; JSON files may contain single or array of alert objects.
    /// </summary>
    /// <param name="content">Raw file bytes (UTF-8 encoded CSV or JSON).</param>
    /// <param name="fileName">Original file name for error context.</param>
    /// <returns>Parsed file with alerts, counts, and account IDs.</returns>
    /// <exception cref="PrismaParseException">Thrown when the file cannot be parsed.</exception>
    ParsedPrismaFile Parse(byte[] content, string fileName);
}

/// <summary>
/// Exception thrown when a Prisma Cloud export file cannot be parsed.
/// </summary>
public class PrismaParseException : Exception
{
    /// <summary>Initializes a new instance of <see cref="PrismaParseException"/>.</summary>
    /// <param name="message">Parse error message.</param>
    public PrismaParseException(string message) : base(message) { }

    /// <summary>Initializes a new instance with an inner exception.</summary>
    /// <param name="message">Parse error message.</param>
    /// <param name="innerException">Inner exception.</param>
    public PrismaParseException(string message, Exception innerException) : base(message, innerException) { }
}
