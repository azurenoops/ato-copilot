using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Ato.Copilot.Core.Interfaces.Compliance;

namespace Ato.Copilot.Agents.Compliance.Services.Engines.Remediation;

/// <summary>
/// Provides curated NIST 800-53 remediation steps by control family,
/// regex-based step parsing from guidance text, and skill level mapping.
/// </summary>
public partial class NistRemediationStepsService : INistRemediationStepsService
{
    private readonly ILogger<NistRemediationStepsService> _logger;

    // Action verbs used to identify remediation steps in free text
    private static readonly string[] ActionVerbs =
    {
        "Enable", "Configure", "Implement", "Review", "Update",
        "Deploy", "Monitor", "Verify", "Create", "Remove",
        "Restrict", "Set", "Add", "Apply", "Disable"
    };

    // Curated remediation steps by control family
    private static readonly Dictionary<string, List<string>> CuratedSteps = new(StringComparer.OrdinalIgnoreCase)
    {
        ["AC"] = new()
        {
            "Review and update account management policies",
            "Configure role-based access control (RBAC) assignments",
            "Enable multi-factor authentication for privileged accounts",
            "Implement least-privilege access principles",
            "Remove unnecessary account permissions",
            "Set account lockout policies and session timeouts"
        },
        ["AU"] = new()
        {
            "Enable diagnostic logging on all Azure resources",
            "Configure log retention policies (minimum 90 days)",
            "Deploy Azure Monitor alert rules for audit events",
            "Verify audit log collection and forwarding to SIEM",
            "Review and update audit event categories",
            "Implement log integrity protection mechanisms"
        },
        ["CM"] = new()
        {
            "Review and update configuration baselines",
            "Enable Azure Policy for configuration enforcement",
            "Deploy configuration change detection and alerting",
            "Implement approved software and service lists",
            "Configure automated configuration remediation",
            "Verify resource configurations against baselines"
        },
        ["CP"] = new()
        {
            "Review and update contingency planning policies",
            "Configure geo-redundant backup for critical resources",
            "Enable automated backup and recovery procedures",
            "Verify backup integrity and restoration procedures",
            "Deploy disaster recovery configurations",
            "Implement failover and alternate processing capabilities"
        },
        ["IA"] = new()
        {
            "Configure Azure AD identity verification policies",
            "Enable multi-factor authentication for all users",
            "Implement certificate-based authentication where applicable",
            "Review and update password policies and complexity requirements",
            "Deploy managed identity for service-to-service authentication",
            "Remove shared or generic account credentials"
        },
        ["IR"] = new()
        {
            "Review and update incident response procedures",
            "Configure Azure Sentinel incident detection rules",
            "Deploy automated incident escalation workflows",
            "Verify incident logging and notification mechanisms",
            "Implement incident containment and eradication procedures"
        },
        ["MA"] = new()
        {
            "Review and update maintenance policies",
            "Configure scheduled maintenance windows",
            "Implement controlled maintenance access procedures",
            "Deploy maintenance activity logging and monitoring",
            "Verify maintenance tool authorization and control"
        },
        ["MP"] = new()
        {
            "Review and update media protection policies",
            "Enable encryption for data at rest on storage resources",
            "Implement media sanitization procedures",
            "Configure transport encryption for data in transit",
            "Deploy access controls for media storage locations"
        },
        ["PE"] = new()
        {
            "Review physical access control policies for datacenters",
            "Verify Azure datacenter compliance certifications",
            "Configure access logging for physical infrastructure",
            "Implement visitor management procedures",
            "Deploy environmental protection monitoring"
        },
        ["PL"] = new()
        {
            "Review and update system security plans",
            "Configure rules of behavior documentation",
            "Implement security planning lifecycle processes",
            "Verify plan alignment with organizational policies"
        },
        ["PS"] = new()
        {
            "Review personnel security policies",
            "Configure access provisioning and de-provisioning procedures",
            "Implement personnel screening requirements",
            "Deploy separation of duties controls",
            "Verify personnel security training requirements"
        },
        ["RA"] = new()
        {
            "Review and update risk assessment procedures",
            "Configure vulnerability scanning schedules",
            "Deploy threat monitoring and intelligence feeds",
            "Implement risk scoring and prioritization frameworks",
            "Verify risk assessment documentation and reporting"
        },
        ["SA"] = new()
        {
            "Review system and service acquisition policies",
            "Configure secure development lifecycle procedures",
            "Implement supply chain risk management",
            "Deploy software assurance and testing controls",
            "Verify third-party service security requirements"
        },
        ["SC"] = new()
        {
            "Configure TLS 1.2 minimum for all communications",
            "Enable encryption for data at rest and in transit",
            "Implement network segmentation and NSG rules",
            "Deploy DDoS protection on public-facing resources",
            "Configure DNS security and DNSSEC where applicable",
            "Restrict boundary protection and information flow enforcement"
        },
        ["SI"] = new()
        {
            "Enable Azure Defender for threat detection",
            "Configure automated vulnerability remediation",
            "Deploy malware detection and prevention tools",
            "Implement system integrity monitoring",
            "Configure security alert notifications and escalation",
            "Verify patch management and update procedures"
        }
    };

    // Skill level mapping by control family
    private static readonly Dictionary<string, string> SkillLevelByFamily = new(StringComparer.OrdinalIgnoreCase)
    {
        ["AC"] = "Intermediate",
        ["AU"] = "Intermediate",
        ["CM"] = "Intermediate",
        ["CP"] = "Intermediate",
        ["IA"] = "Advanced",
        ["IR"] = "Advanced",
        ["MA"] = "Beginner",
        ["MP"] = "Intermediate",
        ["PE"] = "Beginner",
        ["PL"] = "Beginner",
        ["PS"] = "Beginner",
        ["RA"] = "Intermediate",
        ["SA"] = "Advanced",
        ["SC"] = "Advanced",
        ["SI"] = "Intermediate"
    };

    /// <summary>
    /// Initializes a new instance of the <see cref="NistRemediationStepsService"/> class.
    /// </summary>
    public NistRemediationStepsService(ILogger<NistRemediationStepsService> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public List<string> GetRemediationSteps(string controlFamily, string controlId)
    {
        var family = controlFamily.ToUpperInvariant();

        _logger.LogDebug(
            "Looking up NIST remediation steps for family {Family}, control {ControlId}",
            family, controlId);

        if (CuratedSteps.TryGetValue(family, out var steps))
        {
            _logger.LogInformation(
                "Found {Count} curated remediation steps for {Family}",
                steps.Count, family);
            return new List<string>(steps);
        }

        _logger.LogWarning("No curated steps found for control family {Family}", family);
        return new List<string>
        {
            $"Review NIST 800-53 guidance for control {controlId}",
            "Assess current resource configuration against control requirements",
            "Implement required security controls per organizational policy",
            "Validate changes and document compliance evidence"
        };
    }

    /// <inheritdoc />
    public List<string> ParseStepsFromGuidance(string guidanceText)
    {
        if (string.IsNullOrWhiteSpace(guidanceText))
        {
            _logger.LogDebug("Empty guidance text provided, returning empty steps");
            return new List<string>();
        }

        var steps = new List<string>();

        // Pattern 1: Numbered steps (e.g., "1. Do something" or "1) Do something")
        var numberedMatches = NumberedStepRegex().Matches(guidanceText);
        if (numberedMatches.Count > 0)
        {
            foreach (Match match in numberedMatches)
            {
                var step = match.Groups[1].Value.Trim();
                if (!string.IsNullOrWhiteSpace(step))
                    steps.Add(step);
            }
        }

        // Pattern 2: Bulleted lists (e.g., "- Do something" or "* Do something" or "• Do something")
        var bulletMatches = BulletedStepRegex().Matches(guidanceText);
        foreach (Match match in bulletMatches)
        {
            var step = match.Groups[1].Value.Trim();
            if (!string.IsNullOrWhiteSpace(step) && !steps.Contains(step))
                steps.Add(step);
        }

        // Pattern 3: Action verb extraction from sentences
        if (steps.Count == 0)
        {
            var verbPattern = string.Join("|", ActionVerbs);
            var actionMatches = Regex.Matches(
                guidanceText,
                $@"(?:^|(?<=\.\s))({verbPattern})\b[^.]+",
                RegexOptions.Multiline | RegexOptions.IgnoreCase);

            foreach (Match match in actionMatches)
            {
                var step = match.Value.Trim().TrimEnd('.');
                if (!string.IsNullOrWhiteSpace(step) && !steps.Contains(step))
                    steps.Add(step);
            }
        }

        _logger.LogInformation("Parsed {Count} remediation steps from guidance text", steps.Count);
        return steps;
    }

    /// <inheritdoc />
    public string GetSkillLevel(string controlFamily)
    {
        var family = controlFamily.ToUpperInvariant();

        if (SkillLevelByFamily.TryGetValue(family, out var level))
        {
            _logger.LogDebug("Skill level for {Family}: {Level}", family, level);
            return level;
        }

        _logger.LogDebug("No skill level mapping for {Family}, defaulting to Intermediate", family);
        return "Intermediate";
    }

    // ─── Compiled regex patterns ──────────────────────────────────────────────

    [GeneratedRegex(@"(?:^|\n)\s*\d+[.)]\s*(.+)", RegexOptions.Multiline)]
    private static partial Regex NumberedStepRegex();

    [GeneratedRegex(@"(?:^|\n)\s*[-*•]\s*(.+)", RegexOptions.Multiline)]
    private static partial Regex BulletedStepRegex();
}
