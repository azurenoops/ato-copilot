using Microsoft.Extensions.Logging;
using Ato.Copilot.Core.Interfaces.Compliance;
using Ato.Copilot.Core.Models.Compliance;

namespace Ato.Copilot.Agents.Compliance.Services.KnowledgeBase;

/// <summary>
/// Enhanced STIG validation service. Uses real STIG-to-NIST control mappings
/// from the JSON data to validate controls by family. Preserves the backward-compatible
/// ValidateAsync signature for AtoComplianceEngine.
/// </summary>
public class StigValidationService : IStigValidationService
{
    private readonly IStigKnowledgeService _stigService;
    private readonly ILogger<StigValidationService> _logger;

    public StigValidationService(
        IStigKnowledgeService stigService,
        ILogger<StigValidationService> logger)
    {
        _stigService = stigService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<List<ComplianceFinding>> ValidateAsync(
        string familyCode,
        IEnumerable<NistControl> controls,
        string subscriptionId,
        CancellationToken cancellationToken = default)
    {
        var findings = new List<ComplianceFinding>();

        foreach (var control in controls)
        {
            try
            {
                // Search for STIG controls that map to this NIST control
                var stigMapping = await _stigService.GetStigMappingAsync(
                    control.Id.ToUpperInvariant(), cancellationToken);

                if (string.IsNullOrEmpty(stigMapping))
                    continue;

                // Create an informational finding indicating STIG coverage
                findings.Add(new ComplianceFinding
                {
                    ControlId = control.Id,
                    Status = FindingStatus.Open,
                    Severity = FindingSeverity.Informational,
                    Title = $"STIG Mapping: {control.Id} — {control.Title}",
                    Description = $"STIG controls mapped to this NIST control: {stigMapping}",
                    RemediationGuidance = "Review the mapped STIG controls for additional implementation guidance.",
                    ResourceType = "STIG Validation",
                    ResourceId = $"/subscriptions/{subscriptionId}",
                    DiscoveredAt = DateTime.UtcNow,
                    StigFinding = true
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Error validating STIG mapping for control {ControlId}", control.Id);
            }
        }

        _logger.LogDebug(
            "STIG validation for family {Family}: {Count} findings generated",
            familyCode, findings.Count);

        return findings;
    }
}
