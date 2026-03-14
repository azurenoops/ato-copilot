using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Ato.Copilot.Core.Interfaces.Compliance;
using Ato.Copilot.Core.Models.Compliance;

namespace Ato.Copilot.Agents.Compliance.Services.KnowledgeBase;

/// <summary>
/// JSON-backed DoD workflow service.
/// Loads curated authorization workflow data from disk, caches with 24-hour TTL,
/// and provides lookup by ID, organization, and assessment type.
/// </summary>
public class DoDWorkflowService : IDoDWorkflowService
{
    private readonly ILogger<DoDWorkflowService> _logger;
    private readonly Lazy<Task<List<DoDWorkflow>>> _lazyWorkflows;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>Standard RMF 6-step workflow (fallback when no data file is found).</summary>
    private static readonly List<string> StandardWorkflow =
    [
        "Step 1: Categorize — Classify the information system based on impact levels (FIPS 199).",
        "Step 2: Select — Choose the appropriate baseline security controls (NIST SP 800-53).",
        "Step 3: Implement — Apply security controls and document in the SSP.",
        "Step 4: Assess — Evaluate control effectiveness using the SAP.",
        "Step 5: Authorize — Make risk-based authorization decision (ATO/DATO/IATO).",
        "Step 6: Monitor — Continuously monitor controls and maintain authorization."
    ];

    public DoDWorkflowService(
        IMemoryCache cache,
        ILogger<DoDWorkflowService> logger)
    {
        _logger = logger;
        _lazyWorkflows = new Lazy<Task<List<DoDWorkflow>>>(
            LoadWorkflowsCoreAsync, LazyThreadSafetyMode.ExecutionAndPublication);
    }

    /// <inheritdoc />
    public async Task<List<string>> GetWorkflowAsync(
        string assessmentType,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Getting workflow for assessment type {AssessmentType}", assessmentType);

        var workflows = await _lazyWorkflows.Value;
        if (workflows.Count == 0)
            return new List<string>(StandardWorkflow);

        // Try to find a matching workflow by impact level or organization
        var match = workflows.FirstOrDefault(w =>
            w.ImpactLevel.Equals(assessmentType, StringComparison.OrdinalIgnoreCase) ||
            w.Name.Contains(assessmentType, StringComparison.OrdinalIgnoreCase));

        if (match == null)
            return new List<string>(StandardWorkflow);

        return match.Steps
            .OrderBy(s => s.Order)
            .Select(s => $"Step {s.Order}: {s.Title} — {s.Description}")
            .ToList();
    }

    /// <inheritdoc />
    public async Task<DoDWorkflow?> GetWorkflowDetailAsync(
        string workflowId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Getting workflow detail for {WorkflowId}", workflowId);

        var workflows = await _lazyWorkflows.Value;
        return workflows.FirstOrDefault(w =>
            w.WorkflowId.Equals(workflowId, StringComparison.OrdinalIgnoreCase));
    }

    /// <inheritdoc />
    public async Task<List<DoDWorkflow>> GetWorkflowsByOrganizationAsync(
        string organization,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Getting workflows for organization {Organization}", organization);

        var workflows = await _lazyWorkflows.Value;
        return workflows
            .Where(w => w.Organization.Equals(organization, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    /// <summary>
    /// Loads workflow data from the JSON data file (deferred via Lazy&lt;T&gt;).
    /// </summary>
    private async Task<List<DoDWorkflow>> LoadWorkflowsCoreAsync()
    {
        try
        {
            var assembly = typeof(DoDWorkflowService).Assembly;
            var resourceName = assembly.GetManifestResourceNames()
                .FirstOrDefault(n => n.EndsWith("navy-workflows.json", StringComparison.OrdinalIgnoreCase));

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
                var filePath = Path.Combine(basePath, "KnowledgeBase", "Data", "navy-workflows.json");
                if (!File.Exists(filePath))
                {
                    _logger.LogWarning("Workflow data file not found at {Path}", filePath);
                    return new List<DoDWorkflow>();
                }
                json = await File.ReadAllTextAsync(filePath);
            }

            var doc = JsonSerializer.Deserialize<WorkflowDataFile>(json, JsonOptions);
            if (doc == null) return new List<DoDWorkflow>();

            // Convert JSON DTOs to domain models (handle durationDays → Duration string)
            var workflows = doc.Workflows.Select(w => new DoDWorkflow(
                w.WorkflowId,
                w.Name,
                w.Organization,
                w.ImpactLevel,
                w.Description,
                w.Steps.Select(s => new WorkflowStep(
                    s.Order,
                    s.Name,
                    s.Description,
                    $"{s.DurationDays} days")).ToList(),
                w.RequiredDocuments,
                w.ApprovalAuthorities)).ToList();

            _logger.LogInformation("Loaded {Count} DoD workflows from data file", workflows.Count);
            return workflows;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load workflow data file");
            return new List<DoDWorkflow>();
        }
    }

    /// <summary>Internal DTO for deserializing the workflow JSON wrapper.</summary>
    private sealed class WorkflowDataFile
    {
        public string Version { get; set; } = string.Empty;
        public List<WorkflowJsonDto> Workflows { get; set; } = new();
    }

    /// <summary>JSON DTO for workflow deserialization (handles durationDays as int).</summary>
    private sealed class WorkflowJsonDto
    {
        public string WorkflowId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Organization { get; set; } = string.Empty;
        public string ImpactLevel { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public List<WorkflowStepJsonDto> Steps { get; set; } = new();
        public List<string> RequiredDocuments { get; set; } = new();
        public List<string> ApprovalAuthorities { get; set; } = new();
    }

    /// <summary>JSON DTO for workflow step deserialization.</summary>
    private sealed class WorkflowStepJsonDto
    {
        public int Order { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int DurationDays { get; set; }
        public string Responsible { get; set; } = string.Empty;
    }
}
