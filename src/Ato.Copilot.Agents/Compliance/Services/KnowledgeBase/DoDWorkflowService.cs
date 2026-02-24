using Microsoft.Extensions.Logging;
using Ato.Copilot.Core.Interfaces.Compliance;

namespace Ato.Copilot.Agents.Compliance.Services.KnowledgeBase;

/// <summary>
/// Stub DoD workflow service. Returns a standard RMF workflow.
/// Will be replaced by a full implementation with configurable workflow templates.
/// </summary>
public class DoDWorkflowService : IDoDWorkflowService
{
    private readonly ILogger<DoDWorkflowService> _logger;

    /// <summary>Standard RMF 6-step workflow.</summary>
    private static readonly List<string> StandardWorkflow =
    [
        "Step 1: Categorize — Classify the information system based on impact levels (FIPS 199).",
        "Step 2: Select — Choose the appropriate baseline security controls (NIST SP 800-53).",
        "Step 3: Implement — Apply security controls and document in the SSP.",
        "Step 4: Assess — Evaluate control effectiveness using the SAP.",
        "Step 5: Authorize — Make risk-based authorization decision (ATO/DATO/IATO).",
        "Step 6: Monitor — Continuously monitor controls and maintain authorization."
    ];

    /// <summary>
    /// Initializes a new instance of the <see cref="DoDWorkflowService"/> class.
    /// </summary>
    /// <param name="logger">Logger instance.</param>
    public DoDWorkflowService(ILogger<DoDWorkflowService> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<List<string>> GetWorkflowAsync(
        string assessmentType,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("DoD workflow stub invoked for type {AssessmentType}.", assessmentType);

        return Task.FromResult(new List<string>(StandardWorkflow));
    }
}
