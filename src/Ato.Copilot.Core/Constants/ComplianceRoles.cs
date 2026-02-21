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
