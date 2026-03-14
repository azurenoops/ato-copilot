using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Ato.Copilot.Core.Interfaces.Compliance;
using Ato.Copilot.Core.Models.Compliance;

namespace Ato.Copilot.Agents.Compliance.Services.KnowledgeBase;

/// <summary>
/// JSON-backed RMF knowledge service.
/// Loads curated RMF process data from disk, caches with 24-hour TTL, and provides
/// step-by-step guidance, service-specific guidance, and deliverable views.
/// </summary>
public class RmfKnowledgeService : IRmfKnowledgeService
{
    private readonly ILogger<RmfKnowledgeService> _logger;
    private readonly Lazy<Task<RmfProcessData?>> _lazyProcessData;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public RmfKnowledgeService(
        IMemoryCache cache,
        ILogger<RmfKnowledgeService> logger)
    {
        _logger = logger;
        _lazyProcessData = new Lazy<Task<RmfProcessData?>>(
            LoadProcessDataCoreAsync, LazyThreadSafetyMode.ExecutionAndPublication);
    }

    /// <inheritdoc />
    public async Task<string> GetGuidanceAsync(
        string controlId,
        CancellationToken cancellationToken = default)
    {
        var process = await _lazyProcessData.Value;
        if (process == null)
        {
            return $"Refer to NIST SP 800-53 Rev.5, control {controlId}, for detailed RMF guidance. " +
                   "Ensure organizational policies, procedures, and technical controls are aligned " +
                   "with the stated control objectives.";
        }

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"## RMF Guidance for Control {controlId}");
        sb.AppendLine();
        sb.AppendLine("The Risk Management Framework (RMF) addresses this control across the following steps:");
        sb.AppendLine();

        foreach (var step in process.Steps)
        {
            sb.AppendLine($"### Step {step.Step}: {step.Title}");
            sb.AppendLine(step.Description);
            sb.AppendLine();
        }

        sb.AppendLine($"Refer to NIST SP 800-53 Rev.5 for full implementation details of control {controlId}.");

        return sb.ToString();
    }

    /// <inheritdoc />
    public async Task<RmfProcessData?> GetRmfProcessAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Retrieving full RMF process data");
        return await _lazyProcessData.Value;
    }

    /// <inheritdoc />
    public async Task<RmfStep?> GetRmfStepAsync(int step, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Retrieving RMF step {Step}", step);
        var process = await _lazyProcessData.Value;
        return process?.Steps.FirstOrDefault(s => s.Step == step);
    }

    /// <inheritdoc />
    public async Task<ServiceGuidance?> GetServiceGuidanceAsync(string topic, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Retrieving service guidance for topic {Topic}", topic);
        var process = await _lazyProcessData.Value;
        if (process?.ServiceGuidance == null)
            return null;

        // Try exact match first, then case-insensitive
        if (process.ServiceGuidance.TryGetValue(topic, out var guidance))
            return guidance;

        var match = process.ServiceGuidance
            .FirstOrDefault(kvp => kvp.Key.Equals(topic, StringComparison.OrdinalIgnoreCase));

        return match.Value;
    }

    /// <summary>
    /// Loads RMF process data from the JSON data file (deferred via Lazy&lt;T&gt;).
    /// </summary>
    private async Task<RmfProcessData?> LoadProcessDataCoreAsync()
    {
        try
        {
            var assembly = typeof(RmfKnowledgeService).Assembly;
            var resourceName = assembly.GetManifestResourceNames()
                .FirstOrDefault(n => n.EndsWith("rmf-process.json", StringComparison.OrdinalIgnoreCase));

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
                var filePath = Path.Combine(basePath, "KnowledgeBase", "Data", "rmf-process.json");
                if (!File.Exists(filePath))
                {
                    _logger.LogWarning("RMF data file not found at {Path}", filePath);
                    return null;
                }
                json = await File.ReadAllTextAsync(filePath);
            }

            var doc = JsonSerializer.Deserialize<RmfDataFile>(json, JsonOptions);
            if (doc == null) return null;

            var processData = new RmfProcessData(
                doc.Steps,
                doc.ServiceGuidance,
                doc.DeliverablesOverview);

            _logger.LogInformation("Loaded RMF process data with {StepCount} steps", processData.Steps.Count);
            return processData;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load RMF process data file");
            return null;
        }
    }

    /// <summary>Internal DTO for deserializing the RMF JSON wrapper.</summary>
    private sealed class RmfDataFile
    {
        public string Version { get; set; } = string.Empty;
        public List<RmfStep> Steps { get; set; } = new();
        public Dictionary<string, ServiceGuidance> ServiceGuidance { get; set; } = new();
        public List<DeliverableInfo> DeliverablesOverview { get; set; } = new();
    }
}
