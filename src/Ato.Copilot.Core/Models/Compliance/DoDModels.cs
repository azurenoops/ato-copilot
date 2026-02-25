namespace Ato.Copilot.Core.Models.Compliance;

// ───────────────────────────── DoD Entities (Feature 010) ─────────────────────────────

/// <summary>
/// Represents a DoD Instruction document with control mappings.
/// </summary>
/// <param name="InstructionId">Instruction identifier (e.g., "DoDI 8510.01").</param>
/// <param name="Title">Full title of the instruction.</param>
/// <param name="Description">Summary description.</param>
/// <param name="PublicationDate">Publication date string.</param>
/// <param name="Applicability">Scope of applicability.</param>
/// <param name="Url">Reference URL for the instruction.</param>
/// <param name="RelatedNistControls">Mapped NIST 800-53 control IDs.</param>
/// <param name="RelatedStigIds">Mapped STIG IDs.</param>
/// <param name="ControlMappings">Detailed control-to-instruction mappings.</param>
public record DoDInstruction(
    string InstructionId,
    string Title,
    string Description,
    string PublicationDate,
    string Applicability,
    string Url,
    List<string> RelatedNistControls,
    List<string> RelatedStigIds,
    List<ControlMapping> ControlMappings);

/// <summary>
/// Maps a specific control to its DoD instruction requirement and guidance.
/// </summary>
/// <param name="ControlId">NIST control ID.</param>
/// <param name="Requirement">DoD-specific requirement text.</param>
/// <param name="Guidance">Implementation guidance.</param>
public record ControlMapping(
    string ControlId,
    string Requirement,
    string Guidance);

/// <summary>
/// Represents a DoD authorization workflow with ordered steps.
/// </summary>
/// <param name="WorkflowId">Unique workflow identifier.</param>
/// <param name="Name">Workflow name.</param>
/// <param name="Organization">Sponsoring organization (e.g., "Navy").</param>
/// <param name="ImpactLevel">Target impact level.</param>
/// <param name="Description">Workflow description.</param>
/// <param name="Steps">Ordered workflow steps.</param>
/// <param name="RequiredDocuments">Required documentation.</param>
/// <param name="ApprovalAuthorities">Approval chain.</param>
public record DoDWorkflow(
    string WorkflowId,
    string Name,
    string Organization,
    string ImpactLevel,
    string Description,
    List<WorkflowStep> Steps,
    List<string> RequiredDocuments,
    List<string> ApprovalAuthorities);

/// <summary>
/// A single step within a DoD authorization workflow.
/// </summary>
/// <param name="Order">Step order (1-based).</param>
/// <param name="Title">Step title.</param>
/// <param name="Description">Step description.</param>
/// <param name="Duration">Expected duration.</param>
public record WorkflowStep(
    int Order,
    string Title,
    string Description,
    string Duration);
