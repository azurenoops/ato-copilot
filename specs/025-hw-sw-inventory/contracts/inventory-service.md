# Interface Contracts: IInventoryService

**Feature**: 025-hw-sw-inventory | **Date**: 2026-03-11

---

## Service Interface: `IInventoryService`

**Namespace**: `Ato.Copilot.Core.Interfaces.Compliance`
**Registration**: `services.AddSingleton<IInventoryService, InventoryService>()`

### Methods

---

#### `AddItemAsync`

Add a hardware or software inventory item to a system.

```csharp
Task<InventoryItem> AddItemAsync(
    string registeredSystemId,
    InventoryItemInput input,
    string addedBy,
    CancellationToken cancellationToken = default);
```

**Parameters**:
| Name | Type | Required | Description |
|------|------|----------|-------------|
| `registeredSystemId` | `string` | Yes | Target system GUID. |
| `input` | `InventoryItemInput` | Yes | Item data (see Input Types below). |
| `addedBy` | `string` | Yes | User identity. |

**Returns**: The created `InventoryItem`.

**Errors**:
- `SYSTEM_NOT_FOUND`: System does not exist.
- `VALIDATION_FAILED`: Required fields missing per FR-018.
- `DUPLICATE_IP`: IP address already exists in this system's inventory.
- `PARENT_NOT_FOUND`: `ParentHardwareId` references a non-existent or non-hardware item.

---

#### `UpdateItemAsync`

Update fields on an existing inventory item.

```csharp
Task<InventoryItem> UpdateItemAsync(
    string itemId,
    InventoryItemInput input,
    string modifiedBy,
    CancellationToken cancellationToken = default);
```

**Parameters**:
| Name | Type | Required | Description |
|------|------|----------|-------------|
| `itemId` | `string` | Yes | Inventory item GUID. |
| `input` | `InventoryItemInput` | Yes | Updated fields (null fields = no change). |
| `modifiedBy` | `string` | Yes | User identity. |

**Returns**: The updated `InventoryItem`.

**Errors**:
- `ITEM_NOT_FOUND`: Item does not exist.
- `VALIDATION_FAILED`: Updated values violate FR-018 rules.
- `DUPLICATE_IP`: New IP address conflicts with another item in the same system.

---

#### `DecommissionItemAsync`

Soft-delete an inventory item (and cascade to SW children if HW).

```csharp
Task<InventoryItem> DecommissionItemAsync(
    string itemId,
    string rationale,
    string decommissionedBy,
    CancellationToken cancellationToken = default);
```

**Parameters**:
| Name | Type | Required | Description |
|------|------|----------|-------------|
| `itemId` | `string` | Yes | Inventory item GUID. |
| `rationale` | `string` | Yes | Reason for decommissioning. |
| `decommissionedBy` | `string` | Yes | User identity. |

**Returns**: The decommissioned `InventoryItem`.

**Errors**:
- `ITEM_NOT_FOUND`: Item does not exist.
- `ALREADY_DECOMMISSIONED`: Item is already decommissioned.

**Side Effects**: If the item is hardware with active software children, all children are also decommissioned with the same rationale (FR-005).

---

#### `GetItemAsync`

Retrieve a single inventory item by ID, including its installed software (if HW).

```csharp
Task<InventoryItem?> GetItemAsync(
    string itemId,
    CancellationToken cancellationToken = default);
```

**Returns**: The `InventoryItem` with `InstalledSoftware` populated (for HW items), or null.

---

#### `ListItemsAsync`

List and filter inventory items for a system.

```csharp
Task<IReadOnlyList<InventoryItem>> ListItemsAsync(
    string registeredSystemId,
    InventoryListOptions? options = null,
    CancellationToken cancellationToken = default);
```

**Parameters**:
| Name | Type | Required | Description |
|------|------|----------|-------------|
| `registeredSystemId` | `string` | Yes | System GUID. |
| `options` | `InventoryListOptions?` | No | Filter/pagination options. |

**Returns**: Filtered list of `InventoryItem` entries.

---

#### `ExportToExcelAsync`

Export inventory as eMASS-compatible Excel workbook bytes.

```csharp
Task<byte[]> ExportToExcelAsync(
    string registeredSystemId,
    InventoryExportOptions? options = null,
    CancellationToken cancellationToken = default);
```

**Parameters**:
| Name | Type | Required | Description |
|------|------|----------|-------------|
| `registeredSystemId` | `string` | Yes | System GUID. |
| `options` | `InventoryExportOptions?` | No | Export options (type filter, include decommissioned). |

**Returns**: `.xlsx` file bytes.

**Errors**:
- `SYSTEM_NOT_FOUND`: System does not exist.
- `NO_INVENTORY_DATA`: System has no inventory items.

---

#### `ImportFromExcelAsync`

Import inventory items from an eMASS-format Excel workbook.

```csharp
Task<InventoryImportResult> ImportFromExcelAsync(
    byte[] fileBytes,
    string registeredSystemId,
    bool dryRun,
    string importedBy,
    CancellationToken cancellationToken = default);
```

**Parameters**:
| Name | Type | Required | Description |
|------|------|----------|-------------|
| `fileBytes` | `byte[]` | Yes | Excel workbook bytes. |
| `registeredSystemId` | `string` | Yes | Target system GUID. |
| `dryRun` | `bool` | Yes | If true, validate only — don't persist. |
| `importedBy` | `string` | Yes | User identity. |

**Returns**: `InventoryImportResult` with created/skipped counts and errors.

---

#### `CheckCompletenessAsync`

Run completeness check on a system's inventory.

```csharp
Task<InventoryCompleteness> CheckCompletenessAsync(
    string registeredSystemId,
    CancellationToken cancellationToken = default);
```

**Returns**: `InventoryCompleteness` result with issues and score.

---

#### `AutoSeedFromBoundaryAsync`

Create inventory items from authorization boundary resources.

```csharp
Task<IReadOnlyList<InventoryItem>> AutoSeedFromBoundaryAsync(
    string registeredSystemId,
    string seededBy,
    CancellationToken cancellationToken = default);
```

**Returns**: List of newly created `InventoryItem` entries (only items for previously unmatched boundary resources).

**Errors**:
- `SYSTEM_NOT_FOUND`: System does not exist.
- `NO_BOUNDARY_DATA`: System has no boundary resources.

---

## Input Types

### InventoryItemInput

Used by `AddItemAsync` and `UpdateItemAsync`. For updates, null fields are not changed.

| Field | Type | Description |
|-------|------|-------------|
| `ItemName` | `string?` | Display name. |
| `Type` | `InventoryItemType?` | Hardware or Software. |
| `HardwareFunction` | `HardwareFunction?` | HW function classification. |
| `SoftwareFunction` | `SoftwareFunction?` | SW function classification. |
| `Manufacturer` | `string?` | HW manufacturer. |
| `Model` | `string?` | HW model. |
| `SerialNumber` | `string?` | HW serial number. |
| `IpAddress` | `string?` | IPv4/IPv6 address. |
| `MacAddress` | `string?` | MAC address. |
| `Location` | `string?` | Physical/logical location. |
| `Vendor` | `string?` | SW vendor. |
| `Version` | `string?` | SW version. |
| `PatchLevel` | `string?` | SW patch level. |
| `LicenseType` | `string?` | License type. |
| `ParentHardwareId` | `string?` | Parent HW item ID (for SW). |

### InventoryListOptions

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| `Type` | `InventoryItemType?` | null | Filter by type. |
| `Function` | `string?` | null | Filter by function name (HW or SW). |
| `Vendor` | `string?` | null | Filter by vendor/manufacturer (contains). |
| `Status` | `InventoryItemStatus?` | `Active` | Filter by status. Null = all. |
| `SearchText` | `string?` | null | Free-text search on item name. |
| `PageSize` | `int` | 50 | Results per page. |
| `PageNumber` | `int` | 1 | Page number (1-based). |

### InventoryExportOptions

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| `ExportType` | `string` | `"all"` | `"hardware"`, `"software"`, or `"all"`. |
| `IncludeDecommissioned` | `bool` | `false` | Include decommissioned items. |

---

## MCP Tool Contracts

All tools extend `BaseTool` and follow the standard response envelope.

### `inventory_add_item`

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `system_id` | string | Yes | System GUID, name, or acronym. |
| `type` | string | Yes | `"hardware"` or `"software"`. |
| `item_name` | string | Yes | Display name. |
| `function` | string | Yes | Function classification (e.g., "server", "operating_system"). |
| `manufacturer` | string | No | HW manufacturer. |
| `model` | string | No | HW model. |
| `serial_number` | string | No | HW serial number. |
| `ip_address` | string | No | IPv4/IPv6 address. |
| `mac_address` | string | No | MAC address. |
| `location` | string | No | Physical/logical location. |
| `vendor` | string | No | SW vendor. |
| `version` | string | No | SW version. |
| `patch_level` | string | No | SW patch level. |
| `license_type` | string | No | License type. |
| `parent_hardware_id` | string | No | Parent HW item ID (for SW). |

### `inventory_update_item`

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `item_id` | string | Yes | Inventory item GUID. |
| `item_name` | string | No | Updated name. |
| `manufacturer` | string | No | Updated manufacturer. |
| `model` | string | No | Updated model. |
| `serial_number` | string | No | Updated serial number. |
| `ip_address` | string | No | Updated IP address. |
| `mac_address` | string | No | Updated MAC address. |
| `location` | string | No | Updated location. |
| `vendor` | string | No | Updated vendor. |
| `version` | string | No | Updated version. |
| `patch_level` | string | No | Updated patch level. |
| `license_type` | string | No | Updated license type. |
| `parent_hardware_id` | string | No | Updated parent HW item ID. |

### `inventory_decommission_item`

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `item_id` | string | Yes | Inventory item GUID. |
| `rationale` | string | Yes | Reason for decommissioning. |

### `inventory_list`

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `system_id` | string | Yes | System GUID, name, or acronym. |
| `type` | string | No | `"hardware"` or `"software"`. |
| `function` | string | No | Function filter. |
| `vendor` | string | No | Vendor/manufacturer filter. |
| `status` | string | No | `"active"` (default) or `"decommissioned"`. |
| `search` | string | No | Free-text search on item name. |
| `page_size` | integer | No | Results per page (default 50). |
| `page` | integer | No | Page number (default 1). |

### `inventory_get`

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `item_id` | string | Yes | Inventory item GUID. |

### `inventory_export`

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `system_id` | string | Yes | System GUID, name, or acronym. |
| `export_type` | string | No | `"hardware"`, `"software"`, or `"all"` (default). |
| `include_decommissioned` | boolean | No | Include decommissioned items (default false). |

### `inventory_import`

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `system_id` | string | Yes | System GUID, name, or acronym. |
| `file_base64` | string | Yes | Base64-encoded Excel workbook. |
| `dry_run` | boolean | No | Validate only, don't persist (default false). |

### `inventory_completeness`

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `system_id` | string | Yes | System GUID, name, or acronym. |

### `inventory_auto_seed`

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `system_id` | string | Yes | System GUID, name, or acronym. |
