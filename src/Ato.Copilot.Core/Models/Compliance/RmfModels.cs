namespace Ato.Copilot.Core.Models.Compliance;

// ───────────────────────────── RMF Entities (Feature 010) ─────────────────────────────

/// <summary>
/// Represents one step in the 6-step Risk Management Framework process.
/// </summary>
/// <param name="Step">Step number (1-6).</param>
/// <param name="Title">Step title (e.g., "Categorize", "Select").</param>
/// <param name="Description">Detailed step description.</param>
/// <param name="Activities">Key activities performed in this step.</param>
/// <param name="Outputs">Deliverables and outputs produced.</param>
/// <param name="Roles">Responsible roles (e.g., "System Owner", "ISSM").</param>
/// <param name="DodInstruction">Governing DoD instruction reference.</param>
public record RmfStep(
    int Step,
    string Title,
    string Description,
    List<string> Activities,
    List<string> Outputs,
    List<string> Roles,
    string DodInstruction);

/// <summary>
/// Root container for RMF JSON data, including steps, service guidance, and deliverables.
/// </summary>
/// <param name="Steps">All 6 RMF steps.</param>
/// <param name="ServiceGuidance">Branch/service-specific guidance keyed by organization name.</param>
/// <param name="DeliverablesOverview">Aggregated deliverables by step.</param>
public record RmfProcessData(
    List<RmfStep> Steps,
    Dictionary<string, ServiceGuidance> ServiceGuidance,
    List<DeliverableInfo> DeliverablesOverview);

/// <summary>
/// Service/branch-specific guidance for the RMF process.
/// </summary>
/// <param name="Organization">Organization name (e.g., "Navy", "Army").</param>
/// <param name="Description">Description of service-specific requirements.</param>
/// <param name="Contacts">Points of contact.</param>
/// <param name="Requirements">Service-specific requirements.</param>
/// <param name="Timeline">Expected timeline.</param>
/// <param name="Tools">Tools used by this service branch.</param>
public record ServiceGuidance(
    string Organization,
    string Description,
    List<string> Contacts,
    List<string> Requirements,
    string Timeline,
    List<string> Tools);

/// <summary>
/// Aggregated deliverables for a specific RMF step.
/// </summary>
/// <param name="Step">RMF step number.</param>
/// <param name="StepTitle">Step title.</param>
/// <param name="Deliverables">List of deliverable names.</param>
public record DeliverableInfo(
    int Step,
    string StepTitle,
    List<string> Deliverables);
