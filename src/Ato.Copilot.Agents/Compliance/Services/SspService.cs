using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Ato.Copilot.Core.Data.Context;
using Ato.Copilot.Core.Interfaces.Compliance;
using Ato.Copilot.Core.Models.Compliance;

namespace Ato.Copilot.Agents.Compliance.Services;

/// <summary>
/// Implements SSP authoring: narrative CRUD, AI suggestions, inherited auto-population,
/// progress tracking, and SSP Markdown document generation.
/// </summary>
/// <remarks>Feature 015 Phase 7 (US5).</remarks>
public class SspService : ISspService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SspService> _logger;

    public SspService(
        IServiceScopeFactory scopeFactory,
        ILogger<SspService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<ControlImplementation> WriteNarrativeAsync(
        string systemId,
        string controlId,
        string narrative,
        string? status = null,
        string authoredBy = "mcp-user",
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(systemId, nameof(systemId));
        ArgumentException.ThrowIfNullOrWhiteSpace(controlId, nameof(controlId));
        ArgumentException.ThrowIfNullOrWhiteSpace(narrative, nameof(narrative));

        var implStatus = ParseImplementationStatus(status);

        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AtoCopilotContext>();

        // Verify system exists
        var system = await context.RegisteredSystems
            .FirstOrDefaultAsync(s => s.Id == systemId, cancellationToken)
            ?? throw new InvalidOperationException($"System '{systemId}' not found.");

        // Verify control is in baseline (if baseline exists)
        var baseline = await context.ControlBaselines
            .FirstOrDefaultAsync(b => b.RegisteredSystemId == systemId, cancellationToken);

        if (baseline != null && !baseline.ControlIds.Contains(controlId.ToUpperInvariant()) &&
            !baseline.ControlIds.Contains(controlId))
        {
            _logger.LogWarning(
                "Control '{ControlId}' not in baseline for system '{SystemId}', proceeding anyway",
                controlId, systemId);
        }

        // Check for existing narrative (upsert)
        var existing = await context.ControlImplementations
            .FirstOrDefaultAsync(ci =>
                ci.RegisteredSystemId == systemId && ci.ControlId == controlId,
                cancellationToken);

        if (existing != null)
        {
            existing.Narrative = narrative.Trim();
            existing.ImplementationStatus = implStatus;
            existing.ModifiedAt = DateTime.UtcNow;
            existing.AiSuggested = false;
            existing.IsAutoPopulated = false;

            await context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Updated narrative for control '{ControlId}' in system '{SystemId}'",
                controlId, systemId);

            return existing;
        }

        // Create new narrative
        var implementation = new ControlImplementation
        {
            RegisteredSystemId = systemId,
            ControlId = controlId.Trim(),
            Narrative = narrative.Trim(),
            ImplementationStatus = implStatus,
            AuthoredBy = authoredBy,
            AuthoredAt = DateTime.UtcNow
        };

        context.ControlImplementations.Add(implementation);
        await context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Created narrative for control '{ControlId}' in system '{SystemId}' (status={Status})",
            controlId, systemId, implStatus);

        return implementation;
    }

    /// <inheritdoc />
    public async Task<NarrativeSuggestion> SuggestNarrativeAsync(
        string systemId,
        string controlId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(systemId, nameof(systemId));
        ArgumentException.ThrowIfNullOrWhiteSpace(controlId, nameof(controlId));

        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AtoCopilotContext>();

        var system = await context.RegisteredSystems
            .FirstOrDefaultAsync(s => s.Id == systemId, cancellationToken)
            ?? throw new InvalidOperationException($"System '{systemId}' not found.");

        // Check if there's an inheritance mapping for this control
        var baseline = await context.ControlBaselines
            .Include(b => b.Inheritances)
            .FirstOrDefaultAsync(b => b.RegisteredSystemId == systemId, cancellationToken);

        var inheritance = baseline?.Inheritances
            .FirstOrDefault(i => i.ControlId == controlId);

        // Build suggestion based on system context and control type
        var sb = new StringBuilder();
        var references = new List<string>();
        double confidence = 0.5;

        if (inheritance?.InheritanceType == InheritanceType.Inherited)
        {
            var provider = inheritance.Provider ?? "the cloud service provider (CSP)";
            sb.AppendLine($"This control is fully inherited from {provider}.");
            sb.AppendLine();
            sb.AppendLine($"The {system.Name} system inherits the implementation of {controlId} from {provider}, ");
            sb.AppendLine($"which maintains a FedRAMP High authorization. The CSP is responsible for the full ");
            sb.AppendLine($"implementation and ongoing assessment of this control within the {system.HostingEnvironment} environment.");
            confidence = 0.85;
            references.Add($"FedRAMP High Authorization — {provider}");
            references.Add($"Control Inheritance Matrix for {system.Name}");
        }
        else if (inheritance?.InheritanceType == InheritanceType.Shared)
        {
            var provider = inheritance.Provider ?? "the cloud service provider (CSP)";
            sb.AppendLine($"This is a shared control between {provider} and {system.Name}.");
            sb.AppendLine();
            sb.AppendLine($"CSP Responsibility: {provider} provides the underlying infrastructure ");
            sb.AppendLine($"and platform-level implementation of {controlId}.");
            sb.AppendLine();
            sb.AppendLine($"Customer Responsibility: The {system.Name} team is responsible for ");
            sb.Append(inheritance.CustomerResponsibility ?? $"configuring and managing the application-level aspects of {controlId}.");
            confidence = 0.75;
            references.Add($"FedRAMP Shared Responsibility — {provider}");
            references.Add($"Customer Responsibility Matrix for {system.Name}");
        }
        else
        {
            // Customer-implemented control — provide template based on control family
            var family = controlId.Split('-')[0].ToUpperInvariant();
            var narrative = GenerateCustomerNarrativeTemplate(family, controlId, system);
            sb.Append(narrative);
            confidence = 0.55;
            references.Add($"NIST SP 800-53 Rev. 5 — {controlId}");
            references.Add($"{system.Name} System Architecture");
        }

        return new NarrativeSuggestion
        {
            ControlId = controlId,
            Narrative = sb.ToString().Trim(),
            Confidence = confidence,
            References = references
        };
    }

    /// <inheritdoc />
    public async Task<BatchPopulateResult> BatchPopulateNarrativesAsync(
        string systemId,
        string? inheritanceType = null,
        string authoredBy = "mcp-user",
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(systemId, nameof(systemId));

        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AtoCopilotContext>();

        var system = await context.RegisteredSystems
            .FirstOrDefaultAsync(s => s.Id == systemId, cancellationToken)
            ?? throw new InvalidOperationException($"System '{systemId}' not found.");

        var baseline = await context.ControlBaselines
            .Include(b => b.Inheritances)
            .FirstOrDefaultAsync(b => b.RegisteredSystemId == systemId, cancellationToken)
            ?? throw new InvalidOperationException($"No baseline found for system '{systemId}'. Select a baseline first.");

        // Get existing narratives to skip
        var existingControlIds = await context.ControlImplementations
            .Where(ci => ci.RegisteredSystemId == systemId)
            .Select(ci => ci.ControlId)
            .ToListAsync(cancellationToken);

        var existingSet = new HashSet<string>(existingControlIds, StringComparer.OrdinalIgnoreCase);

        // Filter inheritance records
        var inheritances = baseline.Inheritances.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(inheritanceType))
        {
            if (!Enum.TryParse<InheritanceType>(inheritanceType, true, out var parsedType))
                throw new InvalidOperationException($"Invalid inheritance_type: '{inheritanceType}'. Use 'Inherited' or 'Shared'.");

            inheritances = inheritances.Where(i => i.InheritanceType == parsedType);
        }
        else
        {
            // Default: both Inherited and Shared
            inheritances = inheritances.Where(i =>
                i.InheritanceType == InheritanceType.Inherited ||
                i.InheritanceType == InheritanceType.Shared);
        }

        var result = new BatchPopulateResult();
        var inheritanceList = inheritances.ToList();
        var totalToProcess = inheritanceList.Count;
        var processed = 0;

        progress?.Report($"Starting batch populate for {totalToProcess} controls...");

        foreach (var inh in inheritanceList)
        {
            processed++;
            if (existingSet.Contains(inh.ControlId))
            {
                result.SkippedCount++;
                result.SkippedControlIds.Add(inh.ControlId);
                continue;
            }

            var narrative = GenerateInheritedNarrative(inh, system.Name, system.HostingEnvironment);

            var implementation = new ControlImplementation
            {
                RegisteredSystemId = systemId,
                ControlId = inh.ControlId,
                Narrative = narrative,
                ImplementationStatus = inh.InheritanceType == InheritanceType.Inherited
                    ? ImplementationStatus.Implemented
                    : ImplementationStatus.PartiallyImplemented,
                IsAutoPopulated = true,
                AuthoredBy = authoredBy,
                AuthoredAt = DateTime.UtcNow
            };

            context.ControlImplementations.Add(implementation);
            existingSet.Add(inh.ControlId);
            result.PopulatedCount++;
            result.PopulatedControlIds.Add(inh.ControlId);

            // Report progress every 10 controls or on the last one
            if (processed % 10 == 0 || processed == totalToProcess)
            {
                progress?.Report($"Populated {result.PopulatedCount}/{totalToProcess} controls ({processed * 100 / totalToProcess}%)");
            }
        }

        if (result.PopulatedCount > 0)
        {
            await context.SaveChangesAsync(cancellationToken);
        }

        _logger.LogInformation(
            "Batch populated {Count} narratives for system '{SystemId}' (skipped {Skipped})",
            result.PopulatedCount, systemId, result.SkippedCount);

        return result;
    }

    /// <inheritdoc />
    public async Task<NarrativeProgress> GetNarrativeProgressAsync(
        string systemId,
        string? familyFilter = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(systemId, nameof(systemId));

        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AtoCopilotContext>();

        var system = await context.RegisteredSystems
            .FirstOrDefaultAsync(s => s.Id == systemId, cancellationToken)
            ?? throw new InvalidOperationException($"System '{systemId}' not found.");

        var baseline = await context.ControlBaselines
            .FirstOrDefaultAsync(b => b.RegisteredSystemId == systemId, cancellationToken)
            ?? throw new InvalidOperationException($"No baseline found for system '{systemId}'.");

        var controlIds = baseline.ControlIds;
        if (!string.IsNullOrWhiteSpace(familyFilter))
        {
            controlIds = controlIds
                .Where(c => c.StartsWith(familyFilter, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        // Get all narratives for this system
        var narratives = await context.ControlImplementations
            .Where(ci => ci.RegisteredSystemId == systemId)
            .ToListAsync(cancellationToken);

        var narrativeMap = narratives
            .ToDictionary(n => n.ControlId, n => n, StringComparer.OrdinalIgnoreCase);

        var progress = new NarrativeProgress { SystemId = systemId };
        var familyGroups = controlIds
            .GroupBy(c => c.Split('-')[0].ToUpperInvariant())
            .OrderBy(g => g.Key);

        foreach (var familyGroup in familyGroups)
        {
            var fp = new FamilyProgress { Family = familyGroup.Key };

            foreach (var controlId in familyGroup)
            {
                fp.Total++;
                if (narrativeMap.TryGetValue(controlId, out var n) && !string.IsNullOrWhiteSpace(n.Narrative))
                {
                    if (n.ImplementationStatus == ImplementationStatus.Implemented ||
                        n.ImplementationStatus == ImplementationStatus.NotApplicable)
                    {
                        fp.Completed++;
                    }
                    else
                    {
                        fp.Draft++;
                    }
                }
                else
                {
                    fp.Missing++;
                }
            }

            progress.FamilyBreakdowns.Add(fp);
            progress.TotalControls += fp.Total;
            progress.CompletedNarratives += fp.Completed;
            progress.DraftNarratives += fp.Draft;
            progress.MissingNarratives += fp.Missing;
        }

        progress.OverallPercentage = progress.TotalControls > 0
            ? Math.Round((double)(progress.CompletedNarratives + progress.DraftNarratives) / progress.TotalControls * 100, 2)
            : 0;

        return progress;
    }

    /// <inheritdoc />
    public async Task<SspDocument> GenerateSspAsync(
        string systemId,
        string format = "markdown",
        IEnumerable<string>? sections = null,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(systemId, nameof(systemId));

        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AtoCopilotContext>();

        var system = await context.RegisteredSystems
            .FirstOrDefaultAsync(s => s.Id == systemId, cancellationToken)
            ?? throw new InvalidOperationException($"System '{systemId}' not found.");

        var categorization = await context.SecurityCategorizations
            .Include(sc => sc.InformationTypes)
            .FirstOrDefaultAsync(sc => sc.RegisteredSystemId == systemId, cancellationToken);

        var baseline = await context.ControlBaselines
            .Include(b => b.Inheritances)
            .FirstOrDefaultAsync(b => b.RegisteredSystemId == systemId, cancellationToken);

        var narratives = await context.ControlImplementations
            .Where(ci => ci.RegisteredSystemId == systemId)
            .OrderBy(ci => ci.ControlId)
            .ToListAsync(cancellationToken);

        var roles = await context.RmfRoleAssignments
            .Where(r => r.RegisteredSystemId == systemId && r.IsActive)
            .ToListAsync(cancellationToken);

        var interconnections = await context.SystemInterconnections
            .Include(ic => ic.Agreements)
            .Where(ic => ic.RegisteredSystemId == systemId && ic.Status != InterconnectionStatus.Terminated)
            .ToListAsync(cancellationToken);

        var sectionList = sections?.ToList();
        var includeAll = sectionList == null || sectionList.Count == 0;

        var doc = new SspDocument
        {
            SystemId = systemId,
            SystemName = system.Name,
            Format = format
        };

        var sb = new StringBuilder();
        var includedSections = new List<string>();

        progress?.Report("Loading system data for SSP generation...");

        // ─── Section 1: System Information ───────────────────────────────
        if (includeAll || sectionList!.Any(s => s.Equals("system_information", StringComparison.OrdinalIgnoreCase)))
        {
            includedSections.Add("system_information");
            sb.AppendLine("# System Security Plan (SSP)");
            sb.AppendLine();
            sb.AppendLine("## 1. System Information");
            sb.AppendLine();
            sb.AppendLine($"**System Name**: {system.Name}");
            if (!string.IsNullOrWhiteSpace(system.Acronym))
                sb.AppendLine($"**Acronym**: {system.Acronym}");
            sb.AppendLine($"**System Type**: {system.SystemType}");
            sb.AppendLine($"**Mission Criticality**: {system.MissionCriticality}");
            sb.AppendLine($"**Hosting Environment**: {system.HostingEnvironment}");
            sb.AppendLine($"**Current RMF Step**: {system.CurrentRmfStep}");
            if (!string.IsNullOrWhiteSpace(system.Description))
            {
                sb.AppendLine();
                sb.AppendLine($"**Description**: {system.Description}");
            }
            sb.AppendLine();

            if (roles.Count > 0)
            {
                sb.AppendLine("### Key Personnel");
                sb.AppendLine();
                sb.AppendLine("| Role | Name | User ID |");
                sb.AppendLine("|------|------|---------|");
                foreach (var role in roles.OrderBy(r => r.RmfRole))
                {
                    sb.AppendLine($"| {role.RmfRole} | {role.UserDisplayName ?? "—"} | {role.UserId} |");
                }
                sb.AppendLine();
            }
        }

        // ─── Section 2: Security Categorization ─────────────────────────
        progress?.Report("Generating security categorization section...");
        if (includeAll || sectionList!.Any(s => s.Equals("categorization", StringComparison.OrdinalIgnoreCase)))
        {
            includedSections.Add("categorization");
            sb.AppendLine("## 2. Security Categorization");
            sb.AppendLine();

            if (categorization != null)
            {
                sb.AppendLine($"**FIPS 199 Notation**: {categorization.FormalNotation}");
                sb.AppendLine($"**Overall Categorization**: {categorization.OverallCategorization}");
                sb.AppendLine($"**DoD Impact Level**: {categorization.DoDImpactLevel}");
                sb.AppendLine($"**Recommended Baseline**: {categorization.NistBaseline}");
                sb.AppendLine();

                if (categorization.InformationTypes.Count > 0)
                {
                    sb.AppendLine("### Information Types");
                    sb.AppendLine();
                    sb.AppendLine("| SP 800-60 ID | Name | C | I | A |");
                    sb.AppendLine("|-------------|------|---|---|---|");
                    foreach (var it in categorization.InformationTypes.OrderBy(i => i.Sp80060Id))
                    {
                        sb.AppendLine($"| {it.Sp80060Id} | {it.Name} | {it.ConfidentialityImpact} | {it.IntegrityImpact} | {it.AvailabilityImpact} |");
                    }
                    sb.AppendLine();
                }
            }
            else
            {
                sb.AppendLine("*Security categorization has not been performed.*");
                sb.AppendLine();
                doc.Warnings.Add("Security categorization is missing.");
            }
        }

        // ─── Section 3: Control Baseline ────────────────────────────────
        progress?.Report("Generating control baseline section...");
        if (includeAll || sectionList!.Any(s => s.Equals("baseline", StringComparison.OrdinalIgnoreCase)))
        {
            includedSections.Add("baseline");
            sb.AppendLine("## 3. Control Baseline");
            sb.AppendLine();

            if (baseline != null)
            {
                sb.AppendLine($"**Baseline Level**: {baseline.BaselineLevel}");
                if (!string.IsNullOrWhiteSpace(baseline.OverlayApplied))
                    sb.AppendLine($"**Overlay Applied**: {baseline.OverlayApplied}");
                sb.AppendLine($"**Total Controls**: {baseline.TotalControls}");
                sb.AppendLine($"**Customer Controls**: {baseline.CustomerControls}");
                sb.AppendLine($"**Inherited Controls**: {baseline.InheritedControls}");
                sb.AppendLine($"**Shared Controls**: {baseline.SharedControls}");
                sb.AppendLine();
            }
            else
            {
                sb.AppendLine("*Control baseline has not been selected.*");
                sb.AppendLine();
                doc.Warnings.Add("Control baseline is missing.");
            }
        }

        // ─── Section 4: Control Implementations ─────────────────────────
        progress?.Report("Generating control implementation narratives...");
        if (includeAll || sectionList!.Any(s => s.Equals("controls", StringComparison.OrdinalIgnoreCase)))
        {
            includedSections.Add("controls");
            sb.AppendLine("## 4. Control Implementations");
            sb.AppendLine();

            var controlIds = baseline?.ControlIds ?? new List<string>();
            var narrativeMap = narratives
                .ToDictionary(n => n.ControlId, n => n, StringComparer.OrdinalIgnoreCase);

            var inheritanceMap = baseline?.Inheritances
                .ToDictionary(i => i.ControlId, i => i, StringComparer.OrdinalIgnoreCase)
                ?? new Dictionary<string, ControlInheritance>(StringComparer.OrdinalIgnoreCase);

            var familyGroups = controlIds
                .GroupBy(c => c.Split('-')[0].ToUpperInvariant())
                .OrderBy(g => g.Key);

            foreach (var family in familyGroups)
            {
                progress?.Report($"Processing {family.Key} family controls...");
                sb.AppendLine($"### {family.Key} Family");
                sb.AppendLine();

                foreach (var controlId in family.OrderBy(c => c))
                {
                    sb.AppendLine($"#### {controlId}");
                    sb.AppendLine();

                    if (narrativeMap.TryGetValue(controlId, out var impl))
                    {
                        sb.AppendLine($"**Status**: {impl.ImplementationStatus}");
                        if (inheritanceMap.TryGetValue(controlId, out var inh))
                            sb.AppendLine($"**Responsibility**: {inh.InheritanceType}");
                        sb.AppendLine();
                        sb.AppendLine(impl.Narrative ?? "*No narrative provided.*");
                        doc.ControlsWithNarratives++;
                    }
                    else
                    {
                        if (inheritanceMap.TryGetValue(controlId, out var inh))
                            sb.AppendLine($"**Responsibility**: {inh.InheritanceType}");
                        sb.AppendLine();
                        sb.AppendLine("*Implementation narrative not yet documented.*");
                        doc.ControlsMissingNarratives++;
                    }
                    sb.AppendLine();
                }
            }

            doc.TotalControls = controlIds.Count;
        }

        if (doc.ControlsMissingNarratives > 0)
        {
            doc.Warnings.Add($"{doc.ControlsMissingNarratives} controls are missing implementation narratives.");
        }

        // ─── Section 10: System Interconnections (Feature 021) ──────────
        progress?.Report("Generating system interconnections section...");
        if (includeAll || sectionList!.Any(s => s.Equals("interconnections", StringComparison.OrdinalIgnoreCase)))
        {
            includedSections.Add("interconnections");
            sb.AppendLine("## 10. System Interconnections");
            sb.AppendLine();

            if (interconnections.Count > 0)
            {
                sb.AppendLine("| Target System | Connection Type | Data Flow | Classification | Agreement Status | Security Measures |");
                sb.AppendLine("|---------------|----------------|-----------|----------------|-----------------|-------------------|");
                foreach (var ic in interconnections)
                {
                    var agreementStatus = ic.Agreements
                        .Where(a => a.Status == AgreementStatus.Signed)
                        .Any() ? "Signed" : ic.Agreements.Any() ? ic.Agreements.First().Status.ToString() : "None";

                    var measures = ic.SecurityMeasures.Count > 0
                        ? string.Join(", ", ic.SecurityMeasures)
                        : "—";

                    sb.AppendLine($"| {ic.TargetSystemName} | {ic.InterconnectionType} | {ic.DataFlowDirection} | {ic.DataClassification} | {agreementStatus} | {measures} |");
                }
                sb.AppendLine();
            }
            else
            {
                sb.AppendLine("*This system has no interconnections with external systems.*");
                sb.AppendLine();
            }
        }

        doc.Content = sb.ToString();
        doc.Sections = includedSections;

        _logger.LogInformation(
            "Generated SSP for system '{SystemId}': {Controls} controls, {WithNarrative} with narratives, {Warnings} warnings",
            systemId, doc.TotalControls, doc.ControlsWithNarratives, doc.Warnings.Count);

        return doc;
    }

    // ─── Private Helpers ─────────────────────────────────────────────────────

    private static ImplementationStatus ParseImplementationStatus(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
            return ImplementationStatus.Implemented;

        return status.ToLowerInvariant() switch
        {
            "implemented" => ImplementationStatus.Implemented,
            "partiallyimplemented" or "partially_implemented" => ImplementationStatus.PartiallyImplemented,
            "planned" => ImplementationStatus.Planned,
            "notapplicable" or "not_applicable" => ImplementationStatus.NotApplicable,
            _ => throw new InvalidOperationException(
                $"Invalid status: '{status}'. Valid values: Implemented, PartiallyImplemented, Planned, NotApplicable")
        };
    }

    private static string GenerateInheritedNarrative(
        ControlInheritance inheritance,
        string systemName,
        string hostingEnvironment)
    {
        var provider = inheritance.Provider ?? "the cloud service provider (CSP)";

        if (inheritance.InheritanceType == InheritanceType.Inherited)
        {
            return $"This control is fully inherited from {provider}. " +
                   $"The {systemName} system operates within the {hostingEnvironment} environment, " +
                   $"where {provider} maintains the complete implementation of {inheritance.ControlId} " +
                   $"as part of its FedRAMP High authorization.";
        }

        var customerPart = !string.IsNullOrWhiteSpace(inheritance.CustomerResponsibility)
            ? inheritance.CustomerResponsibility
            : $"configuring application-level settings for {inheritance.ControlId}";

        return $"This control is shared between {provider} and {systemName}. " +
               $"{provider} provides the platform-level implementation within {hostingEnvironment}. " +
               $"The {systemName} team is responsible for {customerPart}.";
    }

    private static string GenerateCustomerNarrativeTemplate(
        string family,
        string controlId,
        RegisteredSystem system)
    {
        return family switch
        {
            "AC" => $"The {system.Name} system implements {controlId} through Azure Active Directory " +
                     $"(Entra ID) integration within the {system.HostingEnvironment} environment. " +
                     "Access control policies are enforced through conditional access policies, " +
                     "role-based access control (RBAC), and multi-factor authentication (MFA).",

            "AU" => $"The {system.Name} system implements {controlId} using Azure Monitor, " +
                     "Log Analytics, and Microsoft Defender for Cloud. Audit logs are retained " +
                     "per DoD retention requirements and are protected against unauthorized modification.",

            "CM" => $"The {system.Name} system implements {controlId} through Azure Policy, " +
                     "Azure Resource Manager templates, and Infrastructure as Code (IaC) practices. " +
                     "Configuration baselines are enforced and monitored continuously.",

            "IA" => $"The {system.Name} system implements {controlId} using Azure Active Directory " +
                     $"(Entra ID) with CAC/PIV authentication in the {system.HostingEnvironment} " +
                     "environment. Identity verification follows DoD identity proofing standards.",

            "SC" => $"The {system.Name} system implements {controlId} through Azure network security " +
                     "controls including Network Security Groups (NSGs), Azure Firewall, and TLS 1.2+ " +
                     "encryption for all data in transit.",

            "SI" => $"The {system.Name} system implements {controlId} using Microsoft Defender for Cloud, " +
                     "Azure Security Center, and automated vulnerability scanning. System integrity is " +
                     "monitored continuously with alerts for anomalous behavior.",

            _ => $"The {system.Name} system implements {controlId} within the {system.HostingEnvironment} " +
                  "environment. [Implementation details to be documented by the system engineering team.]"
        };
    }
}
