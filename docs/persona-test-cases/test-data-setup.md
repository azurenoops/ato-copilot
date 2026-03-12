# Test Data Setup: Persona End-to-End Test Suite

**Feature**: 020 | **Date**: _______________ | **Prepared By**: _______________

---

## Purpose

This document describes how to prepare a clean test environment (or reset an existing one) for executing the 172 persona test cases. All test cases use the **same cumulative "Eagle Eye" system** that progresses through the full RMF lifecycle.

---

## 1. Test Data Constants

These values are used throughout all test cases. If you change any value, update **every** natural language input in `spec.md` that references it.

| Constant | Value | Used In |
|----------|-------|---------|
| `SYSTEM_NAME` | Eagle Eye | All persona sections |
| `SYSTEM_TYPE` | Major Application | ISSM-01 |
| `ENVIRONMENT` | Azure Government | ISSM-01 |
| `BASELINE` | Moderate (325 controls) | ISSM-11 |
| `SUBSCRIPTION_ID` | sub-12345-abcde | ISSO-13, ENG-06 |
| `ENGINEER_NAME` | SSgt Rodriguez | ISSM-27, ISSO-22, ENG-11 |
| `ISSO_NAME` | Jane Smith | ISSM-04 |
| `SCA_NAME` | Bob Jones | ISSM-04 |
| `AO_NAME` | Col. Thompson | AO-04 through AO-07 |
| `PII_CATEGORIES` | Name, SSN, Email | ISSM-44, ISSO-26 |
| `RETENTION_PERIOD` | 7 years per DoD 5400.11 | ISSM-47 |
| `INTERCONNECTION_REMOTE_1` | DISA DEE (smtp.dee.disa.mil) | ISSM-48, ISSO-29 |
| `INTERCONNECTION_REMOTE_2` | Azure DevOps (dev.azure.com) | ENG-28 |
| `INTERCONNECTION_PORT_1` | 587 (SMTP/TLS) | ISSM-48, ISSO-29 |
| `INTERCONNECTION_PORT_2` | 443 (HTTPS) | ENG-28 |
| `ISA_SCOPE` | All registered interconnections | ISSM-52 |
| `AGREEMENT_TYPE` | Memorandum of Agreement (MOA) | ISSM-53 |
| `SSP_SECTION_5` | System Architecture | ISSO-31, ENG-29 |
| `SSP_SECTION_6` | Technical Controls | ISSO-32, ENG-29 |
| `HW_ITEM_NAME` | web-server-01 | INV-01, INV-02, INV-03 |
| `HW_MANUFACTURER` | Dell | INV-01 |
| `HW_IP_ADDRESS` | 10.0.0.1 | INV-01 |
| `SW_ITEM_NAME` | RHEL 9.2 | INV-01 (software) |
| `SW_VENDOR` | Red Hat | INV-01 (software) |
| `SW_VERSION` | 9.2 | INV-01 (software) |
| `NGV_CONTROL_ID` | AC-1 | NGV-01 through NGV-07 |
| `NGV_CONTROL_FAMILY` | AC | NGV-06, NGV-07 |
| `NGV_NARRATIVE_TEXT` | The organization develops, documents, and disseminates an access control policy... | NGV-01 |
| `NGV_UPDATED_TEXT` | Updated: The organization maintains access control policy consistent with... | NGV-02 |
| `NGV_CHANGE_REASON` | Updated per ISSM feedback on 2026 assessment | NGV-02 |
| `NGV_REVIEWER` | ISSM (SecurityLead) | NGV-04, NGV-06 |
| `NESSUS_FILE_NAME` | acas-scan-results.nessus | F026 ACAS import |
| `NESSUS_HOST_1` | eagleeye-web01.example.mil | F026 host target |
| `NESSUS_HOST_IP_1` | 10.0.1.10 | F026 host IP |

---

## 2. Clean Environment Preparation

### Option A: Fresh Database (Recommended)

1. Stop the MCP server
2. Delete the local database:
   ```bash
   rm -f ato-copilot.db ato-copilot.db-shm ato-copilot.db-wal
   ```
3. Rebuild and restart the server:
   ```bash
   dotnet build Ato.Copilot.sln
   dotnet run --project src/Ato.Copilot.Mcp
   ```
4. EF Core migrations will auto-apply, creating a clean schema

### Option B: Delete Existing Eagle Eye System

If other test data must be preserved:

1. Query for the system: `@ato Show system details for Eagle Eye`
2. Note the `system_id`
3. Delete via database or admin API (if available)
4. Verify: `@ato Show system details for Eagle Eye` → "System not found"

### Option C: Use Alternative System Name

If "Eagle Eye" cannot be deleted:

1. Choose a new name (e.g., "Eagle Eye v2", "Falcon Watch")
2. Find-and-replace the system name in **all** spec NL inputs
3. Update the constant table above
4. Note the name change in the results template

---

## 3. Required Test Data Files

The following files are required for scan import test cases. Place them in an accessible directory and note the path.

### 3.1 Sample Prisma Cloud CSV

**Used by**: ISSM-19, ISSM-40, ERR-02

**File**: `test-data/prisma-cloud-scan.csv`

**Required Columns**:

| Column | Example Value | Notes |
|--------|--------------|-------|
| Alert ID | PRISMA-001 | Unique Prisma alert identifier |
| Severity | high | high, medium, low, critical |
| Policy Name | Azure Storage Account without secure transfer | Prisma policy that triggered |
| Resource Name | storageaccount01 | Cloud resource name |
| Resource Type | Microsoft.Storage/storageAccounts | Azure resource type |
| Cloud Account | sub-12345-abcde | Subscription ID |
| Region | usgovvirginia | Azure Government region |
| Status | open | open, resolved, dismissed |
| NIST Mapping | SC-8, SC-28 | Comma-separated NIST control IDs |
| First Seen | 2026-01-15T10:00:00Z | ISO 8601 timestamp |
| Last Seen | 2026-03-01T14:30:00Z | ISO 8601 timestamp |

**Minimum Rows**: 10 (mix of severities and statuses)

**Malformed Version** (for ERR-02): Create a copy with missing required columns or garbled data:
- `test-data/prisma-cloud-scan-malformed.csv`
- Remove the "Alert ID" column header
- Add rows with empty severity fields

### 3.2 Prisma Cloud API JSON

**Used by**: ISSM-20

**File**: `test-data/prisma-cloud-api-results.json`

**Required Structure**:

```json
{
  "totalRows": 5,
  "items": [
    {
      "alertId": "P-API-001",
      "status": "open",
      "policy": {
        "name": "Azure Key Vault without purge protection",
        "severity": "high",
        "cloudType": "azure",
        "complianceMetadata": [
          {
            "standardName": "NIST 800-53 Rev 5",
            "requirementId": "SC-12",
            "requirementName": "Cryptographic Key Establishment and Management"
          }
        ]
      },
      "resource": {
        "name": "kv-eagleeye-prod",
        "resourceType": "Microsoft.KeyVault/vaults",
        "accountId": "sub-12345-abcde",
        "regionId": "usgovvirginia"
      },
      "remediation": {
        "description": "Enable purge protection on the Key Vault",
        "cliScript": "az keyvault update --name kv-eagleeye-prod --enable-purge-protection true",
        "actions": { "isAutoRemediable": true }
      },
      "firstSeen": 1706000000000,
      "lastSeen": 1709300000000
    }
  ]
}
```

**Minimum Items**: 5 (mix of auto-remediable and manual)

### 3.3 CKL Checklist File

**Used by**: ISSO-09

**File**: `test-data/windows-2022-stig.ckl`

**Format**: DISA STIG Viewer `.ckl` XML format

**Required Content**:
- STIG Benchmark: Windows Server 2022 (or equivalent)
- Evaluation results for ≥ 20 STIG rules
- Mix of statuses: `NotAFinding`, `Open`, `Not_Applicable`, `Not_Reviewed`
- Each `VULN` element must include `Vuln_Num`, `Rule_ID`, `Severity`, `Status`

**Minimal CKL Structure**:

```xml
<?xml version="1.0" encoding="utf-8"?>
<?xml-stylesheet type='text/xsl' href='STIG_unclass.xsl'?>
<CHECKLIST>
  <ASSET>
    <ROLE>Computing</ROLE>
    <ASSET_TYPE>Computing</ASSET_TYPE>
    <HOST_NAME>eagleeye-web01</HOST_NAME>
    <HOST_IP>10.0.1.10</HOST_IP>
  </ASSET>
  <STIGS>
    <iSTIG>
      <STIG_INFO>
        <SI_DATA>
          <SID_NAME>title</SID_NAME>
          <SID_DATA>Windows Server 2022 STIG</SID_DATA>
        </SI_DATA>
        <SI_DATA>
          <SID_NAME>version</SID_NAME>
          <SID_DATA>1</SID_DATA>
        </SI_DATA>
      </STIG_INFO>
      <VULN>
        <STIG_DATA>
          <VULN_ATTRIBUTE>Vuln_Num</VULN_ATTRIBUTE>
          <ATTRIBUTE_DATA>V-254239</ATTRIBUTE_DATA>
        </STIG_DATA>
        <STIG_DATA>
          <VULN_ATTRIBUTE>Severity</VULN_ATTRIBUTE>
          <ATTRIBUTE_DATA>high</ATTRIBUTE_DATA>
        </STIG_DATA>
        <STIG_DATA>
          <VULN_ATTRIBUTE>Rule_ID</VULN_ATTRIBUTE>
          <ATTRIBUTE_DATA>SV-254239r848646_rule</ATTRIBUTE_DATA>
        </STIG_DATA>
        <STATUS>NotAFinding</STATUS>
        <FINDING_DETAILS>Verified via GPO audit</FINDING_DETAILS>
        <COMMENTS>Automated check passed</COMMENTS>
      </VULN>
      <!-- Additional VULN entries... -->
    </iSTIG>
  </STIGS>
</CHECKLIST>
```

### 3.4 Nessus/ACAS Scan File

**Used by**: Feature 026 — ACAS/Nessus import test cases

**File**: `test-data/acas-scan-results.nessus`

**Format**: Tenable Nessus v2 XML (`.nessus`)

**Required Content**:
- At least 2 hosts with HostProperties (host-ip, hostname, operating-system)
- ≥ 20 ReportItem elements across multiple plugin families
- Mix of severity levels: Critical (4), High (3), Medium (2), Low (1), None (0)
- Plugin families that trigger curated and heuristic control mappings

**Minimal Nessus Structure**:

```xml
<?xml version="1.0" ?>
<NessusClientData_v2>
  <Report name="Eagle Eye ACAS Scan">
    <ReportHost name="eagleeye-web01.example.mil">
      <HostProperties>
        <tag name="host-ip">10.0.1.10</tag>
        <tag name="hostname">eagleeye-web01.example.mil</tag>
        <tag name="operating-system">Microsoft Windows Server 2022</tag>
        <tag name="Credentialed_Scan">true</tag>
        <tag name="HOST_START">Wed Mar 01 08:00:00 2026</tag>
        <tag name="HOST_END">Wed Mar 01 08:30:00 2026</tag>
      </HostProperties>
      <ReportItem port="445" svc_name="cifs" protocol="tcp"
                  severity="4" pluginID="97833"
                  pluginName="MS17-010: Security Update for SMB Server"
                  pluginFamily="Windows : Microsoft Bulletins">
        <risk_factor>Critical</risk_factor>
        <synopsis>Remote code execution vulnerability in SMB</synopsis>
        <description>The remote Windows host is affected by EternalBlue.</description>
        <solution>Apply Microsoft security update MS17-010.</solution>
      </ReportItem>
      <!-- Additional ReportItem entries... -->
    </ReportHost>
  </Report>
</NessusClientData_v2>
```

**Variant Files** (optional for negative testing):
- `test-data/acas-scan-malformed.nessus` — Invalid XML for error handling tests
- `test-data/acas-scan-large.nessus` — 500+ plugins for performance tests

---

### 3.5 XCCDF Results File

**Used by**: ISSO-10

**File**: `test-data/scap-scan-results.xml`

**Format**: NIST SCAP 1.3 XCCDF results XML

**Required Content**:
- Benchmark: Windows Server 2022 or equivalent
- Test results for ≥ 15 rules
- Mix of results: `pass`, `fail`, `notapplicable`, `notchecked`

**Minimal XCCDF Structure**:

```xml
<?xml version="1.0" encoding="UTF-8"?>
<TestResult xmlns="http://checklists.nist.gov/xccdf/1.2"
            id="xccdf_mil.disa.stig_testresult_default"
            version="1.0"
            test-system="cpe:/a:spawar:scap_compliance_checker:5.7">
  <benchmark href="xccdf_mil.disa.stig_benchmark_Windows_Server_2022_STIG"/>
  <title>SCAP Scan Results - Eagle Eye Web Server</title>
  <target>eagleeye-web01</target>
  <target-address>10.0.1.10</target-address>
  <rule-result idref="xccdf_mil.disa.stig_rule_SV-254239r848646_rule">
    <result>pass</result>
    <check>
      <check-content-ref href="Windows_Server_2022_STIG-oval.xml"
                         name="oval:mil.disa.stig.windows_server_2022:def:1001"/>
    </check>
  </rule-result>
  <rule-result idref="xccdf_mil.disa.stig_rule_SV-254240r848649_rule">
    <result>fail</result>
    <check>
      <check-content-ref href="Windows_Server_2022_STIG-oval.xml"
                         name="oval:mil.disa.stig.windows_server_2022:def:1002"/>
    </check>
  </rule-result>
  <!-- Additional rule-result entries... -->
  <score system="urn:xccdf:scoring:default" maximum="100">78.5</score>
</TestResult>
```

---

## 4. Test Data File Locations

After preparing the files, record their locations here:

| File | Path | Verified |
|------|------|----------|
| Prisma CSV | `test-data/prisma-cloud-scan.csv` | ⬜ |
| Prisma CSV (malformed) | `test-data/prisma-cloud-scan-malformed.csv` | ⬜ |
| Prisma API JSON | `test-data/prisma-cloud-api-results.json` | ⬜ |
| CKL checklist | `test-data/windows-2022-stig.ckl` | ⬜ |
| XCCDF results | `test-data/scap-scan-results.xml` | ⬜ |
| Nessus scan | `test-data/acas-scan-results.nessus` | ⬜ |
| Nessus (malformed) | `test-data/acas-scan-malformed.nessus` | ⬜ |
| Nessus (large) | `test-data/acas-scan-large.nessus` | ⬜ |

> **Note**: CKL export (ISSO-25, ENG-27) generates output files — no input file is needed for export tests. PTA, PIA, ISA, and SSP section data are created through natural language queries using the constants above.

---

## 5. Persona Account Setup

Each persona requires a separate user account (or a single account with PIM role switching). Document the test accounts here:

| Persona | Account / UPN | PIM Eligible | Notes |
|---------|---------------|-------------|-------|
| ISSM | _______________ | ⬜ SecurityLead | |
| ISSO | _______________ | ⬜ Analyst | |
| SCA | _______________ | ⬜ Auditor | |
| AO | _______________ | ⬜ AuthorizingOfficial | |
| Engineer | _______________ | ⬜ PlatformEngineer | Default CAC mapping may suffice |

**Single-Account Mode**: If using one account with PIM role switching, verify that only one compliance role can be active at a time (to test separation of duties properly).

---

## 6. Pre-Test Verification Steps

Run these checks after setup to confirm readiness:

```text
Step 1: Start MCP server
  $ dotnet run --project src/Ato.Copilot.Mcp
  ✓ Health check responds

Step 2: Verify clean slate
  @ato Show system details for Eagle Eye
  ✓ "System not found" (or equivalent)

Step 3: Verify PIM eligibility
  @ato What PIM roles am I eligible for?
  ✓ All 5 compliance roles listed

Step 4: Activate ISSM role
  @ato Activate my Compliance.SecurityLead role for 8 hours — persona test suite
  ✓ Role activated

Step 5: Verify test data files
  $ ls test-data/
  ✓ All 5 files present

Step 6: Open results template
  $ code specs/020-persona-test-cases/results-template.md
  ✓ Template opens with all test case rows

Step 7: Begin ISSM-01
  "Register a new system called 'Eagle Eye' as a Major Application
   with mission-critical designation in Azure Government"
  ✓ system_id returned → record in results template
```

---

## 7. Reset Procedure (Between Test Runs)

If you need to re-run the test suite from scratch:

1. **Stop** the MCP server
2. **Delete** the database: `rm -f ato-copilot.db*`
3. **Rebuild**: `dotnet build Ato.Copilot.sln`
4. **Restart**: `dotnet run --project src/Ato.Copilot.Mcp`
5. **Verify** clean slate (Step 2 above)
6. **Re-activate** ISSM PIM role
7. **Begin** from ISSM-01

---

## 8. Troubleshooting

| Issue | Resolution |
|-------|-----------|
| "Eagle Eye" already exists | Use Option B or C from Section 2 |
| PIM role won't activate | Check PIM eligibility assignment in Azure AD; verify no conflicting active roles |
| Import file not found | Verify file path; ensure MCP server can access the directory (check Docker volume mounts if containerized) |
| Database migration error | Delete `.db` files and rebuild; EF Core will re-apply all migrations |
| Tool not found in `/tools/list` | Verify the correct build configuration; ensure all project references are included in the solution |
| Subscription not accessible | Verify Azure CLI login: `az login --use-device-code`; confirm Azure Government cloud: `az cloud set --name AzureUSGovernment` |
