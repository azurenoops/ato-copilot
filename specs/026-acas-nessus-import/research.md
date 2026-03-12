# Research: ACAS/Nessus Scan Import

**Feature**: 026-acas-nessus-import | **Date**: 2025-03-12

---

## 1. .nessus XML File Format (NessusClientData_v2)

### Decision
Parse using `System.Xml.Linq` (XDocument), matching the established CKL/XCCDF parser pattern.

### Format Structure

```xml
<NessusClientData_v2>
  <Policy>...</Policy>          <!-- scan policy config — ignored by parser -->
  <Report name="scan-name">
    <ReportHost name="hostname-or-ip">
      <HostProperties>
        <tag name="HOST_START">Wed Mar 12 08:00:00 2025</tag>
        <tag name="HOST_END">Wed Mar 12 08:45:00 2025</tag>
        <tag name="host-ip">10.0.1.50</tag>
        <tag name="hostname">server01.domain.mil</tag>
        <tag name="operating-system">Microsoft Windows Server 2019</tag>
        <tag name="Credentialed_Scan">true</tag>
        <!-- 15+ additional tags: mac-address, system-type, cpe, etc. -->
      </HostProperties>
      <ReportItem pluginID="97833" pluginName="MS17-010" pluginFamily="Windows : Microsoft Bulletins"
                  severity="4" port="445" protocol="tcp" svc_name="cifs">
        <synopsis>Remote code execution vulnerability</synopsis>
        <description>Full vulnerability description...</description>
        <solution>Apply Microsoft patch...</solution>
        <risk_factor>Critical</risk_factor>
        <plugin_output>Evidence text...</plugin_output>
        <cve>CVE-2017-0143</cve>
        <cve>CVE-2017-0144</cve>
        <cvss3_base_score>8.1</cvss3_base_score>
        <cvss3_vector>CVSS:3.0/AV:N/AC:H/PR:N/UI:N/S:U/C:H/I:H/A:H</cvss3_vector>
        <exploit_available>true</exploit_available>
        <xref>STIG-ID:WN19-00-000010</xref>
        <xref>IAVA:2017-A-0065</xref>
      </ReportItem>
    </ReportHost>
  </Report>
</NessusClientData_v2>
```

### Key ReportItem Attributes

| Attribute | Type | Description |
|-----------|------|-------------|
| `pluginID` | int | Unique Nessus plugin identifier |
| `pluginName` | string | Human-readable plugin name |
| `pluginFamily` | string | Plugin family category (maps to NIST controls) |
| `severity` | int | 0=Info, 1=Low, 2=Medium, 3=High, 4=Critical |
| `port` | int | Port number (0 = host-level finding) |
| `protocol` | string | `tcp`, `udp`, `icmp`, or empty |
| `svc_name` | string | Service name (e.g., `www`, `ssh`, `cifs`) |

### Key Child Elements

| Element | Cardinality | Description |
|---------|-------------|-------------|
| `cve` | 0..n | CVE identifiers (repeatable) |
| `cvss3_base_score` | 0..1 | CVSS v3 base score (0.0–10.0) |
| `cvss3_vector` | 0..1 | CVSS v3 vector string |
| `cvss_base_score` | 0..1 | CVSS v2 base score (fallback) |
| `risk_factor` | 0..1 | `None`, `Low`, `Medium`, `High`, `Critical` |
| `xref` | 0..n | Cross-references: `STIG-ID:`, `IAVA:`, `CWE:`, etc. |
| `exploit_available` | 0..1 | `true` / `false` |
| `synopsis` | 0..1 | Brief vulnerability summary |
| `solution` | 0..1 | Recommended remediation |
| `plugin_output` | 0..1 | Scan-specific evidence output |
| `compliance` | 0..1 | `true` if compliance check |
| `cm:compliance-result` | 0..1 | `PASSED`, `FAILED`, `WARNING`, `ERROR` |
| `cm:compliance-reference` | 0..1 | Direct NIST reference (e.g., `800-53|AC-2`) |
| `stig_severity` | 0..1 | STIG severity: `I`, `II`, `III` |
| `vpr_score` | 0..1 | Tenable Vulnerability Priority Rating |

### Severity Mapping

| Integer | Label | CAT | Parser Action |
|---------|-------|-----|---------------|
| 0 | Informational | — | Exclude from findings; summary count only (per FR-018) |
| 1 | Low | CAT III | Import finding; no POA&M (below threshold per FR-014) |
| 2 | Medium | CAT II | Import finding; create POA&M weakness |
| 3 | High | CAT I | Import finding; create POA&M weakness |
| 4 | Critical | CAT I | Import finding; create POA&M weakness |

### Rationale
XDocument is the established parsing pattern in the codebase (CklParser, XccdfParser). The .nessus format is straightforward hierarchical XML with no schema namespaces (unlike XCCDF), making it simpler to parse.

### Alternatives Considered
- **XmlReader (streaming)**: Better memory for very large files, but loses the ease of LINQ-to-XML queries. Not needed given the 5MB file size limit.
- **XmlSerializer (deserialization)**: Would require mirroring the full Nessus schema as C# classes. Over-engineered for selective extraction.

---

## 2. CVE-to-NIST 800-53 Control Mapping Strategy

### Decision
Layered strategy with priority cascade: STIG-ID xref → Plugin family heuristic. CVE→CWE→NIST deferred to v2.

### Strategy Analysis

#### Strategy 1: STIG-ID Cross-Reference (Priority 1 — Enrichment)

- **How it works**: Some Nessus plugins include `<xref>STIG-ID:...</xref>` values. STIG rules map to CCI items, and CCI items map to NIST 800-53 controls. The existing codebase already has the CCI→NIST chain (7,029 entries).
- **Coverage**: ~15-25% of vulnerability plugins (mainly compliance audit and DISA-related checks)
- **Accuracy**: Very high — DISA's CCI assignments are authoritative
- **Implementation**: Parse `<xref>` elements, extract STIG-ID, look up in existing STIG→CCI→NIST chain via `IStigKnowledgeService`
- **Verdict**: Best accuracy when available. Used as first-priority enrichment.

#### Strategy 2: Plugin Family Heuristic (Priority 2 — Default/Fallback)

- **How it works**: Every Nessus plugin has a `pluginFamily` attribute. A curated static mapping table maps each family to NIST 800-53 controls.
- **Coverage**: 100% — every ReportItem has a `pluginFamily`
- **Accuracy**: Moderate — categorical mapping; correct at the control level but not per-vulnerability precise
- **Implementation**: Static JSON lookup table loaded at startup. Very fast, no external dependencies.
- **Verdict**: Excellent primary/default strategy. Ensures every finding gets at least one control mapping.

#### Strategy 3: CVE → CWE → NIST (DEFERRED to v2)

- **How it works**: NVD provides CVE→CWE mappings (~70-80% coverage). MITRE publishes CWE→NIST crosswalk.
- **Coverage**: ~70-80% of CVEs have CWE mappings
- **Issue**: Produces *software weakness* controls (SI-10, SI-16) rather than *operational response* controls (SI-2, RA-5). Adds complexity (NVD data download, CWE catalog parsing).
- **Verdict**: Valuable supplementary data but deferred for v1 to keep implementation scope manageable.

### Rationale

1. **STIG-ID xrefs first** — leverages existing CCI→NIST infrastructure; zero new data sources; most authoritative mapping.
2. **Plugin family heuristic second** — guarantees 100% coverage; simple static table; operationally relevant controls (SI-2, RA-5, CM-6).
3. **CVE→CWE→NIST deferred** — adds complexity (NVD download, CWE catalog) without critical operational value for v1.

### Data Model Implication

Each finding stores:
- `MappingSource`: `StigXref` | `PluginFamilyHeuristic` (enum)
- `MappingConfidence`: `High` (STIG) | `Medium` (plugin family)
- `NistControls[]`: resolved NIST 800-53 control identifiers

---

## 3. Plugin Family → NIST 800-53 Rev 5 Curated Mapping Table

### Decision
35-entry curated mapping table stored as embedded JSON resource (`plugin-family-mappings.json`). Fixed/static per spec clarification C4.

| # | Plugin Family | Primary Control | Secondary Controls | Notes |
|---|--------------|----------------|-------------------|-------|
| 1 | Windows : Microsoft Bulletins | SI-2 | RA-5, CM-6, SA-11 | Missing Microsoft patches |
| 2 | Ubuntu Local Security Checks | SI-2 | RA-5, CM-6 | Missing Linux patches |
| 3 | Red Hat Local Security Checks | SI-2 | RA-5, CM-6 | Missing RHEL patches |
| 4 | CentOS Local Security Checks | SI-2 | RA-5, CM-6 | Missing CentOS patches |
| 5 | Debian Local Security Checks | SI-2 | RA-5, CM-6 | Missing Debian patches |
| 6 | Amazon Linux Local Security Checks | SI-2 | RA-5, CM-6 | Missing Amazon Linux patches |
| 7 | SuSE Local Security Checks | SI-2 | RA-5, CM-6 | Missing SUSE patches |
| 8 | Oracle Linux Local Security Checks | SI-2 | RA-5, CM-6 | Missing Oracle Linux patches |
| 9 | MacOS X Local Security Checks | SI-2 | RA-5, CM-6 | Missing macOS patches |
| 10 | Firewalls | SC-7 | AC-4, CA-3, CM-6 | Firewall misconfigurations |
| 11 | DNS | SC-20 | SC-21, SC-22, CM-6 | DNS vulnerabilities |
| 12 | Web Servers | SC-8 | SI-2, CM-6, SC-23, AC-17 | Web server vulns |
| 13 | Databases | AC-6 | SC-28, SI-2, CM-6, AU-3 | Database vulns |
| 14 | SMTP Problems | SC-8 | SI-2, SC-7, CM-6 | Mail server vulns |
| 15 | SNMP | CM-7 | IA-2, SC-8, CM-6 | SNMP issues |
| 16 | SSH | AC-17 | IA-2, SC-8, CM-6, SC-13 | SSH misconfig |
| 17 | SSL/TLS | SC-13 | SC-8, SC-23, CM-6 | Weak ciphers, expired certs |
| 18 | Default Unix Accounts | IA-5 | AC-2, CM-6, AC-6 | Default credentials |
| 19 | Windows | CM-6 | SI-2, AC-6, SC-28 | General Windows misconfigs |
| 20 | Windows : User Management | AC-2 | AC-6, IA-2, IA-5 | User account policy findings |
| 21 | Policy Compliance | CM-6 | AC-6, AU-2, SC-8, SI-2 | STIG/baseline compliance checks |
| 22 | Port Scanners | RA-5 | CM-7, SC-7, CM-6 | Open port enumeration |
| 23 | Service Detection | CM-7 | RA-5, SC-7, CM-6 | Unnecessary services |
| 24 | General | RA-5 | SI-5, CM-6 | Generic informational findings |
| 25 | Backdoors | SI-3 | SI-4, IR-4, SC-7, RA-5 | Malware / unauthorized access |
| 26 | Denial of Service | SC-5 | SI-2, RA-5, CP-2 | DoS vulnerabilities |
| 27 | FTP | AC-17 | CM-7, SC-8, IA-2 | FTP service vulns |
| 28 | Gain a Shell Remotely | SI-2 | RA-5, IR-4, SC-7, AC-6 | Remote code execution |
| 29 | Peer-To-Peer File Sharing | CM-7 | SC-7, AC-4, CM-6 | Unauthorized P2P software |
| 30 | SCADA | SI-2 | SC-7, AC-6, PE-3, CM-6 | ICS/SCADA vulnerabilities |
| 31 | VMware ESX Local Security Checks | SI-2 | RA-5, CM-6, SC-7 | VMware hypervisor patches |
| 32 | Cisco | CM-6 | SI-2, SC-7, AC-6 | Cisco IOS/NX-OS vulns |
| 33 | Juniper Local Security Checks | SI-2 | CM-6, SC-7, AC-6 | Juniper patches |
| 34 | Misc. | RA-5 | SI-2, CM-6 | Uncategorized findings |
| 35 | RPC | CM-7 | AC-6, SC-7, CM-6 | RPC service exposure |

### Mapping Notes

- **SI-2 (Flaw Remediation)** — primary for all missing-patch families; most frequently triggered
- **RA-5 (Vulnerability Monitoring)** — secondary to virtually everything (the scan itself demonstrates RA-5)
- **CM-6 (Configuration Settings)** — secondary to most findings (misconfigurations)
- **CM-7 (Least Functionality)** — applies to unnecessary services/software
- Unknown/unrecognized families default to **RA-5** with `Medium` confidence

### Rationale
Curated (fixed) table per spec clarification C4. Stored as embedded JSON resource for fast lookup without database queries. The 35 families cover the vast majority of real-world Nessus scan results.

### Alternatives Considered
- **RBAC-editable database table**: More flexible but adds CRUD complexity, permission concerns, and audit requirements. Rejected per spec clarification C4 — the mapping should be consistent and authoritative.
- **External API (NVD)**: Real-time lookup adds latency, network dependency, and Azure Gov network restrictions. Not viable.

---

## 4. Finding Identity / Deduplication Key

### Decision
Composite key: **Plugin ID + Hostname + Port** (per spec clarification C1).

### Rationale
- **Plugin ID** uniquely identifies the vulnerability check
- **Hostname** identifies the target asset
- **Port** differentiates the same vulnerability on different services (e.g., SSL issues on port 443 vs 8443)
- This matches Nessus's own uniqueness model for results within a scan

### Implementation
When checking for duplicate imports, compare against existing `ScanImportFinding` records using this composite key within the same system. New scan results for the same key update the existing finding rather than creating duplicates.

---

## 5. RBAC and POA&M Threshold

### Decision
- **Import permissions**: ISSO, SCA, System Admin (per spec clarification C2)
- **POA&M threshold**: Critical + High + Medium (CAT I + CAT II) (per spec clarification C3)
- **Informational findings**: Excluded from findings; summary counts only (per spec clarification C5)

### Rationale
These decisions align with DoD RMF operational practices. Medium-severity (CAT II) findings are significant enough to track in POA&M. Informational (severity 0) plugins provide enumeration data (open ports, banners) that is useful for context but not actionable as individual vulnerabilities.

---

## Summary of Resolved Unknowns

| Unknown | Resolution | Source |
|---------|------------|--------|
| .nessus XML format details | Fully documented — XDocument parsing, 35+ element types | Research Task 1 |
| CVE→NIST mapping approach | Layered: STIG-ID xref (Priority 1) → Plugin family heuristic (Priority 2) | Research Task 2 |
| Plugin family → NIST table | 35-entry curated table, embedded JSON | Research Task 3 |
| Finding identity key | Plugin ID + Hostname + Port | Spec clarification C1 |
| Import RBAC | ISSO + SCA + System Admin | Spec clarification C2 |
| POA&M threshold | Critical + High + Medium | Spec clarification C3 |
| Mapping table mutability | Fixed/curated (not RBAC-editable) | Spec clarification C4 |
| Informational plugins | Excluded from findings | Spec clarification C5 |

All NEEDS CLARIFICATION items resolved. No outstanding unknowns.
