using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Ato.Copilot.Core.Constants;
using Ato.Copilot.Core.Interfaces.Compliance;
using Ato.Copilot.Core.Models.Compliance;

namespace Ato.Copilot.Agents.Compliance.Services;

/// <summary>
/// NIST 800-53 Rev 5 controls catalog service with dual-source loading:
/// 1. Online fetch from usnistgov/oscal-content GitHub repo (when NistCatalog:PreferOnline is true)
/// 2. Local file cache with configurable max age
/// 3. Embedded resource fallback for air-gapped/offline environments
/// Tracks LastSyncedAt and CatalogSource for observability.
/// </summary>
public class NistControlsService : INistControlsService
{
    private readonly ILogger<NistControlsService> _logger;
    private readonly IConfiguration _configuration;
    private readonly HttpClient _httpClient;
    private readonly SemaphoreSlim _loadLock = new(1, 1);

    private List<NistControl> _controls = new();
    private bool _loaded;
    private DateTime? _lastSyncedAt;
    private string _catalogSource = "none";

    /// <summary>UTC timestamp of the last successful catalog load.</summary>
    public DateTime? LastSyncedAt => _lastSyncedAt;

    /// <summary>Source of the currently loaded catalog: "online", "cache", "embedded", or "none".</summary>
    public string CatalogSource => _catalogSource;

    /// <summary>
    /// Initializes a new instance of the <see cref="NistControlsService"/> class.
    /// </summary>
    public NistControlsService(
        ILogger<NistControlsService> logger,
        IConfiguration configuration,
        HttpClient httpClient)
    {
        _logger = logger;
        _configuration = configuration;
        _httpClient = httpClient;
    }

    /// <inheritdoc />
    public async Task<NistControl?> GetControlAsync(string controlId, CancellationToken cancellationToken = default)
    {
        await EnsureLoadedAsync(cancellationToken);
        return _controls.FirstOrDefault(c =>
            string.Equals(c.Id, controlId, StringComparison.OrdinalIgnoreCase));
    }

    /// <inheritdoc />
    public async Task<List<NistControl>> GetControlFamilyAsync(
        string familyId,
        bool includeControls = true,
        CancellationToken cancellationToken = default)
    {
        await EnsureLoadedAsync(cancellationToken);

        var familyUpper = familyId.ToUpperInvariant();
        var controls = _controls
            .Where(c => string.Equals(c.Family, familyUpper, StringComparison.OrdinalIgnoreCase)
                        && !c.IsEnhancement)
            .ToList();

        if (!includeControls)
        {
            // Return summary — just family info without nested enhancements
            return controls.Select(c => new NistControl
            {
                Id = c.Id,
                Family = c.Family,
                Title = c.Title,
                ImpactLevel = c.ImpactLevel,
                Baselines = c.Baselines,
                IsEnhancement = c.IsEnhancement
            }).ToList();
        }

        return controls;
    }

    /// <inheritdoc />
    public async Task<List<NistControl>> SearchControlsAsync(
        string query,
        string? controlFamily = null,
        string? impactLevel = null,
        int maxResults = 10,
        CancellationToken cancellationToken = default)
    {
        await EnsureLoadedAsync(cancellationToken);

        var queryLower = query.ToLowerInvariant();

        var results = _controls.AsEnumerable();

        if (!string.IsNullOrEmpty(controlFamily))
            results = results.Where(c =>
                string.Equals(c.Family, controlFamily, StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrEmpty(impactLevel))
            results = results.Where(c =>
                c.Baselines.Any(b => string.Equals(b, impactLevel, StringComparison.OrdinalIgnoreCase)));

        results = results.Where(c =>
            c.Id.Contains(queryLower, StringComparison.OrdinalIgnoreCase) ||
            c.Title.Contains(queryLower, StringComparison.OrdinalIgnoreCase) ||
            c.Description.Contains(queryLower, StringComparison.OrdinalIgnoreCase));

        return results.Take(maxResults).ToList();
    }

    /// <summary>
    /// Returns the status of the catalog including source, sync time, and control count.
    /// </summary>
    public async Task<CatalogStatus> GetCatalogStatusAsync(CancellationToken cancellationToken = default)
    {
        await EnsureLoadedAsync(cancellationToken);

        return new CatalogStatus
        {
            Source = _catalogSource,
            LastSyncedAt = _lastSyncedAt,
            TotalControls = _controls.Count,
            Families = _controls.Select(c => c.Family).Distinct().Count(),
            IsLoaded = _loaded
        };
    }

    /// <summary>
    /// Ensures the catalog is loaded, using the dual-source strategy:
    /// online → cache → embedded resource.
    /// </summary>
    private async Task EnsureLoadedAsync(CancellationToken cancellationToken)
    {
        if (_loaded) return;

        await _loadLock.WaitAsync(cancellationToken);
        try
        {
            if (_loaded) return; // double-check after acquiring lock

            var preferOnline = _configuration.GetValue("NistCatalog:PreferOnline", true);
            var cachePathConfig = _configuration.GetValue<string>("NistCatalog:CachePath")
                                  ?? "data/nist-catalog-cache.json";
            var cacheMaxAgeDays = _configuration.GetValue("NistCatalog:CacheMaxAgeDays", 30);
            var timeoutSeconds = _configuration.GetValue("NistCatalog:FetchTimeoutSeconds", 15);
            var onlineUrl = _configuration.GetValue<string>("NistCatalog:OnlineUrl")
                            ?? "https://raw.githubusercontent.com/usnistgov/oscal-content/main/nist.gov/SP800-53/rev5/json/NIST_SP-800-53_rev5_catalog.json";

            // Try cache first if it exists and is not expired
            if (await TryLoadFromCacheAsync(cachePathConfig, cacheMaxAgeDays, cancellationToken))
            {
                _logger.LogInformation("NIST catalog loaded from cache ({Count} controls)", _controls.Count);
                return;
            }

            // Try online fetch
            if (preferOnline)
            {
                if (await TryLoadFromOnlineAsync(onlineUrl, timeoutSeconds, cachePathConfig, cancellationToken))
                {
                    _logger.LogInformation("NIST catalog loaded from online ({Count} controls)", _controls.Count);
                    return;
                }
            }

            // Fallback to embedded resource
            await LoadFromEmbeddedResourceAsync(cancellationToken);
            _logger.LogInformation("NIST catalog loaded from embedded resource ({Count} controls)", _controls.Count);
        }
        finally
        {
            _loadLock.Release();
        }
    }

    /// <summary>Try loading from local file cache.</summary>
    private async Task<bool> TryLoadFromCacheAsync(string cachePath, int maxAgeDays, CancellationToken ct)
    {
        try
        {
            if (!File.Exists(cachePath)) return false;

            var fileInfo = new FileInfo(cachePath);
            if (fileInfo.LastWriteTimeUtc < DateTime.UtcNow.AddDays(-maxAgeDays))
            {
                _logger.LogInformation("NIST catalog cache expired (>{MaxAge} days old)", maxAgeDays);
                return false;
            }

            var json = await File.ReadAllTextAsync(cachePath, ct);
            var controls = ParseOscalCatalog(json);
            if (controls.Count == 0) return false;

            _controls = controls;
            _loaded = true;
            _lastSyncedAt = fileInfo.LastWriteTimeUtc;
            _catalogSource = "cache";
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load NIST catalog from cache");
            return false;
        }
    }

    /// <summary>Try fetching catalog from online (GitHub) with configured timeout.</summary>
    private async Task<bool> TryLoadFromOnlineAsync(
        string url, int timeoutSeconds, string cachePath, CancellationToken ct)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

            _logger.LogInformation("Fetching NIST catalog from {Url}", url);
            var json = await _httpClient.GetStringAsync(url, cts.Token);

            var controls = ParseOscalCatalog(json);
            if (controls.Count == 0) return false;

            _controls = controls;
            _loaded = true;
            _lastSyncedAt = DateTime.UtcNow;
            _catalogSource = "online";

            // Save to cache
            try
            {
                var dir = Path.GetDirectoryName(cachePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
                await File.WriteAllTextAsync(cachePath, json, ct);
                _logger.LogDebug("NIST catalog cached to {CachePath}", cachePath);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to cache NIST catalog to {CachePath}", cachePath);
            }

            return true;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("NIST catalog online fetch timed out after {Timeout}s", timeoutSeconds);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch NIST catalog from online");
            return false;
        }
    }

    /// <summary>Load from embedded OSCAL catalog resource (air-gapped fallback).</summary>
    private async Task LoadFromEmbeddedResourceAsync(CancellationToken ct)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = "Ato.Copilot.Agents.Compliance.Resources.NIST_SP-800-53_rev5_catalog.json";

        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream == null)
        {
            _logger.LogError("Embedded NIST catalog resource not found: {Resource}", resourceName);
            _controls = new List<NistControl>();
            _loaded = true;
            _catalogSource = "embedded-missing";
            return;
        }

        using var reader = new StreamReader(stream);
        var json = await reader.ReadToEndAsync(ct);

        _controls = ParseOscalCatalog(json);
        _loaded = true;
        _lastSyncedAt = DateTime.UtcNow;
        _catalogSource = "embedded";
    }

    /// <summary>
    /// Parse NIST OSCAL JSON catalog into NistControl objects.
    /// The OSCAL catalog has structure: catalog → groups[] → controls[] → controls[] (enhancements).
    /// </summary>
    private List<NistControl> ParseOscalCatalog(string json)
    {
        var controls = new List<NistControl>();

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // OSCAL structure: { "catalog": { "groups": [ ... ] } }
            if (!root.TryGetProperty("catalog", out var catalog))
            {
                _logger.LogWarning("OSCAL catalog root element 'catalog' not found");
                return controls;
            }

            if (!catalog.TryGetProperty("groups", out var groups))
            {
                _logger.LogWarning("OSCAL catalog 'groups' not found");
                return controls;
            }

            foreach (var group in groups.EnumerateArray())
            {
                var familyId = group.TryGetProperty("id", out var gid) ? gid.GetString() ?? "" : "";
                var familyUpper = familyId.ToUpperInvariant();

                if (!group.TryGetProperty("controls", out var groupControls))
                    continue;

                foreach (var control in groupControls.EnumerateArray())
                {
                    var parsed = ParseControl(control, familyUpper, isEnhancement: false, parentId: null);
                    if (parsed != null)
                    {
                        controls.Add(parsed);

                        // Parse enhancements (depth 1)
                        if (control.TryGetProperty("controls", out var enhancements))
                        {
                            foreach (var enh in enhancements.EnumerateArray())
                            {
                                var enhControl = ParseControl(enh, familyUpper, isEnhancement: true, parentId: parsed.Id);
                                if (enhControl != null)
                                {
                                    parsed.ControlEnhancements.Add(enhControl);
                                    controls.Add(enhControl);
                                }
                            }
                        }
                    }
                }
            }

            _logger.LogDebug("Parsed {Count} controls from OSCAL catalog", controls.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse OSCAL catalog JSON");
        }

        return controls;
    }

    /// <summary>Parse a single OSCAL control element into a NistControl.</summary>
    private NistControl? ParseControl(JsonElement element, string family, bool isEnhancement, string? parentId)
    {
        try
        {
            var id = element.TryGetProperty("id", out var idProp) ? idProp.GetString() ?? "" : "";
            var title = element.TryGetProperty("title", out var titleProp) ? titleProp.GetString() ?? "" : "";

            // Extract description from parts
            var description = ExtractDescription(element);

            var control = new NistControl
            {
                Id = id,
                Family = family,
                Title = title,
                Description = description,
                IsEnhancement = isEnhancement,
                ParentControlId = parentId,
                // Baseline mapping will be derived from OSCAL props
                Baselines = ExtractBaselines(element),
                FedRampParameters = ExtractFedRampParams(element)
            };

            return control;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse control from OSCAL element");
            return null;
        }
    }

    /// <summary>Extract description text from OSCAL control parts.</summary>
    private static string ExtractDescription(JsonElement control)
    {
        if (!control.TryGetProperty("parts", out var parts))
            return string.Empty;

        foreach (var part in parts.EnumerateArray())
        {
            var partName = part.TryGetProperty("name", out var nameProp) ? nameProp.GetString() : null;
            if (partName == "statement" && part.TryGetProperty("prose", out var prose))
                return prose.GetString() ?? string.Empty;
        }

        // Try first part with prose
        foreach (var part in parts.EnumerateArray())
        {
            if (part.TryGetProperty("prose", out var prose))
                return prose.GetString() ?? string.Empty;
        }

        return string.Empty;
    }

    /// <summary>Extract applicable baselines from OSCAL props.</summary>
    private static List<string> ExtractBaselines(JsonElement control)
    {
        var baselines = new List<string>();

        if (!control.TryGetProperty("props", out var props))
            return baselines;

        foreach (var prop in props.EnumerateArray())
        {
            var name = prop.TryGetProperty("name", out var n) ? n.GetString() : null;
            if (name == "label") continue;

            // OSCAL uses class "sp800-53a" with properties indicating baseline inclusion
            var cls = prop.TryGetProperty("class", out var c) ? c.GetString() : null;
            if (cls?.Contains("baseline") == true || name?.Contains("baseline") == true)
            {
                var value = prop.TryGetProperty("value", out var v) ? v.GetString() : null;
                if (!string.IsNullOrEmpty(value) && !baselines.Contains(value))
                    baselines.Add(value);
            }
        }

        // Default to all baselines if none found (base controls are typically in all)
        if (baselines.Count == 0)
        {
            baselines.AddRange(new[] { "Low", "Moderate", "High" });
        }

        return baselines;
    }

    /// <summary>Extract FedRAMP parameter values from OSCAL params.</summary>
    private static string? ExtractFedRampParams(JsonElement control)
    {
        if (!control.TryGetProperty("params", out var parameters))
            return null;

        var paramList = new List<string>();
        foreach (var param in parameters.EnumerateArray())
        {
            var paramId = param.TryGetProperty("id", out var pid) ? pid.GetString() : "?";
            var label = param.TryGetProperty("label", out var lbl) ? lbl.GetString() : null;
            if (label != null)
                paramList.Add($"{paramId}: {label}");
        }

        return paramList.Count > 0 ? string.Join("; ", paramList) : null;
    }
}

/// <summary>
/// Status information for the NIST controls catalog.
/// </summary>
public class CatalogStatus
{
    /// <summary>Source of the loaded catalog (online, cache, embedded).</summary>
    public string Source { get; set; } = "none";

    /// <summary>UTC timestamp of last successful sync.</summary>
    public DateTime? LastSyncedAt { get; set; }

    /// <summary>Total number of controls loaded.</summary>
    public int TotalControls { get; set; }

    /// <summary>Number of distinct control families.</summary>
    public int Families { get; set; }

    /// <summary>Whether the catalog is loaded.</summary>
    public bool IsLoaded { get; set; }
}
