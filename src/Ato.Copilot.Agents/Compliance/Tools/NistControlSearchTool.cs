using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Ato.Copilot.Agents.Common;
using Ato.Copilot.Core.Interfaces.Compliance;

namespace Ato.Copilot.Agents.Compliance.Tools;

/// <summary>
/// MCP tool for searching NIST SP 800-53 Rev 5 controls by keyword.
/// Extends <see cref="BaseTool"/> per Constitution Principle II.
/// Returns matching controls with ID, title, and excerpt in JSON envelope.
/// </summary>
public class NistControlSearchTool : BaseTool
{
    private readonly INistControlsService _nistService;

    /// <summary>Initializes a new instance of the <see cref="NistControlSearchTool"/> class.</summary>
    public NistControlSearchTool(INistControlsService nistService, ILogger<NistControlSearchTool> logger)
        : base(logger)
    {
        _nistService = nistService;
    }

    /// <inheritdoc />
    public override string Name => "search_nist_controls";

    /// <inheritdoc />
    public override string Description =>
        "Search NIST SP 800-53 Rev 5 controls by keyword. Returns matching controls with ID, title, and relevant excerpt.";

    /// <inheritdoc />
    public override IReadOnlyDictionary<string, ToolParameter> Parameters => new Dictionary<string, ToolParameter>
    {
        ["query"] = new()
        {
            Name = "query",
            Description = "Search term to match against control IDs, titles, and descriptions (e.g., 'encryption', 'access control', 'audit')",
            Type = "string",
            Required = true
        },
        ["family"] = new()
        {
            Name = "family",
            Description = "Optional 2-letter control family filter (e.g., 'AC', 'SC', 'AU')",
            Type = "string",
            Required = false
        },
        ["max_results"] = new()
        {
            Name = "max_results",
            Description = "Maximum number of results to return (default: 10, max: 25)",
            Type = "integer",
            Required = false
        }
    };

    /// <inheritdoc />
    public override async Task<string> ExecuteCoreAsync(
        Dictionary<string, object?> arguments,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();

        var query = GetArg<string>(arguments, "query") ?? string.Empty;
        var family = GetArg<string>(arguments, "family");
        var maxResults = GetArg<int?>(arguments, "max_results") ?? 10;
        maxResults = Math.Clamp(maxResults, 1, 25);

        try
        {
            var controls = await _nistService.SearchControlsAsync(
                query,
                controlFamily: family,
                maxResults: maxResults,
                cancellationToken: cancellationToken);

            sw.Stop();

            if (controls.Count == 0)
            {
                return JsonSerializer.Serialize(new
                {
                    status = "success",
                    data = new
                    {
                        query,
                        family_filter = family,
                        total_matches = 0,
                        controls = Array.Empty<object>(),
                        message = $"No controls found matching your search for '{query}'. Try broader terms like 'cryptography', 'access', or 'audit'."
                    },
                    metadata = new
                    {
                        tool = Name,
                        execution_time_ms = sw.ElapsedMilliseconds,
                        timestamp = DateTime.UtcNow.ToString("O")
                    }
                }, new JsonSerializerOptions { WriteIndented = true });
            }

            var controlResults = controls.Select(c => new
            {
                id = c.Id.ToUpperInvariant(),
                title = c.Title,
                family = c.Family,
                excerpt = c.Description.Length > 200
                    ? c.Description[..200] + "..."
                    : c.Description
            }).ToArray();

            return JsonSerializer.Serialize(new
            {
                status = "success",
                data = new
                {
                    query,
                    family_filter = family,
                    total_matches = controls.Count,
                    controls = controlResults
                },
                metadata = new
                {
                    tool = Name,
                    execution_time_ms = sw.ElapsedMilliseconds,
                    timestamp = DateTime.UtcNow.ToString("O")
                }
            }, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "search_nist_controls failed for query '{Query}'", query);
            return JsonSerializer.Serialize(new
            {
                status = "error",
                errorCode = "CATALOG_UNAVAILABLE",
                message = "The NIST controls catalog is currently unavailable. Please try again later.",
                suggestion = "The catalog may still be loading at startup. Wait 15 seconds and retry."
            }, new JsonSerializerOptions { WriteIndented = true });
        }
    }
}
