using Microsoft.Extensions.Logging;
using Ato.Copilot.Core.Interfaces.Compliance;

namespace Ato.Copilot.Agents.Compliance.Services;

/// <summary>
/// Validates that system-critical control IDs exist in the loaded NIST catalog.
/// Full implementation provided in Phase 6 (T018).
/// </summary>
public sealed class ComplianceValidationService
{
    private readonly INistControlsService _nistControlsService;
    private readonly ILogger<ComplianceValidationService> _logger;

    /// <summary>11 system-critical control IDs that must be present in the catalog.</summary>
    private static readonly string[] SystemControlIds =
    [
        "SC-13", "SC-28", "AC-3", "AC-6", "SC-7",
        "AC-4", "AU-2", "SI-4", "CP-9", "CP-10", "IA-5"
    ];

    /// <summary>Initializes a new instance of the <see cref="ComplianceValidationService"/> class.</summary>
    /// <param name="nistControlsService">NIST controls service for catalog access.</param>
    /// <param name="logger">Logger instance.</param>
    public ComplianceValidationService(
        INistControlsService nistControlsService,
        ILogger<ComplianceValidationService> logger)
    {
        _nistControlsService = nistControlsService;
        _logger = logger;
    }

    /// <summary>
    /// Validates that all 11 system-critical control IDs exist in the loaded catalog.
    /// Produces warnings (not errors) for any missing controls via structured logging.
    /// </summary>
    public async Task ValidateControlMappingsAsync(CancellationToken cancellationToken = default)
    {
        var missingControls = new List<string>();

        foreach (var controlId in SystemControlIds)
        {
            var exists = await _nistControlsService.ValidateControlIdAsync(controlId, cancellationToken);
            if (!exists)
            {
                missingControls.Add(controlId);
            }
        }

        if (missingControls.Count > 0)
        {
            _logger.LogWarning(
                "Catalog validation: {MissingCount}/{TotalCount} system-critical controls not found: {MissingControls}",
                missingControls.Count,
                SystemControlIds.Length,
                string.Join(", ", missingControls));
        }
        else
        {
            _logger.LogInformation(
                "Catalog validation passed: all {TotalCount} system-critical controls present",
                SystemControlIds.Length);
        }
    }

    /// <summary>
    /// Validates catalog version and group count.
    /// </summary>
    public async Task ValidateConfigurationAsync(CancellationToken cancellationToken = default)
    {
        var catalog = await _nistControlsService.GetCatalogAsync(cancellationToken);
        if (catalog is null)
        {
            _logger.LogWarning("Configuration validation: catalog unavailable");
            return;
        }

        var version = catalog.Metadata.Version;
        var groupCount = catalog.Groups.Count;

        if (groupCount != 20)
        {
            _logger.LogWarning(
                "Configuration validation: expected 20 control groups, found {GroupCount}",
                groupCount);
        }

        _logger.LogInformation(
            "Configuration validation: catalog version {Version}, {GroupCount} groups",
            version,
            groupCount);
    }
}
