namespace Ato.Copilot.Core.Interfaces.Compliance;

/// <summary>
/// Validates that Azure resource IDs exist in the configured Azure environment.
/// Used by <see cref="IBoundaryService"/> when the <c>ValidateAzureResources</c>
/// feature flag is enabled.
/// </summary>
public interface IAzureResourceValidator
{
    /// <summary>
    /// Validates that a single Azure resource ID exists and is accessible.
    /// </summary>
    /// <param name="resourceId">Full ARM resource ID (e.g., /subscriptions/.../resourceGroups/...).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A validation result indicating whether the resource exists.</returns>
    Task<AzureResourceValidationResult> ValidateResourceAsync(
        string resourceId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates multiple Azure resource IDs in a batch.
    /// </summary>
    /// <param name="resourceIds">Collection of full ARM resource IDs.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Validation results keyed by resource ID.</returns>
    Task<IReadOnlyDictionary<string, AzureResourceValidationResult>> ValidateResourcesAsync(
        IEnumerable<string> resourceIds,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of validating an Azure resource ID.
/// </summary>
public class AzureResourceValidationResult
{
    /// <summary>Whether the resource exists and is accessible.</summary>
    public bool IsValid { get; init; }

    /// <summary>The validated resource ID.</summary>
    public string ResourceId { get; init; } = string.Empty;

    /// <summary>The resolved resource type (e.g., "Microsoft.Compute/virtualMachines"), if available.</summary>
    public string? ResourceType { get; init; }

    /// <summary>The resource display name, if available.</summary>
    public string? ResourceName { get; init; }

    /// <summary>Error message if validation failed.</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>Creates a successful validation result.</summary>
    public static AzureResourceValidationResult Valid(string resourceId, string? resourceType = null, string? resourceName = null) =>
        new() { IsValid = true, ResourceId = resourceId, ResourceType = resourceType, ResourceName = resourceName };

    /// <summary>Creates a failed validation result.</summary>
    public static AzureResourceValidationResult Invalid(string resourceId, string errorMessage) =>
        new() { IsValid = false, ResourceId = resourceId, ErrorMessage = errorMessage };
}
