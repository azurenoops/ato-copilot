using Microsoft.Extensions.Logging;
using Ato.Copilot.Core.Interfaces.Compliance;

namespace Ato.Copilot.Agents.Compliance.Services.KnowledgeBase;

/// <summary>
/// Stub STIG knowledge service. Returns empty mapping.
/// Will be replaced by a full implementation with STIG-to-NIST control mappings.
/// </summary>
public class StigKnowledgeService : IStigKnowledgeService
{
    private readonly ILogger<StigKnowledgeService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="StigKnowledgeService"/> class.
    /// </summary>
    /// <param name="logger">Logger instance.</param>
    public StigKnowledgeService(ILogger<StigKnowledgeService> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<string> GetStigMappingAsync(
        string controlId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("STIG mapping stub invoked for control {ControlId}.", controlId);

        return Task.FromResult(string.Empty);
    }
}
