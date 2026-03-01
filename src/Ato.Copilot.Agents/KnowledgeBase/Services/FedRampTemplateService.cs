using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Ato.Copilot.Core.Interfaces.Compliance;
using Ato.Copilot.Core.Models.Compliance;

namespace Ato.Copilot.Agents.KnowledgeBase.Services;

/// <summary>
/// JSON-backed FedRAMP template service.
/// Loads curated FedRAMP template guidance from disk, caches with 24-hour TTL,
/// and provides lookup and type normalization.
/// </summary>
public class FedRampTemplateService : IFedRampTemplateService
{
    private readonly IMemoryCache _cache;
    private readonly ILogger<FedRampTemplateService> _logger;

    private const string CacheKey = "kb:fedramp:all_templates";
    private static readonly TimeSpan CacheTtl = TimeSpan.FromHours(24);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public FedRampTemplateService(
        IMemoryCache cache,
        ILogger<FedRampTemplateService> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<FedRampTemplate?> GetTemplateGuidanceAsync(
        string templateType,
        string baseline = "High",
        CancellationToken cancellationToken = default)
    {
        var normalized = NormalizeTemplateType(templateType);
        var templates = await LoadDataAsync();
        return templates.FirstOrDefault(t =>
            t.TemplateType.Equals(normalized, StringComparison.OrdinalIgnoreCase));
    }

    /// <inheritdoc />
    public async Task<List<FedRampTemplate>> GetAllTemplatesAsync(
        CancellationToken cancellationToken = default)
    {
        return await LoadDataAsync();
    }

    /// <summary>
    /// Normalizes template type input to canonical form.
    /// Handles: "POA&amp;M"→"POAM", "poa&amp;m"→"POAM", "CONMON"→"CRM",
    /// "conmon"→"CRM", "ssp"→"SSP"
    /// </summary>
    internal static string NormalizeTemplateType(string templateType)
    {
        if (string.IsNullOrWhiteSpace(templateType)) return string.Empty;

        var trimmed = templateType.Trim().ToUpperInvariant();

        // Normalize POA&M variants
        if (trimmed is "POA&M" or "POAM" or "POA&AMP;M" or "POA M" or "PLAN OF ACTION")
            return "POAM";

        // Normalize CONMON to CRM
        if (trimmed is "CONMON" or "CONTINUOUS MONITORING" or "CON MON")
            return "CRM";

        return trimmed;
    }

    private async Task<List<FedRampTemplate>> LoadDataAsync()
    {
        if (_cache.TryGetValue(CacheKey, out List<FedRampTemplate>? cached) && cached != null)
            return cached;

        try
        {
            var assembly = typeof(FedRampTemplateService).Assembly;
            var resourceName = assembly.GetManifestResourceNames()
                .FirstOrDefault(n => n.EndsWith("fedramp-templates.json", StringComparison.OrdinalIgnoreCase));

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
                var filePath = Path.Combine(basePath, "KnowledgeBase", "Data", "fedramp-templates.json");
                if (!File.Exists(filePath))
                {
                    _logger.LogWarning("FedRAMP templates data file not found at {Path}", filePath);
                    return new List<FedRampTemplate>();
                }
                json = await File.ReadAllTextAsync(filePath);
            }

            var doc = JsonSerializer.Deserialize<FedRampDataFile>(json, JsonOptions);
            if (doc == null) return new List<FedRampTemplate>();

            var templates = doc.Templates;
            _cache.Set(CacheKey, templates, CacheTtl);
            _logger.LogInformation("Loaded {Count} FedRAMP templates from data file", templates.Count);
            return templates;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load FedRAMP templates data file");
            return new List<FedRampTemplate>();
        }
    }

    /// <summary>Internal DTO for deserializing the FedRAMP templates JSON wrapper.</summary>
    private sealed class FedRampDataFile
    {
        public string Version { get; set; } = string.Empty;
        public List<FedRampTemplate> Templates { get; set; } = new();
    }
}
