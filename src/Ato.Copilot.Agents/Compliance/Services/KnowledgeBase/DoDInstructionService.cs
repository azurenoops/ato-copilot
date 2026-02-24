using Microsoft.Extensions.Logging;
using Ato.Copilot.Core.Interfaces.Compliance;

namespace Ato.Copilot.Agents.Compliance.Services.KnowledgeBase;

/// <summary>
/// Stub DoD instruction service. Returns generic instruction text.
/// Will be replaced by a full implementation backed by DoD instruction data.
/// </summary>
public class DoDInstructionService : IDoDInstructionService
{
    private readonly ILogger<DoDInstructionService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DoDInstructionService"/> class.
    /// </summary>
    /// <param name="logger">Logger instance.</param>
    public DoDInstructionService(ILogger<DoDInstructionService> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<string> GetInstructionAsync(
        string controlId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("DoD instruction stub invoked for control {ControlId}.", controlId);

        return Task.FromResult(
            $"Follow DoD Instruction 8510.01 and the DoD Cloud Computing SRG " +
            $"for control {controlId}. Ensure all implementation details are documented " +
            "in the System Security Plan (SSP).");
    }
}
