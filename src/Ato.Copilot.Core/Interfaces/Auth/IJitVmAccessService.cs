using Ato.Copilot.Core.Models.Auth;

namespace Ato.Copilot.Core.Interfaces.Auth;

/// <summary>
/// Service interface for Just-in-Time VM access via Azure Defender for Cloud.
/// Creates temporary NSG rules for SSH/RDP access with auto-revocation.
/// All async methods accept CancellationToken per Constitution VIII.
/// </summary>
public interface IJitVmAccessService
{
    /// <summary>
    /// Requests JIT VM access by creating a temporary NSG rule.
    /// </summary>
    /// <param name="userId">Azure AD object ID of the requesting user.</param>
    /// <param name="vmName">Target VM name.</param>
    /// <param name="resourceGroup">Resource group containing the VM.</param>
    /// <param name="subscriptionId">Subscription ID (uses default if null).</param>
    /// <param name="port">Port number (default: 22 for SSH).</param>
    /// <param name="protocol">Protocol: ssh or rdp.</param>
    /// <param name="sourceIp">Source IP (auto-detected if null).</param>
    /// <param name="durationHours">Access duration in hours.</param>
    /// <param name="justification">Justification for access.</param>
    /// <param name="ticketNumber">Optional ticket reference.</param>
    /// <param name="sessionId">CAC session ID.</param>
    /// <param name="conversationId">Conversation ID for audit.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>JIT access result with connection details.</returns>
    Task<JitAccessResult> RequestAccessAsync(
        string userId, string vmName, string resourceGroup,
        string? subscriptionId, int port, string protocol,
        string? sourceIp, int durationHours,
        string justification, string? ticketNumber,
        Guid sessionId, string? conversationId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists active JIT VM access sessions for the user.
    /// </summary>
    /// <param name="userId">Azure AD object ID of the user.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of active JIT sessions.</returns>
    Task<List<JitActiveSession>> ListActiveSessionsAsync(
        string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Revokes JIT VM access by removing the NSG rule.
    /// </summary>
    /// <param name="userId">Azure AD object ID of the user.</param>
    /// <param name="vmName">VM name to revoke access for.</param>
    /// <param name="resourceGroup">Resource group containing the VM.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Revocation result.</returns>
    Task<JitRevokeResult> RevokeAccessAsync(
        string userId, string vmName, string resourceGroup,
        CancellationToken cancellationToken = default);
}

/// <summary>Result of a JIT VM access request.</summary>
public class JitAccessResult
{
    /// <summary>Whether the JIT access request succeeded.</summary>
    public bool Success { get; set; }
    /// <summary>JIT request identifier for tracking.</summary>
    public string JitRequestId { get; set; } = string.Empty;
    /// <summary>Name of the target virtual machine.</summary>
    public string VmName { get; set; } = string.Empty;
    /// <summary>Azure resource group containing the VM.</summary>
    public string ResourceGroup { get; set; } = string.Empty;
    /// <summary>Network port opened for access.</summary>
    public int Port { get; set; }
    /// <summary>Network protocol (e.g., TCP, UDP).</summary>
    public string Protocol { get; set; } = string.Empty;
    /// <summary>Source IP address allowed for access.</summary>
    public string SourceIp { get; set; } = string.Empty;
    /// <summary>Timestamp when JIT access was activated.</summary>
    public DateTimeOffset ActivatedAt { get; set; }
    /// <summary>Timestamp when JIT access expires.</summary>
    public DateTimeOffset ExpiresAt { get; set; }
    /// <summary>Duration of the JIT access in hours.</summary>
    public int DurationHours { get; set; }
    /// <summary>Pre-formatted connection command for the user.</summary>
    public string ConnectionCommand { get; set; } = string.Empty;
    /// <summary>Error code if the request failed.</summary>
    public string? ErrorCode { get; set; }
    /// <summary>Human-readable result message.</summary>
    public string Message { get; set; } = string.Empty;
}

/// <summary>Represents an active JIT VM access session.</summary>
public class JitActiveSession
{
    /// <summary>JIT request identifier for tracking.</summary>
    public string JitRequestId { get; set; } = string.Empty;
    /// <summary>Name of the target virtual machine.</summary>
    public string VmName { get; set; } = string.Empty;
    /// <summary>Azure resource group containing the VM.</summary>
    public string ResourceGroup { get; set; } = string.Empty;
    /// <summary>Network port opened for access.</summary>
    public int Port { get; set; }
    /// <summary>Source IP address allowed for access.</summary>
    public string SourceIp { get; set; } = string.Empty;
    /// <summary>Timestamp when JIT access was activated.</summary>
    public DateTimeOffset ActivatedAt { get; set; }
    /// <summary>Timestamp when JIT access expires.</summary>
    public DateTimeOffset ExpiresAt { get; set; }
    /// <summary>Minutes remaining until access expires.</summary>
    public int RemainingMinutes { get; set; }
}

/// <summary>Result of a JIT VM access revocation.</summary>
public class JitRevokeResult
{
    /// <summary>Whether the JIT access was successfully revoked.</summary>
    public bool Revoked { get; set; }
    /// <summary>Name of the virtual machine.</summary>
    public string VmName { get; set; } = string.Empty;
    /// <summary>Azure resource group containing the VM.</summary>
    public string ResourceGroup { get; set; } = string.Empty;
    /// <summary>Timestamp when access was revoked.</summary>
    public DateTimeOffset RevokedAt { get; set; }
    /// <summary>Error code if revocation failed.</summary>
    public string? ErrorCode { get; set; }
    /// <summary>Human-readable result message.</summary>
    public string Message { get; set; } = string.Empty;
}
