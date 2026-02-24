using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Ato.Copilot.Agents.Common;
using Ato.Copilot.Core.Interfaces.Compliance;

namespace Ato.Copilot.Agents.Compliance.Tools;

/// <summary>
/// MCP tool for explaining a specific NIST SP 800-53 Rev 5 control.
/// Extends <see cref="BaseTool"/> per Constitution Principle II.
/// Returns statement, guidance, and assessment objectives in JSON envelope.
/// </summary>
public class NistControlExplainerTool : BaseTool
{
    private readonly INistControlsService _nistService;

    /// <summary>Initializes a new instance of the <see cref="NistControlExplainerTool"/> class.</summary>
    public NistControlExplainerTool(INistControlsService nistService, ILogger<NistControlExplainerTool> logger)
        : base(logger)
    {
        _nistService = nistService;
    }

    /// <inheritdoc />
    public override string Name => "explain_nist_control";

    /// <inheritdoc />
    public override string Description =>
        "Get a detailed explanation of a specific NIST SP 800-53 Rev 5 control, including its statement, guidance, and assessment objectives.";

    /// <inheritdoc />
    public override IReadOnlyDictionary<string, ToolParameter> Parameters => new Dictionary<string, ToolParameter>
    {
        ["control_id"] = new()
        {
            Name = "control_id",
            Description = "The NIST control identifier (e.g., 'AC-2', 'SC-7', 'AU-6(1)')",
            Type = "string",
            Required = true
        }
    };

    /// <inheritdoc />
    public override async Task<string> ExecuteCoreAsync(
        Dictionary<string, object?> arguments,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        var controlId = GetArg<string>(arguments, "control_id");

        if (string.IsNullOrWhiteSpace(controlId))
        {
            return JsonSerializer.Serialize(new
            {
                status = "error",
                errorCode = "INVALID_INPUT",
                message = "The 'control_id' parameter is required.",
                suggestion = "Provide a NIST control ID like 'AC-2', 'SC-7', or 'AU-6(1)'."
            }, new JsonSerializerOptions { WriteIndented = true });
        }

        try
        {
            var enhancement = await _nistService.GetControlEnhancementAsync(controlId, cancellationToken);
            var version = await _nistService.GetVersionAsync(cancellationToken);
            sw.Stop();

            if (enhancement is null)
            {
                return JsonSerializer.Serialize(new
                {
                    status = "error",
                    errorCode = "CONTROL_NOT_FOUND",
                    message = $"Control '{controlId}' was not found in the NIST SP 800-53 Rev 5 catalog.",
                    suggestion = "Check the control ID format (e.g., 'AC-2', 'SC-7', 'AU-6(1)'). Use 'search_nist_controls' to find controls by keyword."
                }, new JsonSerializerOptions { WriteIndented = true });
            }

            return JsonSerializer.Serialize(new
            {
                status = "success",
                data = new
                {
                    control_id = enhancement.Id,
                    title = enhancement.Title,
                    statement = enhancement.Statement,
                    guidance = enhancement.Guidance,
                    objectives = enhancement.Objectives,
                    catalog_version = version
                },
                metadata = new
                {
                    tool = Name,
                    execution_time_ms = sw.ElapsedMilliseconds,
                    timestamp = DateTime.UtcNow.ToString("O")
                }
            }, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (ArgumentException ex)
        {
            return JsonSerializer.Serialize(new
            {
                status = "error",
                errorCode = "INVALID_INPUT",
                message = ex.Message,
                suggestion = "Provide a valid NIST control ID like 'AC-2' or 'SC-7'."
            }, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "explain_nist_control failed for control '{ControlId}'", controlId);
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
