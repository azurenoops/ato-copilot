namespace Ato.Copilot.Core.Models.Compliance;

// ───────────────────────────── FedRAMP Entities (Feature 010) ─────────────────────────────

/// <summary>
/// Represents FedRAMP authorization package template guidance.
/// </summary>
/// <param name="TemplateType">Template type identifier (e.g., "SSP", "POAM", "CRM").</param>
/// <param name="Title">Template title.</param>
/// <param name="Description">Template purpose description.</param>
/// <param name="Sections">Required sections within the template.</param>
/// <param name="RequiredFields">Required fields with descriptions and examples.</param>
/// <param name="AzureMappings">Azure service to template section mappings.</param>
/// <param name="AuthorizationChecklist">Package checklist items.</param>
public record FedRampTemplate(
    string TemplateType,
    string Title,
    string Description,
    List<TemplateSection> Sections,
    List<FieldDefinition> RequiredFields,
    Dictionary<string, string> AzureMappings,
    List<ChecklistItem> AuthorizationChecklist);

/// <summary>
/// A section within a FedRAMP template.
/// </summary>
/// <param name="Name">Section name.</param>
/// <param name="Description">Section description.</param>
/// <param name="RequiredElements">Required elements within this section.</param>
public record TemplateSection(
    string Name,
    string Description,
    List<string> RequiredElements);

/// <summary>
/// A required field in a FedRAMP template with example content and Azure data source.
/// </summary>
/// <param name="Name">Field name.</param>
/// <param name="Description">Field description.</param>
/// <param name="Example">Example content.</param>
/// <param name="AzureSource">Azure service that provides data for this field.</param>
public record FieldDefinition(
    string Name,
    string Description,
    string Example,
    string AzureSource);

/// <summary>
/// A checklist item for FedRAMP authorization package assembly.
/// </summary>
/// <param name="Item">Checklist item name.</param>
/// <param name="Description">Item description.</param>
/// <param name="Required">Whether this item is mandatory.</param>
public record ChecklistItem(
    string Item,
    string Description,
    bool Required);
