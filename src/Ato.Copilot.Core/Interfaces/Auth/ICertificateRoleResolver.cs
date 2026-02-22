namespace Ato.Copilot.Core.Interfaces.Auth;

/// <summary>
/// Resolves a platform role from a CAC certificate identity using a 4-tier resolution chain (FR-028):
/// 1. Explicit CertificateRoleMapping by thumbprint/subject
/// 2. Azure AD group membership via Graph API
/// 3. Azure RBAC role on target subscription
/// 4. Default to PlatformEngineer
/// </summary>
public interface ICertificateRoleResolver
{
    /// <summary>
    /// Resolves the platform role for a given certificate identity.
    /// </summary>
    /// <param name="thumbprint">SHA-1 thumbprint of the CAC certificate.</param>
    /// <param name="subject">Subject DN of the CAC certificate (e.g., CN=LAST.FIRST.MI.DOD_ID).</param>
    /// <param name="userId">Azure AD object ID for AD group and RBAC lookups.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The resolved platform role name (ComplianceRoles constant).</returns>
    Task<string> ResolveRoleAsync(
        string? thumbprint, string? subject, string userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates or updates a certificate-to-role mapping in the database.
    /// </summary>
    /// <param name="thumbprint">SHA-1 thumbprint of the CAC certificate.</param>
    /// <param name="subject">Subject DN of the CAC certificate.</param>
    /// <param name="role">Platform role to map (must be a valid ComplianceRoles constant).</param>
    /// <param name="createdBy">User ID or display name of the admin creating the mapping.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created or updated CertificateRoleMapping.</returns>
    Task<Models.Auth.CertificateRoleMapping> MapCertificateAsync(
        string thumbprint, string subject, string role, string createdBy,
        CancellationToken cancellationToken = default);
}
