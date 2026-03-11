# Quickstart: Hardware/Software Inventory

**Feature**: 025-hw-sw-inventory | **Date**: 2026-03-11

---

## Prerequisites

- A registered system exists (via `compliance_register_system`)
- Authorization boundary defined (via `compliance_define_boundary`) — needed for auto-seed

---

## Workflow: End-to-End Inventory Management

### Step 1: Auto-Seed from Boundary (optional)

Pre-populate hardware items from existing boundary resources:

```
Tool: inventory_auto_seed
Parameters: { "system_id": "my-system" }
```

Creates one HW item per in-scope boundary resource with name and function pre-populated.

### Step 2: Add Hardware Items

```
Tool: inventory_add_item
Parameters: {
  "system_id": "my-system",
  "type": "hardware",
  "item_name": "Web Server 01",
  "function": "server",
  "manufacturer": "Dell",
  "model": "PowerEdge R740",
  "serial_number": "SVC-2025-001",
  "ip_address": "10.0.1.10",
  "mac_address": "00:1A:2B:3C:4D:5E",
  "location": "Azure US Gov Virginia"
}
```

### Step 3: Add Software Items

Link software to its parent hardware:

```
Tool: inventory_add_item
Parameters: {
  "system_id": "my-system",
  "type": "software",
  "item_name": "Red Hat Enterprise Linux 9",
  "function": "operating_system",
  "vendor": "Red Hat",
  "version": "9.3",
  "patch_level": "9.3-47.el9",
  "license_type": "Enterprise",
  "parent_hardware_id": "<hardware-item-id>"
}
```

For SaaS/PaaS, omit `parent_hardware_id`:

```
Tool: inventory_add_item
Parameters: {
  "system_id": "my-system",
  "type": "software",
  "item_name": "Azure Active Directory",
  "function": "security_tool",
  "vendor": "Microsoft",
  "version": "N/A"
}
```

### Step 4: Review Inventory

```
Tool: inventory_list
Parameters: { "system_id": "my-system" }
```

Filter by type:

```
Tool: inventory_list
Parameters: { "system_id": "my-system", "type": "hardware" }
```

### Step 5: Check Completeness

```
Tool: inventory_completeness
Parameters: { "system_id": "my-system" }
```

Returns missing fields, unmatched boundary resources, and HW items without SW.

### Step 6: Export to eMASS

```
Tool: inventory_export
Parameters: { "system_id": "my-system", "export_type": "all" }
```

Returns a base64-encoded .xlsx workbook with "Hardware" and "Software" worksheets.

### Step 7: Import from Excel (Onboarding)

```
Tool: inventory_import
Parameters: {
  "system_id": "my-system",
  "file_base64": "<base64-xlsx-data>",
  "dry_run": true
}
```

Dry-run first to see what would be created/skipped, then:

```
Tool: inventory_import
Parameters: {
  "system_id": "my-system",
  "file_base64": "<base64-xlsx-data>",
  "dry_run": false
}
```

---

## Key Concepts

| Concept | Description |
|---------|-------------|
| **InventoryItem** | A single HW or SW component tracked with eMASS-required fields. |
| **Parent-Child** | SW items optionally link to a parent HW item. SaaS/PaaS SW items have no parent. |
| **Auto-Seed** | Creates HW items from boundary resources. Idempotent — only seeds unmatched resources. |
| **Completeness Check** | Validates: required fields filled, boundary resources matched, HW items have SW. |
| **Decommission** | Soft-delete with rationale. Decommissioning HW cascades to its SW children. |
| **eMASS Round-Trip** | Export to .xlsx → upload to eMASS. Import from .xlsx → onboard existing inventories. |
