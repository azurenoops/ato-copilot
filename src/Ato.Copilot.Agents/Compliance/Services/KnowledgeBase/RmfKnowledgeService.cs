using Microsoft.Extensions.Logging;
using Ato.Copilot.Core.Interfaces.Compliance;

namespace Ato.Copilot.Agents.Compliance.Services.KnowledgeBase;

/// <summary>
/// Stub RMF knowledge service. Returns generic guidance text.
/// Will be replaced by a full implementation backed by an RMF knowledge base.
/// </summary>
public class RmfKnowledgeService : IRmfKnowledgeService
{
    private readonly ILogger<RmfKnowledgeService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="RmfKnowledgeService"/> class.
    /// </summary>
    /// <param name="logger">Logger instance.</param>
    public RmfKnowledgeService(ILogger<RmfKnowledgeService> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<string> GetGuidanceAsync(
        string controlId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("RMF guidance stub invoked for control {ControlId}.", controlId);

        return Task.FromResult(
            $"Refer to NIST SP 800-53 Rev.5, control {controlId}, for detailed RMF guidance. " +
            "Ensure organizational policies, procedures, and technical controls are aligned " +
            "with the stated control objectives.");
    }
}
