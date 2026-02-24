using Microsoft.Extensions.Logging;
using Ato.Copilot.Core.Interfaces.Compliance;
using Ato.Copilot.Core.Models.Compliance;

namespace Ato.Copilot.Agents.Compliance.Services.KnowledgeBase;

/// <summary>
/// Stub STIG validation service. Returns empty findings list.
/// Will be replaced by a full implementation backed by a STIG knowledge base.
/// </summary>
public class StigValidationService : IStigValidationService
{
    private readonly ILogger<StigValidationService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="StigValidationService"/> class.
    /// </summary>
    /// <param name="logger">Logger instance.</param>
    public StigValidationService(ILogger<StigValidationService> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<List<ComplianceFinding>> ValidateAsync(
        string familyCode,
        IEnumerable<NistControl> controls,
        string subscriptionId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "STIG validation stub invoked for family {Family}, Sub={Sub}. Returning empty.",
            familyCode, subscriptionId);

        return Task.FromResult(new List<ComplianceFinding>());
    }
}
