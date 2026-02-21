# API/Contracts Checklist: Core Compliance Capabilities

**Purpose**: Validate completeness, clarity, and consistency of MCP tool contracts, response schemas, and error handling
**Created**: 2026-02-21
**Feature**: [contracts/](../contracts/)

## Response Envelope Consistency

- [x] CHK001 Is the response envelope schema (`{status, data, metadata}`) specified for ALL 12 MCP tools including `configuration_manage`? [Completeness, Spec §Contracts]
- [x] CHK002 Is `metadata.executionTimeMs` unit (milliseconds vs seconds) explicitly stated in the envelope schema? [Clarity, Spec §Contracts]
- [x] CHK003 Is the `metadata.timestamp` format specified (ISO 8601 with timezone? UTC only?) [Clarity, Spec §Contracts]
- [x] CHK004 Are pagination requirements defined for responses that could return unbounded result sets (findings, audit logs, history)? [Completeness, Gap]
- [x] CHK005 Is maximum response size or item count defined for any tool response? [Completeness, Gap]

## Parameter Validation

- [x] CHK006 Are validation error responses specified for invalid `subscriptionId` format across all tools that accept it? [Consistency, Spec §Contracts]
- [x] CHK007 Is the behavior defined when mutually exclusive parameters are both provided (e.g., `findingId` + `controlFamily` on `compliance_remediate`)? [Clarity, Spec §compliance_remediate]
- [x] CHK008 Are default values for optional parameters documented consistently across all tools? [Consistency, Spec §Contracts]
- [x] CHK009 Is `controlFamilies` comma-separated string format validated (e.g., what if whitespace is included: "AC, AU, SC")? [Clarity, Spec §compliance_assess]
- [x] CHK010 Is case sensitivity defined for enum parameters (`NIST80053` vs `nist80053`, `High` vs `high`)? [Clarity, Gap]

## Error Code Coverage

- [x] CHK011 Are all 14 error codes mapped to specific tool(s) that can produce them? [Completeness, Spec §Error Codes]
- [x] CHK012 Is there a defined error code for invalid `controlId` format that doesn't match the regex `^[A-Z]{2}-\d+(\.\d+)?$`? [Completeness, Spec §INVALID_CONTROL_ID]
- [x] CHK013 Are error codes defined for configuration-specific failures (invalid framework, invalid baseline)? [Completeness, Gap]
- [x] CHK014 Is the error response behavior specified when multiple errors occur simultaneously? [Clarity, Gap]
- [x] CHK015 Are HTTP status codes mapped to MCP error codes for the HTTP bridge mode? [Completeness, Gap]

## Tool Contract Completeness

- [x] CHK016 Does `compliance_assess` define the response structure for each `scanType` individually (resource-only vs policy-only vs combined)? [Clarity, Spec §compliance_assess]
- [x] CHK017 Does `compliance_remediate` define what `confirmationRequired: true` means for the MCP protocol (how does the agent re-invoke with confirmation)? [Clarity, Spec §compliance_remediate]
- [x] CHK018 Does `compliance_collect_evidence` define the response when evidence already exists for a control (append, replace, deduplicate)? [Completeness, Gap]
- [x] CHK019 Does `compliance_generate_document` define maximum document size or content truncation behavior? [Completeness, Gap]
- [x] CHK020 Does `compliance_chat` define how conversation context influences tool routing (e.g., follow-up "fix it" after discussing AC-2)? [Clarity, Spec §compliance_chat]
- [x] CHK021 Is the `configuration_manage` tool's `set_preference` action defined for all valid `preferenceName`/`preferenceValue` combinations? [Completeness, Spec §configuration_manage]

## Cross-Tool Consistency

- [x] CHK022 Do all tools that accept `subscriptionId` behave identically when it's omitted (fall back to configured default)? [Consistency, Spec §Contracts]
- [x] CHK023 Do all tools that accept `framework` use the same enum values (`NIST80053`, `FedRAMPHigh`, etc.)? [Consistency, Spec §Contracts]
- [x] CHK024 Are tool names consistent in naming convention (`compliance_` prefix for all compliance tools, `configuration_` for config)? [Consistency, Spec §Contracts]
- [x] CHK025 Is the relationship between `compliance_status` and `compliance_monitoring` (action: "status") clearly differentiated? [Clarity, Spec §Contracts]

## Notes

- Check items off as completed: `[x]`
- Items tagged `[Gap]` indicate missing contract definitions
- Items tagged with `[Spec §...]` reference existing contract documents
- Contract ambiguities discovered here should be resolved before Phase 4 (US2) implementation
