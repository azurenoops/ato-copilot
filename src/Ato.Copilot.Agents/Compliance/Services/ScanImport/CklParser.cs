// ═══════════════════════════════════════════════════════════════════════════
// Feature 017 — SCAP/STIG Viewer Import: CKL XML Parser
// Parses DISA STIG Viewer .ckl files into typed ParsedCklFile DTOs.
// See specs/017-scap-stig-import/spec.md §3.1 for CKL format documentation.
// ═══════════════════════════════════════════════════════════════════════════

using System.Xml;
using System.Xml.Linq;
using Ato.Copilot.Core.Models.Compliance;
using Microsoft.Extensions.Logging;

namespace Ato.Copilot.Agents.Compliance.Services.ScanImport;

/// <summary>
/// Interface for parsing DISA STIG Viewer CKL XML files.
/// </summary>
public interface ICklParser
{
    /// <summary>
    /// Parses a CKL file from raw bytes into a <see cref="ParsedCklFile"/> DTO.
    /// </summary>
    /// <param name="fileContent">Raw CKL file bytes (UTF-8 XML).</param>
    /// <param name="fileName">Original file name for error messages.</param>
    /// <returns>Parsed CKL data.</returns>
    /// <exception cref="CklParseException">Thrown when XML is malformed or missing required elements.</exception>
    ParsedCklFile Parse(byte[] fileContent, string fileName);
}

/// <summary>
/// Exception thrown when CKL parsing fails.
/// </summary>
public class CklParseException : Exception
{
    /// <summary>Original file name that failed to parse.</summary>
    public string FileName { get; }

    public CklParseException(string fileName, string message)
        : base($"Failed to parse CKL file '{fileName}': {message}")
    {
        FileName = fileName;
    }

    public CklParseException(string fileName, string message, Exception innerException)
        : base($"Failed to parse CKL file '{fileName}': {message}", innerException)
    {
        FileName = fileName;
    }
}

/// <summary>
/// Parses DISA STIG Viewer CKL XML files into typed <see cref="ParsedCklFile"/> DTOs.
/// Uses <see cref="XDocument"/> for XML parsing with descriptive error handling.
/// </summary>
public class CklParser : ICklParser
{
    private readonly ILogger<CklParser> _logger;

    public CklParser(ILogger<CklParser> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public ParsedCklFile Parse(byte[] fileContent, string fileName)
    {
        XDocument doc;
        try
        {
            using var stream = new MemoryStream(fileContent);
            doc = XDocument.Load(stream);
        }
        catch (XmlException ex)
        {
            _logger.LogWarning(ex, "Malformed XML in CKL file {FileName}", fileName);
            throw new CklParseException(fileName, $"Malformed XML at line {ex.LineNumber}, position {ex.LinePosition}: {ex.Message}", ex);
        }

        var checklist = doc.Element("CHECKLIST");
        if (checklist is null)
            throw new CklParseException(fileName, "Missing root <CHECKLIST> element.");

        var asset = ParseAsset(checklist.Element("ASSET"));
        var iStig = checklist.Element("STIGS")?.Element("iSTIG");
        if (iStig is null)
            throw new CklParseException(fileName, "Missing <STIGS>/<iSTIG> element.");

        var stigInfo = ParseStigInfo(iStig.Element("STIG_INFO"));
        var entries = ParseVulnEntries(iStig.Elements("VULN"));

        _logger.LogDebug("Parsed CKL file {FileName}: {EntryCount} VULN entries, benchmark={BenchmarkId}",
            fileName, entries.Count, stigInfo.StigId);

        return new ParsedCklFile(asset, stigInfo, entries);
    }

    /// <summary>
    /// Parses the ASSET element into a <see cref="CklAssetInfo"/> DTO.
    /// </summary>
    private static CklAssetInfo ParseAsset(XElement? assetElement)
    {
        if (assetElement is null)
            return new CklAssetInfo(null, null, null, null, null, null);

        return new CklAssetInfo(
            HostName: GetElementValue(assetElement, "HOST_NAME"),
            HostIp: GetElementValue(assetElement, "HOST_IP"),
            HostFqdn: GetElementValue(assetElement, "HOST_FQDN"),
            HostMac: GetElementValue(assetElement, "HOST_MAC"),
            AssetType: GetElementValue(assetElement, "ASSET_TYPE"),
            TargetKey: GetElementValue(assetElement, "TARGET_KEY"));
    }

    /// <summary>
    /// Parses the STIG_INFO element's SI_DATA children into a <see cref="CklStigInfo"/> DTO.
    /// </summary>
    private static CklStigInfo ParseStigInfo(XElement? stigInfoElement)
    {
        if (stigInfoElement is null)
            return new CklStigInfo(null, null, null, null);

        var siData = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var si in stigInfoElement.Elements("SI_DATA"))
        {
            var name = si.Element("SID_NAME")?.Value;
            var data = si.Element("SID_DATA")?.Value;
            if (!string.IsNullOrEmpty(name))
                siData[name] = data ?? string.Empty;
        }

        return new CklStigInfo(
            StigId: siData.GetValueOrDefault("stigid"),
            Version: siData.GetValueOrDefault("version"),
            ReleaseInfo: siData.GetValueOrDefault("releaseinfo"),
            Title: siData.GetValueOrDefault("title"));
    }

    /// <summary>
    /// Parses all VULN elements into a list of <see cref="ParsedCklEntry"/> DTOs.
    /// </summary>
    private static List<ParsedCklEntry> ParseVulnEntries(IEnumerable<XElement> vulnElements)
    {
        var entries = new List<ParsedCklEntry>();

        foreach (var vuln in vulnElements)
        {
            // Parse STIG_DATA attributes into a dictionary; CCI_REF can appear multiple times
            var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var cciRefs = new List<string>();

            foreach (var stigData in vuln.Elements("STIG_DATA"))
            {
                var attrName = stigData.Element("VULN_ATTRIBUTE")?.Value;
                var attrData = stigData.Element("ATTRIBUTE_DATA")?.Value ?? string.Empty;

                if (string.IsNullOrEmpty(attrName))
                    continue;

                if (string.Equals(attrName, "CCI_REF", StringComparison.OrdinalIgnoreCase))
                {
                    if (!string.IsNullOrWhiteSpace(attrData))
                        cciRefs.Add(attrData);
                }
                else
                {
                    attributes[attrName] = attrData;
                }
            }

            var vulnId = attributes.GetValueOrDefault("Vuln_Num") ?? string.Empty;
            if (string.IsNullOrWhiteSpace(vulnId))
                continue; // Skip VULN entries without a VulnId

            entries.Add(new ParsedCklEntry(
                VulnId: vulnId,
                RuleId: attributes.GetValueOrDefault("Rule_ID"),
                StigVersion: attributes.GetValueOrDefault("Rule_Ver"),
                RuleTitle: attributes.GetValueOrDefault("Rule_Title"),
                Severity: attributes.GetValueOrDefault("Severity") ?? string.Empty,
                Status: vuln.Element("STATUS")?.Value ?? string.Empty,
                FindingDetails: NullIfEmpty(vuln.Element("FINDING_DETAILS")?.Value),
                Comments: NullIfEmpty(vuln.Element("COMMENTS")?.Value),
                SeverityOverride: NullIfEmpty(vuln.Element("SEVERITY_OVERRIDE")?.Value),
                SeverityJustification: NullIfEmpty(vuln.Element("SEVERITY_JUSTIFICATION")?.Value),
                CciRefs: cciRefs,
                GroupTitle: attributes.GetValueOrDefault("Group_Title")));
        }

        return entries;
    }

    /// <summary>
    /// Gets the text value of a child element, or null if empty/missing.
    /// </summary>
    private static string? GetElementValue(XElement parent, string elementName)
    {
        return NullIfEmpty(parent.Element(elementName)?.Value);
    }

    /// <summary>
    /// Returns null if the string is null, empty, or whitespace; otherwise returns the trimmed value.
    /// </summary>
    private static string? NullIfEmpty(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}
