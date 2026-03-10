namespace Ato.Copilot.Agents.Compliance.Services;

/// <summary>
/// Validates OSCAL SSP JSON for structural correctness (Feature 022).
/// </summary>
public interface IOscalValidationService
{
    Task<OscalValidationResult> ValidateSspAsync(
        string oscalJson,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of OSCAL SSP structural validation.
/// </summary>
public record OscalValidationResult(
    bool IsValid,
    List<string> Errors,
    List<string> Warnings,
    OscalStatistics Statistics);
