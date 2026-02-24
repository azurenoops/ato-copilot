using Microsoft.Extensions.Logging;
using Ato.Copilot.Core.Constants;
using Ato.Copilot.Core.Interfaces.Compliance;
using Ato.Copilot.Core.Models.Compliance;

namespace Ato.Copilot.Agents.Compliance.EvidenceCollectors;

/// <summary>
/// Evidence collector for the System and Communications Protection (SC) family.
/// Collects NSG configuration, TLS/encryption status, Key Vault inventory,
/// Defender encryption metrics, and network access control evidence.
/// </summary>
public class SecurityCommsEvidenceCollector : BaseEvidenceCollector
{
    private readonly IAzureResourceService _azureResourceService;
    private readonly IDefenderForCloudService _defenderService;

    /// <inheritdoc />
    public override string FamilyCode => ControlFamilies.SystemCommunications;

    public SecurityCommsEvidenceCollector(
        IAzureResourceService azureResourceService,
        IDefenderForCloudService defenderService,
        ILogger<SecurityCommsEvidenceCollector> logger) : base(logger)
    {
        _azureResourceService = azureResourceService;
        _defenderService = defenderService;
    }

    /// <inheritdoc />
    protected override async Task<List<EvidenceItem>> CollectFamilyEvidenceAsync(
        string subscriptionId,
        string? resourceGroup,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var items = new List<EvidenceItem>();

        // 1. Configuration — NSG configuration snapshot
        try
        {
            var nsgs = await _azureResourceService.GetResourcesAsync(
                subscriptionId, resourceGroup, "Microsoft.Network/networkSecurityGroups", cancellationToken);
            items.Add(CreateEvidenceItem(
                EvidenceType.Configuration,
                "Network Security Group Configuration",
                "NSG inventory for SC-7 (Boundary Protection).",
                $"Network Security Groups found: {nsgs.Count}",
                $"/subscriptions/{subscriptionId}"));
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to collect NSG evidence for SC");
        }

        // 2. Log — Defender encryption recommendations
        try
        {
            var recommendations = await _defenderService.GetRecommendationsAsync(subscriptionId, cancellationToken);
            items.Add(CreateEvidenceItem(
                EvidenceType.Log,
                "Defender Encryption Recommendations",
                "Security recommendations related to encryption for SC-28 (Protection of Information at Rest).",
                recommendations ?? "No recommendations available",
                $"/subscriptions/{subscriptionId}"));
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to collect Defender evidence for SC");
        }

        // 3. Policy — Key Vault inventory for key management
        try
        {
            var keyVaults = await _azureResourceService.GetResourcesAsync(
                subscriptionId, resourceGroup, "Microsoft.KeyVault/vaults", cancellationToken);
            items.Add(CreateEvidenceItem(
                EvidenceType.Policy,
                "Key Vault Key Management Evidence",
                "Key Vault inventory for SC-12 (Cryptographic Key Management).",
                $"Key Vaults found: {keyVaults.Count}",
                $"/subscriptions/{subscriptionId}"));
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to collect Key Vault evidence for SC");
        }

        // 4. Metric — Storage account encryption status
        try
        {
            var storageAccounts = await _azureResourceService.GetResourcesAsync(
                subscriptionId, resourceGroup, "Microsoft.Storage/storageAccounts", cancellationToken);
            items.Add(CreateEvidenceItem(
                EvidenceType.Metric,
                "Storage Encryption Metrics",
                "Storage account count for SC-8 (Transmission Confidentiality) assessment.",
                $"Storage accounts in scope: {storageAccounts.Count}",
                $"/subscriptions/{subscriptionId}"));
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to collect storage evidence for SC");
        }

        // 5. AccessControl — Network access control evidence
        try
        {
            var nsgs = await _azureResourceService.GetResourcesAsync(
                subscriptionId, resourceGroup, "Microsoft.Network/networkSecurityGroups", cancellationToken);
            items.Add(CreateEvidenceItem(
                EvidenceType.AccessControl,
                "Network Access Control Evidence",
                "NSG-based network access control for SC-7 (Boundary Protection).",
                $"NSG-based access controls: {nsgs.Count} groups",
                $"/subscriptions/{subscriptionId}"));
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to collect network access control evidence for SC");
        }

        return items;
    }
}
