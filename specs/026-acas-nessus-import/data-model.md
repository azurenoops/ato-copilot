# Data Model: ACAS/Nessus Scan Import

**Feature**: 026-acas-nessus-import | **Date**: 2025-03-12

---

## 1. Enum Extensions

### ScanImportType (extend existing)

```csharp
// File: src/Ato.Copilot.Core/Models/Compliance/ScanImportModels.cs
public enum ScanImportType
{
    Ckl,
    Xccdf,
    PrismaCsv,
    PrismaApi,
    NessusXml       // ← NEW
}
```

### NessusControlMappingSource (new)

```csharp
// File: src/Ato.Copilot.Core/Models/Compliance/NessusModels.cs
public enum NessusControlMappingSource
{
    StigXref,               // From <xref>STIG-ID:...</xref> → CCI → NIST chain
    PluginFamilyHeuristic   // From curated plugin-family → NIST mapping table
}
```

---

## 2. Entity Extensions

### ScanImportFinding — Nessus-Specific Fields

Add to existing `ScanImportFinding` class in `ScanImportModels.cs`, following the Prisma Cloud field pattern:

```csharp
// ── Nessus/ACAS-specific fields ──────────────────────────────────────────
[MaxLength(20)]
public string? NessusPluginId { get; set; }

[MaxLength(200)]
public string? NessusPluginName { get; set; }

[MaxLength(100)]
public string? NessusPluginFamily { get; set; }

[MaxLength(500)]
public string? NessusHostname { get; set; }

[MaxLength(45)]
public string? NessusHostIp { get; set; }

public int? NessusPort { get; set; }

[MaxLength(10)]
public string? NessusProtocol { get; set; }

[MaxLength(50)]
public string? NessusServiceName { get; set; }

public double? NessusCvssV3BaseScore { get; set; }

[MaxLength(100)]
public string? NessusCvssV3Vector { get; set; }

public double? NessusCvssV2BaseScore { get; set; }

public double? NessusVprScore { get; set; }

public List<string> NessusCves { get; set; } = new();

public bool? NessusExploitAvailable { get; set; }

[MaxLength(30)]
public string? NessusControlMappingSource { get; set; }
```

### Field Mapping to .nessus XML

| Entity Field | XML Source | Notes |
|-------------|-----------|-------|
| `NessusPluginId` | `ReportItem/@pluginID` | String for storage; parsed as int for dedup |
| `NessusPluginName` | `ReportItem/@pluginName` | Plugin title |
| `NessusPluginFamily` | `ReportItem/@pluginFamily` | Used for heuristic control mapping |
| `NessusHostname` | `HostProperties/tag[@name='hostname']` | Fallback to `ReportHost/@name` |
| `NessusHostIp` | `HostProperties/tag[@name='host-ip']` | IPv4/IPv6 address |
| `NessusPort` | `ReportItem/@port` | 0 = host-level finding |
| `NessusProtocol` | `ReportItem/@protocol` | `tcp`, `udp`, `icmp` |
| `NessusServiceName` | `ReportItem/@svc_name` | e.g., `www`, `ssh`, `cifs` |
| `NessusCvssV3BaseScore` | `cvss3_base_score` | Preferred score; fallback to v2 |
| `NessusCvssV3Vector` | `cvss3_vector` | Full CVSS v3 vector string |
| `NessusCvssV2BaseScore` | `cvss_base_score` | Fallback if no v3 score |
| `NessusVprScore` | `vpr_score` | Tenable VPR |
| `NessusCves` | `cve` (repeatable) | All CVE identifiers |
| `NessusExploitAvailable` | `exploit_available` | `true`/`false` |
| `NessusControlMappingSource` | Computed | `StigXref` or `PluginFamilyHeuristic` |

### Existing Fields Reused

| Entity Field | XML Source | Notes |
|-------------|-----------|-------|
| `VulnId` | `ReportItem/@pluginID` | Primary vulnerability identifier |
| `RawStatus` | Derived from severity | `Open` (sev 1-4) or `Informational` (sev 0) |
| `RawSeverity` | `ReportItem/@severity` | `0`–`4` as string |
| `MappedSeverity` | Derived from severity | `CatI` (3,4), `CatII` (2), `CatIII` (1), `null` (0) |
| `FindingDetails` | `plugin_output` | Evidence text |
| `Comments` | `synopsis` + `solution` | Combined synopsis and remediation |
| `ResolvedNistControlIds` | Computed | From STIG-ID xref or plugin family mapping |
| `ResolvedCciRefs` | Computed | From STIG-ID xref lookup (empty for plugin family mappings) |
| `ImportAction` | Computed | `Created`, `Updated`, `Skipped` |

### ScanImportRecord — Nessus-Specific Counter Fields

Add to existing `ScanImportRecord`:

```csharp
// ── Nessus/ACAS-specific counters ────────────────────────────────────────
public int? NessusInformationalCount { get; set; }
public int? NessusCriticalCount { get; set; }
public int? NessusHighCount { get; set; }
public int? NessusMediumCount { get; set; }
public int? NessusLowCount { get; set; }
public int? NessusHostCount { get; set; }
public int? NessusPoamCreatedCount { get; set; }
public bool? NessusCredentialedScan { get; set; }
```

---

## 3. DTOs (Not Persisted)

### File: `src/Ato.Copilot.Core/Models/Compliance/NessusModels.cs`

```csharp
namespace Ato.Copilot.Core.Models.Compliance;

// ─── Parser Output DTOs ──────────────────────────────────────────────────────

public record ParsedNessusFile(
    string ReportName,
    List<NessusReportHost> Hosts,
    int TotalPluginResults,
    int InformationalCount);

public record NessusReportHost(
    string Name,
    string? HostIp,
    string? Hostname,
    string? OperatingSystem,
    string? MacAddress,
    bool CredentialedScan,
    DateTime? ScanStart,
    DateTime? ScanEnd,
    List<NessusPluginResult> PluginResults);

public record NessusPluginResult(
    int PluginId,
    string PluginName,
    string PluginFamily,
    int Severity,
    string RiskFactor,
    int Port,
    string? Protocol,
    string? ServiceName,
    string? Synopsis,
    string? Description,
    string? Solution,
    string? PluginOutput,
    List<string> Cves,
    List<string> Xrefs,
    double? CvssV2BaseScore,
    double? CvssV3BaseScore,
    string? CvssV3Vector,
    double? VprScore,
    bool ExploitAvailable,
    string? StigSeverity);
```

### DTO Relationships

```
ParsedNessusFile
 └─ 1..* NessusReportHost
          └─ 0..* NessusPluginResult
```

### Control Mapping DTOs

```csharp
// ─── Control Mapping DTOs ────────────────────────────────────────────────────

public record PluginFamilyMapping(
    string PluginFamily,
    string PrimaryControl,
    string[] SecondaryControls);

public record NessusControlMappingResult(
    List<string> NistControlIds,
    List<string> CciRefs,
    NessusControlMappingSource MappingSource);
```

### Import Result DTO

```csharp
// ─── Nessus Import Result ────────────────────────────────────────────────────

public record NessusImportResult(
    string ImportRecordId,
    ScanImportStatus Status,
    string ReportName,
    int TotalPluginResults,
    int InformationalCount,
    int CriticalCount,
    int HighCount,
    int MediumCount,
    int LowCount,
    int HostCount,
    int FindingsCreated,
    int FindingsUpdated,
    int SkippedCount,
    int PoamWeaknessesCreated,
    int EffectivenessRecordsCreated,
    int EffectivenessRecordsUpdated,
    int NistControlsAffected,
    bool CredentialedScan,
    bool IsDryRun,
    List<string> Warnings,
    string? ErrorMessage);
```

---

## 4. Interface Extensions

### IScanImportService — New Method

```csharp
// File: src/Ato.Copilot.Core/Interfaces/Compliance/IScanImportService.cs
// Add to IScanImportService interface:

// ─── Feature 026: ACAS/Nessus Import ──────────────────────────────────

Task<NessusImportResult> ImportNessusAsync(
    string systemId,
    string? assessmentId,
    byte[] fileContent,
    string fileName,
    ImportConflictResolution resolution,
    bool dryRun,
    string importedBy,
    CancellationToken ct = default);
```

**Signature matches** the existing `ImportCklAsync` / `ImportXccdfAsync` pattern: system ID, optional assessment, file content as bytes, file name, conflict resolution, dry run flag, user identity, cancellation token.

### INessusParser — New Interface

```csharp
// File: src/Ato.Copilot.Agents/Compliance/Services/ScanImport/NessusParser.cs
namespace Ato.Copilot.Agents.Compliance.Services.ScanImport;

public interface INessusParser
{
    ParsedNessusFile Parse(byte[] fileContent);
}
```

### INessusControlMapper — New Interface

```csharp
// File: src/Ato.Copilot.Agents/Compliance/Services/ScanImport/NessusControlMapper.cs
namespace Ato.Copilot.Agents.Compliance.Services.ScanImport;

public interface INessusControlMapper
{
    NessusControlMappingResult MapToControls(NessusPluginResult plugin);
}
```

---

## 5. Deduplication Key

**Composite key**: `NessusPluginId` + `NessusHostname` + `NessusPort`

Within a system, duplicate detection uses:

```csharp
existingFindings.Any(f =>
    f.NessusPluginId == pluginResult.PluginId.ToString() &&
    f.NessusHostname == hostname &&
    f.NessusPort == pluginResult.Port)
```

Per spec clarification C1, this composite key uniquely identifies a vulnerability finding within a system. Re-importing a scan for the same host updates existing findings rather than creating duplicates.

---

## 6. Severity → CAT Mapping

| .nessus Severity | CAT | MappedSeverity | POA&M Action |
|-----------------|-----|----------------|-------------|
| 4 (Critical) | CAT I | `CatSeverity.CatI` | Create POA&M weakness |
| 3 (High) | CAT I | `CatSeverity.CatI` | Create POA&M weakness |
| 2 (Medium) | CAT II | `CatSeverity.CatII` | Create POA&M weakness |
| 1 (Low) | CAT III | `CatSeverity.CatIII` | Import finding only |
| 0 (Informational) | — | `null` | Exclude from findings |

Per spec clarification C3: Critical + High + Medium create POA&M entries.
Per spec clarification C5: Informational (severity 0) excluded from findings — summary counts only.

---

## 7. State Transitions

```
.nessus file uploaded
    │
    ▼
[Parsing] ──invalid XML──► [Failed] (ScanImportStatus.Failed)
    │
    ▼
[Validation] ──no hosts/results──► [Failed]
    │
    ▼
[Control Mapping]
    │  ├── STIG-ID xref found → StigXref (High confidence)
    │  └── No xref → PluginFamilyHeuristic (Medium confidence)
    │
    ▼
[Dedup Check] ──exists + Skip resolution──► [Skipped]
    │  └── exists + Overwrite──► [Updated]
    │
    ▼
[Finding Creation] ──severity 0──► summary count only
    │  └── severity 1-4──► ScanImportFinding created
    │
    ▼
[POA&M Generation] ──severity ≥ 2──► POA&M weakness (source="ACAS")
    │
    ▼
[Effectiveness Records] ──mapped controls──► control effectiveness updated
    │
    ▼
[Completed] or [CompletedWithWarnings] if unmapped families
```
