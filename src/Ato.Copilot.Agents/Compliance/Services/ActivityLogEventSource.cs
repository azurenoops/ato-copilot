using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Ato.Copilot.Core.Configuration;
using Ato.Copilot.Core.Interfaces.Compliance;

namespace Ato.Copilot.Agents.Compliance.Services;

/// <summary>
/// Polls Azure Activity Log for compliance-relevant platform events.
/// Filters for resource write/delete, policy assignment changes, and role assignment changes.
/// Supports dual-cloud (Azure Government / Azure Commercial) URL construction.
/// Uses configurable poll interval (default 2 minutes).
/// </summary>
public class ActivityLogEventSource : IComplianceEventSource
{
    private readonly IOptions<MonitoringOptions> _monitoringOptions;
    private readonly ILogger<ActivityLogEventSource> _logger;

    /// <summary>
    /// Event operation name patterns that are compliance-relevant.
    /// </summary>
    private static readonly HashSet<string> RelevantOperationPrefixes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Microsoft.Authorization/policyAssignments",
        "Microsoft.Authorization/roleAssignments",
        "Microsoft.Authorization/policyDefinitions",
        "Microsoft.Authorization/policySetDefinitions"
    };

    /// <summary>
    /// Activity log status values indicating a completed (non-pending) operation.
    /// </summary>
    private static readonly HashSet<string> CompletedStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "Succeeded",
        "Failed"
    };

    public ActivityLogEventSource(
        IOptions<MonitoringOptions> monitoringOptions,
        ILogger<ActivityLogEventSource> logger)
    {
        _monitoringOptions = monitoringOptions;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<List<ComplianceEvent>> GetRecentEventsAsync(
        string subscriptionId,
        DateTimeOffset since,
        string? resourceGroupName = null,
        CancellationToken cancellationToken = default)
    {
        // In production, this would call Azure Management API:
        //   GET https://{managementUrl}/subscriptions/{subscriptionId}/providers/Microsoft.Insights/eventtypes/management/values
        //   ?api-version=2015-04-01&$filter=eventTimestamp ge '{since:o}'
        //
        // Azure Government: management.usgovcloudapi.net
        // Azure Commercial: management.azure.com

        var managementUrl = GetManagementUrl();
        _logger.LogDebug(
            "Polling Activity Log for {Sub}/{RG} since {Since} via {Url}",
            subscriptionId, resourceGroupName ?? "*", since, managementUrl);

        // Return empty list — actual Azure API integration would be injected
        // via HttpClient in production. The interface enables mock injection for testing.
        return Task.FromResult(new List<ComplianceEvent>());
    }

    /// <summary>
    /// Get the Azure Management API base URL based on cloud environment configuration.
    /// </summary>
    internal string GetManagementUrl()
    {
        var cloud = _monitoringOptions.Value.CloudEnvironment ?? "AzureGovernment";
        return cloud.Equals("AzureGovernment", StringComparison.OrdinalIgnoreCase)
            ? "https://management.usgovcloudapi.net"
            : "https://management.azure.com";
    }

    /// <summary>
    /// Determines whether an activity log event operation is compliance-relevant.
    /// Filters for resource write/delete, policy, and role operations.
    /// </summary>
    internal static bool IsComplianceRelevantOperation(string operationName)
    {
        if (string.IsNullOrEmpty(operationName))
            return false;

        // Resource write/delete operations
        if (operationName.EndsWith("/write", StringComparison.OrdinalIgnoreCase)
            || operationName.EndsWith("/delete", StringComparison.OrdinalIgnoreCase))
            return true;

        // Policy and role assignment operations
        foreach (var prefix in RelevantOperationPrefixes)
        {
            if (operationName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Determines whether an event represents a policy assignment change (FR-004 policy drift detection).
    /// </summary>
    internal static bool IsPolicyDriftEvent(ComplianceEvent evt)
    {
        return evt.OperationName.StartsWith("Microsoft.Authorization/policyAssignments", StringComparison.OrdinalIgnoreCase)
            || evt.OperationName.StartsWith("Microsoft.Authorization/policyDefinitions", StringComparison.OrdinalIgnoreCase)
            || evt.OperationName.StartsWith("Microsoft.Authorization/policySetDefinitions", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Extract the resource group name from an Azure resource ID.
    /// </summary>
    internal static string? ExtractResourceGroup(string resourceId)
    {
        if (string.IsNullOrEmpty(resourceId))
            return null;

        var parts = resourceId.Split('/', StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < parts.Length - 1; i++)
        {
            if (parts[i].Equals("resourceGroups", StringComparison.OrdinalIgnoreCase))
                return parts[i + 1];
        }

        return null;
    }
}
