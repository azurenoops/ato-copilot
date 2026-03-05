// ═══════════════════════════════════════════════════════════════════════════
// Feature 019 — Prisma Cloud Scan Import: API JSON Parser
// Parses Prisma Cloud RQL alert API responses into ParsedPrismaFile.
// See specs/019-prisma-cloud-import/research.md §R2 for format details.
// ═══════════════════════════════════════════════════════════════════════════

using System.Text.Json;
using Ato.Copilot.Core.Interfaces.Compliance;
using Ato.Copilot.Core.Models.Compliance;
using Microsoft.Extensions.Logging;

namespace Ato.Copilot.Agents.Compliance.Services.ScanImport;

/// <summary>
/// Parses Prisma Cloud API JSON (RQL alert responses) into <see cref="ParsedPrismaFile"/>.
/// Extracts remediation guidance, CLI scripts, alert history, and policy labels.
/// </summary>
public class PrismaApiJsonParser : IPrismaParser
{
    private readonly ILogger<PrismaApiJsonParser> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public PrismaApiJsonParser(ILogger<PrismaApiJsonParser> logger) => _logger = logger;

    /// <summary>
    /// Parse raw JSON bytes into a <see cref="ParsedPrismaFile"/>.
    /// </summary>
    /// <param name="content">Raw file bytes (UTF-8 JSON).</param>
    /// <param name="fileName">Original file name for error context.</param>
    /// <returns>Parsed Prisma file with alerts.</returns>
    /// <exception cref="PrismaParseException">Thrown when JSON is invalid.</exception>
    public ParsedPrismaFile Parse(byte[] content, string fileName)
    {
        if (content.Length == 0)
            throw new PrismaParseException($"File '{fileName}' is empty.");

        List<JsonAlert> jsonAlerts;
        try
        {
            var text = System.Text.Encoding.UTF8.GetString(content).Trim();

            // Support both single object and array
            if (text.StartsWith('['))
            {
                jsonAlerts = JsonSerializer.Deserialize<List<JsonAlert>>(text, JsonOptions)
                    ?? throw new PrismaParseException($"Failed to deserialize JSON array in '{fileName}'.");
            }
            else if (text.StartsWith('{'))
            {
                var single = JsonSerializer.Deserialize<JsonAlert>(text, JsonOptions)
                    ?? throw new PrismaParseException($"Failed to deserialize JSON object in '{fileName}'.");
                jsonAlerts = new List<JsonAlert> { single };
            }
            else
            {
                throw new PrismaParseException($"Invalid JSON format in '{fileName}': expected array or object.");
            }
        }
        catch (JsonException ex)
        {
            throw new PrismaParseException($"JSON parse error in '{fileName}': {ex.Message}", ex);
        }

        if (jsonAlerts.Count == 0)
            throw new PrismaParseException($"JSON contains no alert objects in '{fileName}'.");

        var alerts = new List<ParsedPrismaAlert>();
        var accountIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var ja in jsonAlerts)
        {
            if (string.IsNullOrWhiteSpace(ja.Id))
                throw new PrismaParseException($"Alert missing required 'id' field in '{fileName}'.");
            if (string.IsNullOrWhiteSpace(ja.Status))
                throw new PrismaParseException($"Alert '{ja.Id}' missing required 'status' field in '{fileName}'.");
            if (ja.Policy is null || string.IsNullOrWhiteSpace(ja.Policy.Name))
                throw new PrismaParseException($"Alert '{ja.Id}' missing required 'policy.name' field in '{fileName}'.");

            var resource = ja.Resource ?? new JsonResource();
            var policy = ja.Policy;

            // Extract NIST controls
            var nistControls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (policy.ComplianceMetadata is not null)
            {
                foreach (var cm in policy.ComplianceMetadata)
                {
                    if (!string.IsNullOrWhiteSpace(cm.StandardName) &&
                        cm.StandardName.Contains("NIST 800-53", StringComparison.OrdinalIgnoreCase) &&
                        !string.IsNullOrWhiteSpace(cm.RequirementId))
                    {
                        nistControls.Add(cm.RequirementId.Trim());
                    }
                }
            }

            var accountId = resource.AccountId ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(accountId))
                accountIds.Add(accountId);

            var alertTime = FromEpochMs(ja.AlertTime);

            // Extract remediation script
            string? remediationScript = null;
            if (policy.Remediation is not null &&
                !string.IsNullOrWhiteSpace(policy.Remediation.CliScriptTemplate))
            {
                remediationScript = policy.Remediation.CliScriptTemplate;
            }

            // Extract alert history
            List<PrismaAlertHistoryEntry>? alertHistory = null;
            if (ja.History is not null && ja.History.Count > 0)
            {
                alertHistory = ja.History
                    .Select(h => new PrismaAlertHistoryEntry(
                        h.ModifiedBy ?? "System",
                        FromEpochMs(h.ModifiedOn),
                        h.Reason ?? string.Empty,
                        h.Status ?? "unknown"))
                    .ToList();
            }

            alerts.Add(new ParsedPrismaAlert(
                AlertId: ja.Id,
                Status: ja.Status,
                PolicyName: policy.Name,
                PolicyType: policy.PolicyType ?? string.Empty,
                Severity: policy.Severity ?? "medium",
                CloudType: resource.CloudType ?? string.Empty,
                AccountName: resource.AccountName ?? string.Empty,
                AccountId: accountId,
                Region: resource.Region ?? string.Empty,
                ResourceName: resource.Name ?? string.Empty,
                ResourceId: resource.Id ?? string.Empty,
                ResourceType: resource.ResourceType ?? string.Empty,
                AlertTime: alertTime,
                ResolutionReason: string.IsNullOrWhiteSpace(ja.Reason) ? null : ja.Reason,
                ResolutionTime: null,   // API JSON doesn't have explicit resolution time
                NistControlIds: nistControls.ToList(),
                Description: policy.Description,
                Recommendation: policy.Recommendation,
                RemediationScript: remediationScript,
                PolicyLabels: policy.Labels,
                Remediable: policy.Remediable,
                AlertHistory: alertHistory));
        }

        _logger.LogDebug(
            "Parsed Prisma API JSON '{FileName}': {TotalAlerts} alerts, {AccountCount} accounts",
            fileName, alerts.Count, accountIds.Count);

        return new ParsedPrismaFile(
            SourceType: ScanImportType.PrismaApi,
            Alerts: alerts,
            TotalAlerts: alerts.Count,
            TotalRows: alerts.Count,
            AccountIds: accountIds.ToList());
    }

    // ─── JSON DTOs (for deserialization only) ────────────────────────────────

    private static DateTime FromEpochMs(long? epochMs)
    {
        if (epochMs is null or 0)
            return DateTime.MinValue;
        return DateTimeOffset.FromUnixTimeMilliseconds(epochMs.Value).UtcDateTime;
    }

    private class JsonAlert
    {
        public string Id { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string? Reason { get; set; }
        public long? FirstSeen { get; set; }
        public long? LastSeen { get; set; }
        public long? AlertTime { get; set; }
        public JsonPolicy? Policy { get; set; }
        public JsonResource? Resource { get; set; }
        public List<JsonHistory>? History { get; set; }
    }

    private class JsonPolicy
    {
        public string? PolicyId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? PolicyType { get; set; }
        public string? Severity { get; set; }
        public string? Description { get; set; }
        public string? Recommendation { get; set; }
        public JsonRemediation? Remediation { get; set; }
        public List<string>? Labels { get; set; }
        public bool Remediable { get; set; }
        public List<JsonComplianceMetadata>? ComplianceMetadata { get; set; }
    }

    private class JsonRemediation
    {
        public string? CliScriptTemplate { get; set; }
        public string? Description { get; set; }
        public string? Impact { get; set; }
    }

    private class JsonResource
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
        public string? ResourceType { get; set; }
        public string? Region { get; set; }
        public string? CloudType { get; set; }
        public string? AccountId { get; set; }
        public string? AccountName { get; set; }
        public string? ResourceGroupName { get; set; }
        public string? Rrn { get; set; }
    }

    private class JsonHistory
    {
        public string? ModifiedBy { get; set; }
        public long? ModifiedOn { get; set; }
        public string? Reason { get; set; }
        public string? Status { get; set; }
    }

    private class JsonComplianceMetadata
    {
        public string? StandardName { get; set; }
        public string? StandardDescription { get; set; }
        public string? RequirementId { get; set; }
        public string? RequirementName { get; set; }
        public string? SectionId { get; set; }
        public string? SectionDescription { get; set; }
    }
}
