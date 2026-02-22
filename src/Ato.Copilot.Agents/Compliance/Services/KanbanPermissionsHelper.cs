using Ato.Copilot.Core.Constants;

namespace Ato.Copilot.Agents.Compliance.Services;

/// <summary>
/// Static helper for checking Kanban RBAC permissions per role.
/// Implements the role permission matrix from data-model.md.
/// </summary>
public static class KanbanPermissionsHelper
{
    /// <summary>
    /// Role permission matrix mapping roles to their allowed actions.
    /// </summary>
    private static readonly Dictionary<string, HashSet<string>> RolePermissions = new()
    {
        [ComplianceRoles.Administrator] = new HashSet<string>
        {
            KanbanPermissions.CanCreateBoard,
            KanbanPermissions.CanCreateTask,
            KanbanPermissions.CanAssignAny,
            KanbanPermissions.CanSelfAssign,
            KanbanPermissions.CanMoveOwn,
            KanbanPermissions.CanMoveAny,
            KanbanPermissions.CanCloseWithoutValidation,
            KanbanPermissions.CanComment,
            KanbanPermissions.CanDeleteAnyComment,
            KanbanPermissions.CanExport,
            KanbanPermissions.CanArchive,
        },
        [ComplianceRoles.SecurityLead] = new HashSet<string>
        {
            KanbanPermissions.CanCreateBoard,
            KanbanPermissions.CanCreateTask,
            KanbanPermissions.CanAssignAny,
            KanbanPermissions.CanMoveOwn,
            KanbanPermissions.CanMoveAny,
            KanbanPermissions.CanComment,
            KanbanPermissions.CanDeleteAnyComment,
            KanbanPermissions.CanExport,
        },
        [ComplianceRoles.Analyst] = new HashSet<string>
        {
            KanbanPermissions.CanSelfAssign,
            KanbanPermissions.CanMoveOwn,
            KanbanPermissions.CanComment,
        },
        [ComplianceRoles.Auditor] = new HashSet<string>
        {
            KanbanPermissions.CanExport,
        },
    };

    /// <summary>
    /// Checks whether a given role has permission to perform a specific action.
    /// </summary>
    /// <param name="role">The user's compliance role constant.</param>
    /// <param name="action">The Kanban permission constant to check.</param>
    /// <returns>True if the role is authorized for the action.</returns>
    public static bool CanPerformAction(string role, string action)
    {
        return RolePermissions.TryGetValue(role, out var permissions) && permissions.Contains(action);
    }

    /// <summary>
    /// Gets all permissions for a given role.
    /// </summary>
    /// <param name="role">The user's compliance role constant.</param>
    /// <returns>Set of allowed permission constants, or empty if role not found.</returns>
    public static IReadOnlySet<string> GetPermissionsForRole(string role)
    {
        return RolePermissions.TryGetValue(role, out var permissions)
            ? permissions
            : new HashSet<string>();
    }
}
