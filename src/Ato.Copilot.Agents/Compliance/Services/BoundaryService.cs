using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Ato.Copilot.Core.Data.Context;
using Ato.Copilot.Core.Interfaces.Compliance;
using Ato.Copilot.Core.Models.Compliance;

namespace Ato.Copilot.Agents.Compliance.Services;

/// <summary>
/// Implements authorization boundary management and RMF role assignments
/// for registered systems.
/// </summary>
public class BoundaryService : IBoundaryService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<BoundaryService> _logger;

    public BoundaryService(
        IServiceScopeFactory scopeFactory,
        ILogger<BoundaryService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AuthorizationBoundary>> DefineBoundaryAsync(
        string systemId,
        IEnumerable<BoundaryResourceInput> resources,
        string addedBy,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(systemId, nameof(systemId));
        ArgumentNullException.ThrowIfNull(resources, nameof(resources));
        ArgumentException.ThrowIfNullOrWhiteSpace(addedBy, nameof(addedBy));

        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AtoCopilotContext>();

        // Verify system exists
        var systemExists = await context.RegisteredSystems
            .AnyAsync(s => s.Id == systemId, cancellationToken);

        if (!systemExists)
            throw new InvalidOperationException($"System '{systemId}' not found.");

        var resourceList = resources.ToList();
        if (resourceList.Count == 0)
            throw new ArgumentException("At least one resource is required.", nameof(resources));

        var entries = new List<AuthorizationBoundary>();

        foreach (var r in resourceList)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(r.ResourceId, nameof(r.ResourceId));
            ArgumentException.ThrowIfNullOrWhiteSpace(r.ResourceType, nameof(r.ResourceType));

            // Check for duplicate resource in this system's boundary
            var existing = await context.AuthorizationBoundaries
                .FirstOrDefaultAsync(
                    b => b.RegisteredSystemId == systemId && b.ResourceId == r.ResourceId,
                    cancellationToken);

            if (existing != null)
            {
                // Re-include if previously excluded
                if (!existing.IsInBoundary)
                {
                    existing.IsInBoundary = true;
                    existing.ExclusionRationale = null;
                }

                existing.ResourceType = r.ResourceType;
                existing.ResourceName = r.ResourceName;
                existing.InheritanceProvider = r.InheritanceProvider;
                entries.Add(existing);
            }
            else
            {
                var boundary = new AuthorizationBoundary
                {
                    RegisteredSystemId = systemId,
                    ResourceId = r.ResourceId.Trim(),
                    ResourceType = r.ResourceType.Trim(),
                    ResourceName = r.ResourceName?.Trim(),
                    InheritanceProvider = r.InheritanceProvider?.Trim(),
                    IsInBoundary = true,
                    AddedBy = addedBy
                };

                context.AuthorizationBoundaries.Add(boundary);
                entries.Add(boundary);
            }
        }

        await context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Defined boundary for system {SystemId}: {Count} resource(s) added by {AddedBy}",
            systemId, entries.Count, addedBy);

        return entries;
    }

    /// <inheritdoc />
    public async Task<AuthorizationBoundary?> ExcludeResourceAsync(
        string systemId,
        string resourceId,
        string rationale,
        string userId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(systemId, nameof(systemId));
        ArgumentException.ThrowIfNullOrWhiteSpace(resourceId, nameof(resourceId));
        ArgumentException.ThrowIfNullOrWhiteSpace(rationale, nameof(rationale));

        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AtoCopilotContext>();

        var boundary = await context.AuthorizationBoundaries
            .FirstOrDefaultAsync(
                b => b.RegisteredSystemId == systemId && b.ResourceId == resourceId,
                cancellationToken);

        if (boundary == null)
            return null;

        boundary.IsInBoundary = false;
        boundary.ExclusionRationale = rationale.Trim();

        await context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Excluded resource {ResourceId} from boundary of system {SystemId} by {UserId}",
            resourceId, systemId, userId);

        return boundary;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AuthorizationBoundary>> GetBoundaryAsync(
        string systemId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(systemId, nameof(systemId));

        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AtoCopilotContext>();

        return await context.AuthorizationBoundaries
            .Where(b => b.RegisteredSystemId == systemId)
            .OrderBy(b => b.ResourceType)
            .ThenBy(b => b.ResourceName)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<RmfRoleAssignment> AssignRmfRoleAsync(
        string systemId,
        RmfRole role,
        string userId,
        string? userDisplayName,
        string assignedBy,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(systemId, nameof(systemId));
        ArgumentException.ThrowIfNullOrWhiteSpace(userId, nameof(userId));
        ArgumentException.ThrowIfNullOrWhiteSpace(assignedBy, nameof(assignedBy));

        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AtoCopilotContext>();

        var systemExists = await context.RegisteredSystems
            .AnyAsync(s => s.Id == systemId, cancellationToken);

        if (!systemExists)
            throw new InvalidOperationException($"System '{systemId}' not found.");

        // Check if user already has this role for this system
        var existing = await context.RmfRoleAssignments
            .FirstOrDefaultAsync(
                r => r.RegisteredSystemId == systemId
                     && r.UserId == userId
                     && r.RmfRole == role,
                cancellationToken);

        if (existing != null)
        {
            // Reactivate if previously deactivated
            if (!existing.IsActive)
            {
                existing.IsActive = true;
                await context.SaveChangesAsync(cancellationToken);
            }

            return existing;
        }

        var assignment = new RmfRoleAssignment
        {
            RegisteredSystemId = systemId,
            RmfRole = role,
            UserId = userId,
            UserDisplayName = userDisplayName?.Trim(),
            AssignedBy = assignedBy,
            IsActive = true
        };

        context.RmfRoleAssignments.Add(assignment);
        await context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Assigned role {Role} to {UserId} for system {SystemId} by {AssignedBy}",
            role, userId, systemId, assignedBy);

        return assignment;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<RmfRoleAssignment>> ListRmfRolesAsync(
        string systemId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(systemId, nameof(systemId));

        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AtoCopilotContext>();

        return await context.RmfRoleAssignments
            .Where(r => r.RegisteredSystemId == systemId && r.IsActive)
            .OrderBy(r => r.RmfRole)
            .ThenBy(r => r.UserDisplayName)
            .ToListAsync(cancellationToken);
    }
}
