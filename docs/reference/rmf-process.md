# RMF Process Reference

This document describes how Ato.Copilot implements the NIST Risk Management Framework (RMF) lifecycle defined in NIST SP 800-37 Rev. 2, with continuous monitoring per NIST SP 800-137.

## RMF Phases & Tool Mapping

| # | RMF Phase | NIST SP 800-37 Step | Primary Tool(s) |
|---|-----------|---------------------|-----------------|
| 1 | **Prepare** | Prepare | `compliance_register_system` |
| 2 | **Categorize** | Categorize | `compliance_categorize_system` |
| 3 | **Select** | Select | `compliance_select_baseline`, `compliance_tailor_baseline`, `compliance_set_inheritance` |
| 4 | **Implement** | Implement | `compliance_write_narrative`, `compliance_generate_ssp` |
| 5 | **Assess** | Assess | `compliance_assess_control`, `compliance_generate_sar` |
| 6 | **Authorize** | Authorize | `compliance_issue_authorization`, `compliance_create_poam`, `compliance_generate_rar`, `compliance_bundle_authorization_package` |
| 7 | **Monitor** | Monitor | `compliance_create_conmon_plan`, `compliance_generate_conmon_report`, `compliance_track_ato_expiration`, `compliance_report_significant_change`, `compliance_reauthorization_workflow`, `compliance_multi_system_dashboard` |

## Continuous Monitoring (Phase 7 — Monitor)

Once a system receives an ATO, it enters the continuous monitoring phase. Ato.Copilot provides comprehensive tooling for this phase per NIST SP 800-137 and DoD 8510.01.

### ConMon Plan (§4.1)

Each system has exactly one ConMon plan (`compliance_create_conmon_plan`). The plan defines:
- **Assessment frequency**: How often automated assessments run (Monthly, Quarterly, Annually)
- **Annual review date**: When the full ConMon plan is reviewed
- **Report distribution**: Stakeholders who receive periodic reports
- **Significant change triggers**: Custom triggers beyond the built-in set

Calling the tool again for the same system updates the existing plan (upsert pattern).

### Periodic Reports (§4.2)

The `compliance_generate_conmon_report` tool generates compliance reports that measure:
- **Compliance score**: Percentage of `Satisfied` control effectiveness records
- **Baseline delta**: Drift from the score recorded at authorization
- **Finding trends**: New findings opened vs. resolved in the period
- **POA&M status**: Open and overdue Plan of Action & Milestones items

Reports are persisted as `ConMonReport` entities linked to the ConMon plan.

### ATO Expiration Tracking (§4.3)

The `compliance_track_ato_expiration` tool monitors authorization expiration with graduated alerts:

| Alert Level | Days Remaining | Recommended Action |
|------------|---------------|-------------------|
| None | > 90 or DATO | No action needed |
| Info | 60–90 | Begin reauthorization planning |
| Warning | 30–60 | Submit reauthorization package |
| Urgent | < 30 | Escalate to AO immediately |
| Expired | ≤ 0 | System operating without authorization — auto-deactivated |

**DATO handling**: Denial of Authorization to Operate always returns `None` alert level since these systems should not be operating regardless of expiration date.

### Significant Change Detection (§4.4)

The `compliance_report_significant_change` tool records and classifies changes that may require reauthorization. Built-in trigger types that automatically flag `requires_reauthorization = true`:

1. New Interconnection
2. Major Upgrade
3. Data Type Change
4. Security Architecture Change
5. Operating Environment Change
6. New Threat
7. Security Incident
8. Boundary Change
9. Key Personnel Change
10. Compliance Framework Change

Other change types are recorded but do not automatically trigger reauthorization.

### Reauthorization Workflow (§4.5)

The `compliance_reauthorization_workflow` tool detects three categories of triggers:

1. **Expiration**: ATO expires in < 30 days
2. **Significant changes**: Unreviewed changes flagged as requiring reauthorization
3. **Compliance drift**: Current score > 10% below authorization baseline

When `initiate=true`, the tool:
- Regresses the system's RMF step to **Assess**
- Marks significant changes as `reauthorization_triggered`
- Returns the previous and new RMF steps for audit trail

### Multi-System Dashboard (§4.6)

The `compliance_multi_system_dashboard` tool provides portfolio-level visibility:

| Column | Description |
|--------|-------------|
| System ID / Name / Acronym | System identification |
| Impact Level | Derived from FIPS 199 categorization |
| Current RMF Step | Lifecycle position (Prepare → Monitor) |
| Authorization Status | Authorized, Expired, Pending, None |
| Decision Type | ATO, ATO w/ Conditions, IATT, DATO |
| Expiration Date | Authorization end date |
| Compliance Score | Latest control effectiveness percentage |
| Open Findings | Count of Open + InProgress findings |
| Open POA&M Items | Count of Ongoing + Delayed POA&M items |
| Alert Count | Expiration alerts + unreviewed significant changes |

### Notification Delivery (§4.7)

The `compliance_send_notification` tool delivers:
- **Expiration alerts**: Based on graduated alert levels
- **Significant change notifications**: When changes require review
- **ConMon report notifications**: Report availability announcements

Current delivery channel: MCP response. Future channels: Teams proactive messages and VS Code information messages (requires M365 bot and extension integration).

## Authorization Decision Types

| Type | Abbreviation | Description | Expiration |
|------|-------------|-------------|------------|
| ATO | Authority to Operate | Full authorization | Typically 3 years |
| ATO w/ Conditions | ATOwC | Authorization with constraints | Varies |
| IATT | Interim ATO | Temporary authorization | Typically 90–180 days |
| DATO | Denial of ATO | System must not operate | N/A |

## Entity Relationships

```
RegisteredSystem (1) ──── (1) ConMonPlan
                    ├──── (*) ConMonReport
                    ├──── (*) SignificantChange
                    ├──── (*) AuthorizationDecision
                    ├──── (*) PoamItem
                    ├──── (*) ControlEffectiveness
                    ├──── (1) SecurityCategorization
                    └──── (1) ControlBaseline
```
