using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Ato.Copilot.Core.Data.Context;
using Ato.Copilot.Core.Interfaces.Compliance;
using Ato.Copilot.Core.Models.Compliance;

namespace Ato.Copilot.Agents.Compliance.Services;

/// <summary>
/// Implements HW/SW inventory management: CRUD, export, import,
/// completeness checking, and auto-seed from boundary resources.
/// </summary>
/// <remarks>Feature 025 – HW/SW Inventory.</remarks>
public class InventoryService : IInventoryService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<InventoryService> _logger;

    public InventoryService(
        IServiceScopeFactory scopeFactory,
        ILogger<InventoryService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    // ─── T007: AddItemAsync ──────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<InventoryItem> AddItemAsync(
        string registeredSystemId,
        InventoryItemInput input,
        string addedBy,
        CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AtoCopilotContext>();

        // System existence check
        var systemExists = await db.RegisteredSystems
            .AnyAsync(s => s.Id == registeredSystemId, cancellationToken);
        if (!systemExists)
            throw new InvalidOperationException("SYSTEM_NOT_FOUND: System does not exist.");

        // Parse enums
        if (input.Type == null)
            throw new InvalidOperationException("VALIDATION_FAILED: 'type' is required.");
        if (string.IsNullOrWhiteSpace(input.ItemName))
            throw new InvalidOperationException("VALIDATION_FAILED: 'item_name' is required.");

        var itemType = input.Type.Value;

        // FR-018: Function-based required field validation
        if (itemType == InventoryItemType.Hardware)
        {
            if (input.HardwareFunction == null)
                throw new InvalidOperationException("VALIDATION_FAILED: 'function' is required for hardware items.");
            if (string.IsNullOrWhiteSpace(input.Manufacturer))
                throw new InvalidOperationException("VALIDATION_FAILED: 'manufacturer' is required for hardware items.");

            // Server / NetworkDevice additionally require IP
            if (input.HardwareFunction is HardwareFunction.Server or HardwareFunction.NetworkDevice
                && string.IsNullOrWhiteSpace(input.IpAddress))
            {
                throw new InvalidOperationException("VALIDATION_FAILED: 'ip_address' is required for server and network device hardware.");
            }
        }
        else // Software
        {
            if (input.SoftwareFunction == null)
                throw new InvalidOperationException("VALIDATION_FAILED: 'function' is required for software items.");
            if (string.IsNullOrWhiteSpace(input.Vendor))
                throw new InvalidOperationException("VALIDATION_FAILED: 'vendor' is required for software items.");
            if (string.IsNullOrWhiteSpace(input.Version))
                throw new InvalidOperationException("VALIDATION_FAILED: 'version' is required for software items.");
        }

        // FR-016: Unique IP within system
        if (!string.IsNullOrWhiteSpace(input.IpAddress))
        {
            var ipExists = await db.InventoryItems
                .AnyAsync(i => i.RegisteredSystemId == registeredSystemId
                    && i.IpAddress == input.IpAddress
                    && i.Status == InventoryItemStatus.Active,
                    cancellationToken);
            if (ipExists)
                throw new InvalidOperationException("DUPLICATE_IP: IP address already exists in this system's inventory.");
        }

        // ParentHardwareId validation
        if (!string.IsNullOrWhiteSpace(input.ParentHardwareId))
        {
            var parent = await db.InventoryItems
                .FirstOrDefaultAsync(i => i.Id == input.ParentHardwareId, cancellationToken);
            if (parent == null)
                throw new InvalidOperationException("PARENT_NOT_FOUND: Parent hardware item does not exist.");
            if (parent.Type != InventoryItemType.Hardware)
                throw new InvalidOperationException("PARENT_NOT_FOUND: Parent item is not a hardware item.");
        }

        var item = new InventoryItem
        {
            RegisteredSystemId = registeredSystemId,
            ItemName = input.ItemName,
            Type = itemType,
            HardwareFunction = input.HardwareFunction,
            SoftwareFunction = input.SoftwareFunction,
            Manufacturer = input.Manufacturer,
            Model = input.Model,
            SerialNumber = input.SerialNumber,
            IpAddress = input.IpAddress,
            MacAddress = input.MacAddress,
            Location = input.Location,
            Vendor = input.Vendor,
            Version = input.Version,
            PatchLevel = input.PatchLevel,
            LicenseType = input.LicenseType,
            ParentHardwareId = input.ParentHardwareId,
            CreatedBy = addedBy
        };

        db.InventoryItems.Add(item);
        await db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Added inventory item {ItemId} ({Type}/{Name}) to system {SystemId}",
            item.Id, item.Type, item.ItemName, registeredSystemId);

        return item;
    }

    // ─── T008: UpdateItemAsync ───────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<InventoryItem> UpdateItemAsync(
        string itemId,
        InventoryItemInput input,
        string modifiedBy,
        CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AtoCopilotContext>();

        var item = await db.InventoryItems
            .FirstOrDefaultAsync(i => i.Id == itemId, cancellationToken);
        if (item == null)
            throw new InvalidOperationException("ITEM_NOT_FOUND: Inventory item does not exist.");

        // Apply partial updates (null = no change)
        if (input.ItemName != null) item.ItemName = input.ItemName;
        if (input.HardwareFunction != null) item.HardwareFunction = input.HardwareFunction;
        if (input.SoftwareFunction != null) item.SoftwareFunction = input.SoftwareFunction;
        if (input.Manufacturer != null) item.Manufacturer = input.Manufacturer;
        if (input.Model != null) item.Model = input.Model;
        if (input.SerialNumber != null) item.SerialNumber = input.SerialNumber;
        if (input.MacAddress != null) item.MacAddress = input.MacAddress;
        if (input.Location != null) item.Location = input.Location;
        if (input.Vendor != null) item.Vendor = input.Vendor;
        if (input.Version != null) item.Version = input.Version;
        if (input.PatchLevel != null) item.PatchLevel = input.PatchLevel;
        if (input.LicenseType != null) item.LicenseType = input.LicenseType;
        if (input.ParentHardwareId != null) item.ParentHardwareId = input.ParentHardwareId;

        // IP address update with uniqueness re-validation
        if (input.IpAddress != null)
        {
            if (!string.IsNullOrWhiteSpace(input.IpAddress))
            {
                var ipExists = await db.InventoryItems
                    .AnyAsync(i => i.RegisteredSystemId == item.RegisteredSystemId
                        && i.IpAddress == input.IpAddress
                        && i.Id != itemId
                        && i.Status == InventoryItemStatus.Active,
                        cancellationToken);
                if (ipExists)
                    throw new InvalidOperationException("DUPLICATE_IP: IP address already exists in this system's inventory.");
            }
            item.IpAddress = input.IpAddress;
        }

        // FR-003: Record modifier and timestamp
        item.ModifiedBy = modifiedBy;
        item.ModifiedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Updated inventory item {ItemId} by {ModifiedBy}", itemId, modifiedBy);

        return item;
    }

    // ─── T009: DecommissionItemAsync ─────────────────────────────────────────

    /// <inheritdoc />
    public async Task<InventoryItem> DecommissionItemAsync(
        string itemId,
        string rationale,
        string decommissionedBy,
        CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AtoCopilotContext>();

        var item = await db.InventoryItems
            .Include(i => i.InstalledSoftware)
            .FirstOrDefaultAsync(i => i.Id == itemId, cancellationToken);
        if (item == null)
            throw new InvalidOperationException("ITEM_NOT_FOUND: Inventory item does not exist.");

        if (item.Status == InventoryItemStatus.Decommissioned)
            throw new InvalidOperationException("ALREADY_DECOMMISSIONED: Item is already decommissioned.");

        // Soft-delete
        item.Status = InventoryItemStatus.Decommissioned;
        item.DecommissionDate = DateTime.UtcNow;
        item.DecommissionRationale = rationale;
        item.ModifiedBy = decommissionedBy;
        item.ModifiedAt = DateTime.UtcNow;

        // FR-005: Cascade decommission to child software
        if (item.Type == InventoryItemType.Hardware && item.InstalledSoftware != null)
        {
            foreach (var sw in item.InstalledSoftware.Where(s => s.Status == InventoryItemStatus.Active))
            {
                sw.Status = InventoryItemStatus.Decommissioned;
                sw.DecommissionDate = DateTime.UtcNow;
                sw.DecommissionRationale = rationale;
                sw.ModifiedBy = decommissionedBy;
                sw.ModifiedAt = DateTime.UtcNow;
            }
        }

        await db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Decommissioned inventory item {ItemId} by {DecommissionedBy}",
            itemId, decommissionedBy);

        return item;
    }

    // ─── T010: GetItemAsync ──────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<InventoryItem?> GetItemAsync(
        string itemId,
        CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AtoCopilotContext>();

        // FR-007: Eager-load InstalledSoftware for hardware items
        return await db.InventoryItems
            .Include(i => i.InstalledSoftware)
            .FirstOrDefaultAsync(i => i.Id == itemId, cancellationToken);
    }

    // ─── T011: AutoSeedFromBoundaryAsync ─────────────────────────────────────

    /// <inheritdoc />
    public async Task<IReadOnlyList<InventoryItem>> AutoSeedFromBoundaryAsync(
        string registeredSystemId,
        string seededBy,
        CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AtoCopilotContext>();

        // System existence check
        var systemExists = await db.RegisteredSystems
            .AnyAsync(s => s.Id == registeredSystemId, cancellationToken);
        if (!systemExists)
            throw new InvalidOperationException("SYSTEM_NOT_FOUND: System does not exist.");

        // Get in-boundary resources
        var boundaryResources = await db.AuthorizationBoundaries
            .Where(b => b.RegisteredSystemId == registeredSystemId && b.IsInBoundary)
            .ToListAsync(cancellationToken);

        if (boundaryResources.Count == 0)
            throw new InvalidOperationException("NO_BOUNDARY_DATA: System has no boundary resources.");

        // FR-014: Get already-linked boundary resource IDs for idempotency
        var linkedBoundaryIds = await db.InventoryItems
            .Where(i => i.RegisteredSystemId == registeredSystemId && i.BoundaryResourceId != null)
            .Select(i => i.BoundaryResourceId!)
            .ToListAsync(cancellationToken);

        var linkedSet = new HashSet<string>(linkedBoundaryIds);
        var created = new List<InventoryItem>();

        foreach (var br in boundaryResources)
        {
            if (linkedSet.Contains(br.Id))
                continue; // Already linked — idempotent skip

            var hwFunction = MapResourceTypeToFunction(br.ResourceType);

            var item = new InventoryItem
            {
                RegisteredSystemId = registeredSystemId,
                ItemName = br.ResourceName ?? br.ResourceId,
                Type = InventoryItemType.Hardware,
                HardwareFunction = hwFunction,
                Manufacturer = br.InheritanceProvider,
                BoundaryResourceId = br.Id,
                CreatedBy = seededBy
            };

            db.InventoryItems.Add(item);
            created.Add(item);
        }

        if (created.Count > 0)
            await db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Auto-seeded {Count} inventory items from boundary resources for system {SystemId}",
            created.Count, registeredSystemId);

        return created;
    }

    // ─── T017: ListItemsAsync ────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<IReadOnlyList<InventoryItem>> ListItemsAsync(
        string registeredSystemId,
        InventoryListOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AtoCopilotContext>();

        var opts = options ?? new InventoryListOptions();

        IQueryable<InventoryItem> query = db.InventoryItems
            .Where(i => i.RegisteredSystemId == registeredSystemId);

        // Status filter (default Active)
        if (opts.Status != null)
            query = query.Where(i => i.Status == opts.Status.Value);
        else
            query = query.Where(i => i.Status == InventoryItemStatus.Active);

        // Type filter
        if (opts.Type != null)
            query = query.Where(i => i.Type == opts.Type.Value);

        // Function filter (match against HW or SW function as string)
        if (!string.IsNullOrWhiteSpace(opts.Function))
        {
            var fn = opts.Function;
            query = query.Where(i =>
                (i.HardwareFunction != null && i.HardwareFunction.ToString()! == fn) ||
                (i.SoftwareFunction != null && i.SoftwareFunction.ToString()! == fn));
        }

        // Vendor/Manufacturer filter (contains)
        if (!string.IsNullOrWhiteSpace(opts.Vendor))
        {
            var vendor = opts.Vendor;
            query = query.Where(i =>
                (i.Vendor != null && i.Vendor.Contains(vendor)) ||
                (i.Manufacturer != null && i.Manufacturer.Contains(vendor)));
        }

        // Free-text search on ItemName
        if (!string.IsNullOrWhiteSpace(opts.SearchText))
        {
            var search = opts.SearchText;
            query = query.Where(i => i.ItemName.Contains(search));
        }

        // Pagination
        var pageSize = opts.PageSize > 0 ? opts.PageSize : 50;
        var pageNumber = opts.PageNumber > 0 ? opts.PageNumber : 1;

        query = query
            .OrderBy(i => i.ItemName)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize);

        return await query.ToListAsync(cancellationToken);
    }

    // ─── T019: ExportToExcelAsync ────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<byte[]> ExportToExcelAsync(
        string registeredSystemId,
        InventoryExportOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AtoCopilotContext>();

        var systemExists = await db.RegisteredSystems
            .AnyAsync(s => s.Id == registeredSystemId, cancellationToken);
        if (!systemExists)
            throw new InvalidOperationException("SYSTEM_NOT_FOUND: System does not exist.");

        var opts = options ?? new InventoryExportOptions();

        IQueryable<InventoryItem> query = db.InventoryItems
            .Where(i => i.RegisteredSystemId == registeredSystemId);

        if (!opts.IncludeDecommissioned)
            query = query.Where(i => i.Status == InventoryItemStatus.Active);

        var items = await query.ToListAsync(cancellationToken);

        if (items.Count == 0)
            throw new InvalidOperationException("NO_INVENTORY_DATA: System has no inventory items.");

        var hwItems = (opts.ExportType?.Equals("software", StringComparison.OrdinalIgnoreCase) == true)
            ? new List<InventoryItem>()
            : items.Where(i => i.Type == InventoryItemType.Hardware).ToList();

        var swItems = (opts.ExportType?.Equals("hardware", StringComparison.OrdinalIgnoreCase) == true)
            ? new List<InventoryItem>()
            : items.Where(i => i.Type == InventoryItemType.Software).ToList();

        using var workbook = new ClosedXML.Excel.XLWorkbook();

        // Hardware worksheet
        if (hwItems.Count > 0 || opts.ExportType?.Equals("software", StringComparison.OrdinalIgnoreCase) != true)
        {
            var hwSheet = workbook.Worksheets.Add("Hardware");
            var hwHeaders = new[] { "System Name", "Hardware Name", "Manufacturer", "Model", "Serial Number", "Function", "IP Address", "MAC Address", "Location", "Status" };
            for (var c = 0; c < hwHeaders.Length; c++)
                hwSheet.Cell(1, c + 1).Value = hwHeaders[c];

            for (var r = 0; r < hwItems.Count; r++)
            {
                var hw = hwItems[r];
                hwSheet.Cell(r + 2, 1).Value = hw.RegisteredSystemId;
                hwSheet.Cell(r + 2, 2).Value = hw.ItemName;
                hwSheet.Cell(r + 2, 3).Value = hw.Manufacturer ?? string.Empty;
                hwSheet.Cell(r + 2, 4).Value = hw.Model ?? string.Empty;
                hwSheet.Cell(r + 2, 5).Value = hw.SerialNumber ?? string.Empty;
                hwSheet.Cell(r + 2, 6).Value = hw.HardwareFunction?.ToString() ?? string.Empty;
                hwSheet.Cell(r + 2, 7).Value = hw.IpAddress ?? string.Empty;
                hwSheet.Cell(r + 2, 8).Value = hw.MacAddress ?? string.Empty;
                hwSheet.Cell(r + 2, 9).Value = hw.Location ?? string.Empty;
                hwSheet.Cell(r + 2, 10).Value = hw.Status.ToString();
            }
        }

        // Software worksheet
        if (swItems.Count > 0 || opts.ExportType?.Equals("hardware", StringComparison.OrdinalIgnoreCase) != true)
        {
            var swSheet = workbook.Worksheets.Add("Software");
            var swHeaders = new[] { "System Name", "Software Name", "Vendor", "Version", "Patch Level", "Function", "License Type", "Installed On", "Status" };
            for (var c = 0; c < swHeaders.Length; c++)
                swSheet.Cell(1, c + 1).Value = swHeaders[c];

            for (var r = 0; r < swItems.Count; r++)
            {
                var sw = swItems[r];
                swSheet.Cell(r + 2, 1).Value = sw.RegisteredSystemId;
                swSheet.Cell(r + 2, 2).Value = sw.ItemName;
                swSheet.Cell(r + 2, 3).Value = sw.Vendor ?? string.Empty;
                swSheet.Cell(r + 2, 4).Value = sw.Version ?? string.Empty;
                swSheet.Cell(r + 2, 5).Value = sw.PatchLevel ?? string.Empty;
                swSheet.Cell(r + 2, 6).Value = sw.SoftwareFunction?.ToString() ?? string.Empty;
                swSheet.Cell(r + 2, 7).Value = sw.LicenseType ?? string.Empty;
                swSheet.Cell(r + 2, 8).Value = sw.ParentHardwareId ?? string.Empty;
                swSheet.Cell(r + 2, 9).Value = sw.Status.ToString();
            }
        }

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        return ms.ToArray();
    }

    // ─── T021: ImportFromExcelAsync ──────────────────────────────────────────

    /// <inheritdoc />
    public async Task<InventoryImportResult> ImportFromExcelAsync(
        byte[] fileBytes,
        string registeredSystemId,
        bool dryRun,
        string importedBy,
        CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AtoCopilotContext>();

        var systemExists = await db.RegisteredSystems
            .AnyAsync(s => s.Id == registeredSystemId, cancellationToken);
        if (!systemExists)
            throw new InvalidOperationException("SYSTEM_NOT_FOUND: System does not exist.");

        var existingItems = await db.InventoryItems
            .Where(i => i.RegisteredSystemId == registeredSystemId)
            .Select(i => new { i.ItemName, i.Type, i.IpAddress })
            .ToListAsync(cancellationToken);
        var existingNames = new HashSet<string>(existingItems.Select(i => $"{i.ItemName}|{i.Type}"));
        var existingIps = new HashSet<string>(existingItems.Where(i => i.IpAddress != null).Select(i => i.IpAddress!));

        var result = new InventoryImportResult { SystemId = registeredSystemId };
        var importErrors = new List<ImportRowError>();

        using var ms = new MemoryStream(fileBytes);
        using var workbook = new ClosedXML.Excel.XLWorkbook(ms);

        // Process Hardware worksheet first (so SW "Installed On" references resolve)
        if (workbook.TryGetWorksheet("Hardware", out var hwSheet))
        {
            var lastRow = hwSheet.LastRowUsed()?.RowNumber() ?? 1;
            for (var row = 2; row <= lastRow; row++)
            {
                var name = hwSheet.Cell(row, 2).GetString().Trim();
                var mfg = hwSheet.Cell(row, 3).GetString().Trim();
                var model = hwSheet.Cell(row, 4).GetString().Trim();
                var serial = hwSheet.Cell(row, 5).GetString().Trim();
                var funcStr = hwSheet.Cell(row, 6).GetString().Trim();
                var ip = hwSheet.Cell(row, 7).GetString().Trim();
                var mac = hwSheet.Cell(row, 8).GetString().Trim();
                var loc = hwSheet.Cell(row, 9).GetString().Trim();

                // Validate required fields (FR-018)
                var errors = new List<string>();
                if (string.IsNullOrWhiteSpace(name)) errors.Add("Hardware Name is required");
                if (string.IsNullOrWhiteSpace(mfg)) errors.Add("Manufacturer is required");

                HardwareFunction? hwFunc = null;
                if (string.IsNullOrWhiteSpace(funcStr))
                    errors.Add("Function is required");
                else if (!Enum.TryParse<HardwareFunction>(funcStr, true, out var parsedFunc))
                    errors.Add($"Invalid function '{funcStr}'");
                else
                    hwFunc = parsedFunc;

                if (hwFunc is HardwareFunction.Server or HardwareFunction.NetworkDevice && string.IsNullOrWhiteSpace(ip))
                    errors.Add("IP Address is required for server/network device");

                // Duplicate check
                if (!string.IsNullOrWhiteSpace(name) && existingNames.Contains($"{name}|Hardware"))
                {
                    result.RowsSkipped++;
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(ip) && existingIps.Contains(ip))
                    errors.Add($"Duplicate IP address: {ip}");

                if (errors.Count > 0)
                {
                    importErrors.Add(new ImportRowError { RowNumber = row, Worksheet = "Hardware", Error = string.Join("; ", errors) });
                    continue;
                }

                var item = new InventoryItem
                {
                    RegisteredSystemId = registeredSystemId,
                    ItemName = name,
                    Type = InventoryItemType.Hardware,
                    HardwareFunction = hwFunc,
                    Manufacturer = string.IsNullOrWhiteSpace(mfg) ? null : mfg,
                    Model = string.IsNullOrWhiteSpace(model) ? null : model,
                    SerialNumber = string.IsNullOrWhiteSpace(serial) ? null : serial,
                    IpAddress = string.IsNullOrWhiteSpace(ip) ? null : ip,
                    MacAddress = string.IsNullOrWhiteSpace(mac) ? null : mac,
                    Location = string.IsNullOrWhiteSpace(loc) ? null : loc,
                    CreatedBy = importedBy
                };

                if (!dryRun)
                    db.InventoryItems.Add(item);

                existingNames.Add($"{name}|Hardware");
                if (!string.IsNullOrWhiteSpace(ip)) existingIps.Add(ip);
                result.HardwareCreated++;
            }
        }

        // Process Software worksheet
        if (workbook.TryGetWorksheet("Software", out var swSheet))
        {
            var lastRow = swSheet.LastRowUsed()?.RowNumber() ?? 1;
            for (var row = 2; row <= lastRow; row++)
            {
                var name = swSheet.Cell(row, 2).GetString().Trim();
                var vendor = swSheet.Cell(row, 3).GetString().Trim();
                var version = swSheet.Cell(row, 4).GetString().Trim();
                var patchLevel = swSheet.Cell(row, 5).GetString().Trim();
                var funcStr = swSheet.Cell(row, 6).GetString().Trim();
                var licType = swSheet.Cell(row, 7).GetString().Trim();
                var installedOn = swSheet.Cell(row, 8).GetString().Trim();

                // Validate required fields (FR-018)
                var errors = new List<string>();
                if (string.IsNullOrWhiteSpace(name)) errors.Add("Software Name is required");
                if (string.IsNullOrWhiteSpace(vendor)) errors.Add("Vendor is required");
                if (string.IsNullOrWhiteSpace(version)) errors.Add("Version is required");

                SoftwareFunction? swFunc = null;
                if (!string.IsNullOrWhiteSpace(funcStr) && !Enum.TryParse<SoftwareFunction>(funcStr, true, out var parsedSwFunc))
                    errors.Add($"Invalid function '{funcStr}'");
                else if (!string.IsNullOrWhiteSpace(funcStr))
                    swFunc = Enum.Parse<SoftwareFunction>(funcStr, true);

                // Duplicate check
                if (!string.IsNullOrWhiteSpace(name) && existingNames.Contains($"{name}|Software"))
                {
                    result.RowsSkipped++;
                    continue;
                }

                if (errors.Count > 0)
                {
                    importErrors.Add(new ImportRowError { RowNumber = row, Worksheet = "Software", Error = string.Join("; ", errors) });
                    continue;
                }

                var item = new InventoryItem
                {
                    RegisteredSystemId = registeredSystemId,
                    ItemName = name,
                    Type = InventoryItemType.Software,
                    SoftwareFunction = swFunc,
                    Vendor = string.IsNullOrWhiteSpace(vendor) ? null : vendor,
                    Version = string.IsNullOrWhiteSpace(version) ? null : version,
                    PatchLevel = string.IsNullOrWhiteSpace(patchLevel) ? null : patchLevel,
                    LicenseType = string.IsNullOrWhiteSpace(licType) ? null : licType,
                    ParentHardwareId = string.IsNullOrWhiteSpace(installedOn) ? null : installedOn,
                    CreatedBy = importedBy
                };

                if (!dryRun)
                    db.InventoryItems.Add(item);

                existingNames.Add($"{name}|Software");
                result.SoftwareCreated++;
            }
        }

        result.Errors = importErrors;

        if (!dryRun && (result.HardwareCreated > 0 || result.SoftwareCreated > 0))
            await db.SaveChangesAsync(cancellationToken);

        result.DryRun = dryRun;

        _logger.LogInformation("Import completed for system {SystemId}: {HwCreated} HW + {SwCreated} SW created, {Skipped} skipped, {Errors} errors (dry_run={DryRun})",
            registeredSystemId, result.HardwareCreated, result.SoftwareCreated, result.RowsSkipped, result.Errors.Count, dryRun);

        return result;
    }

    // ─── T023: CheckCompletenessAsync ────────────────────────────────────────

    /// <inheritdoc />
    public async Task<InventoryCompleteness> CheckCompletenessAsync(
        string registeredSystemId,
        CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AtoCopilotContext>();

        var systemExists = await db.RegisteredSystems
            .AnyAsync(s => s.Id == registeredSystemId, cancellationToken);
        if (!systemExists)
            throw new InvalidOperationException("SYSTEM_NOT_FOUND: System does not exist.");

        var items = await db.InventoryItems
            .Where(i => i.RegisteredSystemId == registeredSystemId && i.Status != InventoryItemStatus.Decommissioned)
            .ToListAsync(cancellationToken);

        var hwItems = items.Where(i => i.Type == InventoryItemType.Hardware).ToList();
        var swItems = items.Where(i => i.Type == InventoryItemType.Software).ToList();

        // Dimension 1: Missing required fields (FR-018)
        var missingFields = new List<InventoryIssue>();
        foreach (var item in items)
        {
            var missing = new List<string>();
            if (string.IsNullOrWhiteSpace(item.ItemName)) missing.Add("ItemName");

            if (item.Type == InventoryItemType.Hardware)
            {
                if (string.IsNullOrWhiteSpace(item.Manufacturer)) missing.Add("Manufacturer");
                if (item.HardwareFunction == null) missing.Add("HardwareFunction");
                if (item.HardwareFunction is HardwareFunction.Server or HardwareFunction.NetworkDevice
                    && string.IsNullOrWhiteSpace(item.IpAddress))
                    missing.Add("IpAddress");
            }
            else
            {
                if (string.IsNullOrWhiteSpace(item.Vendor)) missing.Add("Vendor");
                if (string.IsNullOrWhiteSpace(item.Version)) missing.Add("Version");
            }

            if (missing.Count > 0)
                missingFields.Add(new InventoryIssue { ItemId = item.Id, ItemName = item.ItemName, MissingFields = missing });
        }

        // Dimension 2: Boundary resources not represented in inventory
        var linkedBoundaryIds = items
            .Where(i => i.BoundaryResourceId != null)
            .Select(i => i.BoundaryResourceId!)
            .ToHashSet();

        var unmatchedBoundary = await db.AuthorizationBoundaries
            .Where(b => b.RegisteredSystemId == registeredSystemId && b.IsInBoundary)
            .Where(b => !linkedBoundaryIds.Contains(b.Id))
            .Select(b => new UnmatchedBoundaryResource
            {
                BoundaryResourceId = b.Id,
                ResourceName = b.ResourceName,
                ResourceType = b.ResourceType
            })
            .ToListAsync(cancellationToken);

        // Dimension 3: Hardware items with zero installed software
        var hwIdsWithSw = swItems
            .Where(s => s.ParentHardwareId != null)
            .Select(s => s.ParentHardwareId!)
            .ToHashSet();
        var hwWithoutSw = hwItems
            .Where(h => !hwIdsWithSw.Contains(h.Id))
            .Select(h => h.Id)
            .ToList();

        // Compute score
        var issueItemIds = missingFields.Select(i => i.ItemId).ToHashSet();
        var itemsWithIssues = issueItemIds.Count + hwWithoutSw.Count(id => !issueItemIds.Contains(id));
        var score = items.Count > 0 ? Math.Round((double)(items.Count - itemsWithIssues) / items.Count * 100, 1) : 100.0;
        var isComplete = missingFields.Count == 0 && unmatchedBoundary.Count == 0 && hwWithoutSw.Count == 0;

        return new InventoryCompleteness
        {
            SystemId = registeredSystemId,
            TotalItems = items.Count,
            HardwareCount = hwItems.Count,
            SoftwareCount = swItems.Count,
            ItemsWithMissingFields = missingFields,
            UnmatchedBoundaryResources = unmatchedBoundary,
            HardwareWithoutSoftware = hwWithoutSw,
            CompletenessScore = score,
            IsComplete = isComplete
        };
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private static HardwareFunction MapResourceTypeToFunction(string resourceType)
    {
        var upper = resourceType.ToUpperInvariant();
        if (upper.Contains("VM") || upper.Contains("VIRTUAL") || upper.Contains("COMPUTE"))
            return HardwareFunction.Server;
        if (upper.Contains("NETWORK") || upper.Contains("FIREWALL") || upper.Contains("GATEWAY")
            || upper.Contains("LOADBALANCER") || upper.Contains("NSG"))
            return HardwareFunction.NetworkDevice;
        if (upper.Contains("STORAGE") || upper.Contains("DISK") || upper.Contains("BLOB"))
            return HardwareFunction.Storage;
        return HardwareFunction.Other;
    }
}
