using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Ato.Copilot.Core.Constants;
using Ato.Copilot.Core.Data.Context;
using Ato.Copilot.Core.Interfaces.Auth;
using Ato.Copilot.Core.Models.Auth;

namespace Ato.Copilot.Agents.Compliance.Services;

/// <summary>
/// Resolves platform roles from CAC certificate identities using a 4-tier resolution chain (FR-028):
/// 1. Explicit CertificateRoleMapping by thumbprint/subject in database
/// 2. Azure AD group membership via Graph API (simulated)
/// 3. Azure RBAC role on target subscription (simulated)
/// 4. Default to PlatformEngineer
/// </summary>
public class CertificateRoleResolver : ICertificateRoleResolver
{
    private readonly IDbContextFactory<AtoCopilotContext> _dbFactory;
    private readonly ILogger<CertificateRoleResolver> _logger;

    /// <summary>Valid role names accepted for certificate mapping.</summary>
    private static readonly HashSet<string> ValidRoles = new(StringComparer.OrdinalIgnoreCase)
    {
        ComplianceRoles.Administrator,
        ComplianceRoles.Auditor,
        ComplianceRoles.Analyst,
        ComplianceRoles.Viewer,
        ComplianceRoles.SecurityLead,
        ComplianceRoles.PlatformEngineer
    };

    /// <summary>
    /// Initializes a new instance of the <see cref="CertificateRoleResolver"/> class.
    /// </summary>
    public CertificateRoleResolver(
        IDbContextFactory<AtoCopilotContext> dbFactory,
        ILogger<CertificateRoleResolver> logger)
    {
        _dbFactory = dbFactory;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<string> ResolveRoleAsync(
        string? thumbprint, string? subject, string userId,
        CancellationToken cancellationToken = default)
    {
        // Tier 1: Check CertificateRoleMapping table by thumbprint/subject
        var mappedRole = await ResolveFromMappingAsync(thumbprint, subject, cancellationToken);
        if (mappedRole != null)
        {
            _logger.LogInformation(
                "Certificate role resolved via explicit mapping for user {UserId}: {Role}",
                userId, mappedRole);
            return mappedRole;
        }

        // Tier 2: Check Azure AD group membership via Graph API (simulated)
        var adGroupRole = await ResolveFromAdGroupsAsync(userId, cancellationToken);
        if (adGroupRole != null)
        {
            _logger.LogInformation(
                "Certificate role resolved via AD group membership for user {UserId}: {Role}",
                userId, adGroupRole);
            return adGroupRole;
        }

        // Tier 3: Check Azure RBAC on target subscription (simulated)
        var rbacRole = await ResolveFromRbacAsync(userId, cancellationToken);
        if (rbacRole != null)
        {
            _logger.LogInformation(
                "Certificate role resolved via Azure RBAC for user {UserId}: {Role}",
                userId, rbacRole);
            return rbacRole;
        }

        // Tier 4: Default to PlatformEngineer
        _logger.LogInformation(
            "No explicit role mapping found for user {UserId}, defaulting to {Role}",
            userId, ComplianceRoles.PlatformEngineer);
        return ComplianceRoles.PlatformEngineer;
    }

    /// <inheritdoc />
    public async Task<CertificateRoleMapping> MapCertificateAsync(
        string thumbprint, string subject, string role, string createdBy,
        CancellationToken cancellationToken = default)
    {
        if (!ValidRoles.Contains(role))
        {
            _logger.LogWarning("Certificate mapping failed: invalid role '{Role}' for thumbprint={Thumbprint}", role, thumbprint);
            throw new ArgumentException(
                $"Invalid role '{role}'. Must be one of: {string.Join(", ", ValidRoles)}", nameof(role));
        }

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        // Check for existing mapping by thumbprint
        var existing = await db.CertificateRoleMappings
            .FirstOrDefaultAsync(m =>
                m.CertificateThumbprint == thumbprint && m.IsActive,
                cancellationToken);

        if (existing != null)
        {
            // Update existing mapping
            existing.MappedRole = role;
            existing.CertificateSubject = subject;
            await db.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Updated certificate mapping for thumbprint {Thumbprint} to role {Role}",
                thumbprint, role);
            return existing;
        }

        // Create new mapping
        var mapping = new CertificateRoleMapping
        {
            CertificateThumbprint = thumbprint,
            CertificateSubject = subject,
            MappedRole = role,
            CreatedBy = createdBy,
            IsActive = true
        };

        db.CertificateRoleMappings.Add(mapping);
        await db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Created certificate mapping for thumbprint {Thumbprint} → {Role} by {CreatedBy}",
            thumbprint, role, createdBy);
        return mapping;
    }

    /// <summary>
    /// Checks if a role string is a valid ComplianceRoles constant.
    /// </summary>
    public static bool IsValidRole(string role) => ValidRoles.Contains(role);

    // ─── Private Resolution Methods ──────────────────────────────────────

    private async Task<string?> ResolveFromMappingAsync(
        string? thumbprint, string? subject, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(thumbprint) && string.IsNullOrEmpty(subject))
            return null;

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        // Try thumbprint first, then subject
        CertificateRoleMapping? mapping = null;

        if (!string.IsNullOrEmpty(thumbprint))
        {
            mapping = await db.CertificateRoleMappings
                .FirstOrDefaultAsync(m =>
                    m.CertificateThumbprint == thumbprint && m.IsActive,
                    cancellationToken);
        }

        if (mapping == null && !string.IsNullOrEmpty(subject))
        {
            mapping = await db.CertificateRoleMappings
                .FirstOrDefaultAsync(m =>
                    m.CertificateSubject == subject && m.IsActive,
                    cancellationToken);
        }

        return mapping?.MappedRole;
    }

    /// <summary>
    /// Simulated Azure AD group membership check. In production, this would use
    /// GraphServiceClient to check the user's group memberships against known
    /// compliance role groups.
    /// </summary>
    private Task<string?> ResolveFromAdGroupsAsync(string userId, CancellationToken cancellationToken)
    {
        // Simulated: No AD group resolution in dev mode
        // In production: GraphServiceClient.Users[userId].MemberOf.GetAsync()
        // then map group display names to ComplianceRoles
        _logger.LogDebug("AD group resolution skipped (simulated) for user {UserId}", userId);
        return Task.FromResult<string?>(null);
    }

    /// <summary>
    /// Simulated Azure RBAC role check. In production, this would use
    /// Azure.ResourceManager to check the user's RBAC assignments on the
    /// target subscription.
    /// </summary>
    private Task<string?> ResolveFromRbacAsync(string userId, CancellationToken cancellationToken)
    {
        // Simulated: No RBAC resolution in dev mode
        // In production: Check AuthorizationManagementClient for role assignments
        _logger.LogDebug("Azure RBAC resolution skipped (simulated) for user {UserId}", userId);
        return Task.FromResult<string?>(null);
    }
}
