# Data Model: CAC Simulation Mode

**Feature**: `027-cac-simulation-mode` | **Date**: 2026-03-12

## Entities

### 1. `SimulatedIdentityOptions` (New Configuration POCO)

**Location**: `src/Ato.Copilot.Core/Configuration/GatewayOptions.cs` (nested class or same file)  
**Namespace**: `Ato.Copilot.Core.Configuration`  
**Purpose**: Configuration shape for the simulated identity, bound from `CacAuth:SimulatedIdentity` section.

| Field | Type | Required | Default | Validation | Notes |
|-------|------|----------|---------|------------|-------|
| `UserPrincipalName` | `string` | Yes (when simulation active) | `""` | Non-null, non-empty at startup validation | e.g. `"dev.user@dev.mil"` |
| `DisplayName` | `string` | Yes (when simulation active) | `""` | Non-null, non-empty at startup validation | e.g. `"Dev User"` |
| `CertificateThumbprint` | `string?` | No | `null` | — | Optional simulated thumbprint |
| `Roles` | `List<string>` | No | `[]` | Non-null (null normalized to empty) | e.g. `["Global Reader", "ISSO"]` |

**Binding path**: `CacAuth:SimulatedIdentity:*`

```csharp
public class SimulatedIdentityOptions
{
    public string UserPrincipalName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? CertificateThumbprint { get; set; }
    public List<string> Roles { get; set; } = [];
}
```

---

### 2. `CacAuthOptions` (Extended — Existing)

**Location**: `src/Ato.Copilot.Core/Configuration/GatewayOptions.cs` (lines ~155–180)  
**Namespace**: `Ato.Copilot.Core.Configuration`  
**Purpose**: Extended with simulation mode configuration properties.

| Field | Type | Status | Default | Notes |
|-------|------|--------|---------|-------|
| `SectionName` | `const string` | Existing | `"CacAuth"` | — |
| `DefaultSessionTimeoutHours` | `int` | Existing | `8` | — |
| `MaxSessionTimeoutHours` | `int` | Existing | `24` | — |
| **`SimulationMode`** | **`bool`** | **New** | **`false`** | Activates simulation; guarded by environment check |
| **`SimulatedIdentity`** | **`SimulatedIdentityOptions?`** | **New** | **`null`** | Required when `SimulationMode == true` in Development |

**Configuration example** (`appsettings.Development.json`):

```json
{
  "CacAuth": {
    "SimulationMode": true,
    "SimulatedIdentity": {
      "UserPrincipalName": "dev.user@dev.mil",
      "DisplayName": "Dev User (Simulated)",
      "CertificateThumbprint": "ABC123DEF456",
      "Roles": ["Global Reader", "ISSO"]
    }
  }
}
```

**Environment variable override**:

```
CacAuth__SimulationMode=true
CacAuth__SimulatedIdentity__UserPrincipalName=test.issm@dev.mil
CacAuth__SimulatedIdentity__DisplayName=Test ISSM
CacAuth__SimulatedIdentity__Roles__0=ISSM
CacAuth__SimulatedIdentity__Roles__1=Global Reader
```

---

### 3. `ClientType` Enum (Extended — Existing)

**Location**: `src/Ato.Copilot.Core/Models/Auth/AuthEnums.cs` (lines ~6–20)  
**Namespace**: `Ato.Copilot.Core.Models.Auth`  
**Purpose**: Add `Simulated` value to distinguish simulated sessions in audit/evidence.

| Value | Status | Purpose |
|-------|--------|---------|
| `VSCode` | Existing | VS Code extension client |
| `Teams` | Existing | Microsoft Teams bot client |
| `Web` | Existing | Web browser client |
| `CLI` | Existing | Command-line interface client |
| **`Simulated`** | **New** | Simulated CAC session (FR-013); excluded from compliance evidence (FR-014) |

---

## Entity Relationship Diagram

```
┌─────────────────────────┐     ┌──────────────────────────┐
│   CacAuthOptions        │     │  SimulatedIdentityOptions │
│  (Configuration)        │────▶│  (Configuration)          │
│                         │ 0..1│                           │
│ SimulationMode: bool    │     │ UserPrincipalName: string │
│ SimulatedIdentity?      │     │ DisplayName: string       │
│ DefaultSessionTimeout   │     │ CertificateThumbprint?    │
│ MaxSessionTimeout       │     │ Roles: List<string>       │
└─────────────────────────┘     └──────────────────────────┘
                                          │
                                          │ consumed by
                                          ▼
                                ┌──────────────────────────┐
                                │  CacAuthMiddleware        │
                                │  (Middleware)             │
                                │                           │
                                │ → Synthesizes Claims      │
                                │ → Sets context.User       │
                                │ → Sets ClientType enum    │
                                └──────────────────────────┘
                                          │
                                          │ populates
                                          ▼
                                ┌──────────────────────────┐
                                │     CacSession            │
                                │     (Existing Model)      │
                                │                           │
                                │ ClientType: ClientType    │
                                │   (now includes Simulated)│
                                └──────────────────────────┘
```

## State Transitions

### Simulation Mode Activation

```
┌─────────────┐   SimulationMode=true    ┌──────────────────┐
│  App Startup │──── + Development ──────▶│ Validate Config  │
│              │     environment          │                  │
└─────────────┘                           └────────┬─────────┘
                                                   │
                                          ┌────────┴─────────┐
                                          │                  │
                                     Valid config       Invalid config
                                          │                  │
                                          ▼                  ▼
                                 ┌─────────────┐    ┌─────────────────┐
                                 │ Middleware    │    │ FAIL FAST       │
                                 │ synthesizes  │    │ Descriptive     │
                                 │ ClaimsPrinc. │    │ error message   │
                                 │ from config  │    └─────────────────┘
                                 └──────┬──────┘
                                        │
                                        ▼
                                 ┌─────────────┐
                                 │ Log startup  │
                                 │ info with    │
                                 │ simulated UPN│
                                 └─────────────┘
```

```
┌─────────────┐   SimulationMode=true    ┌──────────────────┐
│  App Startup │──── + Non-Development ──▶│ Log SECURITY     │
│              │     environment          │ WARNING          │
└─────────────┘                           └────────┬─────────┘
                                                   │
                                                   ▼
                                          ┌─────────────────┐
                                          │ Use real JWT    │
                                          │ authentication  │
                                          │ (ignore flag)   │
                                          └─────────────────┘
```

## No Database Schema Changes

This feature does **not** require EF Core migrations. The `CacSession` table already has a `ClientType` column. Adding a new enum value (`Simulated`) to the C# `ClientType` enum is additive — EF Core stores enum values as integers (or strings if configured), and the new value simply extends the range.

Verify: If `ClientType` is stored as a **string** column, no migration is needed. If stored as an **integer** column, the new enum value gets the next ordinal. Either way, no schema change required.
