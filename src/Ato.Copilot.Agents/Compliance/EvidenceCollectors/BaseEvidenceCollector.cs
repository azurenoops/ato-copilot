using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using Ato.Copilot.Core.Constants;
using Ato.Copilot.Core.Interfaces.Compliance;
using Ato.Copilot.Core.Models.Compliance;

namespace Ato.Copilot.Agents.Compliance.EvidenceCollectors;

/// <summary>
/// Abstract base class for evidence collectors using template method pattern.
/// Provides completeness scoring, attestation generation, content hashing,
/// and 5 evidence type collection.
/// Subclasses implement <see cref="CollectFamilyEvidenceAsync"/> for family-specific logic.
/// </summary>
public abstract class BaseEvidenceCollector : IEvidenceCollector
{
    /// <summary>Logger instance for the derived collector.</summary>
    protected readonly ILogger Logger;

    /// <summary>Expected evidence types per family (typically 5).</summary>
    protected const int ExpectedEvidenceTypes = 5;

    /// <inheritdoc />
    public abstract string FamilyCode { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="BaseEvidenceCollector"/> class.
    /// </summary>
    /// <param name="logger">Logger instance.</param>
    protected BaseEvidenceCollector(ILogger logger)
    {
        Logger = logger;
    }

    /// <inheritdoc />
    public async Task<EvidencePackage> CollectAsync(
        string subscriptionId,
        string? resourceGroup,
        CancellationToken cancellationToken = default)
    {
        var familyName = ControlFamilies.FamilyNames.TryGetValue(FamilyCode, out var name)
            ? name
            : FamilyCode;

        var package = new EvidencePackage
        {
            FamilyCode = FamilyCode,
            SubscriptionId = subscriptionId,
            ExpectedEvidenceTypes = ExpectedEvidenceTypes
        };

        var stopwatch = Stopwatch.StartNew();

        try
        {
            Logger.LogInformation("Collecting evidence for family {Family} ({Name}) Sub={Sub}",
                FamilyCode, familyName, subscriptionId);

            var items = await CollectFamilyEvidenceAsync(
                subscriptionId, resourceGroup, cancellationToken);

            // Add content hashes to each item
            foreach (var item in items)
            {
                if (string.IsNullOrEmpty(item.ContentHash) && !string.IsNullOrEmpty(item.Content))
                {
                    item.ContentHash = ComputeSha256Hash(item.Content);
                }
            }

            package.EvidenceItems = items;
            package.CollectedEvidenceTypes = items
                .Select(e => e.Type)
                .Distinct()
                .Count();
            package.CompletenessScore = package.ExpectedEvidenceTypes > 0
                ? (double)package.CollectedEvidenceTypes / package.ExpectedEvidenceTypes * 100.0
                : 0.0;
            package.Summary = $"Collected {items.Count} evidence items " +
                              $"({package.CollectedEvidenceTypes}/{package.ExpectedEvidenceTypes} types) " +
                              $"for {familyName} family.";
            package.AttestationStatement = GenerateAttestation(familyName, package);
            package.CollectedAt = DateTime.UtcNow;

            stopwatch.Stop();

            Logger.LogInformation(
                "Evidence collection for family {Family} complete: {Items} items, " +
                "{Types}/{Expected} types, {Score:F0}% completeness in {Duration}ms",
                FamilyCode, items.Count, package.CollectedEvidenceTypes,
                package.ExpectedEvidenceTypes, package.CompletenessScore,
                stopwatch.ElapsedMilliseconds);

            return package;
        }
        catch (OperationCanceledException)
        {
            Logger.LogWarning("Evidence collection for family {Family} cancelled", FamilyCode);
            throw;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            Logger.LogError(ex, "Evidence collection for family {Family} failed", FamilyCode);

            package.Summary = $"Evidence collection failed for {familyName}: {ex.Message}";
            package.AttestationStatement = $"INCOMPLETE: Evidence collection for {familyName} " +
                                           "could not be completed due to errors.";
            return package;
        }
    }

    /// <summary>
    /// Family-specific evidence collection logic. Implemented by derived classes.
    /// Should collect artifacts across all 5 evidence types.
    /// </summary>
    /// <param name="subscriptionId">Azure subscription ID.</param>
    /// <param name="resourceGroup">Optional resource group constraint.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of evidence items.</returns>
    protected abstract Task<List<EvidenceItem>> CollectFamilyEvidenceAsync(
        string subscriptionId,
        string? resourceGroup,
        CancellationToken cancellationToken);

    /// <summary>
    /// Generates a formal attestation statement for the evidence package.
    /// </summary>
    /// <param name="familyName">Human-readable family name.</param>
    /// <param name="package">The evidence package.</param>
    /// <returns>Attestation statement text.</returns>
    protected virtual string GenerateAttestation(string familyName, EvidencePackage package)
    {
        return $"This attestation certifies that evidence has been collected for the " +
               $"{familyName} control family. {package.CollectedEvidenceTypes} of " +
               $"{package.ExpectedEvidenceTypes} expected evidence types were collected, " +
               $"achieving {package.CompletenessScore:F0}% completeness. " +
               $"Evidence was collected at {package.CollectedAt:O} UTC.";
    }

    /// <summary>
    /// Computes a SHA-256 hash of the given content.
    /// </summary>
    /// <param name="content">Content to hash.</param>
    /// <returns>Lowercase hex-encoded SHA-256 hash.</returns>
    protected static string ComputeSha256Hash(string content)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(content));
        return Convert.ToHexStringLower(bytes);
    }

    /// <summary>
    /// Creates an evidence item with common fields populated.
    /// </summary>
    /// <param name="type">Evidence type.</param>
    /// <param name="title">Evidence title.</param>
    /// <param name="description">Evidence description.</param>
    /// <param name="content">Evidence content.</param>
    /// <param name="resourceId">Optional Azure resource ID.</param>
    /// <returns>Populated evidence item.</returns>
    protected static EvidenceItem CreateEvidenceItem(
        EvidenceType type,
        string title,
        string description,
        string content,
        string? resourceId = null)
    {
        return new EvidenceItem
        {
            Type = type,
            Title = title,
            Description = description,
            Content = content,
            ResourceId = resourceId,
            CollectedAt = DateTime.UtcNow,
            ContentHash = ComputeSha256Hash(content)
        };
    }
}
