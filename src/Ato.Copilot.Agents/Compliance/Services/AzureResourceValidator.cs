using Azure;
using Azure.ResourceManager;
using Microsoft.Extensions.Logging;
using Ato.Copilot.Core.Interfaces.Compliance;

namespace Ato.Copilot.Agents.Compliance.Services;

/// <summary>
/// Validates Azure resource IDs against the Azure Resource Manager API.
/// Uses the singleton <see cref="ArmClient"/> registered during startup.
/// Failures (network, auth, non-existent) are reported per-resource, never throw.
/// </summary>
public class AzureResourceValidator : IAzureResourceValidator
{
    private readonly ArmClient _armClient;
    private readonly ILogger<AzureResourceValidator> _logger;

    public AzureResourceValidator(
        ArmClient armClient,
        ILogger<AzureResourceValidator> logger)
    {
        _armClient = armClient;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<AzureResourceValidationResult> ValidateResourceAsync(
        string resourceId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(resourceId))
            return AzureResourceValidationResult.Invalid(resourceId, "Resource ID is empty.");

        var trimmed = resourceId.Trim();

        // Basic ARM resource ID format check
        if (!trimmed.StartsWith("/subscriptions/", StringComparison.OrdinalIgnoreCase))
            return AzureResourceValidationResult.Invalid(trimmed,
                "Invalid ARM resource ID format. Expected: /subscriptions/{subId}/resourceGroups/{rg}/providers/{provider}/{type}/{name}");

        try
        {
            var identifier = new Azure.Core.ResourceIdentifier(trimmed);
            var resource = _armClient.GetGenericResource(identifier);
            var response = await resource.GetAsync(cancellationToken);

            var data = response.Value.Data;
            _logger.LogDebug(
                "Validated Azure resource: {ResourceId} → {ResourceType} ({Name})",
                trimmed, data.ResourceType.ToString(), data.Name);

            return AzureResourceValidationResult.Valid(
                trimmed,
                data.ResourceType.ToString(),
                data.Name);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            _logger.LogWarning("Azure resource not found: {ResourceId}", trimmed);
            return AzureResourceValidationResult.Invalid(trimmed,
                $"Azure resource not found (404). Verify the resource ID is correct and the resource exists.");
        }
        catch (RequestFailedException ex) when (ex.Status == 403)
        {
            _logger.LogWarning("Access denied for Azure resource: {ResourceId} — {Error}", trimmed, ex.Message);
            return AzureResourceValidationResult.Invalid(trimmed,
                $"Access denied (403). The service principal does not have read access to this resource.");
        }
        catch (RequestFailedException ex)
        {
            _logger.LogWarning(ex, "Azure ARM request failed for {ResourceId}: {Status} {Code}",
                trimmed, ex.Status, ex.ErrorCode);
            return AzureResourceValidationResult.Invalid(trimmed,
                $"Azure validation failed: {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error validating Azure resource {ResourceId}", trimmed);
            return AzureResourceValidationResult.Invalid(trimmed,
                $"Validation error: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<string, AzureResourceValidationResult>> ValidateResourcesAsync(
        IEnumerable<string> resourceIds,
        CancellationToken cancellationToken = default)
    {
        var results = new Dictionary<string, AzureResourceValidationResult>();
        var ids = resourceIds.ToList();

        // Validate all resources concurrently (bounded to avoid throttling)
        var semaphore = new SemaphoreSlim(5); // max 5 concurrent ARM calls
        var tasks = ids.Select(async id =>
        {
            await semaphore.WaitAsync(cancellationToken);
            try
            {
                return (Id: id.Trim(), Result: await ValidateResourceAsync(id, cancellationToken));
            }
            finally
            {
                semaphore.Release();
            }
        });

        var completed = await Task.WhenAll(tasks);
        foreach (var (id, result) in completed)
        {
            results[id] = result;
        }

        var valid = results.Count(r => r.Value.IsValid);
        var invalid = results.Count(r => !r.Value.IsValid);

        _logger.LogInformation(
            "Azure resource validation complete: {Valid} valid, {Invalid} invalid out of {Total}",
            valid, invalid, results.Count);

        return results;
    }
}
