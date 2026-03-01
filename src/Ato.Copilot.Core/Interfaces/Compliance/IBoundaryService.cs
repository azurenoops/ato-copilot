using Ato.Copilot.Core.Models.Compliance;

namespace Ato.Copilot.Core.Interfaces.Compliance;

/// <summary>
/// Service for managing authorization boundaries and RMF role assignments
/// for registered systems.
/// </summary>
public interface IBoundaryService
{
    /// <summary>
    /// Define the authorization boundary by adding resources to a registered system.
    /// </summary>
    /// <param name="systemId">System ID.</param>
    /// <param name="resources">Resources to include in the boundary.</param>
    /// <param name="addedBy">Identity of the user adding the resources.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of created boundary entries.</returns>
    Task<IReadOnlyList<AuthorizationBoundary>> DefineBoundaryAsync(
        string systemId,
        IEnumerable<BoundaryResourceInput> resources,
        string addedBy,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Exclude a resource from the authorization boundary with rationale.
    /// </summary>
    /// <param name="systemId">System ID.</param>
    /// <param name="resourceId">Azure resource ID to exclude.</param>
    /// <param name="rationale">Justification for exclusion.</param>
    /// <param name="userId">Identity of the user performing the exclusion.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The updated boundary entry.</returns>
    Task<AuthorizationBoundary?> ExcludeResourceAsync(
        string systemId,
        string resourceId,
        string rationale,
        string userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get the authorization boundary for a registered system.
    /// </summary>
    /// <param name="systemId">System ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of boundary resources.</returns>
    Task<IReadOnlyList<AuthorizationBoundary>> GetBoundaryAsync(
        string systemId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Assign an RMF role to a user for a registered system.
    /// </summary>
    /// <param name="systemId">System ID.</param>
    /// <param name="role">RMF role to assign.</param>
    /// <param name="userId">User identity to assign the role to.</param>
    /// <param name="userDisplayName">Display name of the user.</param>
    /// <param name="assignedBy">Identity of the user performing the assignment.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created role assignment.</returns>
    Task<RmfRoleAssignment> AssignRmfRoleAsync(
        string systemId,
        RmfRole role,
        string userId,
        string? userDisplayName,
        string assignedBy,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// List RMF role assignments for a registered system.
    /// </summary>
    /// <param name="systemId">System ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of role assignments.</returns>
    Task<IReadOnlyList<RmfRoleAssignment>> ListRmfRolesAsync(
        string systemId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Input for adding a resource to the authorization boundary.
/// </summary>
public class BoundaryResourceInput
{
    /// <summary>Azure resource ID.</summary>
    public string ResourceId { get; set; } = string.Empty;

    /// <summary>Azure resource type (e.g., "Microsoft.Compute/virtualMachines").</summary>
    public string ResourceType { get; set; } = string.Empty;

    /// <summary>Display name for the resource.</summary>
    public string? ResourceName { get; set; }

    /// <summary>Inheritance provider (CSP) if applicable.</summary>
    public string? InheritanceProvider { get; set; }
}
