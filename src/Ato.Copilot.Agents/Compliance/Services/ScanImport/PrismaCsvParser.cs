// ═══════════════════════════════════════════════════════════════════════════
// Feature 019 — Prisma Cloud Scan Import: CSV Parser
// Parses Prisma Cloud CSPM compliance CSV exports into ParsedPrismaFile.
// See specs/019-prisma-cloud-import/research.md §R1 for format details.
// ═══════════════════════════════════════════════════════════════════════════

using System.Globalization;
using System.Text;
using Ato.Copilot.Core.Interfaces.Compliance;
using Ato.Copilot.Core.Models.Compliance;
using Microsoft.Extensions.Logging;

namespace Ato.Copilot.Agents.Compliance.Services.ScanImport;

/// <summary>
/// Parses Prisma Cloud CSV exports using a quote-aware RFC 4180 state machine.
/// Groups multi-row alerts by <c>Alert ID</c>, extracts NIST 800-53 controls
/// from <c>Compliance Standard</c>/<c>Compliance Requirement</c> columns.
/// </summary>
public class PrismaCsvParser : IPrismaParser
{
    private readonly ILogger<PrismaCsvParser> _logger;

    /// <summary>Required CSV header columns.</summary>
    private static readonly string[] RequiredHeaders = { "Alert ID", "Status", "Policy Name", "Severity" };

    public PrismaCsvParser(ILogger<PrismaCsvParser> logger) => _logger = logger;

    /// <summary>
    /// Parse raw CSV bytes into a <see cref="ParsedPrismaFile"/>.
    /// </summary>
    /// <param name="content">Raw file bytes (UTF-8).</param>
    /// <param name="fileName">Original file name for error context.</param>
    /// <returns>Parsed Prisma file with grouped alerts.</returns>
    /// <exception cref="PrismaParseException">Thrown when CSV is invalid.</exception>
    public ParsedPrismaFile Parse(byte[] content, string fileName)
    {
        var text = DecodeUtf8(content);

        if (string.IsNullOrWhiteSpace(text))
            throw new PrismaParseException($"File '{fileName}' is empty.");

        var rows = ParseCsvRows(text);
        if (rows.Count == 0)
            throw new PrismaParseException($"File '{fileName}' contains no data.");

        // First row is the header
        var header = rows[0];
        var columnIndex = BuildColumnIndex(header, fileName);

        // Validate required headers
        ValidateRequiredHeaders(columnIndex, fileName);

        // Parse data rows (skip header)
        var dataRows = rows.Skip(1).Where(r => r.Count > 0 && !r.All(string.IsNullOrWhiteSpace)).ToList();
        if (dataRows.Count == 0)
            throw new PrismaParseException($"CSV contains no alert rows in file '{fileName}'.");

        // Group by Alert ID
        var alertGroups = new Dictionary<string, List<List<string>>>(StringComparer.Ordinal);
        foreach (var row in dataRows)
        {
            var alertId = GetField(row, columnIndex, "Alert ID");
            if (string.IsNullOrWhiteSpace(alertId))
                continue;

            if (!alertGroups.ContainsKey(alertId))
                alertGroups[alertId] = new List<List<string>>();
            alertGroups[alertId].Add(row);
        }

        // Build ParsedPrismaAlert for each group
        var alerts = new List<ParsedPrismaAlert>();
        var accountIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (alertId, groupRows) in alertGroups)
        {
            var firstRow = groupRows[0];

            // Extract NIST controls from ALL rows in the group
            var nistControls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var row in groupRows)
            {
                var standard = GetField(row, columnIndex, "Compliance Standard");
                var requirement = GetField(row, columnIndex, "Compliance Requirement");

                if (!string.IsNullOrWhiteSpace(standard) &&
                    standard.Contains("NIST 800-53", StringComparison.OrdinalIgnoreCase) &&
                    !string.IsNullOrWhiteSpace(requirement))
                {
                    nistControls.Add(requirement.Trim());
                }
            }

            var accountId = GetField(firstRow, columnIndex, "Account ID");
            if (!string.IsNullOrWhiteSpace(accountId))
                accountIds.Add(accountId);

            var alertTimeStr = GetField(firstRow, columnIndex, "Alert Time");
            var alertTime = ParseDateTime(alertTimeStr);

            var resolutionTimeStr = GetField(firstRow, columnIndex, "Resolution Time");
            var resolutionTime = string.IsNullOrWhiteSpace(resolutionTimeStr)
                ? (DateTime?)null
                : ParseDateTime(resolutionTimeStr);

            var resolutionReason = GetField(firstRow, columnIndex, "Resolution Reason");

            alerts.Add(new ParsedPrismaAlert(
                AlertId: alertId,
                Status: GetField(firstRow, columnIndex, "Status"),
                PolicyName: GetField(firstRow, columnIndex, "Policy Name"),
                PolicyType: GetField(firstRow, columnIndex, "Policy Type"),
                Severity: GetField(firstRow, columnIndex, "Severity"),
                CloudType: GetField(firstRow, columnIndex, "Cloud Type"),
                AccountName: GetField(firstRow, columnIndex, "Account Name"),
                AccountId: accountId ?? string.Empty,
                Region: GetField(firstRow, columnIndex, "Region"),
                ResourceName: GetField(firstRow, columnIndex, "Resource Name"),
                ResourceId: GetField(firstRow, columnIndex, "Resource ID"),
                ResourceType: GetField(firstRow, columnIndex, "Resource Type"),
                AlertTime: alertTime,
                ResolutionReason: string.IsNullOrWhiteSpace(resolutionReason) ? null : resolutionReason,
                ResolutionTime: resolutionTime,
                NistControlIds: nistControls.ToList()
            ));
        }

        _logger.LogDebug(
            "Parsed Prisma CSV '{FileName}': {TotalRows} rows → {TotalAlerts} unique alerts, {AccountCount} accounts",
            fileName, dataRows.Count, alerts.Count, accountIds.Count);

        return new ParsedPrismaFile(
            SourceType: ScanImportType.PrismaCsv,
            Alerts: alerts,
            TotalAlerts: alerts.Count,
            TotalRows: dataRows.Count,
            AccountIds: accountIds.ToList());
    }

    // ─── CSV State Machine (RFC 4180) ────────────────────────────────────────

    private enum CsvState { OutsideField, InsideQuotedField, QuoteInQuotedField }

    /// <summary>
    /// Parse CSV text into a list of rows, each row being a list of field values.
    /// Handles quoted fields with embedded commas, newlines, and escaped quotes.
    /// </summary>
    private static List<List<string>> ParseCsvRows(string text)
    {
        var rows = new List<List<string>>();
        var currentRow = new List<string>();
        var currentField = new StringBuilder();
        var state = CsvState.OutsideField;

        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];

            switch (state)
            {
                case CsvState.OutsideField:
                    if (c == '"')
                    {
                        state = CsvState.InsideQuotedField;
                    }
                    else if (c == ',')
                    {
                        currentRow.Add(currentField.ToString());
                        currentField.Clear();
                    }
                    else if (c == '\r')
                    {
                        // Handle \r\n or bare \r
                        currentRow.Add(currentField.ToString());
                        currentField.Clear();
                        rows.Add(currentRow);
                        currentRow = new List<string>();
                        if (i + 1 < text.Length && text[i + 1] == '\n')
                            i++; // consume \n
                    }
                    else if (c == '\n')
                    {
                        currentRow.Add(currentField.ToString());
                        currentField.Clear();
                        rows.Add(currentRow);
                        currentRow = new List<string>();
                    }
                    else
                    {
                        currentField.Append(c);
                    }
                    break;

                case CsvState.InsideQuotedField:
                    if (c == '"')
                    {
                        state = CsvState.QuoteInQuotedField;
                    }
                    else
                    {
                        currentField.Append(c);
                    }
                    break;

                case CsvState.QuoteInQuotedField:
                    if (c == '"')
                    {
                        // Escaped quote (double-quote)
                        currentField.Append('"');
                        state = CsvState.InsideQuotedField;
                    }
                    else if (c == ',')
                    {
                        currentRow.Add(currentField.ToString());
                        currentField.Clear();
                        state = CsvState.OutsideField;
                    }
                    else if (c == '\r')
                    {
                        currentRow.Add(currentField.ToString());
                        currentField.Clear();
                        rows.Add(currentRow);
                        currentRow = new List<string>();
                        state = CsvState.OutsideField;
                        if (i + 1 < text.Length && text[i + 1] == '\n')
                            i++;
                    }
                    else if (c == '\n')
                    {
                        currentRow.Add(currentField.ToString());
                        currentField.Clear();
                        rows.Add(currentRow);
                        currentRow = new List<string>();
                        state = CsvState.OutsideField;
                    }
                    else
                    {
                        // Character after closing quote that isn't comma/newline
                        currentField.Append(c);
                        state = CsvState.OutsideField;
                    }
                    break;
            }
        }

        // Flush last field and row
        if (currentField.Length > 0 || currentRow.Count > 0)
        {
            currentRow.Add(currentField.ToString());
            rows.Add(currentRow);
        }

        return rows;
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    /// <summary>Decode UTF-8 bytes, stripping BOM if present.</summary>
    private static string DecodeUtf8(byte[] content)
    {
        // Strip UTF-8 BOM (0xEF 0xBB 0xBF)
        if (content.Length >= 3 && content[0] == 0xEF && content[1] == 0xBB && content[2] == 0xBF)
            return Encoding.UTF8.GetString(content, 3, content.Length - 3);

        return Encoding.UTF8.GetString(content);
    }

    /// <summary>Build column name → index mapping from the header row.</summary>
    private static Dictionary<string, int> BuildColumnIndex(List<string> headerRow, string fileName)
    {
        var index = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < headerRow.Count; i++)
        {
            var name = headerRow[i].Trim();
            if (!string.IsNullOrEmpty(name) && !index.ContainsKey(name))
                index[name] = i;
        }
        return index;
    }

    /// <summary>Validate that all required header columns are present.</summary>
    private static void ValidateRequiredHeaders(Dictionary<string, int> columnIndex, string fileName)
    {
        var missing = RequiredHeaders.Where(h => !columnIndex.ContainsKey(h)).ToList();
        if (missing.Count > 0)
        {
            throw new PrismaParseException(
                $"CSV header missing required columns: {string.Join(", ", missing)} in file '{fileName}'.");
        }
    }

    /// <summary>Get a field value from a row by column name. Returns empty string if column missing.</summary>
    private static string GetField(List<string> row, Dictionary<string, int> columnIndex, string columnName)
    {
        if (!columnIndex.TryGetValue(columnName, out var idx) || idx >= row.Count)
            return string.Empty;

        return row[idx].Trim();
    }

    /// <summary>Parse an ISO 8601 datetime string to UTC DateTime.</summary>
    private static DateTime ParseDateTime(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return DateTime.MinValue;

        if (DateTime.TryParse(value, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var dt))
            return dt;

        return DateTime.MinValue;
    }
}
