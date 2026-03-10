# Contracts: 021 — MCP Tool Contracts

**Date**: 2026-03-07 | **Plan**: [plan.md](../plan.md) | **Data Model**: [data-model.md](../data-model.md)

All tools follow the existing MCP JSON-RPC 2.0 protocol (`McpRequest`/`McpResponse`) and the standard tool envelope:

```json
{
  "content": [{ "type": "text", "text": "<response>" }],
  "isError": false
}
```

Errors use `McpToolResult.Error(message)`. All tools extend `BaseTool` and are registered via `RegisterTool()`.

**Total tools**: 12 (4 privacy + 5 interconnection + 3 agreement/certification)

---

## Phase 1 — Privacy Tools

### `compliance_create_pta`

Conduct a Privacy Threshold Analysis for a registered system. Auto-detects PII from system information types or accepts manual answers.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `system_id` | string | yes | RegisteredSystem ID |
| `manual_mode` | bool | no | Default: false. When true, uses explicit PII parameters instead of auto-detection. |
| `collects_pii` | bool | no | Manual mode: whether system collects PII |
| `maintains_pii` | bool | no | Manual mode: whether system stores/maintains PII |
| `disseminates_pii` | bool | no | Manual mode: whether system shares PII externally |
| `pii_categories` | string[] | no | Manual mode: PII types (e.g., "SSN", "Medical Records", "Financial Data") |
| `estimated_record_count` | int | no | Manual mode: estimated number of PII records |
| `exemption_rationale` | string | no | Manual mode: rationale if system is exempt from PIA |

**Returns**: PTA determination JSON:

```json
{
  "ptaId": "guid",
  "determination": "PiaRequired",
  "collectsPii": true,
  "maintainsPii": true,
  "disseminatesPii": false,
  "piiCategories": ["Personnel Records", "Financial Data"],
  "piiSourceInfoTypes": ["D.8.1", "D.28.2"],
  "rationale": "System processes 2 PII-containing information types: D.8.1 (Personnel Records) classified as Moderate confidentiality, and D.28.2 (Financial Data) classified as High confidentiality. Estimated record count (500) exceeds E-Government Act threshold of 10. Full PIA required per OMB M-03-22."
}
```

**RBAC**: `Compliance.Analyst`, `Compliance.SecurityLead`

**Validation**:
- System must exist and be active
- System must have a `SecurityCategorization` with at least one `InformationType` (for auto mode)
- If manual mode, at least `collects_pii` must be provided
- If `exemption_rationale` is provided, determination is set to `Exempt`

**Error cases**:
- `system_id` not found → `"System '{system_id}' not found"`
- No categorization (auto mode) → `"System has no security categorization. Complete FIPS 199 categorization before running PTA."`
- No information types (auto mode) → `"System has no information types defined. Add information types to security categorization first."`

---

### `compliance_generate_pia`

Generate a Privacy Impact Assessment document for a system that requires one.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `system_id` | string | yes | RegisteredSystem ID |

**Returns**: PIA document JSON:

```json
{
  "piaId": "guid",
  "status": "Draft",
  "version": 1,
  "narrativeDocument": "# Privacy Impact Assessment\n\n## 1. System Information\n\nThe ACME Portal (ACME) is a Major Application...\n\n## 2. Information Collected\n\n...",
  "sections": [
    {
      "sectionId": "1.1",
      "title": "System Information",
      "question": "Describe the system, including its name, purpose, and operational environment.",
      "answer": "The ACME Portal (ACME) is a Major Application hosted in Azure Government...",
      "isPrePopulated": true,
      "sourceField": "RegisteredSystem.Description"
    },
    {
      "sectionId": "2.1",
      "title": "Information Collected",
      "question": "What PII does the system collect, and from whom?",
      "answer": "The system collects Personnel Records (SP 800-60 D.8.1) and Financial Data (SP 800-60 D.28.2)...",
      "isPrePopulated": true,
      "sourceField": "PrivacyThresholdAnalysis.PiiCategories"
    }
  ],
  "prePopulatedSections": 4,
  "totalSections": 8
}
```

**RBAC**: `Compliance.Analyst`, `Compliance.SecurityLead`

**Validation**:
- System must exist and be active
- PTA must exist with `Determination = PiaRequired`
- If PIA already exists in Approved status, return existing PIA (no regeneration unless expired)

**Error cases**:
- `system_id` not found → `"System '{system_id}' not found"`
- No PTA → `"No Privacy Threshold Analysis found. Run compliance_create_pta first."`
- PTA = PiaNotRequired → `"PTA determination is 'PIA Not Required'. No PIA needed for this system."`
- PTA = Exempt → `"System is exempt from PIA requirement. Exemption rationale: {rationale}"`

---

### `compliance_review_pia`

Review a PIA — approve or request revisions. ISSM/Privacy Officer only.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `system_id` | string | yes | RegisteredSystem ID |
| `decision` | string | yes | `"approve"` or `"request_revision"` |
| `comments` | string | no | Reviewer comments |
| `deficiencies` | string[] | no | Specific deficiencies (required if decision = request_revision) |

**Returns**: Review result JSON:

```json
{
  "piaId": "guid",
  "decision": "Approved",
  "newStatus": "Approved",
  "reviewerComments": "PIA is comprehensive and meets OMB M-03-22 requirements.",
  "deficiencies": [],
  "expirationDate": "2027-03-07T00:00:00Z"
}
```

**RBAC**: `Compliance.SecurityLead` only

**Validation**:
- System must exist and be active
- PIA must exist in `Draft` or `UnderReview` status
- `decision` must be "approve" or "request_revision"
- If "request_revision", at least one deficiency should be provided (warning if empty)

**Error cases**:
- `system_id` not found → `"System '{system_id}' not found"`
- No PIA → `"No Privacy Impact Assessment found for this system."`
- PIA already approved → `"PIA is already approved (approved on {date}). Use compliance_check_privacy_compliance to view status."`
- Invalid decision → `"Invalid decision '{decision}'. Must be 'approve' or 'request_revision'."`

---

### `compliance_check_privacy_compliance`

Get a comprehensive privacy and interconnection compliance dashboard for a system.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `system_id` | string | yes | RegisteredSystem ID |

**Returns**: Privacy compliance dashboard JSON:

```json
{
  "systemId": "guid",
  "systemName": "ACME Portal",
  "ptaDetermination": "PiaRequired | PiaNotRequired | Exempt | PendingConfirmation",
  "piaStatus": "Approved",
  "privacyGateSatisfied": true,
  "activeInterconnections": 3,
  "interconnectionsWithAgreements": 3,
  "expiredAgreements": 0,
  "expiringWithin90Days": 1,
  "interconnectionGateSatisfied": true,
  "hasNoExternalInterconnections": false,
  "overallStatus": "Compliant"
}
```

**RBAC**: `Compliance.Auditor`, `Compliance.SecurityLead`, `Compliance.AuthorizingOfficial`

**Validation**:
- System must exist and be active

**Error cases**:
- `system_id` not found → `"System '{system_id}' not found"`

---

## Phase 2 — Interconnection Tools

### `compliance_add_interconnection`

Register a system-to-system interconnection that crosses the authorization boundary.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `system_id` | string | yes | RegisteredSystem ID (source system) |
| `target_system_name` | string | yes | Name of external system |
| `target_system_owner` | string | no | Organization/POC owning target system |
| `target_system_acronym` | string | no | Target system abbreviation |
| `connection_type` | string | yes | `"direct"` \| `"vpn"` \| `"api"` \| `"federated"` \| `"wireless"` \| `"remote_access"` |
| `data_flow_direction` | string | yes | `"inbound"` \| `"outbound"` \| `"bidirectional"` |
| `data_classification` | string | yes | `"unclassified"` \| `"cui"` \| `"secret"` \| `"top_secret"` |
| `data_description` | string | no | Description of data exchanged |
| `protocols` | string[] | no | Protocols used (e.g., ["TLS 1.3", "REST/HTTPS"]) |
| `ports` | string[] | no | Ports used (e.g., ["443", "8443"]) |
| `security_measures` | string[] | no | Security controls (e.g., ["AES-256", "Mutual TLS"]) |
| `authentication_method` | string | no | How systems authenticate to each other |

**Returns**: Interconnection summary JSON:

```json
{
  "interconnectionId": "guid",
  "targetSystemName": "DISA SIPR Gateway",
  "status": "Proposed",
  "hasAgreement": false
}
```

**RBAC**: `Compliance.PlatformEngineer`, `Compliance.Analyst`

**Validation**:
- System must exist and be active
- `connection_type` must be a valid enum value
- `data_flow_direction` must be a valid enum value
- `data_classification` must be a valid enum value
- Adding an interconnection clears `HasNoExternalInterconnections` flag if previously set

**Error cases**:
- `system_id` not found → `"System '{system_id}' not found"`
- Invalid `connection_type` → `"Invalid connection type '{value}'. Valid values: direct, vpn, api, federated, wireless, remote_access"`
- Invalid `data_flow_direction` → `"Invalid data flow direction '{value}'. Valid values: inbound, outbound, bidirectional"`
- Invalid `data_classification` → `"Invalid data classification '{value}'. Valid values: unclassified, cui, secret, top_secret"`

---

### `compliance_list_interconnections`

List all system interconnections with agreement status summaries.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `system_id` | string | yes | RegisteredSystem ID |
| `status_filter` | string | no | Filter by status: `"proposed"` \| `"active"` \| `"suspended"` \| `"terminated"` |

**Returns**: Interconnection list JSON:

```json
{
  "systemId": "guid",
  "systemName": "ACME Portal",
  "totalInterconnections": 3,
  "interconnections": [
    {
      "id": "guid",
      "targetSystemName": "DISA SIPR Gateway",
      "targetSystemOwner": "DISA",
      "connectionType": "Vpn",
      "dataFlowDirection": "Bidirectional",
      "dataClassification": "Secret",
      "status": "Active",
      "agreements": [
        {
          "id": "guid",
          "type": "Isa",
          "title": "ISA — ACME Portal ↔ DISA SIPR Gateway",
          "status": "Signed",
          "expirationDate": "2027-01-15T00:00:00Z"
        }
      ]
    }
  ]
}
```

**RBAC**: All roles (`Compliance.Analyst`, `Compliance.SecurityLead`, `Compliance.Auditor`, `Compliance.AuthorizingOfficial`, `Compliance.PlatformEngineer`)

**Validation**:
- System must exist and be active

**Error cases**:
- `system_id` not found → `"System '{system_id}' not found"`
- Invalid `status_filter` → `"Invalid status filter '{value}'. Valid values: proposed, active, suspended, terminated"`

---

### `compliance_update_interconnection`

Update an existing interconnection's details or status.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `interconnection_id` | string | yes | SystemInterconnection ID |
| `status` | string | no | New status: `"proposed"` \| `"active"` \| `"suspended"` \| `"terminated"` |
| `status_reason` | string | no | Reason for status change (required for suspended/terminated) |
| `connection_type` | string | no | Updated connection type |
| `data_classification` | string | no | Updated data classification |
| `protocols` | string[] | no | Updated protocols |
| `ports` | string[] | no | Updated ports |
| `security_measures` | string[] | no | Updated security measures |

**Returns**: Updated interconnection JSON (same format as add).

**RBAC**: `Compliance.Analyst`, `Compliance.SecurityLead`

**Validation**:
- Interconnection must exist
- If status = `"suspended"` or `"terminated"`, `status_reason` is required
- If `data_classification` changes, a warning is returned: "Data classification changed — ISA review recommended"

**Error cases**:
- `interconnection_id` not found → `"Interconnection '{interconnection_id}' not found"`
- Missing status_reason for suspension → `"Status reason is required when suspending or terminating an interconnection"`

---

### `compliance_generate_isa`

Generate an AI-drafted Interconnection Security Agreement from interconnection data.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `interconnection_id` | string | yes | SystemInterconnection ID |

**Returns**: ISA generation result JSON:

```json
{
  "agreementId": "guid",
  "title": "Interconnection Security Agreement — ACME Portal ↔ DISA SIPR Gateway",
  "agreementType": "Isa",
  "narrativeDocument": "# Interconnection Security Agreement\n\n## 1. Introduction\n\nThis Interconnection Security Agreement (ISA) establishes the security requirements for the connection between ACME Portal (ACME) operated by [Organization] and DISA SIPR Gateway operated by DISA...\n\n## 2. System Description\n\n### 2.1 System A: ACME Portal\n- **System Type**: Major Application\n- **Hosting**: Azure Government\n- **Impact Level**: IL4\n\n### 2.2 System B: DISA SIPR Gateway\n- **Owner**: DISA\n\n## 3. Interconnection Details\n\n- **Connection Type**: VPN\n- **Data Flow**: Bidirectional\n- **Data Classification**: Secret\n- **Protocols**: IPSec, TLS 1.3\n- **Ports**: 443, 500, 4500\n\n## 4. Security Controls\n\n- AES-256 encryption\n- Mutual TLS authentication\n- Multi-factor authentication\n\n## 5. Roles and Responsibilities\n\n### System A Personnel\n- **ISSM**: [from RmfRoleAssignment]\n- **ISSO**: [from RmfRoleAssignment]\n\n### System B Personnel\n- **Point of Contact**: DISA\n\n## 6. Agreement Terms\n\n- **Effective Date**: [To be determined]\n- **Duration**: 1 year with annual renewal\n- **Termination**: Either party may terminate with 30 days written notice\n\n## 7. Signatures\n\n..."
}
```

**RBAC**: `Compliance.SecurityLead` only

**Validation**:
- Interconnection must exist
- Interconnection must be in `Proposed` or `Active` status (not terminated)

**Error cases**:
- `interconnection_id` not found → `"Interconnection '{interconnection_id}' not found"`
- Terminated interconnection → `"Cannot generate ISA for a terminated interconnection"`

---

### `compliance_validate_agreements`

Validate that all active system interconnections have signed, current agreements.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `system_id` | string | yes | RegisteredSystem ID |

**Returns**: Agreement validation report JSON:

```json
{
  "totalInterconnections": 3,
  "compliantCount": 2,
  "expiringWithin90DaysCount": 1,
  "missingAgreementCount": 0,
  "expiredAgreementCount": 0,
  "isFullyCompliant": true,
  "items": [
    {
      "interconnectionId": "guid",
      "targetSystemName": "DISA SIPR Gateway",
      "validationStatus": "Compliant",
      "agreementTitle": "ISA — ACME Portal ↔ DISA SIPR Gateway",
      "expirationDate": "2027-01-15T00:00:00Z",
      "notes": null
    },
    {
      "interconnectionId": "guid",
      "targetSystemName": "DFAS Financial System",
      "validationStatus": "ExpiringSoon",
      "agreementTitle": "MOU — ACME Portal ↔ DFAS Financial System",
      "expirationDate": "2026-05-20T00:00:00Z",
      "notes": "Agreement expires in 74 days"
    },
    {
      "interconnectionId": "guid",
      "targetSystemName": "Enterprise LDAP",
      "validationStatus": "Compliant",
      "agreementTitle": "ISA — ACME Portal ↔ Enterprise LDAP",
      "expirationDate": "2027-09-01T00:00:00Z",
      "notes": null
    }
  ]
}
```

**RBAC**: `Compliance.Auditor`, `Compliance.SecurityLead`

**Validation**:
- System must exist and be active
- Only evaluates `Active` interconnections (Proposed/Suspended/Terminated excluded)

**Error cases**:
- `system_id` not found → `"System '{system_id}' not found"`

---

### `compliance_register_agreement`

Register a pre-existing ISA, MOU, or SLA agreement for a system interconnection.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `interconnection_id` | string | yes | SystemInterconnection ID |
| `agreement_type` | string | yes | `"isa"` \| `"mou"` \| `"sla"` |
| `title` | string | yes | Agreement title |
| `document_reference` | string | no | URL or path to agreement document |
| `status` | string | no | Initial status: `"draft"` \| `"pending_signature"` \| `"signed"`. Default: `"draft"` |
| `effective_date` | string | no | ISO 8601 date when agreement becomes effective |
| `expiration_date` | string | no | ISO 8601 date when agreement expires |
| `signed_by_local` | string | no | Local signatory name/title |
| `signed_by_local_date` | string | no | ISO 8601 local signature date |
| `signed_by_remote` | string | no | Remote/partner signatory name/title |
| `signed_by_remote_date` | string | no | ISO 8601 remote signature date |

**Returns**: Agreement registration result JSON:

```json
{
  "agreementId": "guid",
  "title": "ISA — ACME Portal ↔ DISA SIPR Gateway",
  "agreementType": "Isa",
  "status": "Signed",
  "expirationDate": "2027-03-07T00:00:00Z"
}
```

**RBAC**: `Compliance.SecurityLead` only

**Validation**:
- Interconnection must exist and not be Terminated
- `agreement_type` must be a valid enum value
- If `status` = `"signed"`, `effective_date` should be provided (warning if empty)
- If `status` = `"signed"`, `expiration_date` should be provided (warning if empty)

**Error cases**:
- `interconnection_id` not found → `"Interconnection '{interconnection_id}' not found"`
- Terminated interconnection → `"Cannot register agreement for a terminated interconnection"`
- Invalid `agreement_type` → `"Invalid agreement type '{value}'. Valid values: isa, mou, sla"`

---

### `compliance_update_agreement`

Update an existing agreement's status or metadata.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `agreement_id` | string | yes | InterconnectionAgreement ID |
| `status` | string | no | New status: `"draft"` \| `"pending_signature"` \| `"signed"` \| `"expired"` \| `"terminated"` |
| `document_reference` | string | no | Updated URL or path to agreement document |
| `effective_date` | string | no | Updated ISO 8601 effective date |
| `expiration_date` | string | no | Updated ISO 8601 expiration date |
| `signed_by_local` | string | no | Updated local signatory |
| `signed_by_local_date` | string | no | Updated ISO 8601 local signature date |
| `signed_by_remote` | string | no | Updated remote signatory |
| `signed_by_remote_date` | string | no | Updated ISO 8601 remote signature date |
| `review_notes` | string | no | Review or renewal notes |

**Returns**: Updated agreement JSON:

```json
{
  "agreementId": "guid",
  "title": "ISA — ACME Portal ↔ DISA SIPR Gateway",
  "agreementType": "Isa",
  "status": "Signed",
  "expirationDate": "2027-03-07T00:00:00Z"
}
```

**RBAC**: `Compliance.SecurityLead` only

**Validation**:
- Agreement must exist
- If transitioning to `"signed"`, at least one signatory should be provided (warning if empty)
- Terminated agreements cannot be updated (except review_notes)

**Error cases**:
- `agreement_id` not found → `"Agreement '{agreement_id}' not found"`
- Invalid `status` → `"Invalid status '{value}'. Valid values: draft, pending_signature, signed, expired, terminated"`
- Terminated agreement → `"Cannot update a terminated agreement (except review_notes)"`

---

### `compliance_certify_no_interconnections`

Certify that a system has no external interconnections, satisfying Gate 4 without requiring interconnection records.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `system_id` | string | yes | RegisteredSystem ID |
| `certify` | bool | yes | `true` to certify no interconnections, `false` to revoke certification |

**Returns**: Certification result JSON:

```json
{
  "systemId": "guid",
  "hasNoExternalInterconnections": true,
  "interconnectionGateSatisfied": true
}
```

**RBAC**: `Compliance.Analyst`, `Compliance.SecurityLead`

**Validation**:
- System must exist and be active
- If `certify = true` and system has Active interconnections, return error (must suspend/terminate them first)

**Error cases**:
- `system_id` not found → `"System '{system_id}' not found"`
- Active interconnections exist → `"System has {count} active interconnection(s). Suspend or terminate them before certifying no interconnections."`

---

## RBAC Summary Matrix

| Tool | PlatformEngineer | Analyst | SecurityLead | Auditor | AuthorizingOfficial |
|------|:---:|:---:|:---:|:---:|:---:|
| `compliance_create_pta` | ❌ | ✅ | ✅ | ❌ | ❌ |
| `compliance_generate_pia` | ❌ | ✅ | ✅ | ❌ | ❌ |
| `compliance_review_pia` | ❌ | ❌ | ✅ | ❌ | ❌ |
| `compliance_check_privacy_compliance` | ❌ | ❌ | ✅ | ✅ | ✅ |
| `compliance_add_interconnection` | ✅ | ✅ | ❌ | ❌ | ❌ |
| `compliance_list_interconnections` | ✅ | ✅ | ✅ | ✅ | ✅ |
| `compliance_update_interconnection` | ❌ | ✅ | ✅ | ❌ | ❌ |
| `compliance_generate_isa` | ❌ | ❌ | ✅ | ❌ | ❌ |
| `compliance_register_agreement` | ❌ | ❌ | ✅ | ❌ | ❌ |
| `compliance_update_agreement` | ❌ | ❌ | ✅ | ❌ | ❌ |
| `compliance_validate_agreements` | ❌ | ❌ | ✅ | ✅ | ❌ |
| `compliance_certify_no_interconnections` | ❌ | ✅ | ✅ | ❌ | ❌ |
