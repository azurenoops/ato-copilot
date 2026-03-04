// ═══════════════════════════════════════════════════════════════════════════
// Feature 017 — SCAP/STIG Import: XCCDF Parser
// Parses SCAP Compliance Checker XCCDF TestResult XML files.
// ═══════════════════════════════════════════════════════════════════════════

using System.Globalization;
using System.Xml.Linq;
using Ato.Copilot.Core.Models.Compliance;
using Microsoft.Extensions.Logging;

namespace Ato.Copilot.Agents.Compliance.Services.ScanImport;

/// <summary>
/// Parses XCCDF (Extensible Configuration Checklist Description Format) results XML.
/// Supports both XCCDF 1.1 and 1.2 namespace variations.
/// </summary>
public interface IXccdfParser
{
    /// <summary>
    /// Parse XCCDF TestResult XML bytes into a <see cref="ParsedXccdfFile"/>.
    /// </summary>
    /// <param name="content">Raw XML bytes.</param>
    /// <param name="fileName">Original file name (for error messages).</param>
    /// <returns>Parsed XCCDF file with all rule-results.</returns>
    /// <exception cref="XccdfParseException">File is not valid XCCDF XML.</exception>
    ParsedXccdfFile Parse(byte[] content, string fileName);
}

/// <summary>
/// Exception thrown when XCCDF parsing fails.
/// </summary>
public class XccdfParseException : Exception
{
    public string FileName { get; }

    public XccdfParseException(string fileName, string message)
        : base($"XCCDF parse error in '{fileName}': {message}")
    {
        FileName = fileName;
    }

    public XccdfParseException(string fileName, string message, Exception inner)
        : base($"XCCDF parse error in '{fileName}': {message}", inner)
    {
        FileName = fileName;
    }
}

/// <summary>
/// Implementation of <see cref="IXccdfParser"/>. Uses namespace-aware XDocument parsing
/// to support XCCDF 1.1 (<c>http://checklists.nist.gov/xccdf/1.1</c>) and
/// XCCDF 1.2 (<c>http://checklists.nist.gov/xccdf/1.2</c>).
/// </summary>
public class XccdfParser : IXccdfParser
{
    // XCCDF namespace URIs
    private static readonly XNamespace Ns12 = "http://checklists.nist.gov/xccdf/1.2";
    private static readonly XNamespace Ns11 = "http://checklists.nist.gov/xccdf/1.1";

    private readonly ILogger<XccdfParser> _logger;

    public XccdfParser(ILogger<XccdfParser> logger)
    {
        _logger = logger;
    }

    public ParsedXccdfFile Parse(byte[] content, string fileName)
    {
        if (content is null || content.Length == 0)
            throw new XccdfParseException(fileName, "File is empty.");

        XDocument doc;
        try
        {
            using var stream = new MemoryStream(content);
            doc = XDocument.Load(stream);
        }
        catch (Exception ex)
        {
            throw new XccdfParseException(fileName, $"Invalid XML: {ex.Message}", ex);
        }

        var root = doc.Root
            ?? throw new XccdfParseException(fileName, "XML document has no root element.");

        // Detect XCCDF namespace
        var ns = DetectNamespace(root, fileName);

        // The root might be <TestResult> directly or <Benchmark> containing <TestResult>
        var testResult = FindTestResult(root, ns, fileName);

        // Parse benchmark info
        var benchmarkHref = ParseBenchmarkHref(testResult, ns);
        var title = testResult.Element(ns + "title")?.Value;

        // Parse target info
        var target = testResult.Element(ns + "target")?.Value;
        var targetAddress = testResult.Element(ns + "target-address")?.Value;

        // Parse timestamps
        var startTime = ParseTimestamp(testResult.Attribute("start-time")?.Value);
        var endTime = ParseTimestamp(testResult.Attribute("end-time")?.Value);

        // Parse target facts
        var targetFacts = ParseTargetFacts(testResult, ns);

        // Parse score
        var (score, maxScore) = ParseScore(testResult, ns);

        // Parse rule results
        var results = ParseRuleResults(testResult, ns, fileName);

        _logger.LogInformation(
            "Parsed XCCDF file '{FileName}': {ResultCount} rule-results, target={Target}, score={Score}/{MaxScore}",
            fileName, results.Count, target ?? "(unknown)", score, maxScore);

        return new ParsedXccdfFile(
            BenchmarkHref: benchmarkHref,
            Title: title,
            Target: target,
            TargetAddress: targetAddress,
            StartTime: startTime,
            EndTime: endTime,
            Score: score,
            MaxScore: maxScore,
            TargetFacts: targetFacts,
            Results: results);
    }

    private XNamespace DetectNamespace(XElement root, string fileName)
    {
        var rootNs = root.Name.Namespace;

        if (rootNs == Ns12) return Ns12;
        if (rootNs == Ns11) return Ns11;

        // Try to infer from child elements
        if (root.Descendants(Ns12 + "TestResult").Any()) return Ns12;
        if (root.Descendants(Ns11 + "TestResult").Any()) return Ns11;

        // Fallback: no namespace (some tools emit XCCDF without namespace)
        if (root.Name.LocalName == "TestResult" || root.Name.LocalName == "Benchmark")
            return XNamespace.None;

        throw new XccdfParseException(fileName,
            $"Could not detect XCCDF namespace. Root element: <{root.Name}>. " +
            "Expected XCCDF 1.1 or 1.2 namespace.");
    }

    private XElement FindTestResult(XElement root, XNamespace ns, string fileName)
    {
        // Direct TestResult root
        if (root.Name.LocalName == "TestResult")
            return root;

        // TestResult inside Benchmark
        var testResult = root.Element(ns + "TestResult")
            ?? root.Descendants(ns + "TestResult").FirstOrDefault();

        return testResult
            ?? throw new XccdfParseException(fileName,
                "No <TestResult> element found in XCCDF XML.");
    }

    private string? ParseBenchmarkHref(XElement testResult, XNamespace ns)
    {
        var benchmark = testResult.Element(ns + "benchmark");
        return benchmark?.Attribute("href")?.Value;
    }

    private Dictionary<string, string> ParseTargetFacts(XElement testResult, XNamespace ns)
    {
        var facts = new Dictionary<string, string>();
        var factsEl = testResult.Element(ns + "target-facts");
        if (factsEl is null) return facts;

        foreach (var fact in factsEl.Elements(ns + "fact"))
        {
            var name = fact.Attribute("name")?.Value;
            var value = fact.Value;
            if (!string.IsNullOrEmpty(name))
                facts[name] = value;
        }

        return facts;
    }

    private (decimal? Score, decimal? MaxScore) ParseScore(XElement testResult, XNamespace ns)
    {
        var scoreEl = testResult.Element(ns + "score");
        if (scoreEl is null) return (null, null);

        decimal? score = decimal.TryParse(scoreEl.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var s)
            ? s : null;
        decimal? maxScore = decimal.TryParse(scoreEl.Attribute("maximum")?.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var m)
            ? m : null;

        return (score, maxScore);
    }

    private List<ParsedXccdfResult> ParseRuleResults(XElement testResult, XNamespace ns, string fileName)
    {
        var results = new List<ParsedXccdfResult>();

        foreach (var rr in testResult.Elements(ns + "rule-result"))
        {
            var idref = rr.Attribute("idref")?.Value ?? string.Empty;
            var extractedRuleId = ExtractRuleId(idref);
            var result = rr.Element(ns + "result")?.Value ?? "unknown";
            var severity = rr.Attribute("severity")?.Value ?? "unknown";

            decimal weight = 0;
            if (decimal.TryParse(rr.Attribute("weight")?.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var w))
                weight = w;

            var timestamp = ParseTimestamp(rr.Attribute("time")?.Value);

            // Message element
            var message = rr.Element(ns + "message")?.Value;

            // Check reference
            var checkEl = rr.Element(ns + "check");
            var checkRef = checkEl?.Element(ns + "check-content-ref")?.Attribute("name")?.Value;

            results.Add(new ParsedXccdfResult(
                RuleIdRef: idref,
                ExtractedRuleId: extractedRuleId,
                Result: result.ToLowerInvariant(),
                Severity: severity.ToLowerInvariant(),
                Weight: weight,
                Timestamp: timestamp,
                Message: message,
                CheckRef: checkRef));
        }

        return results;
    }

    /// <summary>
    /// Extract DISA rule ID from XCCDF idref.
    /// E.g., <c>xccdf_mil.disa.stig_rule_SV-254239r849090_rule</c> → <c>SV-254239r849090_rule</c>
    /// </summary>
    internal static string ExtractRuleId(string idref)
    {
        if (string.IsNullOrEmpty(idref)) return string.Empty;

        // Pattern: xccdf_mil.disa.stig_rule_SV-XXXXXX...
        const string prefix = "xccdf_mil.disa.stig_rule_";
        if (idref.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return idref[prefix.Length..];

        // Also handle direct SV- references
        if (idref.StartsWith("SV-", StringComparison.OrdinalIgnoreCase))
            return idref;

        // Return as-is if unrecognized format
        return idref;
    }

    private static DateTime? ParseTimestamp(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        return DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var dt)
            ? dt
            : null;
    }
}
