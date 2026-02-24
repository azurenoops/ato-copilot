using Microsoft.Extensions.Logging;
using Ato.Copilot.Core.Interfaces.Compliance;

namespace Ato.Copilot.Agents.Compliance.EvidenceCollectors;

/// <summary>
/// Dictionary-based evidence collector dispatch registry. Returns the specialized
/// collector for a given family code, falling back to <c>DefaultEvidenceCollector</c>
/// for unregistered families.
/// </summary>
public class EvidenceCollectorRegistry : IEvidenceCollectorRegistry
{
    private readonly Dictionary<string, IEvidenceCollector> _collectors;
    private readonly IEvidenceCollector _defaultCollector;
    private readonly ILogger<EvidenceCollectorRegistry> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="EvidenceCollectorRegistry"/> class.
    /// </summary>
    /// <param name="collectors">All registered evidence collectors (injected via DI).</param>
    /// <param name="logger">Logger instance.</param>
    public EvidenceCollectorRegistry(
        IEnumerable<IEvidenceCollector> collectors,
        ILogger<EvidenceCollectorRegistry> logger)
    {
        _logger = logger;

        var collectorList = collectors.ToList();

        // Separate default collector from specialized collectors
        _defaultCollector = collectorList.FirstOrDefault(c =>
            string.Equals(c.FamilyCode, "DEFAULT", StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException("DefaultEvidenceCollector must be registered");

        _collectors = new Dictionary<string, IEvidenceCollector>(StringComparer.OrdinalIgnoreCase);

        foreach (var collector in collectorList.Where(c =>
            !string.Equals(c.FamilyCode, "DEFAULT", StringComparison.OrdinalIgnoreCase)))
        {
            _collectors[collector.FamilyCode] = collector;
            _logger.LogDebug("Registered evidence collector for family {Family}: {Type}",
                collector.FamilyCode, collector.GetType().Name);
        }

        _logger.LogInformation("EvidenceCollectorRegistry initialized with {Count} specialized collectors + default",
            _collectors.Count);
    }

    /// <inheritdoc />
    public IEvidenceCollector GetCollector(string familyCode)
    {
        if (_collectors.TryGetValue(familyCode, out var collector))
        {
            _logger.LogDebug("Dispatching family {Family} to {Collector}",
                familyCode, collector.GetType().Name);
            return collector;
        }

        _logger.LogDebug("No specialized collector for family {Family}, using default", familyCode);
        return _defaultCollector;
    }
}
