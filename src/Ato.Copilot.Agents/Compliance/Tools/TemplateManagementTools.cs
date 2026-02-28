// ─────────────────────────────────────────────────────────────────────────────
// Feature 015 · Phase 13 — Document Templates & PDF Export (US11)
// T157: UploadTemplateTool, T158: ListTemplatesTool
// T159: UpdateTemplateTool + DeleteTemplateTool
// ─────────────────────────────────────────────────────────────────────────────

using System.Text.Json;
using Microsoft.Extensions.Logging;
using Ato.Copilot.Agents.Common;
using Ato.Copilot.Core.Interfaces.Compliance;

namespace Ato.Copilot.Agents.Compliance.Tools;

// ─────────────────────────────────────────────────────────────────────────────
// T157: UploadTemplateTool — compliance_upload_template
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Upload a custom DOCX template for compliance document generation.
/// Validates merge fields against the document type schema.
/// RBAC: ISSM, AO.
/// </summary>
public class UploadTemplateTool : BaseTool
{
    private readonly IDocumentTemplateService _service;

    public UploadTemplateTool(
        IDocumentTemplateService service,
        ILogger<UploadTemplateTool> logger) : base(logger)
    {
        _service = service;
    }

    public override string Name => "compliance_upload_template";

    public override string Description =>
        "Upload a custom DOCX template for compliance document generation. " +
        "Validates merge fields ({{FieldName}}) against the document type schema.";

    public override IReadOnlyDictionary<string, ToolParameter> Parameters => new Dictionary<string, ToolParameter>
    {
        ["template_name"] = new() { Name = "template_name", Description = "Friendly name for the template", Type = "string", Required = true },
        ["document_type"] = new() { Name = "document_type", Description = "Document type: ssp, sar, poam, rar", Type = "string", Required = true },
        ["file_base64"] = new() { Name = "file_base64", Description = "Base64-encoded DOCX file content", Type = "string", Required = true },
        ["uploaded_by"] = new() { Name = "uploaded_by", Description = "User who is uploading the template", Type = "string", Required = true }
    };

    public override async Task<string> ExecuteCoreAsync(
        Dictionary<string, object?> arguments,
        CancellationToken cancellationToken = default)
    {
        var templateName = GetArg<string>(arguments, "template_name")
            ?? throw new ArgumentException("template_name is required.");
        var documentType = GetArg<string>(arguments, "document_type")
            ?? throw new ArgumentException("document_type is required.");
        var fileBase64 = GetArg<string>(arguments, "file_base64")
            ?? throw new ArgumentException("file_base64 is required.");
        var uploadedBy = GetArg<string>(arguments, "uploaded_by")
            ?? throw new ArgumentException("uploaded_by is required.");

        byte[] fileBytes;
        try
        {
            fileBytes = Convert.FromBase64String(fileBase64);
        }
        catch (FormatException)
        {
            return JsonSerializer.Serialize(new
            {
                status = "error",
                message = "Invalid base64 encoding for file_base64."
            }, new JsonSerializerOptions { WriteIndented = true });
        }

        var result = await _service.UploadTemplateAsync(
            templateName, documentType, fileBytes, uploadedBy, cancellationToken);

        var response = new
        {
            status = "success",
            data = new
            {
                template_id = result.TemplateId,
                template_name = result.TemplateName,
                document_type = result.DocumentType,
                is_valid = result.IsValid,
                merge_fields_found = result.MergeFieldsFound,
                merge_fields_missing = result.MergeFieldsMissing,
                warnings = result.Warnings
            }
        };

        return JsonSerializer.Serialize(response,
            new JsonSerializerOptions { WriteIndented = true });
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// T158: ListTemplatesTool — compliance_list_templates
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// List available document templates, optionally filtered by document type.
/// </summary>
public class ListTemplatesTool : BaseTool
{
    private readonly IDocumentTemplateService _service;

    public ListTemplatesTool(
        IDocumentTemplateService service,
        ILogger<ListTemplatesTool> logger) : base(logger)
    {
        _service = service;
    }

    public override string Name => "compliance_list_templates";

    public override string Description =>
        "List available document templates. Optionally filter by document type (ssp, sar, poam, rar).";

    public override IReadOnlyDictionary<string, ToolParameter> Parameters => new Dictionary<string, ToolParameter>
    {
        ["document_type"] = new() { Name = "document_type", Description = "Filter by document type: ssp, sar, poam, rar. Omit to list all.", Type = "string" }
    };

    public override async Task<string> ExecuteCoreAsync(
        Dictionary<string, object?> arguments,
        CancellationToken cancellationToken = default)
    {
        var documentType = GetArg<string>(arguments, "document_type");

        var templates = await _service.ListTemplatesAsync(documentType, cancellationToken);

        var response = new
        {
            status = "success",
            data = new
            {
                total = templates.Count,
                templates = templates.Select(t => new
                {
                    template_id = t.TemplateId,
                    template_name = t.TemplateName,
                    document_type = t.DocumentType,
                    uploaded_by = t.UploadedBy,
                    uploaded_at = t.UploadedAt,
                    file_size_bytes = t.FileSizeBytes,
                    is_default = t.IsDefault
                }).ToList()
            }
        };

        return JsonSerializer.Serialize(response,
            new JsonSerializerOptions { WriteIndented = true });
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// T159: UpdateTemplateTool — compliance_update_template
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Update an existing document template (replace file and/or rename).
/// RBAC: ISSM, AO.
/// </summary>
public class UpdateTemplateTool : BaseTool
{
    private readonly IDocumentTemplateService _service;

    public UpdateTemplateTool(
        IDocumentTemplateService service,
        ILogger<UpdateTemplateTool> logger) : base(logger)
    {
        _service = service;
    }

    public override string Name => "compliance_update_template";

    public override string Description =>
        "Update an existing document template. Can replace the DOCX file, " +
        "rename the template, or both.";

    public override IReadOnlyDictionary<string, ToolParameter> Parameters => new Dictionary<string, ToolParameter>
    {
        ["template_id"] = new() { Name = "template_id", Description = "ID of the template to update", Type = "string", Required = true },
        ["file_base64"] = new() { Name = "file_base64", Description = "New base64-encoded DOCX file content (optional)", Type = "string" },
        ["new_name"] = new() { Name = "new_name", Description = "New template name (optional)", Type = "string" },
        ["updated_by"] = new() { Name = "updated_by", Description = "User performing the update", Type = "string", Required = true }
    };

    public override async Task<string> ExecuteCoreAsync(
        Dictionary<string, object?> arguments,
        CancellationToken cancellationToken = default)
    {
        var templateId = GetArg<string>(arguments, "template_id")
            ?? throw new ArgumentException("template_id is required.");
        var fileBase64 = GetArg<string>(arguments, "file_base64");
        var newName = GetArg<string>(arguments, "new_name");
        var updatedBy = GetArg<string>(arguments, "updated_by")
            ?? throw new ArgumentException("updated_by is required.");

        byte[]? fileBytes = null;
        if (!string.IsNullOrEmpty(fileBase64))
        {
            try
            {
                fileBytes = Convert.FromBase64String(fileBase64);
            }
            catch (FormatException)
            {
                return JsonSerializer.Serialize(new
                {
                    status = "error",
                    message = "Invalid base64 encoding for file_base64."
                }, new JsonSerializerOptions { WriteIndented = true });
            }
        }

        var result = await _service.UpdateTemplateAsync(
            templateId, fileBytes, newName, updatedBy, cancellationToken);

        var response = new
        {
            status = "success",
            data = new
            {
                template_id = result.TemplateId,
                template_name = result.TemplateName,
                document_type = result.DocumentType,
                is_valid = result.IsValid,
                merge_fields_found = result.MergeFieldsFound,
                merge_fields_missing = result.MergeFieldsMissing,
                warnings = result.Warnings
            }
        };

        return JsonSerializer.Serialize(response,
            new JsonSerializerOptions { WriteIndented = true });
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// T159: DeleteTemplateTool — compliance_delete_template
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Delete a document template by ID. RBAC: ISSM, AO.
/// </summary>
public class DeleteTemplateTool : BaseTool
{
    private readonly IDocumentTemplateService _service;

    public DeleteTemplateTool(
        IDocumentTemplateService service,
        ILogger<DeleteTemplateTool> logger) : base(logger)
    {
        _service = service;
    }

    public override string Name => "compliance_delete_template";

    public override string Description =>
        "Delete a document template by ID. Cannot be undone.";

    public override IReadOnlyDictionary<string, ToolParameter> Parameters => new Dictionary<string, ToolParameter>
    {
        ["template_id"] = new() { Name = "template_id", Description = "ID of the template to delete", Type = "string", Required = true }
    };

    public override async Task<string> ExecuteCoreAsync(
        Dictionary<string, object?> arguments,
        CancellationToken cancellationToken = default)
    {
        var templateId = GetArg<string>(arguments, "template_id")
            ?? throw new ArgumentException("template_id is required.");

        var deleted = await _service.DeleteTemplateAsync(templateId, cancellationToken);

        var response = new
        {
            status = deleted ? "success" : "not_found",
            message = deleted
                ? $"Template '{templateId}' deleted successfully."
                : $"Template '{templateId}' not found."
        };

        return JsonSerializer.Serialize(response,
            new JsonSerializerOptions { WriteIndented = true });
    }
}
