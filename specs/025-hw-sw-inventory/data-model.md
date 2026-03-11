# Data Model: Hardware/Software Inventory

**Feature**: 025-hw-sw-inventory | **Date**: 2026-03-11

---

## Entities

### InventoryItem

A single hardware or software component within a system's authorization boundary.

| Field | Type | Constraints | Description |
|-------|------|-------------|-------------|
| `Id` | `string` | `[Key] [MaxLength(36)]`, default `Guid.NewGuid().ToString()` | Unique identifier (GUID string). |
| `RegisteredSystemId` | `string` | `[Required] [MaxLength(36)]`, FK → `RegisteredSystem.Id` | System this item belongs to. |
| `ItemName` | `string` | `[Required] [MaxLength(300)]` | Display name (e.g., "Web Server 01", "Red Hat Enterprise Linux 9"). |
| `Type` | `InventoryItemType` | `[Required]`, enum → string | `Hardware` or `Software`. |
| `HardwareFunction` | `HardwareFunction?` | nullable, enum → string | Function classification for hardware items: Server, Workstation, NetworkDevice, Storage, Other. Null for software items. |
| `SoftwareFunction` | `SoftwareFunction?` | nullable, enum → string | Function classification for software items: OperatingSystem, Database, Middleware, Application, SecurityTool, Other. Null for hardware items. |
| `Manufacturer` | `string?` | `[MaxLength(300)]` | Hardware manufacturer (e.g., "Dell", "Cisco"). |
| `Model` | `string?` | `[MaxLength(300)]` | Hardware model (e.g., "PowerEdge R740"). |
| `SerialNumber` | `string?` | `[MaxLength(200)]` | Hardware serial number. |
| `IpAddress` | `string?` | `[MaxLength(45)]` | IPv4 or IPv6 address. Max length 45 covers IPv6 full notation. |
| `MacAddress` | `string?` | `[MaxLength(17)]` | MAC address in colon-separated hex (e.g., `00:1A:2B:3C:4D:5E`). |
| `Location` | `string?` | `[MaxLength(500)]` | Physical or logical location. |
| `Vendor` | `string?` | `[MaxLength(300)]` | Software vendor (e.g., "Microsoft", "Red Hat"). |
| `Version` | `string?` | `[MaxLength(100)]` | Software version (e.g., "9.3", "2022 SP1"). |
| `PatchLevel` | `string?` | `[MaxLength(200)]` | Current patch level or update identifier. |
| `LicenseType` | `string?` | `[MaxLength(200)]` | License type (e.g., "Enterprise", "Open Source", "Government"). |
| `Status` | `InventoryItemStatus` | `[Required]`, enum → string, default `Active` | `Active` or `Decommissioned`. |
| `ParentHardwareId` | `string?` | `[MaxLength(36)]`, FK → self (`InventoryItem.Id`) | Optional reference to parent hardware item (for SW installed on HW). Null for standalone/SaaS/PaaS software. |
| `BoundaryResourceId` | `string?` | `[MaxLength(36)]`, FK → `AuthorizationBoundary.Id` | Optional link to boundary resource for auto-seed idempotency. |
| `DecommissionDate` | `DateTime?` | | UTC date when item was decommissioned. |
| `DecommissionRationale` | `string?` | `[MaxLength(2000)]` | Reason for decommissioning. |
| `CreatedBy` | `string` | `[Required] [MaxLength(200)]` | User who created the item. |
| `CreatedAt` | `DateTime` | default `DateTime.UtcNow` | Creation timestamp (UTC). |
| `ModifiedBy` | `string?` | `[MaxLength(200)]` | User who last modified the item. |
| `ModifiedAt` | `DateTime?` | | Last modification timestamp (UTC). |

#### Navigation Properties

| Property | Type | Relationship |
|----------|------|-------------|
| `RegisteredSystem` | `RegisteredSystem?` | Many-to-one (FK: `RegisteredSystemId`) |
| `ParentHardware` | `InventoryItem?` | Self-referencing many-to-one (FK: `ParentHardwareId`) |
| `InstalledSoftware` | `ICollection<InventoryItem>` | Self-referencing one-to-many (inverse of `ParentHardware`) |
| `BoundaryResource` | `AuthorizationBoundary?` | Many-to-one (FK: `BoundaryResourceId`) |

---

## Enums

### InventoryItemType

```csharp
public enum InventoryItemType { Hardware, Software }
```

### InventoryItemStatus

```csharp
public enum InventoryItemStatus { Active, Decommissioned }
```

### HardwareFunction

```csharp
public enum HardwareFunction { Server, Workstation, NetworkDevice, Storage, Other }
```

### SoftwareFunction

```csharp
public enum SoftwareFunction { OperatingSystem, Database, Middleware, Application, SecurityTool, Other }
```

---

## Computed Types (Not Persisted)

### InventoryCompleteness

Returned by `IInventoryService.CheckCompletenessAsync()`. Not stored in the database.

| Field | Type | Description |
|-------|------|-------------|
| `SystemId` | `string` | The system being checked. |
| `TotalItems` | `int` | Total inventory items (active only). |
| `HardwareCount` | `int` | Total active hardware items. |
| `SoftwareCount` | `int` | Total active software items. |
| `ItemsWithMissingFields` | `IReadOnlyList<InventoryIssue>` | Items missing required fields per FR-018. |
| `UnmatchedBoundaryResources` | `IReadOnlyList<UnmatchedBoundaryResource>` | Boundary resources with no inventory item. |
| `HardwareWithoutSoftware` | `IReadOnlyList<string>` | IDs of HW items with no SW children. |
| `CompletenessScore` | `double` | Percentage: (items without issues) / total items × 100. |
| `IsComplete` | `bool` | True if no issues found across all three dimensions. |

### InventoryIssue

| Field | Type | Description |
|-------|------|-------------|
| `ItemId` | `string` | The inventory item ID. |
| `ItemName` | `string` | The inventory item name. |
| `MissingFields` | `IReadOnlyList<string>` | List of field names that are missing. |

### UnmatchedBoundaryResource

| Field | Type | Description |
|-------|------|-------------|
| `BoundaryResourceId` | `string` | The `AuthorizationBoundary.Id`. |
| `ResourceName` | `string?` | The boundary resource display name. |
| `ResourceType` | `string` | The Azure resource type string. |

### InventoryImportResult

Returned by `IInventoryService.ImportFromExcelAsync()`.

| Field | Type | Description |
|-------|------|-------------|
| `SystemId` | `string` | Target system ID. |
| `DryRun` | `bool` | Whether this was a dry-run. |
| `HardwareCreated` | `int` | Number of hardware items created. |
| `SoftwareCreated` | `int` | Number of software items created. |
| `RowsSkipped` | `int` | Number of rows skipped due to errors. |
| `Errors` | `IReadOnlyList<ImportRowError>` | Per-row error details. |

### ImportRowError

| Field | Type | Description |
|-------|------|-------------|
| `Worksheet` | `string` | "Hardware" or "Software". |
| `RowNumber` | `int` | 1-based row number in the worksheet. |
| `Error` | `string` | Human-readable error description. |

---

## EF Core Configuration

### DbContext Registration

Add to `AtoCopilotContext`:

```csharp
// ─── HW/SW Inventory (Feature 025) ──────────────────────────────────────
/// <summary>Hardware and software inventory items for registered systems.</summary>
public DbSet<InventoryItem> InventoryItems => Set<InventoryItem>();
```

### OnModelCreating Configuration

```csharp
// ─── Inventory Item (Feature 025) ────────────────────────────────────────
modelBuilder.Entity<InventoryItem>(e =>
{
    e.HasKey(i => i.Id);

    // Enum-to-string conversions
    e.Property(i => i.Type).HasConversion<string>();
    e.Property(i => i.Status).HasConversion<string>();
    e.Property(i => i.HardwareFunction).HasConversion<string>();
    e.Property(i => i.SoftwareFunction).HasConversion<string>();

    // FK → RegisteredSystem (required)
    e.HasOne(i => i.RegisteredSystem)
     .WithMany()
     .HasForeignKey(i => i.RegisteredSystemId)
     .OnDelete(DeleteBehavior.Cascade);

    // Self-referencing FK: SW → parent HW (optional)
    e.HasOne(i => i.ParentHardware)
     .WithMany(i => i.InstalledSoftware)
     .HasForeignKey(i => i.ParentHardwareId)
     .OnDelete(DeleteBehavior.Restrict);

    // FK → AuthorizationBoundary (optional, for auto-seed idempotency)
    e.HasOne(i => i.BoundaryResource)
     .WithMany()
     .HasForeignKey(i => i.BoundaryResourceId)
     .OnDelete(DeleteBehavior.SetNull);

    // Indexes
    e.HasIndex(i => i.RegisteredSystemId);
    e.HasIndex(i => new { i.RegisteredSystemId, i.Type });
    e.HasIndex(i => new { i.RegisteredSystemId, i.IpAddress })
     .IsUnique()
     .HasFilter("[IpAddress] IS NOT NULL");
    e.HasIndex(i => i.BoundaryResourceId);
});
```

---

## Entity Relationship Diagram

```
RegisteredSystem (1) ──────── (*) InventoryItem
                                      │
                                      │ ParentHardwareId (self-ref, optional)
                                      ▼
                               InventoryItem (parent HW)
                                      │
                                      │ InstalledSoftware (collection)
                                      ▼
                               InventoryItem (child SW) [0..*]

AuthorizationBoundary (1) ──── (0..1) InventoryItem.BoundaryResourceId
```

- `RegisteredSystem` → `InventoryItem`: One system has many inventory items (Cascade delete).
- `InventoryItem` → `InventoryItem` (self): Software optionally references parent hardware (Restrict delete — cannot delete HW with active SW children).
- `AuthorizationBoundary` → `InventoryItem`: Optional link for auto-seed tracking (SetNull on boundary delete).
