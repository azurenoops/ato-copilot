namespace Ato.Copilot.Core.Constants;

/// <summary>
/// Compliance RBAC roles for authorization policies.
/// </summary>
public static class ComplianceRoles
{
    public const string Administrator = "Compliance.Administrator";
    public const string Auditor = "Compliance.Auditor";
    public const string Analyst = "Compliance.Analyst";
    public const string Viewer = "Compliance.Viewer";
    public const string SecurityLead = "Compliance.SecurityLead";

    /// <summary>Default fallback role for CAC-authenticated users with no explicit mapping (FR-028).</summary>
    public const string PlatformEngineer = "Compliance.PlatformEngineer";

    /// <summary>Authorizing Official — issues ATO/ATOwC/IATT/DATO decisions per DoDI 8510.01.</summary>
    public const string AuthorizingOfficial = "Compliance.AuthorizingOfficial";
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

/// <summary>
/// PIM-specific permissions for role activation and JIT access operations.
/// </summary>
public static class PimPermissions
{
    public const string ActivateRole = "Pim.ActivateRole";
    public const string DeactivateRole = "Pim.DeactivateRole";
    public const string ExtendRole = "Pim.ExtendRole";
    public const string ApproveRequest = "Pim.ApproveRequest";
    public const string DenyRequest = "Pim.DenyRequest";
    public const string ViewHistory = "Pim.ViewHistory";
    public const string ManageJitAccess = "Pim.ManageJitAccess";
    public const string MapCertificate = "Pim.MapCertificate";
}
