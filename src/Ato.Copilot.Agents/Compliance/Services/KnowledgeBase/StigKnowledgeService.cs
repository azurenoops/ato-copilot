using System.Text.Json;
using System.Reflection;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Ato.Copilot.Core.Interfaces.Compliance;
using Ato.Copilot.Core.Models.Compliance;

namespace Ato.Copilot.Agents.Compliance.Services.KnowledgeBase;

/// <summary>
/// JSON-backed STIG knowledge service.
/// Loads curated STIG data from disk, caches with 24-hour TTL, and provides
/// lookup, search, and cross-reference capabilities.
/// </summary>
public class StigKnowledgeService : IStigKnowledgeService
{
    private readonly IMemoryCache _cache;
    private readonly IDoDInstructionService _dodInstructionService;
    private readonly ILogger<StigKnowledgeService> _logger;

    private const string CacheKey = "kb:stig:all_controls";
    private const string CciCacheKey = "kb:cci:all_mappings";
    private const string RuleIdIndexCacheKey = "kb:stig:rule_id_index";
    private static readonly TimeSpan CacheTtl = TimeSpan.FromHours(24);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public StigKnowledgeService(
        IMemoryCache cache,
        IDoDInstructionService dodInstructionService,
        ILogger<StigKnowledgeService> logger)
    {
        _cache = cache;
        _dodInstructionService = dodInstructionService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<string> GetStigMappingAsync(
        string controlId,
        CancellationToken cancellationToken = default)
    {
        var controls = await LoadControlsAsync();
        var matching = controls
            .Where(c => c.NistControls.Contains(controlId, StringComparer.OrdinalIgnoreCase))
            .ToList();

        if (matching.Count == 0)
            return string.Empty;

        return string.Join(", ", matching.Select(c => $"{c.StigId} ({c.Title})"));
    }

    /// <inheritdoc />
    public async Task<StigControl?> GetStigControlAsync(string stigId, CancellationToken cancellationToken = default)
    {
        var controls = await LoadControlsAsync();
        return controls.FirstOrDefault(c =>
            string.Equals(c.StigId, stigId, StringComparison.OrdinalIgnoreCase));
    }

    /// <inheritdoc />
    public async Task<StigControl?> GetStigControlByRuleIdAsync(string ruleId, CancellationToken cancellationToken = default)
    {
        var index = await LoadRuleIdIndexAsync();
        index.TryGetValue(ruleId, out var control);
        return control;
    }

    /// <inheritdoc />
    public async Task<List<StigControl>> GetStigControlsByBenchmarkAsync(string benchmarkId, CancellationToken cancellationToken = default)
    {
        var controls = await LoadControlsAsync();
        return controls
            .Where(c => string.Equals(c.BenchmarkId, benchmarkId, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    /// <summary>
    /// Builds and caches a dictionary keyed by RuleId (case-insensitive) for fast lookups.
    /// </summary>
    private async Task<Dictionary<string, StigControl>> LoadRuleIdIndexAsync()
    {
        if (_cache.TryGetValue(RuleIdIndexCacheKey, out Dictionary<string, StigControl>? cached) && cached != null)
            return cached;

        var controls = await LoadControlsAsync();
        var index = new Dictionary<string, StigControl>(StringComparer.OrdinalIgnoreCase);
        foreach (var control in controls)
        {
            if (!string.IsNullOrEmpty(control.RuleId) && !index.ContainsKey(control.RuleId))
                index[control.RuleId] = control;
        }

        _cache.Set(RuleIdIndexCacheKey, index, CacheTtl);
        _logger.LogDebug("Built RuleId index with {Count} entries", index.Count);
        return index;
    }

    /// <inheritdoc />
    public async Task<List<StigControl>> SearchStigsAsync(
        string query,
        StigSeverity? severity = null,
        int maxResults = 10,
        CancellationToken cancellationToken = default)
    {
        var controls = await LoadControlsAsync();
        var lower = query.ToLowerInvariant();

        var results = controls.Where(c =>
            c.Title.Contains(lower, StringComparison.OrdinalIgnoreCase) ||
            c.Description.Contains(lower, StringComparison.OrdinalIgnoreCase) ||
            c.StigId.Contains(lower, StringComparison.OrdinalIgnoreCase) ||
            c.Category.Contains(lower, StringComparison.OrdinalIgnoreCase) ||
            c.StigFamily.Contains(lower, StringComparison.OrdinalIgnoreCase));

        if (severity.HasValue)
            results = results.Where(c => c.Severity == severity.Value);

        return results.Take(maxResults).ToList();
    }

    /// <inheritdoc />
    public async Task<StigCrossReference?> GetStigCrossReferenceAsync(string stigId, CancellationToken cancellationToken = default)
    {
        var control = await GetStigControlAsync(stigId, cancellationToken);
        if (control == null)
            return null;

        // Enrich with related DoD instructions
        var relatedInstructions = new List<DoDInstruction>();
        foreach (var nistId in control.NistControls)
        {
            var instructions = await _dodInstructionService.GetInstructionsByControlAsync(nistId, cancellationToken);
            if (instructions != null)
                relatedInstructions.AddRange(instructions);
        }

        // Deduplicate by InstructionId
        relatedInstructions = relatedInstructions
            .GroupBy(i => i.InstructionId)
            .Select(g => g.First())
            .ToList();

        return new StigCrossReference(
            stigId,
            control,
            control.NistControls,
            relatedInstructions);
    }

    /// <inheritdoc />
    public async Task<List<StigControl>> GetStigsByCciChainAsync(
        string controlId,
        StigSeverity? severity = null,
        CancellationToken cancellationToken = default)
    {
        var cciMappings = await LoadCciMappingsAsync();
        var controls = await LoadControlsAsync();

        // Step 1: Find all CCI IDs mapped to this NIST control
        var normalizedControlId = controlId.ToUpperInvariant();
        var matchingCciIds = cciMappings
            .Where(m => m.NistControlId.Equals(controlId, StringComparison.OrdinalIgnoreCase))
            .Select(m => m.CciId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Step 2: Find STIG controls that reference any of these CCI IDs
        var matched = controls.Where(c =>
            c.CciRefs.Any(cci => matchingCciIds.Contains(cci)) ||
            c.NistControls.Any(nc => nc.Equals(controlId, StringComparison.OrdinalIgnoreCase)));

        if (severity.HasValue)
            matched = matched.Where(c => c.Severity == severity.Value);

        // Deduplicate by StigId
        return matched
            .GroupBy(c => c.StigId)
            .Select(g => g.First())
            .OrderBy(c => c.Severity)
            .ThenBy(c => c.StigId)
            .ToList();
    }

    /// <inheritdoc />
    public async Task<List<CciMapping>> GetCciMappingsAsync(
        string controlId,
        CancellationToken cancellationToken = default)
    {
        var allMappings = await LoadCciMappingsAsync();
        return allMappings
            .Where(m => m.NistControlId.Equals(controlId, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    /// <summary>
    /// Loads STIG controls from the JSON data file, with 24-hour cache TTL.
    /// </summary>
    private async Task<List<StigControl>> LoadControlsAsync()
    {
        if (_cache.TryGetValue(CacheKey, out List<StigControl>? cached) && cached != null)
            return cached;

        try
        {
            var assembly = typeof(StigKnowledgeService).Assembly;
            // Try embedded resource first
            var resourceName = assembly.GetManifestResourceNames()
                .FirstOrDefault(n => n.EndsWith("stig-controls.json", StringComparison.OrdinalIgnoreCase));

            string json;
            if (resourceName != null)
            {
                await using var stream = assembly.GetManifestResourceStream(resourceName)!;
                using var reader = new StreamReader(stream);
                json = await reader.ReadToEndAsync();
            }
            else
            {
                // Fallback to file on disk
                var basePath = AppContext.BaseDirectory;
                var filePath = Path.Combine(basePath, "KnowledgeBase", "Data", "stig-controls.json");
                if (!File.Exists(filePath))
                {
                    _logger.LogWarning("STIG data file not found at {Path}", filePath);
                    return new List<StigControl>();
                }
                json = await File.ReadAllTextAsync(filePath);
            }

            var doc = JsonSerializer.Deserialize<StigDataFile>(json, JsonOptions);
            var controls = doc?.Controls ?? new List<StigControl>();

            _cache.Set(CacheKey, controls, CacheTtl);
            _logger.LogInformation("Loaded {Count} STIG controls from data file", controls.Count);
            return controls;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load STIG data file");
            return new List<StigControl>();
        }
    }

    /// <summary>
    /// Loads CCI-NIST mappings from the embedded JSON resource, with 24-hour cache TTL.
    /// </summary>
    private async Task<List<CciMapping>> LoadCciMappingsAsync()
    {
        if (_cache.TryGetValue(CciCacheKey, out List<CciMapping>? cached) && cached != null)
            return cached;

        try
        {
            var assembly = typeof(StigKnowledgeService).Assembly;
            var resourceName = assembly.GetManifestResourceNames()
                .FirstOrDefault(n => n.EndsWith("cci-nist-mapping.json", StringComparison.OrdinalIgnoreCase));

            if (resourceName == null)
            {
                _logger.LogWarning("CCI-NIST mapping embedded resource not found");
                return new List<CciMapping>();
            }

            await using var stream = assembly.GetManifestResourceStream(resourceName)!;
            using var reader = new StreamReader(stream);
            var json = await reader.ReadToEndAsync();

            var doc = JsonSerializer.Deserialize<CciDataFile>(json, JsonOptions);
            var mappings = doc?.Mappings ?? new List<CciMapping>();

            _cache.Set(CciCacheKey, mappings, CacheTtl);
            _logger.LogInformation("Loaded {Count} CCI-NIST mappings from embedded resource", mappings.Count);
            return mappings;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load CCI-NIST mapping data");
            return new List<CciMapping>();
        }
    }

    /// <summary>Internal DTO for deserializing the STIG JSON wrapper.</summary>
    private sealed class StigDataFile
    {
        public string Version { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty;
        public List<StigControl> Controls { get; set; } = new();
    }

    /// <summary>Internal DTO for deserializing the CCI mapping JSON wrapper.</summary>
    private sealed class CciDataFile
    {
        public string Version { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty;
        public int TotalMappings { get; set; }
        public List<CciMapping> Mappings { get; set; } = new();
    }
}
