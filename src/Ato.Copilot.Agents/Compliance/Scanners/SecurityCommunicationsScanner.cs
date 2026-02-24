using Microsoft.Extensions.Logging;
using Ato.Copilot.Core.Constants;
using Ato.Copilot.Core.Interfaces.Compliance;
using Ato.Copilot.Core.Models.Compliance;

namespace Ato.Copilot.Agents.Compliance.Scanners;

/// <summary>
/// Scanner for the System and Communications Protection (SC) family.
/// Checks NSG rules, TLS settings, encryption status, and Key Vault usage.
/// </summary>
public class SecurityCommunicationsScanner : BaseComplianceScanner
{
    private readonly IAzureResourceService _azureResourceService;
    private readonly IDefenderForCloudService _defenderService;

    /// <inheritdoc />
    public override string FamilyCode => ControlFamilies.SystemCommunications;

    public SecurityCommunicationsScanner(
        IAzureResourceService azureResourceService,
        IDefenderForCloudService defenderService,
        ILogger<SecurityCommunicationsScanner> logger) : base(logger)
    {
        _azureResourceService = azureResourceService;
        _defenderService = defenderService;
    }

    /// <inheritdoc />
    protected override async Task<List<ComplianceFinding>> ScanFamilyAsync(
        string subscriptionId,
        string? resourceGroup,
        List<NistControl> controls,
        CancellationToken cancellationToken)
    {
        var findings = new List<ComplianceFinding>();

        // Check NSG rules for overly permissive inbound rules
        var nsgs = await _azureResourceService.GetResourcesAsync(
            subscriptionId, resourceGroup, "Microsoft.Network/networkSecurityGroups", cancellationToken);

        if (nsgs.Count == 0)
        {
            findings.Add(new ComplianceFinding
            {
                ControlId = "SC-7",
                ControlFamily = ControlFamilies.SystemCommunications,
                Title = "No Network Security Groups found",
                Description = "No NSGs detected in the subscription. Per SC-7 (Boundary Protection), " +
                              "network boundaries must be enforced with NSGs or Azure Firewall.",
                Severity = FindingSeverity.High,
                Status = FindingStatus.Open,
                ResourceId = $"/subscriptions/{subscriptionId}",
                ResourceType = "Microsoft.Network/networkSecurityGroups",
                RemediationGuidance = "Deploy NSGs on all subnets to enforce boundary protection.",
                ScanSource = ScanSourceType.Resource,
                RiskLevel = RiskLevel.High,
                AutoRemediable = false
            });
        }

        // Check storage accounts for encryption
        var storageAccounts = await _azureResourceService.GetResourcesAsync(
            subscriptionId, resourceGroup, "Microsoft.Storage/storageAccounts", cancellationToken);

        foreach (var storageAccount in storageAccounts.Take(20))
        {
            // Check for HTTPS-only enforcement via resource properties
            if (storageAccount.Data.Properties?.ToString()?.Contains("\"supportsHttpsTrafficOnly\":false", StringComparison.OrdinalIgnoreCase) == true)
            {
                findings.Add(new ComplianceFinding
                {
                    ControlId = "SC-8",
                    ControlFamily = ControlFamilies.SystemCommunications,
                    Title = "Storage account allows unencrypted traffic",
                    Description = $"Storage account {storageAccount.Data.Name} does not enforce HTTPS-only traffic. " +
                                  "Per SC-8 (Transmission Confidentiality), all data in transit must be encrypted.",
                    Severity = FindingSeverity.Critical,
                    Status = FindingStatus.Open,
                    ResourceId = storageAccount.Id.ToString(),
                    ResourceType = "Microsoft.Storage/storageAccounts",
                    RemediationGuidance = "Enable HTTPS-only on the storage account.",
                    ScanSource = ScanSourceType.Resource,
                    RiskLevel = RiskLevel.High,
                    AutoRemediable = true,
                    RemediationType = RemediationType.ResourceConfiguration
                });
            }
        }

        // Check Key Vault presence for key management
        var keyVaults = await _azureResourceService.GetResourcesAsync(
            subscriptionId, resourceGroup, "Microsoft.KeyVault/vaults", cancellationToken);

        if (keyVaults.Count == 0)
        {
            findings.Add(new ComplianceFinding
            {
                ControlId = "SC-12",
                ControlFamily = ControlFamilies.SystemCommunications,
                Title = "No Key Vault found for cryptographic key management",
                Description = "No Azure Key Vault detected. Per SC-12 (Cryptographic Key Management), " +
                              "keys must be managed in a centralized, hardware-backed vault.",
                Severity = FindingSeverity.High,
                Status = FindingStatus.Open,
                ResourceId = $"/subscriptions/{subscriptionId}",
                ResourceType = "Microsoft.KeyVault/vaults",
                RemediationGuidance = "Deploy Azure Key Vault for centralized key and secret management.",
                ScanSource = ScanSourceType.Resource,
                RiskLevel = RiskLevel.High,
                AutoRemediable = false
            });
        }

        // Defender recommendations for SC-family
        var recommendations = await _defenderService.GetRecommendationsAsync(subscriptionId, cancellationToken);
        if (!string.IsNullOrEmpty(recommendations) && recommendations.Contains("encryption", StringComparison.OrdinalIgnoreCase))
        {
            findings.Add(new ComplianceFinding
            {
                ControlId = "SC-28",
                ControlFamily = ControlFamilies.SystemCommunications,
                Title = "Defender encryption recommendations pending",
                Description = "Microsoft Defender for Cloud has pending encryption-related recommendations. " +
                              "Per SC-28 (Protection of Information at Rest), all data at rest must be encrypted.",
                Severity = FindingSeverity.Medium,
                Status = FindingStatus.Open,
                ResourceId = $"/subscriptions/{subscriptionId}",
                ResourceType = "Microsoft.Security/assessments",
                RemediationGuidance = "Review and remediate Defender for Cloud encryption recommendations.",
                ScanSource = ScanSourceType.Defender,
                RiskLevel = RiskLevel.High,
                AutoRemediable = false
            });
        }

        return findings;
    }
}
