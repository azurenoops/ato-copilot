using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Ato.Copilot.Agents.Common;
using Ato.Copilot.Core.Interfaces.Compliance;
using Ato.Copilot.Core.Models.Compliance;

namespace Ato.Copilot.Agents.Compliance.Tools;

// ────────────────────────────────────────────────────────────────────────────
// inventory_add_item
// ────────────────────────────────────────────────────────────────────────────

/// <summary>
/// MCP tool: inventory_add_item — Add a hardware or software inventory item.
/// </summary>
public class InventoryAddItemTool : BaseTool
{
    private readonly IInventoryService _svc;
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    public InventoryAddItemTool(
        IInventoryService svc,
        ILogger<InventoryAddItemTool> logger) : base(logger)
    {
        _svc = svc;
    }

    public override string Name => "inventory_add_item";

    public override string Description =>
        "Add a hardware or software inventory item to a registered system. " +
        "Validates required fields per item type and function classification.";

    public override IReadOnlyDictionary<string, ToolParameter> Parameters => new Dictionary<string, ToolParameter>
    {
        ["system_id"] = new() { Name = "system_id", Description = "System GUID, name, or acronym", Type = "string", Required = true },
        ["type"] = new() { Name = "type", Description = "'hardware' or 'software'", Type = "string", Required = true },
        ["item_name"] = new() { Name = "item_name", Description = "Display name of the item", Type = "string", Required = true },
        ["function"] = new() { Name = "function", Description = "Function classification (e.g., 'server', 'operating_system')", Type = "string", Required = true },
        ["manufacturer"] = new() { Name = "manufacturer", Description = "HW manufacturer", Type = "string", Required = false },
        ["model"] = new() { Name = "model", Description = "HW model", Type = "string", Required = false },
        ["serial_number"] = new() { Name = "serial_number", Description = "HW serial number", Type = "string", Required = false },
        ["ip_address"] = new() { Name = "ip_address", Description = "IPv4/IPv6 address", Type = "string", Required = false },
        ["mac_address"] = new() { Name = "mac_address", Description = "MAC address", Type = "string", Required = false },
        ["location"] = new() { Name = "location", Description = "Physical/logical location", Type = "string", Required = false },
        ["vendor"] = new() { Name = "vendor", Description = "SW vendor", Type = "string", Required = false },
        ["version"] = new() { Name = "version", Description = "SW version", Type = "string", Required = false },
        ["patch_level"] = new() { Name = "patch_level", Description = "SW patch level", Type = "string", Required = false },
        ["license_type"] = new() { Name = "license_type", Description = "License type", Type = "string", Required = false },
        ["parent_hardware_id"] = new() { Name = "parent_hardware_id", Description = "Parent HW item ID (for software)", Type = "string", Required = false }
    };

    public override async Task<string> ExecuteCoreAsync(
        Dictionary<string, object?> arguments,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        var systemId = GetArg<string>(arguments, "system_id");
        var typeStr = GetArg<string>(arguments, "type");
        var itemName = GetArg<string>(arguments, "item_name");
        var functionStr = GetArg<string>(arguments, "function");

        if (string.IsNullOrWhiteSpace(systemId))
            return Error("INVALID_INPUT", "The 'system_id' parameter is required.");
        if (string.IsNullOrWhiteSpace(typeStr))
            return Error("INVALID_INPUT", "The 'type' parameter is required.");
        if (string.IsNullOrWhiteSpace(itemName))
            return Error("INVALID_INPUT", "The 'item_name' parameter is required.");
        if (string.IsNullOrWhiteSpace(functionStr))
            return Error("INVALID_INPUT", "The 'function' parameter is required.");

        if (!Enum.TryParse<InventoryItemType>(typeStr, true, out var itemType))
            return Error("INVALID_INPUT", "Invalid 'type'. Must be 'hardware' or 'software'.");

        var input = new InventoryItemInput
        {
            ItemName = itemName,
            Type = itemType,
            Manufacturer = GetArg<string>(arguments, "manufacturer"),
            Model = GetArg<string>(arguments, "model"),
            SerialNumber = GetArg<string>(arguments, "serial_number"),
            IpAddress = GetArg<string>(arguments, "ip_address"),
            MacAddress = GetArg<string>(arguments, "mac_address"),
            Location = GetArg<string>(arguments, "location"),
            Vendor = GetArg<string>(arguments, "vendor"),
            Version = GetArg<string>(arguments, "version"),
            PatchLevel = GetArg<string>(arguments, "patch_level"),
            LicenseType = GetArg<string>(arguments, "license_type"),
            ParentHardwareId = GetArg<string>(arguments, "parent_hardware_id")
        };

        // Parse function enum based on type
        if (itemType == InventoryItemType.Hardware)
        {
            if (Enum.TryParse<HardwareFunction>(functionStr, true, out var hwFunc))
                input.HardwareFunction = hwFunc;
            else
                return Error("INVALID_INPUT", $"Invalid hardware function '{functionStr}'. Valid values: Server, Workstation, NetworkDevice, Storage, Other.");
        }
        else
        {
            if (Enum.TryParse<SoftwareFunction>(functionStr, true, out var swFunc))
                input.SoftwareFunction = swFunc;
            else
                return Error("INVALID_INPUT", $"Invalid software function '{functionStr}'. Valid values: OperatingSystem, Database, Middleware, Application, SecurityTool, Other.");
        }

        try
        {
            var item = await _svc.AddItemAsync(systemId, input, "copilot-user", cancellationToken);
            sw.Stop();
            return JsonSerializer.Serialize(new
            {
                status = "success",
                data = MapItem(item),
                metadata = Meta(sw)
            }, JsonOpts);
        }
        catch (InvalidOperationException ex)
        {
            return Error(ExtractErrorCode(ex), ex.Message);
        }
    }

    private static object MapItem(InventoryItem item) => new
    {
        id = item.Id,
        system_id = item.RegisteredSystemId,
        item_name = item.ItemName,
        type = item.Type.ToString(),
        hardware_function = item.HardwareFunction?.ToString(),
        software_function = item.SoftwareFunction?.ToString(),
        manufacturer = item.Manufacturer,
        model = item.Model,
        serial_number = item.SerialNumber,
        ip_address = item.IpAddress,
        mac_address = item.MacAddress,
        location = item.Location,
        vendor = item.Vendor,
        version = item.Version,
        patch_level = item.PatchLevel,
        license_type = item.LicenseType,
        status = item.Status.ToString(),
        parent_hardware_id = item.ParentHardwareId,
        boundary_resource_id = item.BoundaryResourceId,
        decommission_date = item.DecommissionDate?.ToString("O"),
        decommission_rationale = item.DecommissionRationale,
        created_by = item.CreatedBy,
        created_at = item.CreatedAt.ToString("O"),
        modified_by = item.ModifiedBy,
        modified_at = item.ModifiedAt?.ToString("O")
    };

    private static string Error(string code, string message) =>
        JsonSerializer.Serialize(new { status = "error", errorCode = code, message }, JsonOpts);

    private object Meta(Stopwatch sw) => new
    {
        tool = Name,
        duration_ms = sw.ElapsedMilliseconds,
        timestamp = DateTime.UtcNow.ToString("O")
    };

    private static string ExtractErrorCode(InvalidOperationException ex) =>
        ex.Message.Contains(':') ? ex.Message[..ex.Message.IndexOf(':')] : "OPERATION_FAILED";
}

// ────────────────────────────────────────────────────────────────────────────
// inventory_update_item
// ────────────────────────────────────────────────────────────────────────────

/// <summary>
/// MCP tool: inventory_update_item — Update fields on an existing inventory item.
/// </summary>
public class InventoryUpdateItemTool : BaseTool
{
    private readonly IInventoryService _svc;
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    public InventoryUpdateItemTool(
        IInventoryService svc,
        ILogger<InventoryUpdateItemTool> logger) : base(logger)
    {
        _svc = svc;
    }

    public override string Name => "inventory_update_item";

    public override string Description =>
        "Update fields on an existing inventory item. Null fields are not changed.";

    public override IReadOnlyDictionary<string, ToolParameter> Parameters => new Dictionary<string, ToolParameter>
    {
        ["item_id"] = new() { Name = "item_id", Description = "Inventory item GUID", Type = "string", Required = true },
        ["item_name"] = new() { Name = "item_name", Description = "Updated name", Type = "string", Required = false },
        ["manufacturer"] = new() { Name = "manufacturer", Description = "Updated manufacturer", Type = "string", Required = false },
        ["model"] = new() { Name = "model", Description = "Updated model", Type = "string", Required = false },
        ["serial_number"] = new() { Name = "serial_number", Description = "Updated serial number", Type = "string", Required = false },
        ["ip_address"] = new() { Name = "ip_address", Description = "Updated IP address", Type = "string", Required = false },
        ["mac_address"] = new() { Name = "mac_address", Description = "Updated MAC address", Type = "string", Required = false },
        ["location"] = new() { Name = "location", Description = "Updated location", Type = "string", Required = false },
        ["vendor"] = new() { Name = "vendor", Description = "Updated vendor", Type = "string", Required = false },
        ["version"] = new() { Name = "version", Description = "Updated version", Type = "string", Required = false },
        ["patch_level"] = new() { Name = "patch_level", Description = "Updated patch level", Type = "string", Required = false },
        ["license_type"] = new() { Name = "license_type", Description = "Updated license type", Type = "string", Required = false },
        ["parent_hardware_id"] = new() { Name = "parent_hardware_id", Description = "Updated parent HW item ID", Type = "string", Required = false }
    };

    public override async Task<string> ExecuteCoreAsync(
        Dictionary<string, object?> arguments,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        var itemId = GetArg<string>(arguments, "item_id");

        if (string.IsNullOrWhiteSpace(itemId))
            return Error("INVALID_INPUT", "The 'item_id' parameter is required.");

        var input = new InventoryItemInput
        {
            ItemName = GetArg<string>(arguments, "item_name"),
            Manufacturer = GetArg<string>(arguments, "manufacturer"),
            Model = GetArg<string>(arguments, "model"),
            SerialNumber = GetArg<string>(arguments, "serial_number"),
            IpAddress = GetArg<string>(arguments, "ip_address"),
            MacAddress = GetArg<string>(arguments, "mac_address"),
            Location = GetArg<string>(arguments, "location"),
            Vendor = GetArg<string>(arguments, "vendor"),
            Version = GetArg<string>(arguments, "version"),
            PatchLevel = GetArg<string>(arguments, "patch_level"),
            LicenseType = GetArg<string>(arguments, "license_type"),
            ParentHardwareId = GetArg<string>(arguments, "parent_hardware_id")
        };

        try
        {
            var item = await _svc.UpdateItemAsync(itemId, input, "copilot-user", cancellationToken);
            sw.Stop();
            return JsonSerializer.Serialize(new
            {
                status = "success",
                data = MapItem(item),
                metadata = Meta(sw)
            }, JsonOpts);
        }
        catch (InvalidOperationException ex)
        {
            return Error(ExtractErrorCode(ex), ex.Message);
        }
    }

    private static object MapItem(InventoryItem item) => new
    {
        id = item.Id,
        system_id = item.RegisteredSystemId,
        item_name = item.ItemName,
        type = item.Type.ToString(),
        status = item.Status.ToString(),
        modified_by = item.ModifiedBy,
        modified_at = item.ModifiedAt?.ToString("O")
    };

    private static string Error(string code, string message) =>
        JsonSerializer.Serialize(new { status = "error", errorCode = code, message }, JsonOpts);

    private object Meta(Stopwatch sw) => new
    {
        tool = Name,
        duration_ms = sw.ElapsedMilliseconds,
        timestamp = DateTime.UtcNow.ToString("O")
    };

    private static string ExtractErrorCode(InvalidOperationException ex) =>
        ex.Message.Contains(':') ? ex.Message[..ex.Message.IndexOf(':')] : "OPERATION_FAILED";
}

// ────────────────────────────────────────────────────────────────────────────
// inventory_decommission_item
// ────────────────────────────────────────────────────────────────────────────

/// <summary>
/// MCP tool: inventory_decommission_item — Soft-delete an inventory item.
/// </summary>
public class InventoryDecommissionItemTool : BaseTool
{
    private readonly IInventoryService _svc;
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    public InventoryDecommissionItemTool(
        IInventoryService svc,
        ILogger<InventoryDecommissionItemTool> logger) : base(logger)
    {
        _svc = svc;
    }

    public override string Name => "inventory_decommission_item";

    public override string Description =>
        "Soft-delete an inventory item with a decommission rationale. " +
        "If the item is hardware, all child software items are also decommissioned.";

    public override IReadOnlyDictionary<string, ToolParameter> Parameters => new Dictionary<string, ToolParameter>
    {
        ["item_id"] = new() { Name = "item_id", Description = "Inventory item GUID", Type = "string", Required = true },
        ["rationale"] = new() { Name = "rationale", Description = "Reason for decommissioning", Type = "string", Required = true }
    };

    public override async Task<string> ExecuteCoreAsync(
        Dictionary<string, object?> arguments,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        var itemId = GetArg<string>(arguments, "item_id");
        var rationale = GetArg<string>(arguments, "rationale");

        if (string.IsNullOrWhiteSpace(itemId))
            return Error("INVALID_INPUT", "The 'item_id' parameter is required.");
        if (string.IsNullOrWhiteSpace(rationale))
            return Error("INVALID_INPUT", "The 'rationale' parameter is required.");

        try
        {
            var item = await _svc.DecommissionItemAsync(itemId, rationale, "copilot-user", cancellationToken);
            var cascadedCount = item.InstalledSoftware?
                .Count(s => s.Status == InventoryItemStatus.Decommissioned) ?? 0;

            sw.Stop();
            return JsonSerializer.Serialize(new
            {
                status = "success",
                data = new
                {
                    id = item.Id,
                    item_name = item.ItemName,
                    type = item.Type.ToString(),
                    status = item.Status.ToString(),
                    decommission_date = item.DecommissionDate?.ToString("O"),
                    decommission_rationale = item.DecommissionRationale
                },
                metadata = new
                {
                    tool = Name,
                    duration_ms = sw.ElapsedMilliseconds,
                    timestamp = DateTime.UtcNow.ToString("O"),
                    cascaded_decommissions = cascadedCount
                }
            }, JsonOpts);
        }
        catch (InvalidOperationException ex)
        {
            return Error(ExtractErrorCode(ex), ex.Message);
        }
    }

    private static string Error(string code, string message) =>
        JsonSerializer.Serialize(new { status = "error", errorCode = code, message }, JsonOpts);

    private object Meta(Stopwatch sw) => new
    {
        tool = Name,
        duration_ms = sw.ElapsedMilliseconds,
        timestamp = DateTime.UtcNow.ToString("O")
    };

    private static string ExtractErrorCode(InvalidOperationException ex) =>
        ex.Message.Contains(':') ? ex.Message[..ex.Message.IndexOf(':')] : "OPERATION_FAILED";
}

// ────────────────────────────────────────────────────────────────────────────
// inventory_list
// ────────────────────────────────────────────────────────────────────────────

/// <summary>
/// MCP tool: inventory_list — List and filter inventory items for a system.
/// </summary>
public class InventoryListTool : BaseTool
{
    private readonly IInventoryService _svc;
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    public InventoryListTool(
        IInventoryService svc,
        ILogger<InventoryListTool> logger) : base(logger)
    {
        _svc = svc;
    }

    public override string Name => "inventory_list";

    public override string Description =>
        "List and filter hardware/software inventory items for a system with " +
        "options for type, function, vendor, status, and free-text search.";

    public override IReadOnlyDictionary<string, ToolParameter> Parameters => new Dictionary<string, ToolParameter>
    {
        ["system_id"] = new() { Name = "system_id", Description = "System GUID, name, or acronym", Type = "string", Required = true },
        ["type"] = new() { Name = "type", Description = "'hardware' or 'software'", Type = "string", Required = false },
        ["function"] = new() { Name = "function", Description = "Function filter", Type = "string", Required = false },
        ["vendor"] = new() { Name = "vendor", Description = "Vendor/manufacturer filter", Type = "string", Required = false },
        ["status"] = new() { Name = "status", Description = "'active' (default) or 'decommissioned'", Type = "string", Required = false },
        ["search"] = new() { Name = "search", Description = "Free-text search on item name", Type = "string", Required = false },
        ["page_size"] = new() { Name = "page_size", Description = "Results per page (default 50)", Type = "integer", Required = false },
        ["page"] = new() { Name = "page", Description = "Page number (default 1)", Type = "integer", Required = false }
    };

    public override async Task<string> ExecuteCoreAsync(
        Dictionary<string, object?> arguments,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        var systemId = GetArg<string>(arguments, "system_id");

        if (string.IsNullOrWhiteSpace(systemId))
            return Error("INVALID_INPUT", "The 'system_id' parameter is required.");

        var typeStr = GetArg<string>(arguments, "type");
        var functionStr = GetArg<string>(arguments, "function");
        var vendor = GetArg<string>(arguments, "vendor");
        var statusStr = GetArg<string>(arguments, "status");
        var search = GetArg<string>(arguments, "search");
        var pageSizeStr = GetArg<string>(arguments, "page_size");
        var pageStr = GetArg<string>(arguments, "page");

        var options = new InventoryListOptions
        {
            Function = functionStr,
            Vendor = vendor,
            SearchText = search,
            PageSize = int.TryParse(pageSizeStr, out var ps) ? ps : 50,
            PageNumber = int.TryParse(pageStr, out var p) ? p : 1
        };

        if (!string.IsNullOrWhiteSpace(typeStr) && Enum.TryParse<InventoryItemType>(typeStr, true, out var itemType))
            options.Type = itemType;

        if (!string.IsNullOrWhiteSpace(statusStr) && Enum.TryParse<InventoryItemStatus>(statusStr, true, out var itemStatus))
            options.Status = itemStatus;

        try
        {
            var items = await _svc.ListItemsAsync(systemId, options, cancellationToken);
            sw.Stop();
            return JsonSerializer.Serialize(new
            {
                status = "success",
                data = new
                {
                    system_id = systemId,
                    count = items.Count,
                    page = options.PageNumber,
                    page_size = options.PageSize,
                    items = items.Select(i => new
                    {
                        id = i.Id,
                        item_name = i.ItemName,
                        type = i.Type.ToString(),
                        hardware_function = i.HardwareFunction?.ToString(),
                        software_function = i.SoftwareFunction?.ToString(),
                        manufacturer = i.Manufacturer,
                        vendor = i.Vendor,
                        version = i.Version,
                        ip_address = i.IpAddress,
                        status = i.Status.ToString()
                    })
                },
                metadata = Meta(sw)
            }, JsonOpts);
        }
        catch (InvalidOperationException ex)
        {
            return Error(ExtractErrorCode(ex), ex.Message);
        }
    }

    private static string Error(string code, string message) =>
        JsonSerializer.Serialize(new { status = "error", errorCode = code, message }, JsonOpts);

    private object Meta(Stopwatch sw) => new
    {
        tool = Name,
        duration_ms = sw.ElapsedMilliseconds,
        timestamp = DateTime.UtcNow.ToString("O")
    };

    private static string ExtractErrorCode(InvalidOperationException ex) =>
        ex.Message.Contains(':') ? ex.Message[..ex.Message.IndexOf(':')] : "OPERATION_FAILED";
}

// ────────────────────────────────────────────────────────────────────────────
// inventory_get
// ────────────────────────────────────────────────────────────────────────────

/// <summary>
/// MCP tool: inventory_get — Retrieve a single inventory item by ID.
/// </summary>
public class InventoryGetTool : BaseTool
{
    private readonly IInventoryService _svc;
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    public InventoryGetTool(
        IInventoryService svc,
        ILogger<InventoryGetTool> logger) : base(logger)
    {
        _svc = svc;
    }

    public override string Name => "inventory_get";

    public override string Description =>
        "Retrieve a single inventory item by ID, including installed software for hardware items.";

    public override IReadOnlyDictionary<string, ToolParameter> Parameters => new Dictionary<string, ToolParameter>
    {
        ["item_id"] = new() { Name = "item_id", Description = "Inventory item GUID", Type = "string", Required = true }
    };

    public override async Task<string> ExecuteCoreAsync(
        Dictionary<string, object?> arguments,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        var itemId = GetArg<string>(arguments, "item_id");

        if (string.IsNullOrWhiteSpace(itemId))
            return Error("INVALID_INPUT", "The 'item_id' parameter is required.");

        var item = await _svc.GetItemAsync(itemId, cancellationToken);
        if (item == null)
            return Error("ITEM_NOT_FOUND", $"Inventory item '{itemId}' not found.");

        sw.Stop();
        return JsonSerializer.Serialize(new
        {
            status = "success",
            data = new
            {
                id = item.Id,
                system_id = item.RegisteredSystemId,
                item_name = item.ItemName,
                type = item.Type.ToString(),
                hardware_function = item.HardwareFunction?.ToString(),
                software_function = item.SoftwareFunction?.ToString(),
                manufacturer = item.Manufacturer,
                model = item.Model,
                serial_number = item.SerialNumber,
                ip_address = item.IpAddress,
                mac_address = item.MacAddress,
                location = item.Location,
                vendor = item.Vendor,
                version = item.Version,
                patch_level = item.PatchLevel,
                license_type = item.LicenseType,
                status = item.Status.ToString(),
                parent_hardware_id = item.ParentHardwareId,
                boundary_resource_id = item.BoundaryResourceId,
                installed_software = item.InstalledSoftware?.Select(s => new
                {
                    id = s.Id,
                    item_name = s.ItemName,
                    software_function = s.SoftwareFunction?.ToString(),
                    vendor = s.Vendor,
                    version = s.Version,
                    status = s.Status.ToString()
                }),
                created_by = item.CreatedBy,
                created_at = item.CreatedAt.ToString("O")
            },
            metadata = Meta(sw)
        }, JsonOpts);
    }

    private static string Error(string code, string message) =>
        JsonSerializer.Serialize(new { status = "error", errorCode = code, message }, JsonOpts);

    private object Meta(Stopwatch sw) => new
    {
        tool = Name,
        duration_ms = sw.ElapsedMilliseconds,
        timestamp = DateTime.UtcNow.ToString("O")
    };
}

// ────────────────────────────────────────────────────────────────────────────
// inventory_export
// ────────────────────────────────────────────────────────────────────────────

/// <summary>
/// MCP tool: inventory_export — Export inventory to eMASS-compatible Excel workbook.
/// </summary>
public class InventoryExportTool : BaseTool
{
    private readonly IInventoryService _svc;
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    public InventoryExportTool(
        IInventoryService svc,
        ILogger<InventoryExportTool> logger) : base(logger)
    {
        _svc = svc;
    }

    public override string Name => "inventory_export";

    public override string Description =>
        "Export a system's HW/SW inventory as an eMASS-compatible Excel workbook (base64-encoded).";

    public override IReadOnlyDictionary<string, ToolParameter> Parameters => new Dictionary<string, ToolParameter>
    {
        ["system_id"] = new() { Name = "system_id", Description = "System GUID, name, or acronym", Type = "string", Required = true },
        ["export_type"] = new() { Name = "export_type", Description = "'hardware', 'software', or 'all' (default)", Type = "string", Required = false },
        ["include_decommissioned"] = new() { Name = "include_decommissioned", Description = "Include decommissioned items (default false)", Type = "boolean", Required = false }
    };

    public override async Task<string> ExecuteCoreAsync(
        Dictionary<string, object?> arguments,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        var systemId = GetArg<string>(arguments, "system_id");

        if (string.IsNullOrWhiteSpace(systemId))
            return Error("INVALID_INPUT", "The 'system_id' parameter is required.");

        var exportType = GetArg<string>(arguments, "export_type") ?? "all";
        var includeDecommStr = GetArg<string>(arguments, "include_decommissioned");
        var includeDecomm = string.Equals(includeDecommStr, "true", StringComparison.OrdinalIgnoreCase);

        var options = new InventoryExportOptions
        {
            ExportType = exportType,
            IncludeDecommissioned = includeDecomm
        };

        try
        {
            var bytes = await _svc.ExportToExcelAsync(systemId, options, cancellationToken);
            sw.Stop();
            return JsonSerializer.Serialize(new
            {
                status = "success",
                data = new
                {
                    system_id = systemId,
                    export_type = exportType,
                    file_base64 = Convert.ToBase64String(bytes),
                    file_size_bytes = bytes.Length
                },
                metadata = Meta(sw)
            }, JsonOpts);
        }
        catch (InvalidOperationException ex)
        {
            return Error(ExtractErrorCode(ex), ex.Message);
        }
    }

    private static string Error(string code, string message) =>
        JsonSerializer.Serialize(new { status = "error", errorCode = code, message }, JsonOpts);

    private object Meta(Stopwatch sw) => new
    {
        tool = Name,
        duration_ms = sw.ElapsedMilliseconds,
        timestamp = DateTime.UtcNow.ToString("O")
    };

    private static string ExtractErrorCode(InvalidOperationException ex) =>
        ex.Message.Contains(':') ? ex.Message[..ex.Message.IndexOf(':')] : "OPERATION_FAILED";
}

// ────────────────────────────────────────────────────────────────────────────
// inventory_import
// ────────────────────────────────────────────────────────────────────────────

/// <summary>
/// MCP tool: inventory_import — Import inventory from an eMASS-format Excel workbook.
/// </summary>
public class InventoryImportTool : BaseTool
{
    private readonly IInventoryService _svc;
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    public InventoryImportTool(
        IInventoryService svc,
        ILogger<InventoryImportTool> logger) : base(logger)
    {
        _svc = svc;
    }

    public override string Name => "inventory_import";

    public override string Description =>
        "Import HW/SW inventory from a base64-encoded eMASS-format Excel workbook " +
        "with optional dry-run validation mode.";

    public override IReadOnlyDictionary<string, ToolParameter> Parameters => new Dictionary<string, ToolParameter>
    {
        ["system_id"] = new() { Name = "system_id", Description = "System GUID, name, or acronym", Type = "string", Required = true },
        ["file_base64"] = new() { Name = "file_base64", Description = "Base64-encoded Excel workbook", Type = "string", Required = true },
        ["dry_run"] = new() { Name = "dry_run", Description = "Validate only, don't persist (default false)", Type = "boolean", Required = false }
    };

    public override async Task<string> ExecuteCoreAsync(
        Dictionary<string, object?> arguments,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var systemId = GetArg<string>(arguments, "system_id");
            var fileBase64 = GetArg<string>(arguments, "file_base64");
            var dryRun = arguments.TryGetValue("dry_run", out var dr) && dr is true;

            byte[] fileBytes;
            try
            {
                fileBytes = Convert.FromBase64String(fileBase64);
            }
            catch (FormatException)
            {
                return Error("INVALID_BASE64", "The file_base64 value is not valid base64.");
            }

            var result = await _svc.ImportFromExcelAsync(fileBytes, systemId, dryRun, "copilot", cancellationToken);
            sw.Stop();

            return JsonSerializer.Serialize(new
            {
                status = "success",
                data = new
                {
                    hardware_created = result.HardwareCreated,
                    software_created = result.SoftwareCreated,
                    rows_skipped = result.RowsSkipped,
                    dry_run = result.DryRun,
                    error_count = result.Errors.Count,
                    errors = result.Errors.Select(e => new { row = e.RowNumber, worksheet = e.Worksheet, error = e.Error })
                },
                metadata = Meta(sw)
            }, JsonOpts);
        }
        catch (InvalidOperationException ex)
        {
            sw.Stop();
            return Error(ExtractErrorCode(ex), ex.Message);
        }
    }

    private static string Error(string code, string message) =>
        JsonSerializer.Serialize(new { status = "error", errorCode = code, message }, JsonOpts);

    private object Meta(Stopwatch sw) => new
    {
        tool = Name,
        duration_ms = sw.ElapsedMilliseconds,
        timestamp = DateTime.UtcNow.ToString("O")
    };

    private static string ExtractErrorCode(InvalidOperationException ex) =>
        ex.Message.Contains(':') ? ex.Message[..ex.Message.IndexOf(':')] : "OPERATION_FAILED";
}

// ────────────────────────────────────────────────────────────────────────────
// inventory_completeness
// ────────────────────────────────────────────────────────────────────────────

/// <summary>
/// MCP tool: inventory_completeness — Run completeness check on a system's inventory.
/// </summary>
public class InventoryCompletenessTool : BaseTool
{
    private readonly IInventoryService _svc;
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    public InventoryCompletenessTool(
        IInventoryService svc,
        ILogger<InventoryCompletenessTool> logger) : base(logger)
    {
        _svc = svc;
    }

    public override string Name => "inventory_completeness";

    public override string Description =>
        "Run a completeness check on a system's inventory identifying missing fields, " +
        "unmatched boundary resources, and hardware without software entries.";

    public override IReadOnlyDictionary<string, ToolParameter> Parameters => new Dictionary<string, ToolParameter>
    {
        ["system_id"] = new() { Name = "system_id", Description = "System GUID, name, or acronym", Type = "string", Required = true }
    };

    public override async Task<string> ExecuteCoreAsync(
        Dictionary<string, object?> arguments,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var systemId = GetArg<string>(arguments, "system_id");
            var result = await _svc.CheckCompletenessAsync(systemId, cancellationToken);
            sw.Stop();

            return JsonSerializer.Serialize(new
            {
                status = "success",
                data = new
                {
                    system_id = result.SystemId,
                    total_items = result.TotalItems,
                    hardware_count = result.HardwareCount,
                    software_count = result.SoftwareCount,
                    completeness_score = result.CompletenessScore,
                    is_complete = result.IsComplete,
                    items_with_missing_fields = result.ItemsWithMissingFields.Select(i => new
                    {
                        item_id = i.ItemId,
                        item_name = i.ItemName,
                        missing_fields = i.MissingFields
                    }),
                    unmatched_boundary_resources = result.UnmatchedBoundaryResources.Select(b => new
                    {
                        boundary_resource_id = b.BoundaryResourceId,
                        resource_name = b.ResourceName,
                        resource_type = b.ResourceType
                    }),
                    hardware_without_software = result.HardwareWithoutSoftware
                },
                metadata = Meta(sw)
            }, JsonOpts);
        }
        catch (InvalidOperationException ex)
        {
            sw.Stop();
            return Error(ExtractErrorCode(ex), ex.Message);
        }
    }

    private static string Error(string code, string message) =>
        JsonSerializer.Serialize(new { status = "error", errorCode = code, message }, JsonOpts);

    private object Meta(Stopwatch sw) => new
    {
        tool = Name,
        duration_ms = sw.ElapsedMilliseconds,
        timestamp = DateTime.UtcNow.ToString("O")
    };

    private static string ExtractErrorCode(InvalidOperationException ex) =>
        ex.Message.Contains(':') ? ex.Message[..ex.Message.IndexOf(':')] : "OPERATION_FAILED";
}

// ────────────────────────────────────────────────────────────────────────────
// inventory_auto_seed
// ────────────────────────────────────────────────────────────────────────────

/// <summary>
/// MCP tool: inventory_auto_seed — Create inventory items from boundary resources.
/// </summary>
public class InventoryAutoSeedTool : BaseTool
{
    private readonly IInventoryService _svc;
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    public InventoryAutoSeedTool(
        IInventoryService svc,
        ILogger<InventoryAutoSeedTool> logger) : base(logger)
    {
        _svc = svc;
    }

    public override string Name => "inventory_auto_seed";

    public override string Description =>
        "Auto-seed inventory items from authorization boundary resources. " +
        "Creates hardware items based on resource types — idempotent (skips already-linked resources).";

    public override IReadOnlyDictionary<string, ToolParameter> Parameters => new Dictionary<string, ToolParameter>
    {
        ["system_id"] = new() { Name = "system_id", Description = "System GUID, name, or acronym", Type = "string", Required = true }
    };

    public override async Task<string> ExecuteCoreAsync(
        Dictionary<string, object?> arguments,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        var systemId = GetArg<string>(arguments, "system_id");

        if (string.IsNullOrWhiteSpace(systemId))
            return Error("INVALID_INPUT", "The 'system_id' parameter is required.");

        try
        {
            var items = await _svc.AutoSeedFromBoundaryAsync(systemId, "copilot-user", cancellationToken);
            sw.Stop();
            return JsonSerializer.Serialize(new
            {
                status = "success",
                data = new
                {
                    system_id = systemId,
                    created_count = items.Count,
                    items = items.Select(i => new
                    {
                        id = i.Id,
                        item_name = i.ItemName,
                        hardware_function = i.HardwareFunction?.ToString(),
                        boundary_resource_id = i.BoundaryResourceId
                    })
                },
                metadata = Meta(sw)
            }, JsonOpts);
        }
        catch (InvalidOperationException ex)
        {
            return Error(ExtractErrorCode(ex), ex.Message);
        }
    }

    private static string Error(string code, string message) =>
        JsonSerializer.Serialize(new { status = "error", errorCode = code, message }, JsonOpts);

    private object Meta(Stopwatch sw) => new
    {
        tool = Name,
        duration_ms = sw.ElapsedMilliseconds,
        timestamp = DateTime.UtcNow.ToString("O")
    };

    private static string ExtractErrorCode(InvalidOperationException ex) =>
        ex.Message.Contains(':') ? ex.Message[..ex.Message.IndexOf(':')] : "OPERATION_FAILED";
}
