// ═══════════════════════════════════════════════════════════════════════════
// Feature 026 — ACAS/Nessus Scan Import: Control Mapper (T019)
// Maps Nessus plugin findings to NIST 800-53 controls via:
//   Priority 1: STIG-ID xref → IStigKnowledgeService CCI → NIST chain
//   Priority 2: PluginFamilyMappings heuristic fallback
// ═══════════════════════════════════════════════════════════════════════════

using Ato.Copilot.Core.Interfaces.Compliance;
using Ato.Copilot.Core.Models.Compliance;
using Microsoft.Extensions.Logging;

namespace Ato.Copilot.Agents.Compliance.Services.ScanImport;

/// <summary>
/// Maps Nessus plugin results to NIST 800-53 controls.
/// </summary>
public interface INessusControlMapper
{
    /// <summary>
    /// Resolve NIST 800-53 controls for a given plugin result.
    /// Tries STIG-ID xref chain first (Definitive), then plugin-family heuristic (Heuristic).
    /// Returns empty result if neither strategy resolves controls.
    /// </summary>
    Task<NessusControlMappingResult> MapAsync(
        NessusPluginResult plugin,
        CancellationToken ct = default);
}

/// <summary>
/// Implementation of <see cref="INessusControlMapper"/>.
/// </summary>
public class NessusControlMapper : INessusControlMapper
{
    private const string StigIdXrefPrefix = "STIG-ID:";

    private readonly IStigKnowledgeService _stigService;
    private readonly PluginFamilyMappings _familyMappings;
    private readonly ILogger<NessusControlMapper> _logger;

    public NessusControlMapper(
        IStigKnowledgeService stigService,
        PluginFamilyMappings familyMappings,
        ILogger<NessusControlMapper> logger)
    {
        _stigService = stigService;
        _familyMappings = familyMappings;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<NessusControlMappingResult> MapAsync(
        NessusPluginResult plugin,
        CancellationToken ct = default)
    {
        // ── Priority 1: STIG-ID xref → CCI → NIST (Definitive) ──────────
        var stigIds = ExtractStigIds(plugin.Xrefs);

        foreach (var stigVersion in stigIds)
        {
            var control = await ResolveStigControlAsync(stigVersion, ct);
            if (control is not null && control.NistControls.Count > 0)
            {
                _logger.LogDebug(
                    "Plugin {PluginId} mapped via STIG-ID xref {StigVersion} → NIST [{Controls}]",
                    plugin.PluginId, stigVersion, string.Join(", ", control.NistControls));

                return new NessusControlMappingResult(
                    NistControlIds: control.NistControls,
                    CciRefs: control.CciRefs ?? new List<string>(),
                    MappingSource: NessusControlMappingSource.StigXref);
            }
        }

        // ── Priority 2: Plugin family heuristic ─────────────────────────
        var familyMapping = _familyMappings.GetMapping(plugin.PluginFamily);

        var nistIds = new List<string> { familyMapping.PrimaryControl };
        if (familyMapping.SecondaryControls.Length > 0)
            nistIds.AddRange(familyMapping.SecondaryControls);

        // Check if this was a known family or the default fallback
        var isKnownFamily = !string.Equals(familyMapping.PluginFamily, "Unknown", StringComparison.OrdinalIgnoreCase);

        if (isKnownFamily)
        {
            _logger.LogDebug(
                "Plugin {PluginId} mapped via plugin family heuristic '{Family}' → NIST [{Controls}]",
                plugin.PluginId, plugin.PluginFamily, string.Join(", ", nistIds));
        }
        else
        {
            _logger.LogDebug(
                "Plugin {PluginId} has unrecognized family '{Family}' — defaulting to RA-5",
                plugin.PluginId, plugin.PluginFamily);
        }

        return new NessusControlMappingResult(
            NistControlIds: nistIds,
            CciRefs: new List<string>(),
            MappingSource: NessusControlMappingSource.PluginFamilyHeuristic);
    }

    /// <summary>
    /// Extract STIG-ID values from the plugin's xref list.
    /// Xrefs follow the format "STIG-ID:WN19-00-000010".
    /// </summary>
    internal static List<string> ExtractStigIds(List<string> xrefs)
    {
        var stigIds = new List<string>();
        foreach (var xref in xrefs)
        {
            if (xref.StartsWith(StigIdXrefPrefix, StringComparison.OrdinalIgnoreCase))
            {
                var stigId = xref.Substring(StigIdXrefPrefix.Length).Trim();
                if (!string.IsNullOrEmpty(stigId))
                    stigIds.Add(stigId);
            }
        }
        return stigIds;
    }

    /// <summary>
    /// Resolve a STIG control by version string (e.g., "WN19-00-000010").
    /// Tries multiple lookup strategies: StigId, StigVersion search.
    /// </summary>
    private async Task<StigControl?> ResolveStigControlAsync(string stigVersion, CancellationToken ct)
    {
        // Try direct StigId lookup (works if the version matches a StigId)
        var control = await _stigService.GetStigControlAsync(stigVersion, ct);
        if (control is not null)
            return control;

        // Try search by STIG version string (matches against StigId, Title, Description, etc.)
        var searchResults = await _stigService.SearchStigsAsync(stigVersion, maxResults: 1, cancellationToken: ct);
        if (searchResults.Count > 0)
        {
            // Only accept if the StigVersion field matches exactly
            var match = searchResults.FirstOrDefault(s =>
                string.Equals(s.StigVersion, stigVersion, StringComparison.OrdinalIgnoreCase));
            if (match is not null)
                return match;
        }

        _logger.LogDebug("STIG-ID xref '{StigVersion}' not found in knowledge base", stigVersion);
        return null;
    }
}
