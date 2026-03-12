// ═══════════════════════════════════════════════════════════════════════════
// Feature 026 — ACAS/Nessus Scan Import: Plugin Family Mapping Loader (T018)
// Loads curated plugin-family → NIST 800-53 control mappings from embedded JSON.
// ═══════════════════════════════════════════════════════════════════════════

using System.Reflection;
using System.Text.Json;
using Ato.Copilot.Core.Models.Compliance;
using Microsoft.Extensions.Logging;

namespace Ato.Copilot.Agents.Compliance.Services.ScanImport;

/// <summary>
/// Provides curated plugin-family → NIST control mappings loaded from an embedded resource.
/// Unknown families default to RA-5 (Vulnerability Monitoring and Scanning).
/// </summary>
public class PluginFamilyMappings
{
    private static readonly PluginFamilyMapping DefaultMapping = new("Unknown", "RA-5", Array.Empty<string>());

    private readonly Dictionary<string, PluginFamilyMapping> _mappings;
    private readonly ILogger<PluginFamilyMappings> _logger;

    public PluginFamilyMappings(ILogger<PluginFamilyMappings> logger)
    {
        _logger = logger;
        _mappings = LoadMappings();
    }

    /// <summary>
    /// Get the NIST control mapping for a given plugin family.
    /// Returns the curated mapping if found; otherwise <c>RA-5</c> as default.
    /// </summary>
    public PluginFamilyMapping GetMapping(string pluginFamily)
    {
        if (string.IsNullOrWhiteSpace(pluginFamily))
            return DefaultMapping;

        return _mappings.TryGetValue(pluginFamily, out var mapping)
            ? mapping
            : DefaultMapping;
    }

    /// <summary>Number of curated mapping entries loaded.</summary>
    public int Count => _mappings.Count;

    private Dictionary<string, PluginFamilyMapping> LoadMappings()
    {
        const string resourceName = "Ato.Copilot.Agents.Compliance.Resources.plugin-family-mappings.json";
        var assembly = Assembly.GetExecutingAssembly();

        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream is null)
        {
            _logger.LogError("Embedded resource {ResourceName} not found", resourceName);
            return new Dictionary<string, PluginFamilyMapping>(StringComparer.OrdinalIgnoreCase);
        }

        var entries = JsonSerializer.Deserialize<List<PluginFamilyMappingJson>>(stream,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (entries is null || entries.Count == 0)
        {
            _logger.LogWarning("Plugin family mapping resource is empty");
            return new Dictionary<string, PluginFamilyMapping>(StringComparer.OrdinalIgnoreCase);
        }

        var result = new Dictionary<string, PluginFamilyMapping>(entries.Count, StringComparer.OrdinalIgnoreCase);
        foreach (var e in entries)
        {
            result[e.PluginFamily] = new PluginFamilyMapping(
                e.PluginFamily,
                e.PrimaryControl,
                e.SecondaryControls ?? Array.Empty<string>());
        }

        _logger.LogInformation("Loaded {Count} plugin-family-to-NIST mappings", result.Count);
        return result;
    }

    /// <summary>JSON deserialization helper.</summary>
    private sealed class PluginFamilyMappingJson
    {
        public string PluginFamily { get; set; } = string.Empty;
        public string PrimaryControl { get; set; } = string.Empty;
        public string[]? SecondaryControls { get; set; }
    }
}
