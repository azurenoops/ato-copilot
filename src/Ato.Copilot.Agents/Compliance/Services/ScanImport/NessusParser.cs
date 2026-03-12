// ═══════════════════════════════════════════════════════════════════════════
// Feature 026 — ACAS/Nessus Scan Import: .nessus XML Parser
// Parses NessusClientData_v2 XML files into typed ParsedNessusFile DTOs.
// See specs/026-acas-nessus-import/research.md §1 for format documentation.
// ═══════════════════════════════════════════════════════════════════════════

using System.Globalization;
using System.Xml;
using System.Xml.Linq;
using Ato.Copilot.Core.Models.Compliance;
using Microsoft.Extensions.Logging;

namespace Ato.Copilot.Agents.Compliance.Services.ScanImport;

/// <summary>
/// Interface for parsing ACAS/Nessus .nessus XML files.
/// </summary>
public interface INessusParser
{
    /// <summary>
    /// Parses a .nessus file from raw bytes into a <see cref="ParsedNessusFile"/> DTO.
    /// </summary>
    /// <param name="fileContent">Raw .nessus file bytes (UTF-8 XML).</param>
    /// <returns>Parsed Nessus data with hosts and plugin results.</returns>
    /// <exception cref="NessusParseException">Thrown when XML is malformed or missing required elements.</exception>
    ParsedNessusFile Parse(byte[] fileContent);
}

/// <summary>
/// Exception thrown when .nessus file parsing fails.
/// </summary>
public class NessusParseException : Exception
{
    public NessusParseException(string message)
        : base(message) { }

    public NessusParseException(string message, Exception innerException)
        : base(message, innerException) { }
}

/// <summary>
/// Parses ACAS/Nessus .nessus XML files (NessusClientData_v2 schema) into typed DTOs.
/// Uses <see cref="XDocument"/> for XML parsing, matching the CKL/XCCDF parser pattern.
/// </summary>
public class NessusParser : INessusParser
{
    private readonly ILogger<NessusParser> _logger;

    public NessusParser(ILogger<NessusParser> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public ParsedNessusFile Parse(byte[] fileContent)
    {
        XDocument doc;
        try
        {
            using var stream = new MemoryStream(fileContent);
            doc = XDocument.Load(stream);
        }
        catch (XmlException ex)
        {
            _logger.LogWarning(ex, "Malformed XML in .nessus file");
            throw new NessusParseException(
                $"Invalid .nessus file: malformed XML at line {ex.LineNumber}, position {ex.LinePosition}: {ex.Message}", ex);
        }

        var root = doc.Element("NessusClientData_v2");
        if (root is null)
            throw new NessusParseException("Invalid .nessus file: missing <NessusClientData_v2> root element.");

        var report = root.Element("Report");
        if (report is null)
            throw new NessusParseException("Invalid .nessus file: missing <Report> element.");

        var reportName = report.Attribute("name")?.Value ?? "Unknown";
        var reportHosts = report.Elements("ReportHost").ToList();

        var hosts = new List<NessusReportHost>(reportHosts.Count);
        var totalPluginResults = 0;
        var informationalCount = 0;

        foreach (var hostEl in reportHosts)
        {
            var host = ParseReportHost(hostEl);
            totalPluginResults += host.PluginResults.Count;
            informationalCount += host.PluginResults.Count(p => p.Severity == 0);

            // Remove informational plugins from the host's results list (not persisted)
            var nonInfoResults = host.PluginResults.Where(p => p.Severity > 0).ToList();
            hosts.Add(host with { PluginResults = nonInfoResults });
        }

        // Include informational count in total before filtering
        totalPluginResults += 0; // informational already counted above

        _logger.LogDebug("Parsed .nessus file: {HostCount} hosts, {TotalPlugins} total plugins, {InfoCount} informational",
            hosts.Count, totalPluginResults, informationalCount);

        return new ParsedNessusFile(reportName, hosts, totalPluginResults, informationalCount);
    }

    /// <summary>
    /// Parses a single <c>&lt;ReportHost&gt;</c> element.
    /// </summary>
    private NessusReportHost ParseReportHost(XElement hostEl)
    {
        var name = hostEl.Attribute("name")?.Value ?? "unknown";
        var props = hostEl.Element("HostProperties");

        var hostIp = GetTag(props, "host-ip");
        var hostname = GetTag(props, "hostname") ?? name;
        var os = GetTag(props, "operating-system");
        var mac = GetTag(props, "mac-address");
        var credentialed = string.Equals(GetTag(props, "Credentialed_Scan"), "true", StringComparison.OrdinalIgnoreCase);
        var scanStart = ParseNessusTimestamp(GetTag(props, "HOST_START"));
        var scanEnd = ParseNessusTimestamp(GetTag(props, "HOST_END"));

        var pluginResults = new List<NessusPluginResult>();
        foreach (var item in hostEl.Elements("ReportItem"))
        {
            pluginResults.Add(ParseReportItem(item));
        }

        return new NessusReportHost(
            name, hostIp, hostname, os, mac, credentialed, scanStart, scanEnd, pluginResults);
    }

    /// <summary>
    /// Parses a single <c>&lt;ReportItem&gt;</c> element.
    /// </summary>
    private static NessusPluginResult ParseReportItem(XElement item)
    {
        var pluginId = int.Parse(item.Attribute("pluginID")?.Value ?? "0", CultureInfo.InvariantCulture);
        var pluginName = item.Attribute("pluginName")?.Value ?? string.Empty;
        var pluginFamily = item.Attribute("pluginFamily")?.Value ?? string.Empty;
        var severity = int.Parse(item.Attribute("severity")?.Value ?? "0", CultureInfo.InvariantCulture);
        var port = int.Parse(item.Attribute("port")?.Value ?? "0", CultureInfo.InvariantCulture);
        var protocol = item.Attribute("protocol")?.Value;
        var serviceName = item.Attribute("svc_name")?.Value;

        var synopsis = item.Element("synopsis")?.Value;
        var description = item.Element("description")?.Value;
        var solution = item.Element("solution")?.Value;
        var pluginOutput = item.Element("plugin_output")?.Value;
        var riskFactor = item.Element("risk_factor")?.Value ?? "None";

        // CVEs (repeatable element)
        var cves = item.Elements("cve").Select(e => e.Value).ToList();

        // Cross-references (repeatable element)
        var xrefs = item.Elements("xref").Select(e => e.Value).ToList();

        // CVSS scores
        var cvssV2 = ParseDouble(item.Element("cvss_base_score")?.Value);
        var cvssV3 = ParseDouble(item.Element("cvss3_base_score")?.Value);
        var cvssV3Vector = item.Element("cvss3_vector")?.Value;

        // VPR score
        var vpr = ParseDouble(item.Element("vpr_score")?.Value);

        // Exploit availability
        var exploitAvailable = string.Equals(
            item.Element("exploit_available")?.Value, "true", StringComparison.OrdinalIgnoreCase);

        // STIG severity
        var stigSeverity = item.Element("stig_severity")?.Value;

        return new NessusPluginResult(
            pluginId, pluginName, pluginFamily, severity, riskFactor,
            port, protocol, serviceName,
            synopsis, description, solution, pluginOutput,
            cves, xrefs,
            cvssV2, cvssV3, cvssV3Vector, vpr,
            exploitAvailable, stigSeverity);
    }

    /// <summary>
    /// Gets a HostProperties tag value by name.
    /// </summary>
    private static string? GetTag(XElement? props, string tagName)
    {
        return props?.Elements("tag")
            .FirstOrDefault(t => t.Attribute("name")?.Value == tagName)
            ?.Value;
    }

    /// <summary>
    /// Parses Nessus timestamp format (e.g., "Wed Mar 12 08:00:00 2025") to UTC DateTime.
    /// </summary>
    private static DateTime? ParseNessusTimestamp(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        // Nessus uses: "ddd MMM dd HH:mm:ss yyyy" or "ddd MMM  d HH:mm:ss yyyy"
        string[] formats =
        [
            "ddd MMM dd HH:mm:ss yyyy",
            "ddd MMM  d HH:mm:ss yyyy"
        ];

        if (DateTime.TryParseExact(value, formats, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var result))
        {
            return result;
        }

        return null;
    }

    /// <summary>
    /// Parses a string to double, returning null on failure.
    /// </summary>
    private static double? ParseDouble(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        return double.TryParse(value, CultureInfo.InvariantCulture, out var result) ? result : null;
    }
}
