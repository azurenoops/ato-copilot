using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Ato.Copilot.Core.Interfaces.Compliance;
using Ato.Copilot.Core.Models.Compliance;

namespace Ato.Copilot.Agents.Compliance.Services.KnowledgeBase;

/// <summary>
/// JSON-backed DoD instruction service.
/// Loads curated DoD instruction data from disk, caches with 24-hour TTL,
/// and provides lookup, search, and cross-reference capabilities.
/// </summary>
public class DoDInstructionService : IDoDInstructionService
{
    private readonly ILogger<DoDInstructionService> _logger;
    private readonly Lazy<Task<List<DoDInstruction>>> _lazyInstructions;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public DoDInstructionService(
        IMemoryCache cache,
        ILogger<DoDInstructionService> logger)
    {
        _logger = logger;
        _lazyInstructions = new Lazy<Task<List<DoDInstruction>>>(
            LoadInstructionsCoreAsync, LazyThreadSafetyMode.ExecutionAndPublication);
    }

    /// <inheritdoc />
    public async Task<string> GetInstructionAsync(
        string controlId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("DoD instruction lookup for control {ControlId}", controlId);

        var instructions = await _lazyInstructions.Value;
        var matching = instructions
            .Where(i => i.RelatedNistControls.Contains(controlId, StringComparer.OrdinalIgnoreCase))
            .ToList();

        if (matching.Count == 0)
        {
            return $"Follow DoD Instruction 8510.01 and the DoD Cloud Computing SRG " +
                   $"for control {controlId}. Ensure all implementation details are documented " +
                   "in the System Security Plan (SSP).";
        }

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"## DoD Instructions for Control {controlId}");
        sb.AppendLine();

        foreach (var instruction in matching)
        {
            sb.AppendLine($"### {instruction.InstructionId}: {instruction.Title}");
            sb.AppendLine(instruction.Description);
            sb.AppendLine();

            var relevantMappings = instruction.ControlMappings
                .Where(m => m.ControlId.Equals(controlId, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (relevantMappings.Count > 0)
            {
                sb.AppendLine("**Specific Guidance:**");
                foreach (var mapping in relevantMappings)
                {
                    sb.AppendLine($"- **{mapping.Requirement}**: {mapping.Guidance}");
                }
                sb.AppendLine();
            }
        }

        return sb.ToString();
    }

    /// <inheritdoc />
    public async Task<DoDInstruction?> ExplainInstructionAsync(
        string instructionId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Explaining DoD instruction {InstructionId}", instructionId);

        var instructions = await _lazyInstructions.Value;
        var normalized = NormalizeInstructionId(instructionId);

        return instructions.FirstOrDefault(i =>
            NormalizeInstructionId(i.InstructionId).Equals(normalized, StringComparison.OrdinalIgnoreCase));
    }

    /// <inheritdoc />
    public async Task<List<DoDInstruction>> GetInstructionsByControlAsync(
        string controlId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Finding DoD instructions for control {ControlId}", controlId);

        var instructions = await _lazyInstructions.Value;
        return instructions
            .Where(i => i.RelatedNistControls.Contains(controlId, StringComparer.OrdinalIgnoreCase) ||
                        i.ControlMappings.Any(m => m.ControlId.Equals(controlId, StringComparison.OrdinalIgnoreCase)))
            .ToList();
    }

    /// <summary>Normalizes instruction IDs for flexible matching (e.g., "8510.01" → "DoDI-8510.01").</summary>
    internal static string NormalizeInstructionId(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return string.Empty;

        var trimmed = id.Trim();

        // Already in canonical form
        if (trimmed.StartsWith("DoDI-", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("CNSSI-", StringComparison.OrdinalIgnoreCase))
            return trimmed;

        // Handle "DoDI 8510.01" → "DoDI-8510.01"
        if (trimmed.StartsWith("DoDI ", StringComparison.OrdinalIgnoreCase))
            return "DoDI-" + trimmed[5..].Trim();

        // Handle "CNSSI 1253" → "CNSSI-1253"
        if (trimmed.StartsWith("CNSSI ", StringComparison.OrdinalIgnoreCase))
            return "CNSSI-" + trimmed[6..].Trim();

        // Handle bare number "8510.01" → "DoDI-8510.01"
        if (char.IsDigit(trimmed[0]))
            return "DoDI-" + trimmed;

        return trimmed;
    }

    /// <summary>
    /// Loads DoD instruction data from the JSON data file (deferred via Lazy&lt;T&gt;).
    /// </summary>
    private async Task<List<DoDInstruction>> LoadInstructionsCoreAsync()
    {
        try
        {
            var assembly = typeof(DoDInstructionService).Assembly;
            var resourceName = assembly.GetManifestResourceNames()
                .FirstOrDefault(n => n.EndsWith("dod-instructions.json", StringComparison.OrdinalIgnoreCase));

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
                var filePath = Path.Combine(basePath, "KnowledgeBase", "Data", "dod-instructions.json");
                if (!File.Exists(filePath))
                {
                    _logger.LogWarning("DoD instructions data file not found at {Path}", filePath);
                    return new List<DoDInstruction>();
                }
                json = await File.ReadAllTextAsync(filePath);
            }

            var doc = JsonSerializer.Deserialize<DoDInstructionsDataFile>(json, JsonOptions);
            var instructions = doc?.Instructions ?? new List<DoDInstruction>();

            _logger.LogInformation("Loaded {Count} DoD instructions from data file", instructions.Count);
            return instructions;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load DoD instructions data file");
            return new List<DoDInstruction>();
        }
    }

    /// <summary>Internal DTO for deserializing the DoD instructions JSON wrapper.</summary>
    private sealed class DoDInstructionsDataFile
    {
        public string Version { get; set; } = string.Empty;
        public List<DoDInstruction> Instructions { get; set; } = new();
    }
}
