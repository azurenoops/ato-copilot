namespace Ato.Copilot.Core.Constants;

/// <summary>
/// Compliance RBAC roles for authorization policies
/// </summary>
public static class ComplianceRoles
{
    public const string Administrator = "Compliance.Administrator";
    public const string Auditor = "Compliance.Auditor";
    public const string Analyst = "Compliance.Analyst";
    public const string Viewer = "Compliance.Viewer";
    public const string SecurityLead = "Compliance.SecurityLead";
}

/// <summary>
/// Compliance-specific permissions
/// </summary>
public static class CompliancePermissions
{
    public const string GenerateDocuments = "Compliance.GenerateDocuments";
    public const string ExecuteRemediation = "Compliance.ExecuteRemediation";
    public const string CollectEvidence = "Compliance.CollectEvidence";
    public const string RunAssessment = "Compliance.RunAssessment";
}

/// <summary>
/// Kanban-specific permissions for remediation board operations
/// </summary>
public static class KanbanPermissions
{
    public const string CanCreateBoard = "Kanban.CreateBoard";
    public const string CanCreateTask = "Kanban.CreateTask";
    public const string CanAssignAny = "Kanban.AssignAny";
    public const string CanSelfAssign = "Kanban.SelfAssign";
    public const string CanMoveOwn = "Kanban.MoveOwn";
    public const string CanMoveAny = "Kanban.MoveAny";
    public const string CanCloseWithoutValidation = "Kanban.CloseWithoutValidation";
    public const string CanComment = "Kanban.Comment";
    public const string CanDeleteAnyComment = "Kanban.DeleteAnyComment";
    public const string CanExport = "Kanban.Export";
    public const string CanArchive = "Kanban.Archive";
}
