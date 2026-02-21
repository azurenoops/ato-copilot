namespace Ato.Copilot.Core.Constants;

/// <summary>
/// NIST 800-53 Rev 5 control family identifiers and metadata.
/// Contains all 20 control families including PT and SR added in Rev 5.
/// </summary>
public static class ControlFamilies
{
    /// <summary>Access Control</summary>
    public const string AccessControl = "AC";

    /// <summary>Awareness and Training</summary>
    public const string AwarenessTraining = "AT";

    /// <summary>Audit and Accountability</summary>
    public const string AuditAccountability = "AU";

    /// <summary>Assessment, Authorization, and Monitoring</summary>
    public const string AssessmentAuthorization = "CA";

    /// <summary>Configuration Management</summary>
    public const string ConfigurationManagement = "CM";

    /// <summary>Contingency Planning</summary>
    public const string ContingencyPlanning = "CP";

    /// <summary>Identification and Authentication</summary>
    public const string IdentificationAuthentication = "IA";

    /// <summary>Incident Response</summary>
    public const string IncidentResponse = "IR";

    /// <summary>Maintenance</summary>
    public const string Maintenance = "MA";

    /// <summary>Media Protection</summary>
    public const string MediaProtection = "MP";

    /// <summary>Physical and Environmental Protection</summary>
    public const string PhysicalEnvironmental = "PE";

    /// <summary>Planning</summary>
    public const string Planning = "PL";

    /// <summary>Program Management</summary>
    public const string ProgramManagement = "PM";

    /// <summary>Personnel Security</summary>
    public const string PersonnelSecurity = "PS";

    /// <summary>Personally Identifiable Information Processing and Transparency</summary>
    public const string PiiProcessing = "PT";

    /// <summary>Risk Assessment</summary>
    public const string RiskAssessment = "RA";

    /// <summary>System and Services Acquisition</summary>
    public const string SystemServicesAcquisition = "SA";

    /// <summary>System and Communications Protection</summary>
    public const string SystemCommunications = "SC";

    /// <summary>System and Information Integrity</summary>
    public const string SystemInformationIntegrity = "SI";

    /// <summary>Supply Chain Risk Management</summary>
    public const string SupplyChainRisk = "SR";

    /// <summary>
    /// High-risk control families that require additional approval for remediation.
    /// Changes to AC (Access Control), IA (Identification/Authentication), or SC
    /// (System/Communications) can impact user access and security boundaries.
    /// </summary>
    public static readonly HashSet<string> HighRiskFamilies = new(StringComparer.OrdinalIgnoreCase)
    {
        AccessControl,
        IdentificationAuthentication,
        SystemCommunications
    };

    /// <summary>
    /// All 20 NIST 800-53 Rev 5 control family abbreviations.
    /// </summary>
    public static readonly HashSet<string> AllFamilies = new(StringComparer.OrdinalIgnoreCase)
    {
        AccessControl, AwarenessTraining, AuditAccountability,
        AssessmentAuthorization, ConfigurationManagement, ContingencyPlanning,
        IdentificationAuthentication, IncidentResponse, Maintenance,
        MediaProtection, PhysicalEnvironmental, Planning,
        ProgramManagement, PersonnelSecurity, PiiProcessing,
        RiskAssessment, SystemServicesAcquisition, SystemCommunications,
        SystemInformationIntegrity, SupplyChainRisk
    };

    /// <summary>
    /// Maps control family abbreviation to full display name.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> FamilyNames =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [AccessControl] = "Access Control",
            [AwarenessTraining] = "Awareness and Training",
            [AuditAccountability] = "Audit and Accountability",
            [AssessmentAuthorization] = "Assessment, Authorization, and Monitoring",
            [ConfigurationManagement] = "Configuration Management",
            [ContingencyPlanning] = "Contingency Planning",
            [IdentificationAuthentication] = "Identification and Authentication",
            [IncidentResponse] = "Incident Response",
            [Maintenance] = "Maintenance",
            [MediaProtection] = "Media Protection",
            [PhysicalEnvironmental] = "Physical and Environmental Protection",
            [Planning] = "Planning",
            [ProgramManagement] = "Program Management",
            [PersonnelSecurity] = "Personnel Security",
            [PiiProcessing] = "PII Processing and Transparency",
            [RiskAssessment] = "Risk Assessment",
            [SystemServicesAcquisition] = "System and Services Acquisition",
            [SystemCommunications] = "System and Communications Protection",
            [SystemInformationIntegrity] = "System and Information Integrity",
            [SupplyChainRisk] = "Supply Chain Risk Management"
        };

    /// <summary>
    /// Returns true if the given family abbreviation is a recognized NIST 800-53 Rev 5 family.
    /// </summary>
    /// <param name="familyId">Two-letter control family abbreviation (e.g., "AC", "AU").</param>
    /// <returns>True if the family is valid.</returns>
    public static bool IsValidFamily(string familyId) =>
        AllFamilies.Contains(familyId);

    /// <summary>
    /// Returns true if the given family is classified as high-risk for remediation.
    /// </summary>
    /// <param name="familyId">Two-letter control family abbreviation.</param>
    /// <returns>True if AC, IA, or SC.</returns>
    public static bool IsHighRisk(string familyId) =>
        HighRiskFamilies.Contains(familyId);
}
