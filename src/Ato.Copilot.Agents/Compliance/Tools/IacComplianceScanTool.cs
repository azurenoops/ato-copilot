using System.Text.Json;
using Microsoft.Extensions.Logging;
using Ato.Copilot.Agents.Common;

namespace Ato.Copilot.Agents.Compliance.Tools;

/// <summary>
/// MCP tool for scanning Infrastructure-as-Code files (Bicep, Terraform, ARM templates)
/// against NIST 800-53 / FedRAMP compliance controls. Returns structured compliance findings.
/// Extends <see cref="BaseTool"/> per Constitution Principle II (FR-029f, R-009).
/// </summary>
public class IacComplianceScanTool : BaseTool
{
    /// <summary>Initializes a new instance of the <see cref="IacComplianceScanTool"/> class.</summary>
    /// <param name="logger">Logger instance for diagnostic output.</param>
    public IacComplianceScanTool(ILogger<IacComplianceScanTool> logger)
        : base(logger)
    {
    }

    /// <inheritdoc />
    public override string Name => "iac_compliance_scan";

    /// <inheritdoc />
    public override string Description =>
        "Scan Infrastructure-as-Code files (Bicep, Terraform, ARM) for NIST 800-53 / FedRAMP compliance findings.";

    /// <inheritdoc />
    public override IReadOnlyDictionary<string, ToolParameter> Parameters => new Dictionary<string, ToolParameter>
    {
        ["filePath"] = new()
        {
            Name = "filePath",
            Description = "Path to the IaC file to scan (e.g., 'main.bicep', 'main.tf')",
            Type = "string",
            Required = true
        },
        ["fileContent"] = new()
        {
            Name = "fileContent",
            Description = "The content of the IaC file to scan",
            Type = "string",
            Required = true
        },
        ["fileType"] = new()
        {
            Name = "fileType",
            Description = "The type of IaC file: 'bicep', 'terraform', 'arm'",
            Type = "string",
            Required = true
        },
        ["framework"] = new()
        {
            Name = "framework",
            Description = "Compliance framework to scan against (default: 'nist-800-53-r5'). Options: 'nist-800-53-r5', 'fedramp-high', 'fedramp-moderate'",
            Type = "string",
            Required = false
        }
    };

    /// <inheritdoc />
    /// <remarks>
    /// Honors <paramref name="cancellationToken"/> for cooperative cancellation (Constitution VIII).
    /// </remarks>
    public override async Task<string> ExecuteCoreAsync(
        Dictionary<string, object?> arguments,
        CancellationToken cancellationToken = default)
    {
        var filePath = GetArg<string>(arguments, "filePath") ?? string.Empty;
        var fileContent = GetArg<string>(arguments, "fileContent") ?? string.Empty;
        var fileType = GetArg<string>(arguments, "fileType") ?? "bicep";
        var framework = GetArg<string>(arguments, "framework") ?? "nist-800-53-r5";

        Logger.LogInformation("IaC compliance scan | File: {FilePath}, Type: {FileType}, Framework: {Framework}",
            filePath, fileType, framework);

        if (string.IsNullOrWhiteSpace(fileContent))
        {
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = "No file content provided for scanning.",
                findings = Array.Empty<object>()
            });
        }

        // Determine if the file is actually an IaC file
        var supportedTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "bicep", "terraform", "arm", "tf" };

        if (!supportedTypes.Contains(fileType))
        {
            return JsonSerializer.Serialize(new
            {
                success = true,
                message = $"File type '{fileType}' is not a recognized IaC format. No compliance findings to report.",
                findings = Array.Empty<object>(),
                fileType,
                framework
            });
        }

        cancellationToken.ThrowIfCancellationRequested();

        // Perform rule-based IaC compliance scanning
        var findings = await ScanIacContentAsync(fileContent, fileType, framework, cancellationToken);

        var result = new
        {
            success = true,
            filePath,
            fileType,
            framework,
            totalFindings = findings.Count,
            findings,
            scannedAt = DateTime.UtcNow
        };

        Logger.LogInformation("IaC scan complete | File: {FilePath}, Findings: {Count}",
            filePath, findings.Count);

        return JsonSerializer.Serialize(result, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        });
    }

    /// <summary>
    /// Scans IaC content for compliance findings using rule-based pattern matching.
    /// </summary>
    private Task<List<IacFinding>> ScanIacContentAsync(
        string content, string fileType, string framework, CancellationToken cancellationToken)
    {
        var findings = new List<IacFinding>();
        var lines = content.Split('\n');

        // Common compliance rules applicable to all IaC types
        var rules = GetComplianceRules(fileType, framework);

        for (var lineNum = 0; lineNum < lines.Length; lineNum++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var line = lines[lineNum].Trim();
            foreach (var rule in rules)
            {
                if (rule.Matches(line, fileType))
                {
                    findings.Add(new IacFinding
                    {
                        FindingId = $"IAC-{rule.ControlId}-{lineNum + 1}",
                        ControlId = rule.ControlId,
                        ControlFamily = rule.ControlFamily,
                        Severity = rule.Severity,
                        Title = rule.Title,
                        Description = rule.Description,
                        LineNumber = lineNum + 1,
                        LineContent = line,
                        Remediation = rule.Remediation,
                        AutoRemediable = rule.AutoRemediable,
                        Framework = framework
                    });
                }
            }
        }

        return Task.FromResult(findings);
    }

    /// <summary>
    /// Returns the set of compliance rules for the given IaC file type and framework.
    /// </summary>
    private static List<IacComplianceRule> GetComplianceRules(string fileType, string framework)
    {
        var rules = new List<IacComplianceRule>
        {
            // SC-8: Transmission Confidentiality — HTTPS required
            new()
            {
                ControlId = "SC-8",
                ControlFamily = "System and Communications Protection",
                Severity = "High",
                Title = "Unencrypted HTTP protocol detected",
                Description = "Resources should use HTTPS to ensure transmission confidentiality.",
                Remediation = "Change 'http://' to 'https://' or enable TLS/SSL.",
                AutoRemediable = true,
                MatchesFunc = (line, _) => line.Contains("http://", StringComparison.OrdinalIgnoreCase)
                    && !line.Contains("https://", StringComparison.OrdinalIgnoreCase)
                    && !line.TrimStart().StartsWith("//") && !line.TrimStart().StartsWith("#")
            },
            // SC-28: Protection of Information at Rest — encryption required
            new()
            {
                ControlId = "SC-28",
                ControlFamily = "System and Communications Protection",
                Severity = "High",
                Title = "Storage encryption not enabled",
                Description = "Storage accounts and disks should have encryption enabled at rest.",
                Remediation = "Set encryption.enabled to true or add encryption configuration.",
                AutoRemediable = true,
                MatchesFunc = (line, _) => line.Contains("encryption", StringComparison.OrdinalIgnoreCase)
                    && (line.Contains("false", StringComparison.OrdinalIgnoreCase)
                        || line.Contains("disabled", StringComparison.OrdinalIgnoreCase))
            },
            // AC-6: Least Privilege — admin access
            new()
            {
                ControlId = "AC-6",
                ControlFamily = "Access Control",
                Severity = "Critical",
                Title = "Administrative access detected in resource configuration",
                Description = "Resources should follow the principle of least privilege.",
                Remediation = "Use specific role assignments instead of broad administrative access.",
                AutoRemediable = false,
                MatchesFunc = (line, _) => line.Contains("admin", StringComparison.OrdinalIgnoreCase)
                    && (line.Contains("true", StringComparison.OrdinalIgnoreCase)
                        || line.Contains("'Owner'", StringComparison.Ordinal)
                        || line.Contains("\"Owner\"", StringComparison.Ordinal))
            },
            // AU-6: Audit Review — logging disabled
            new()
            {
                ControlId = "AU-6",
                ControlFamily = "Audit and Accountability",
                Severity = "Medium",
                Title = "Logging or auditing appears disabled",
                Description = "Resources should have audit logging enabled for accountability.",
                Remediation = "Enable diagnostic logging and audit log retention.",
                AutoRemediable = true,
                MatchesFunc = (line, _) => (line.Contains("logging", StringComparison.OrdinalIgnoreCase)
                    || line.Contains("diagnostics", StringComparison.OrdinalIgnoreCase))
                    && (line.Contains("false", StringComparison.OrdinalIgnoreCase)
                        || line.Contains("disabled", StringComparison.OrdinalIgnoreCase))
            },
            // IA-5: Authenticator Management — hardcoded secrets
            new()
            {
                ControlId = "IA-5",
                ControlFamily = "Identification and Authentication",
                Severity = "Critical",
                Title = "Potential hardcoded secret or password detected",
                Description = "Secrets should not be hardcoded in IaC files. Use Key Vault references.",
                Remediation = "Replace hardcoded value with a Key Vault reference or parameter.",
                AutoRemediable = false,
                MatchesFunc = (line, _) => (line.Contains("password", StringComparison.OrdinalIgnoreCase)
                    || line.Contains("secret", StringComparison.OrdinalIgnoreCase)
                    || line.Contains("apiKey", StringComparison.OrdinalIgnoreCase))
                    && (line.Contains("=", StringComparison.Ordinal)
                        || line.Contains(":", StringComparison.Ordinal))
                    && !line.TrimStart().StartsWith("//") && !line.TrimStart().StartsWith("#")
                    && !line.Contains("keyVault", StringComparison.OrdinalIgnoreCase)
                    && !line.Contains("@secure", StringComparison.OrdinalIgnoreCase)
            }
        };

        return rules;
    }
}

/// <summary>
/// A compliance finding from an IaC scan.
/// </summary>
internal class IacFinding
{
    public string FindingId { get; set; } = string.Empty;
    public string ControlId { get; set; } = string.Empty;
    public string ControlFamily { get; set; } = string.Empty;
    public string Severity { get; set; } = "Medium";
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int LineNumber { get; set; }
    public string LineContent { get; set; } = string.Empty;
    public string Remediation { get; set; } = string.Empty;
    public bool AutoRemediable { get; set; }
    public string Framework { get; set; } = string.Empty;
}

/// <summary>
/// A compliance rule for pattern-based IaC scanning.
/// </summary>
internal class IacComplianceRule
{
    public string ControlId { get; set; } = string.Empty;
    public string ControlFamily { get; set; } = string.Empty;
    public string Severity { get; set; } = "Medium";
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Remediation { get; set; } = string.Empty;
    public bool AutoRemediable { get; set; }
    public Func<string, string, bool> MatchesFunc { get; set; } = (_, _) => false;

    /// <summary>Checks if a line matches this compliance rule.</summary>
    public bool Matches(string line, string fileType) => MatchesFunc(line, fileType);
}
