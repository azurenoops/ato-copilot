using Microsoft.Extensions.Logging;
using Ato.Copilot.Core.Interfaces.Compliance;

namespace Ato.Copilot.Agents.Compliance.Scanners;

/// <summary>
/// Dictionary-based scanner dispatch registry. Returns the specialized scanner
/// for a given family code, falling back to <c>DefaultComplianceScanner</c>
/// for unregistered families.
/// </summary>
public class ScannerRegistry : IScannerRegistry
{
    private readonly Dictionary<string, IComplianceScanner> _scanners;
    private readonly IComplianceScanner _defaultScanner;
    private readonly ILogger<ScannerRegistry> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ScannerRegistry"/> class.
    /// </summary>
    /// <param name="scanners">All registered scanners (injected via DI).</param>
    /// <param name="logger">Logger instance.</param>
    public ScannerRegistry(
        IEnumerable<IComplianceScanner> scanners,
        ILogger<ScannerRegistry> logger)
    {
        _logger = logger;

        var scannerList = scanners.ToList();

        // Separate default scanner from specialized scanners
        _defaultScanner = scannerList.FirstOrDefault(s =>
            string.Equals(s.FamilyCode, "DEFAULT", StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException("DefaultComplianceScanner must be registered");

        _scanners = new Dictionary<string, IComplianceScanner>(StringComparer.OrdinalIgnoreCase);

        foreach (var scanner in scannerList.Where(s =>
            !string.Equals(s.FamilyCode, "DEFAULT", StringComparison.OrdinalIgnoreCase)))
        {
            _scanners[scanner.FamilyCode] = scanner;
            _logger.LogDebug("Registered scanner for family {Family}: {Type}",
                scanner.FamilyCode, scanner.GetType().Name);
        }

        _logger.LogInformation("ScannerRegistry initialized with {Count} specialized scanners + default",
            _scanners.Count);
    }

    /// <inheritdoc />
    public IComplianceScanner GetScanner(string familyCode)
    {
        if (_scanners.TryGetValue(familyCode, out var scanner))
        {
            _logger.LogDebug("Dispatching family {Family} to {Scanner}",
                familyCode, scanner.GetType().Name);
            return scanner;
        }

        _logger.LogDebug("No specialized scanner for family {Family}, using default", familyCode);
        return _defaultScanner;
    }
}
