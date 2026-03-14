using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Ato.Copilot.Core.Interfaces.Compliance;
using Ato.Copilot.Core.Models.Compliance;

namespace Ato.Copilot.Agents.KnowledgeBase.Services;

/// <summary>
/// JSON-backed impact level service.
/// Loads curated DoD Impact Level and FedRAMP baseline data from disk,
/// caches with 24-hour TTL, and provides lookup and normalization.
/// </summary>
public class ImpactLevelService : IImpactLevelService
{
    private readonly ILogger<ImpactLevelService> _logger;
    private readonly Lazy<Task<List<ImpactLevel>>> _lazyData;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public ImpactLevelService(
        IMemoryCache cache,
        ILogger<ImpactLevelService> logger)
    {
        _logger = logger;
        _lazyData = new Lazy<Task<List<ImpactLevel>>>(
            LoadDataCoreAsync, LazyThreadSafetyMode.ExecutionAndPublication);
    }

    /// <inheritdoc />
    public async Task<ImpactLevel?> GetImpactLevelAsync(
        string level,
        CancellationToken cancellationToken = default)
    {
        var normalized = NormalizeLevel(level);
        var data = await _lazyData.Value;
        return data.FirstOrDefault(il =>
            il.Level.Equals(normalized, StringComparison.OrdinalIgnoreCase));
    }

    /// <inheritdoc />
    public async Task<List<ImpactLevel>> GetAllImpactLevelsAsync(
        CancellationToken cancellationToken = default)
    {
        var data = await _lazyData.Value;
        return data.Where(il => il.Level.StartsWith("IL", StringComparison.OrdinalIgnoreCase)).ToList();
    }

    /// <inheritdoc />
    public async Task<ImpactLevel?> GetFedRampBaselineAsync(
        string baseline,
        CancellationToken cancellationToken = default)
    {
        var normalized = NormalizeBaseline(baseline);
        var data = await _lazyData.Value;
        return data.FirstOrDefault(il =>
            il.Level.Equals(normalized, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Normalizes impact level input to canonical form.
    /// Handles: "IL5", "IL-5", "5", "il5", "IMPACT LEVEL 5" -> "IL5"
    /// </summary>
    internal static string NormalizeLevel(string level)
    {
        if (string.IsNullOrWhiteSpace(level)) return string.Empty;

        var trimmed = level.Trim().ToUpperInvariant();

        // Already canonical
        if (trimmed.StartsWith("IL") && trimmed.Length <= 4 && !trimmed.Contains('-'))
            return trimmed;

        // Handle "FedRAMP-*" passthrough
        if (trimmed.StartsWith("FEDRAMP", StringComparison.OrdinalIgnoreCase))
            return NormalizeBaseline(level);

        // Handle "IL-5" -> "IL5"
        if (trimmed.StartsWith("IL-"))
            return "IL" + trimmed[3..];

        // Handle bare number "5" -> "IL5"
        if (trimmed.Length <= 2 && int.TryParse(trimmed, out _))
            return "IL" + trimmed;

        // Handle "IMPACT LEVEL 5" -> "IL5"
        if (trimmed.StartsWith("IMPACT LEVEL "))
            return "IL" + trimmed[13..].Trim();

        return trimmed;
    }

    /// <summary>
    /// Normalizes FedRAMP baseline input to canonical form.
    /// Handles: "High", "FEDRAMP-HIGH", "fedramp high", "FedRAMP-High" -> "FedRAMP-High"
    /// </summary>
    internal static string NormalizeBaseline(string baseline)
    {
        if (string.IsNullOrWhiteSpace(baseline)) return string.Empty;

        var trimmed = baseline.Trim();
        var upper = trimmed.ToUpperInvariant();

        // Extract the baseline level
        string level;
        if (upper.StartsWith("FEDRAMP-"))
            level = trimmed[8..];
        else if (upper.StartsWith("FEDRAMP "))
            level = trimmed[8..];
        else
            level = trimmed;

        // Capitalize first letter
        var normalized = char.ToUpperInvariant(level[0]) + level[1..].ToLowerInvariant();
        return $"FedRAMP-{normalized}";
    }

    private async Task<List<ImpactLevel>> LoadDataCoreAsync()
    {
        try
        {
            var assembly = typeof(ImpactLevelService).Assembly;
            var resourceName = assembly.GetManifestResourceNames()
                .FirstOrDefault(n => n.EndsWith("impact-levels.json", StringComparison.OrdinalIgnoreCase));

            string json;
            if (resourceName != null)
            {
                await using var stream = assembly.GetManifestResourceStream(resourceName)!;
                using var reader = new StreamReader(stream);
                json = await reader.ReadToEndAsync();
            }
            else
            {
                var basePath = AppContext.BaseDirectory;
                var filePath = Path.Combine(basePath, "KnowledgeBase", "Data", "impact-levels.json");
                if (!File.Exists(filePath))
                {
                    _logger.LogWarning("Impact levels data file not found at {Path}", filePath);
                    return new List<ImpactLevel>();
                }
                json = await File.ReadAllTextAsync(filePath);
            }

            var doc = JsonSerializer.Deserialize<ImpactLevelDataFile>(json, JsonOptions);
            if (doc == null) return new List<ImpactLevel>();

            var allLevels = new List<ImpactLevel>();
            allLevels.AddRange(doc.ImpactLevels);
            allLevels.AddRange(doc.FedRampBaselines);

            _logger.LogInformation("Loaded {Count} impact levels from data file", allLevels.Count);
            return allLevels;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load impact levels data file");
            return new List<ImpactLevel>();
        }
    }

    /// <summary>Internal DTO for deserializing the impact levels JSON wrapper.</summary>
    private sealed class ImpactLevelDataFile
    {
        public string Version { get; set; } = string.Empty;
        public List<ImpactLevel> ImpactLevels { get; set; } = new();
        public List<ImpactLevel> FedRampBaselines { get; set; } = new();
    }
}
