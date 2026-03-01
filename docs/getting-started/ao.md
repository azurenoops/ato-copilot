# Getting Started: Authorizing Official (AO)

> First-time setup and orientation for Authorizing Official users.

---

## Prerequisites

| Requirement | Details |
|------------|---------|
| **Access** | CAC enrolled with `Compliance.AuthorizingOfficial` role (typically provisioned by Administrator) |
| **Designation** | Designated as AO for one or more systems (O-6/GS-15+ per DoDI 8510.01) |
| **Tools** | Microsoft Teams (primary — Adaptive Cards for authorization decisions) |

## First-Time Setup

1. **Verify your role**

    ```
    "What role am I logged in as?"
    ```

    Expected result: `Compliance.AuthorizingOfficial`.

2. **View your portfolio**

    ```
    "Show the multi-system compliance dashboard"
    ```

    Expected result: Portfolio view of all systems under your authority with compliance scores, RMF phases, and ATO expiration dates.

3. **Review an authorization package**

    ```
    "Show the authorization package summary for system {id}"
    ```

    Expected result: Package summary including SSP status, SAR/RAR availability, compliance score, finding breakdown by CAT level, and residual risk assessment.

## Your First 3 Commands

### 1. View Portfolio Dashboard

> **"Show the multi-system compliance dashboard"**

Expected result: Table of all systems under your authority — system name, compliance score, RMF phase, ATO expiration date, and alert indicators.

### 2. Review Authorization Package

> **"Show the authorization package summary for system {id}"**

Expected result: Summary of the complete package — SSP completion, assessment results, risk analysis, open findings by CAT level, and whether the package is ready for decision.

### 3. Issue an Authorization Decision

> **"Issue an ATO for system {id} expiring January 15, 2028 with Low residual risk — all CAT I findings remediated, 2 CAT III findings accepted"**

Expected result: Authorization decision recorded (ATO, ATOwC, IATT, or DATO), system transitioned to Monitor phase, expiration tracking enabled, and risk acceptance entries created.

## Authorization Decision Types

| Type | Description | When to Use |
|------|-------------|-------------|
| **ATO** | Authority to Operate | All significant findings remediated |
| **ATOwC** | ATO with Conditions | Acceptable risk with stipulations |
| **IATT** | Interim Authority to Test | Limited scope testing authorization |
| **DATO** | Denial of Authorization | Unacceptable risk — system cannot operate |

## What's Next

- [Full AO Guide](../guides/ao-quick-reference.md) — Complete Authorize workflow and risk acceptance
- [RMF Phase Reference](../rmf-phases/index.md) — Phase-by-phase details
- [Quick Reference Card](../reference/quick-reference-cards.md) — Printable AO cheat sheet

## Common First-Day Issues

| Issue | Cause | Fix |
|-------|-------|-----|
| "Role not recognized" or empty portfolio | Administrator has not provisioned `Compliance.AuthorizingOfficial` role | Contact your Administrator to assign the role via identity management |
| "Package not ready for authorization" | SSP or SAR is incomplete — the system has not reached Authorize phase | Wait for the ISSM to advance the system to the Authorize phase |
| "Cannot issue DATO" without justification | All authorization decisions require justification text | Include your rationale in the natural language command |
