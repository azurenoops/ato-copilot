# Feature Specification: CAC Authentication & Privileged Identity Management

**Feature Branch**: `003-cac-auth-pim`  
**Created**: 2026-02-22  
**Status**: Draft  
**Input**: User description: "CAC Authentication and Privileged Identity Management (PIM) capabilities — integrating CAC/PIV smart card authentication as the gateway for all live Azure operations, and Azure AD PIM for just-in-time role elevation through natural language conversation."

## User Scenarios & Testing *(mandatory)*

### User Story 1 — CAC/PIV Authentication Gate (Priority: P1)

A user attempts an operation that touches live Azure resources (e.g., running a compliance assessment, executing a remediation, collecting evidence). The system detects that the operation requires CAC-authenticated access, checks whether the user has an active CAC session, and either proceeds (if authenticated) or prompts the user to authenticate via their CAC/PIV smart card before continuing. Once authenticated via MSAL against Azure AD (Azure Government endpoint), a session is established with a configurable timeout (default 8 hours). All Azure operations execute under the user's identity via On-Behalf-Of (OBO) flow — never under a service principal.

**Why this priority**: CAC authentication is the foundational security gate. Without it, no live Azure operation can execute. Every other feature in this spec depends on having a working authentication layer that enforces government-grade identity verification.

**Independent Test**: Trigger a live Azure operation (e.g., "Run compliance assessment on production") without an active CAC session. Verify the agent prompts for CAC authentication, the user authenticates via MSAL, a session is established with the correct timeout, and the original operation resumes successfully under the user's Azure AD identity.

**Acceptance Scenarios**:

1. **Given** a user with no active CAC session, **When** they request "Run compliance assessment on production," **Then** the agent responds "This operation requires CAC authentication. Please authenticate with your CAC/PIV card," triggers the MSAL authentication flow, and upon success resumes the assessment under the user's identity.
2. **Given** a user with an active CAC session (6 hours remaining), **When** they ask "Am I authenticated?", **Then** the agent responds with their identity, session expiration time, and any active PIM roles.
3. **Given** a user with an active CAC session, **When** they request a local operation (e.g., "Show NIST AC-2 control description"), **Then** the operation executes immediately without any authentication prompt.
4. **Given** a user with an expired CAC session, **When** they request an Azure operation, **Then** the agent informs them their session has expired and prompts for re-authentication without losing any in-progress work context.
5. **Given** a user with an active CAC session, **When** they say "Sign out" or "Lock my session," **Then** the session is terminated, the token is cleared, and the agent confirms the user can still use local features.

---

### User Story 2 — Two-Tier Access Model (Priority: P1)

The platform enforces a clear separation between operations that require CAC authentication and those that do not. Unauthenticated users can freely browse NIST control descriptions, view cached assessment results, generate local templates, manage Kanban board tasks (comments, views), and configure local preferences. Any operation that touches live Azure resources — assessments, remediations, evidence collection, resource discovery, policy changes, or PIM activations — requires an active CAC session.

**Why this priority**: The two-tier model ensures the platform is usable without a CAC for planning and knowledge work, while enforcing strict authentication for any operation with real-world impact. This is critical for both usability and security posture.

**Independent Test**: Without authenticating, successfully browse NIST controls, view a cached assessment, and add a comment to a Kanban task. Then attempt to run a live assessment and verify the system blocks the request with a clear authentication prompt.

**Acceptance Scenarios**:

1. **Given** an unauthenticated user, **When** they request "What are the AC-2 control requirements?", **Then** the Knowledge Base Agent returns the control description without requiring authentication.
2. **Given** an unauthenticated user, **When** they request "Show my remediation board," **Then** the system displays the cached board data without requiring authentication.
3. **Given** an unauthenticated user, **When** they request "Collect evidence from production," **Then** the agent responds that this operation requires CAC authentication and offers to initiate the authentication flow.
4. **Given** an unauthenticated user, **When** they request "Deploy this template to Azure," **Then** the agent blocks the operation and requests CAC authentication.

---

### User Story 3 — PIM Role Activation via Chat (Priority: P1)

A user who needs elevated Azure access (e.g., Contributor on a production subscription) requests activation through natural language. The agent identifies their eligible PIM roles, collects the required justification (minimum 20 characters) and optional ticket number (when RequireTicketNumber is enabled), confirms the scope and duration, and activates the role via the Azure AD PIM API. The activation is recorded in the audit database with full provenance including conversation ID, session ID, justification, and ticket reference (if provided).

**Why this priority**: PIM activation is the core value proposition of this feature — making just-in-time privilege elevation accessible through conversational AI. Without it, users must navigate the Azure Portal PIM UI manually, breaking the flow of compliance and remediation workflows.

**Independent Test**: As a Platform Engineer with an eligible Contributor role, say "I need Contributor access to production." Verify the agent identifies the eligible role, collects justification (and ticket number if RequireTicketNumber is enabled), activates the role with the requested duration, and confirms activation with the PIM request ID and expiration time.

**Acceptance Scenarios**:

1. **Given** a Platform Engineer eligible for Contributor on Production, **When** they say "I need Contributor access to production" and provide justification "Remediating AC-2.1 finding per assessment RUN-2026-0221" and ticket "SNOW-INC-4521," **Then** the role is activated for the default duration (4 hours), and the agent confirms with role, scope, expiration time, and PIM request ID.
2. **Given** a user with no eligible roles on the target scope, **When** they request a PIM activation, **Then** the agent responds that they are not eligible and lists any roles they are eligible for on other scopes.
3. **Given** a user requesting activation with a justification under 20 characters, **When** the agent validates the input, **Then** it rejects with a message stating the minimum length requirement.
4. **Given** a user providing a one-line request "Activate Contributor on production for 4 hours — ticket SNOW-4521, fixing AC-2.1," **Then** the agent parses all fields from the single message and activates immediately without additional prompts.

---

### User Story 4 — PIM Session Management (Priority: P1)

Users can view, extend, and deactivate their active PIM sessions through natural language commands. They can check which roles are currently active and their remaining durations, request extensions within policy limits, and proactively deactivate roles they no longer need to restore least-privilege posture. The agent also provides 15-minute expiration warnings.

**Why this priority**: Session management is essential for operational awareness and least-privilege enforcement. Without it, users have no visibility into their elevated access and cannot proactively clean up privileges, undermining the security model.

**Independent Test**: Activate a PIM role, then say "Show my active PIM roles." Verify the response lists the role, scope, and remaining duration. Then say "Deactivate my Contributor role" and verify the role is immediately deactivated.

**Acceptance Scenarios**:

1. **Given** a user with two active PIM roles (Contributor on Production, Reader on Staging), **When** they say "Show my active PIM roles," **Then** the agent lists both roles with scope, remaining duration, and activation time.
2. **Given** a user with Contributor active for 3 hours (5 hours remaining), **When** they say "Extend my Contributor role by 2 hours," **Then** the extension is applied if within the maximum policy duration, and the new expiration time is confirmed.
3. **Given** a user requesting an extension that would exceed the maximum policy duration, **When** the agent processes the request, **Then** it responds with the maximum allowed duration and current expiration time.
4. **Given** a user with an active role expiring in 15 minutes, **When** the timer triggers, **Then** the agent proactively notifies the user and offers to extend or deactivate.
5. **Given** a user saying "What roles am I eligible for?", **Then** the agent lists all eligible PIM roles across all subscriptions, indicating which are currently active and which require approval.

---

### User Story 5 — High-Privilege Role Approval Workflow (Priority: P2)

When a user requests activation of a high-privilege role (Owner, User Access Administrator, Security Administrator, Global Administrator, Privileged Role Administrator), the system requires approval from a Security Lead or Compliance Officer before activation. The agent submits the approval request, notifies the designated approvers, and tracks the request status. Approvers can approve or deny requests through natural language, and the requester is notified of the outcome.

**Why this priority**: High-privilege roles carry significant risk. An approval gate ensures oversight and accountability, mapping directly to NIST AC-6(1) (Authorize Access to Security Functions). This is essential for government compliance but not needed for the basic activation flow.

**Independent Test**: As a Platform Engineer, request Owner on production. Verify the system submits an approval request, notifies the Security Lead, and blocks activation until approval is received. As the Security Lead, approve the request and verify the requester is notified and the role activates.

**Acceptance Scenarios**:

1. **Given** a Platform Engineer requesting Owner on Production, **When** they provide justification and ticket, **Then** the agent displays a warning about the high-privilege nature, submits an approval request, notifies the Security Lead and Compliance Officer, and shows the request status as "Pending Approval."
2. **Given** a pending approval request, **When** a Security Lead says "Approve John's Owner request," **Then** the role activates, the requester is notified, and the approval is recorded with the approver's identity and timestamp.
3. **Given** a pending approval request, **When** a Security Lead says "Deny John's Owner request — insufficient justification," **Then** the request is denied, the requester is notified with the denial reason, and the denial is recorded.
4. **Given** a user with a Compliance Officer role, **When** they request a standard role (Reader, Contributor), **Then** no approval is required and the role activates immediately.
5. **Given** a pending request older than 30 minutes with no response, **When** the user checks status, **Then** the agent shows elapsed time and offers to re-notify approvers.

---

### User Story 6 — PIM-Integrated Compliance Workflows (Priority: P2)

When the Compliance Agent or Security Agent detects that a user lacks the required Azure RBAC role for a requested operation (assessment, remediation, evidence collection), the agent checks PIM eligibility and offers to activate the required role inline. After the operation completes, the agent offers to deactivate the role to restore least privilege. This creates a seamless flow where authentication and authorization are handled conversationally without breaking the user's workflow.

**Why this priority**: This integration makes PIM invisible to the user experience — privilege elevation happens naturally as part of compliance work rather than as a separate administrative task. It depends on both the authentication gate (US1) and basic PIM activation (US3) being in place.

**Independent Test**: As a Platform Engineer with no active Reader role but an eligible Reader via PIM, say "Run compliance assessment on production." Verify the agent detects the missing role, offers PIM activation, activates Reader with minimal friction, runs the assessment, and then offers to deactivate Reader afterward.

**Acceptance Scenarios**:

1. **Given** a user without Reader on Production but eligible via PIM, **When** they say "Run compliance assessment on production," **Then** the agent says "You don't have Reader access. You're eligible via PIM. Activate Reader for this assessment? (Duration: 1 hour)" and on confirmation activates and proceeds.
2. **Given** a user needing Contributor for remediation, **When** the remediation completes successfully, **Then** the agent offers "Remediation applied. Deactivate Contributor?" and on confirmation deactivates to restore least privilege.
3. **Given** a batch remediation of 5 Critical findings, **When** the agent estimates the work duration, **Then** it suggests a PIM duration matching the estimated time (e.g., 2 hours) and collects justification and ticket in a single exchange.

---

### User Story 7 — Just-in-Time VM Access (Priority: P2)

A user requests direct access (SSH or RDP) to a specific virtual machine. The agent creates a Just-in-Time (JIT) VM access request through Azure Defender for Cloud, specifying the port, source IP, and duration. The NSG rule is automatically opened for the requesting user's IP and automatically revoked when the duration expires. Users can view active JIT sessions and manually revoke access early.

**Why this priority**: JIT VM access is a distinct PIM capability that extends beyond role elevation to network-level access control. It enables secure troubleshooting workflows critical for remediation verification but is not needed for the core PIM role activation flow.

**Independent Test**: Say "I need SSH access to vm-web01" and verify the agent creates a JIT request, opens port 22 for the user's IP, confirms the access window, and after expiration the NSG rule is automatically removed.

**Acceptance Scenarios**:

1. **Given** a user requesting SSH access to vm-web01, **When** they provide justification "Troubleshooting failed remediation on AC-2.1, ticket SNOW-4521," **Then** JIT access is granted for the default duration (3 hours) on port 22 from the user's current IP, and the agent provides the connection command.
2. **Given** a user requesting RDP access to vm-admin01 on port 3389, **When** the request is processed, **Then** JIT access is granted and the agent confirms the port, IP, and duration.
3. **Given** a user with active JIT sessions, **When** they say "Show my active JIT sessions," **Then** the agent lists all VMs with active access, ports, and remaining time.
4. **Given** a user saying "Revoke JIT access to vm-web01," **Then** the NSG rule is immediately removed and the agent confirms revocation.

---

### User Story 8 — CAC Session Configuration (Priority: P2)

Users can query and configure their CAC session parameters. Session status is visible at all times (via a status bar or equivalent UI element). Users can set custom session timeouts (within policy limits), manually end sessions, and receive proactive warnings when sessions are about to expire. Mid-operation session expiration is handled gracefully — the system pauses, preserves state, prompts re-authentication, and resumes.

**Why this priority**: Session management improves usability and security awareness. It prevents surprise authentication failures during long operations and gives users control over their security posture. It builds on the authentication gate (US1) but adds management capabilities.

**Independent Test**: Authenticate via CAC, then say "Set my CAC timeout to 4 hours." Verify the timeout is updated. Wait for the session to approach expiration and verify the 15-minute warning appears (per SessionExpirationWarningMinutes default). After expiration, verify that the next Azure operation prompts for re-authentication.

**Acceptance Scenarios**:

1. **Given** an authenticated user, **When** they say "Set my CAC timeout to 4 hours," **Then** the session timeout is updated and the agent confirms the new expiration time.
2. **Given** a session with 15 minutes remaining, **When** the warning threshold is reached, **Then** the agent proactively notifies "Your CAC session expires in 15 minutes" and offers extension or sign-out.
3. **Given** a user performing a long-running remediation, **When** their CAC session expires mid-operation, **Then** the system pauses the operation, preserves state, prompts re-authentication, and resumes from where it left off after re-auth.
4. **Given** an authenticated user, **When** they say "Lock my session," **Then** the session is ended, the token is cleared, and the status updates to show unauthenticated state.

---

### User Story 9 — Certificate-to-Role Mapping (Priority: P3)

The Configuration Agent allows mapping CAC certificate identities (thumbprint or subject) to platform roles. Once mapped, future logins from that certificate automatically resolve to the assigned role. If no explicit mapping exists, the system falls back to Azure AD group membership, then Azure RBAC on the target subscription, and finally to the default least-privilege role (Platform Engineer).

**Why this priority**: Certificate mapping streamlines the login experience for repeat users and enables role auto-detection. It's an optimization over the baseline role resolution (which works via Azure AD groups) and is not required for core functionality.

**Independent Test**: Map a CAC certificate to the Compliance Officer role via "Map my CAC to Compliance Officer role." Log out and re-authenticate. Verify the system automatically resolves to Compliance Officer without additional prompts.

**Acceptance Scenarios**:

1. **Given** an authenticated user, **When** they say "Map my CAC to Compliance Officer role," **Then** the certificate thumbprint is stored with the Compliance Officer role mapping and the agent confirms.
2. **Given** a user with no explicit certificate mapping but Azure AD group membership in "Compliance-Officers," **When** they authenticate, **Then** the system resolves their role from group membership.
3. **Given** a user with no explicit mapping and no matching Azure AD group, **When** they authenticate, **Then** the system checks Azure RBAC on the target subscription and falls back to Platform Engineer if no match is found.
4. **Given** a user with an explicit mapping, **When** the mapping is removed and they re-authenticate, **Then** the system falls back to the next resolution tier.

---

### User Story 10 — PIM Audit Trail and Compliance Evidence (Priority: P3)

Every PIM action (activation, deactivation, approval, denial, extension, expiration) is recorded in the JitRequestEntity database with full provenance: user identity, conversation ID, session ID, role, scope, justification, ticket number, timestamps, and status transitions. The Compliance Agent can query PIM history for compliance evidence generation, linking PIM actions to NIST 800-53 controls (AC-2, AC-6, AU-2, AU-3).

**Why this priority**: Audit trail is essential for ATO compliance but is an output of the system operating correctly. It depends on PIM activation (US3) and session management (US4) being implemented and generating events to record.

**Independent Test**: Perform a full PIM lifecycle (activate, use for a remediation, deactivate), then query "Show all PIM activations for the last 7 days." Verify the complete history is returned with all tracked fields. Then run "Generate evidence for AC-2" and verify PIM logs are included.

**Acceptance Scenarios**:

1. **Given** a completed PIM activation lifecycle, **When** the Compliance Agent queries "Show PIM history for last 7 days," **Then** the response includes all activations/deactivations with timestamps, durations, justifications, ticket numbers, and status.
2. **Given** PIM activity on Access Control-related subscriptions, **When** the agent runs "Generate evidence for AC-2 (Account Management)," **Then** the evidence bundle includes PIM activation/deactivation logs demonstrating least-privilege enforcement.
3. **Given** an auditor requesting "Who had Owner access in the last 30 days?", **Then** the system lists all Owner activations with user identity, justification, duration, and approver.
4. **Given** PIM sessions that exceeded the configured SLA, **When** an auditor asks "Are there any PIM sessions that exceeded SLA?", **Then** the system identifies and lists those sessions.

---

### User Story 11 — PIM Notifications (Priority: P3)

PIM events generate notifications through the existing notification infrastructure (email, Teams, Slack). Standard role activations notify the Security Lead. High-privilege activations escalate to both the Security Lead and Compliance Officer with warning severity. Expiration warnings are sent to the user 15 minutes before session end. Approval requests are sent to designated approvers. All notification events are configurable per-user.

**Why this priority**: Notifications are a delivery mechanism for awareness, not a core capability. All PIM operations function without notifications; notifications add proactive visibility. They depend on the notification infrastructure from Feature 002 and PIM activation from US3.

**Independent Test**: Activate a high-privilege role and verify the Security Lead and Compliance Officer receive escalation notifications. Verify the user receives a 15-minute expiration warning.

**Acceptance Scenarios**:

1. **Given** a standard role activation (Reader), **When** the activation completes, **Then** the Security Lead receives an informational notification with user, time, and scope details.
2. **Given** a high-privilege role activation (Owner), **When** the activation completes, **Then** the Security Lead and Compliance Officer both receive warning-level notifications.
3. **Given** a PIM session with 15 minutes remaining, **When** the warning threshold is reached, **Then** the user receives a notification offering to extend or deactivate.
4. **Given** a pending approval request, **When** the request is submitted, **Then** designated approvers receive a notification with approve/deny actions.

---

### User Story 12 — Multi-Client CAC Support (Priority: P3)

CAC authentication works consistently across all client surfaces: VS Code (GitHub Copilot extension), Microsoft Teams (M365 extension), web chat UI, and direct stdio/CLI. Each surface uses the appropriate authentication mechanism (VS Code secure storage, Teams SSO, browser MSAL redirect, environment variable or CLI flag) while maintaining the same security guarantees. The CAC session state is visible in each client's native UI pattern.

**Why this priority**: Multi-client support extends the reach of the authentication system but is not required for the core server-side authentication and PIM logic. The MCP server validates JWT tokens identically regardless of which client acquired them. Client-specific integration is an incremental enhancement.

**Independent Test**: Authenticate via CAC in the web chat UI and verify the session status is visible. Then authenticate via the VS Code extension and verify the same session semantics apply (timeout, status display, re-authentication prompt on expiry).

**Acceptance Scenarios**:

1. **Given** a VS Code user, **When** an Azure operation is triggered, **Then** a notification prompts "CAC required — click to authenticate," MSAL authenticates via system browser, and the token is cached in VS Code secure storage.
2. **Given** a Teams user with active Teams SSO + certificate-based auth, **When** their CAC session expires, **Then** an Adaptive Card appears with a "Re-authenticate" button.
3. **Given** a CLI user, **When** they set `PLATFORM_COPILOT_TOKEN=<jwt>`, **Then** the MCP server validates the token and establishes the session.
4. **Given** any client surface, **When** the user is authenticated, **Then** the session status is visible in the client's native UI pattern (status bar, header, badge).

---

### Edge Cases

- What happens when a CAC/PIV card is removed mid-session? — The session remains valid until timeout; removing the card does not invalidate the token. The system relies on token expiry, not physical card presence.
- What happens when two PIM activations for the same role on the same scope are requested simultaneously? — The second request detects the active session and offers to extend rather than create a duplicate.
- What happens when Azure AD PIM API is unavailable? — The agent reports the service outage, suggests retrying, and offers to queue the request for manual processing.
- What happens when a JIT VM access request targets a VM that does not exist? — The agent returns a clear error indicating the VM was not found and suggests checking the resource name.
- What happens when a user's eligible PIM role is removed in Azure AD while they have an active session? — The active session continues until expiration; the role will not be available for future activations.
- What happens when multiple approvers receive the same approval request? — The first approval or denial takes effect; subsequent responses are acknowledged but have no additional effect.
- What happens when a user requests PIM activation without a CAC session? — PIM activation requires Tier 2 access; the agent prompts for CAC authentication first.

## Requirements *(mandatory)*

### Functional Requirements

#### CAC/PIV Authentication

- **FR-001**: System MUST classify every agent operation into one of three tiers: Tier 1 (no authentication required — local/cached operations), Tier 2a (CAC authentication + read-eligible PIM role required — assessments, evidence collection, resource discovery), or Tier 2b (CAC authentication + write-eligible PIM role required — remediations, policy modifications, resource changes). Each tool MUST declare its required tier via a dedicated property. The MCP Server MUST enforce CAC session validity and PIM elevation status server-side before executing any tool marked as Tier 2a or 2b.
- **FR-002**: System MUST validate JWT tokens by checking audience, issuer, and CAC/MFA claims (the `amr` claim must contain both "mfa" and "rsa").
- **FR-003**: System MUST support the MSAL On-Behalf-Of (OBO) flow to exchange the user's JWT for an ARM-scoped token, ensuring all Azure operations execute under the user's identity.
- **FR-004**: System MUST establish a CAC session upon successful authentication with a configurable timeout (default: 8 hours, maximum: 24 hours). When an unauthenticated user triggers a tool requiring authentication, the system MUST prompt for CAC authentication and PIM elevation with a clear, actionable message indicating which step is missing (CAC, PIM, or both).
- **FR-005**: System MUST prompt for re-authentication when a session expires, preserving any in-progress operation context so the operation can resume after re-authentication.
- **FR-006**: System MUST allow users to manually end their session via natural language commands ("Sign out," "Lock my session").
- **FR-007**: System MUST expose session status information (authenticated identity, remaining time, active PIM roles) via a query command ("Am I authenticated?").
- **FR-008**: System MUST use the Azure Government endpoint (`login.microsoftonline.us`) for all MSAL authentication flows.

#### PIM Role Activation

- **FR-009**: System MUST query Azure AD PIM to determine the user's eligible role assignments for a given scope (subscription, resource group, or resource).
- **FR-010**: System MUST collect justification (minimum 20 characters) and duration (within policy limits) before activating a PIM role. PIM elevations MUST have a configurable timeout (default: 4 hours, maximum: 8 hours per Azure AD PIM policy). When `RequireTicketNumber` is enabled, the system MUST also collect a ticket number from an approved ticketing system. When disabled, ticket number is optional and ticket validation is skipped.
- **FR-011**: System MUST support single-message "quick activation" by parsing justification, ticket, duration, and scope from one natural language input.
- **FR-012**: System MUST activate PIM roles via the Azure AD PIM API and return a confirmation with role name, scope, expiration time, and PIM request ID.
- **FR-013**: System MUST classify roles as "standard" or "high-privilege" based on a configurable list and enforce additional confirmation and approver notification for high-privilege activations. Write operations (remediations, policy modifications) MUST require a PIM elevation with a write-eligible role (Contributor or higher) distinct from read-only operations (assessments) which require only a read-eligible role (Reader). The system MUST distinguish between read-eligible and write-eligible PIM tiers when evaluating authorization.
- **FR-014**: System MUST allow users to view all currently active PIM roles with remaining duration, extend active roles within policy limits, and deactivate roles on demand.
- **FR-015**: System MUST provide a 15-minute expiration warning for active PIM sessions and offer extension or deactivation.

#### PIM Approval Workflow

- **FR-016**: System MUST submit an approval request for high-privilege role activations, notify designated approvers (Security Lead, Compliance Officer), and track request status (Submitted, Approved, Denied).
- **FR-017**: System MUST allow approvers to approve or deny PIM requests via natural language commands, recording the approver's identity, timestamp, and any comments.
- **FR-018**: System MUST notify the requester when their approval request is approved or denied, including the reason for denial when applicable.

#### PIM-Integrated Workflows

- **FR-019**: System MUST detect when a user lacks the required Azure RBAC role for a requested compliance operation, check PIM eligibility, and offer inline activation before proceeding. PIM elevation is enforced on-demand — the system checks eligibility and prompts activation when a Tier 2a or 2b operation requires RBAC above the user's baseline, rather than requiring a standing PIM elevation for all authenticated operations. The system MUST check PIM role eligibility before prompting for activation; if a user is not eligible for the required PIM role, the system MUST return a descriptive message indicating the required role and how to request eligibility.
- **FR-020**: System MUST offer to deactivate PIM roles after the triggering operation completes, enabling automatic least-privilege restoration.
- **FR-021**: System MUST support an "auto-deactivate after remediation" configuration option that automatically deactivates the PIM role used for a remediation upon successful completion.

#### Just-in-Time VM Access

- **FR-022**: System MUST create JIT VM access requests via the Azure Defender for Cloud API, specifying port, source IP (auto-detected or user-provided), and duration (default: 3 hours, maximum: 24 hours).
- **FR-023**: System MUST allow users to view active JIT sessions and manually revoke access, immediately closing the NSG rule.
- **FR-024**: System MUST support SSH (port 22) and RDP (port 3389) access requests, with support for custom ports.

#### CAC Session Configuration

- **FR-025**: System MUST allow users to configure their CAC session timeout within policy limits via the Configuration Agent.
- **FR-026**: System MUST handle mid-operation session expiration by pausing the operation, preserving state, prompting re-authentication, and resuming after re-auth.

#### Certificate Mapping

- **FR-027**: System MUST support mapping CAC certificate identities (thumbprint or subject) to platform roles via the Configuration Agent.
- **FR-028**: System MUST resolve user roles in the following order: (1) explicit certificate mapping, (2) Azure AD group membership, (3) Azure RBAC on target subscription, (4) default Platform Engineer role.

#### Audit and Compliance

- **FR-029**: System MUST record every PIM action (activation, deactivation, approval, denial, extension, expiration) in the JitRequestEntity with user identity, conversation ID, session ID, role, scope, justification, ticket number, timestamps, and status.
- **FR-030**: System MUST support querying PIM history by time range, user, role, and scope for compliance evidence generation.
- **FR-031**: System MUST map PIM activities to NIST 800-53 controls (AC-2, AC-6, AC-6(1), AC-6(2), AC-6(5), AC-2(2), AC-2(3), AU-2, AU-3) for evidence generation.

#### Notifications

- **FR-032**: System MUST generate notifications for PIM events (activation, high-privilege escalation, expiration warning, approval request) through the existing notification infrastructure (email, Teams, Slack).
- **FR-033**: System MUST escalate high-privilege role activations to both the Security Lead and Compliance Officer with warning-level severity.

#### Error Handling

- **FR-034**: System MUST provide clear, actionable error messages for all authentication and PIM failure modes: CAC not detected, certificate expired, MFA claim missing, token expired, not eligible, approval required, maximum sessions reached, ticket required, justification too short, and duration exceeds policy. Unauthorized actions MUST return a descriptive message stating the required role, required PIM tier (read or write), and the user's current roles and elevation status.
- **FR-035**: System MUST never leave Azure resources in an inconsistent state when a PIM session expires mid-operation — operations must stop safely and log partial progress. Failed remediations MUST stop immediately, describe the failure in plain language with troubleshooting suggestions and retry options, offer rollback guidance, and be audit-logged. Raw exceptions MUST NOT be shown to users.

#### Development & Deployment

- **FR-036**: System MUST support a development bypass mode (configurable via `RequireCac: false` and `RequirePim: false` in application settings) for local testing that disables both CAC and PIM enforcement while maintaining the authentication flow structure and enforcement points in code.
- **FR-037**: CAC certificate details, PIM elevation tokens, and Azure credentials MUST NOT be cached in persistent storage, exposed in logs, error messages, or chat responses. Token hashes may be stored for session validation but plaintext tokens must never be persisted.
- **FR-038**: In production environments, all secrets, connection strings, API keys, and Azure credentials MUST be stored in Azure Key Vault. The application MUST authenticate to Key Vault using managed identity (no credentials in application configuration or environment variables). Key Vault MUST be FIPS 140-2 Level 2 validated per IL5/IL6 requirements. For local development, environment variables via `.env` files are permitted as a convenience fallback.

#### Role-Based Access Control

- **FR-039**: System MUST support six user roles: Administrator (Compliance.Administrator), Auditor (Compliance.Auditor), Analyst (Compliance.Analyst), Viewer (Compliance.Viewer), Security Lead (Compliance.SecurityLead), and Platform Engineer (Compliance.PlatformEngineer). A user MAY hold multiple roles simultaneously; when multiple roles are assigned, the user receives the union of all role permissions (highest privilege across all assigned roles applies). Platform Engineer is the default fallback role for CAC-authenticated users with no explicit mapping (FR-028).
- **FR-040**: Role MUST be derived from CAC identity, directory group membership, and active PIM role assignments. The system MUST store the CAC certificate mapping and track PIM elevation state.
- **FR-041**: Role determines which tools a user can execute, not which agents or tools are visible. All users MUST see all agents. PIM elevation tier (read vs. write) further constrains which operations are permitted within a role.

#### Data Retention

- **FR-042**: Assessment results, evidence packages, and compliance documents MUST be retained for a minimum of 3 years from creation date.
- **FR-043**: Audit log entries MUST be retained for a minimum of 7 years from creation date and MUST be immutable (append-only; no modification or deletion permitted).
- **FR-044**: System MUST support configurable retention policies per data category. Retention defaults (3 years for assessments, 7 years for audit logs) MUST apply unless overridden by organizational policy.

#### Observability

- **FR-045**: System MUST expose a health check endpoint (`/health`) that reports overall system status and per-agent availability (healthy, degraded, unavailable).
- **FR-046**: System MUST emit structured metrics for each agent and tool invocation including: request latency (p50, p95, p99), error rate, throughput (requests per minute), and active session count.
- **FR-047**: System MUST propagate a correlation ID across all agent calls within a single user request, enabling distributed tracing from Orchestrator routing through agent execution to tool invocation and Azure API calls.
- **FR-048**: All structured logs MUST include the correlation ID, agent name, tool name, user identity (redacted as needed), and timestamp. Logs MUST follow the structured logging format defined in the constitution (Principle V).

### Key Entities

- **CacSession**: Represents an active CAC authentication session — user identity (Azure AD object ID, display name, email), token hash (for validation, not stored plaintext), session start time, expiration time, client type (VSCode, Teams, Web, CLI), IP address, and status (Active, Expired, Terminated).
- **JitRequestEntity**: Represents a PIM operation — request type (PimRoleActivation, PimGroupMembership, JitVmAccess), PIM request ID, user identity, conversation ID, session ID, role name, scope (subscription/resource group/resource), justification, ticket number, ticket system, requested duration, actual duration, status (Submitted, PendingApproval, Approved, Denied, Active, Expired, Deactivated, Failed), approver identity, approval timestamp, approval comments, and all lifecycle timestamps (requested, approved, activated, deactivated, expired).
- **CertificateRoleMapping**: Maps a CAC certificate identity to a platform role — certificate thumbprint, certificate subject, mapped role, created by, created at, and active status.
- **PimServiceOptions**: Stores PIM policy settings per scope — default activation duration (4 hours), maximum activation duration (8 hours), require ticket number flag, approved ticket systems, minimum justification length, high-privilege role list, notification preferences, and auto-deactivate settings. Loaded from `appsettings.json` `Pim` section via `IOptions<PimServiceOptions>`.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Users can authenticate via CAC/PIV and begin Azure operations within 30 seconds of card insertion (excluding smart card PIN entry time).
- **SC-002**: PIM role activation completes within 60 seconds from the user's final confirmation (excluding Azure AD propagation time).
- **SC-003**: 95% of PIM activations require no more than 3 conversational turns (request, provide justification/ticket, confirmation).
- **SC-004**: "Quick activation" single-message requests succeed on the first attempt for 90% of well-formed inputs.
- **SC-005**: Users can view, extend, and deactivate PIM sessions through natural language with 100% functional parity to the Azure Portal PIM UI.
- **SC-006**: High-privilege approval requests reach designated approvers within 60 seconds of submission.
- **SC-007**: JIT VM access NSG rules are created within 30 seconds of user confirmation and automatically removed at expiration with zero manual intervention.
- **SC-008**: 100% of PIM actions are recorded in the audit database with all required provenance fields (user, role, scope, justification, ticket, timestamps).
- **SC-009**: Mid-operation CAC expiration results in zero data loss — the operation pauses, re-authentication succeeds, and the operation resumes to completion.
- **SC-010**: Users can distinguish between Tier 1 and Tier 2 operations through clear system messaging, with zero false authentication prompts for Tier 1 operations.
- **SC-011**: PIM audit logs integrate with NIST 800-53 evidence generation, covering controls AC-2, AC-6, and AU-2/AU-3 without manual data entry.
- **SC-012**: Expired PIM sessions are automatically cleaned up and the user's access reverts to their baseline eligible roles with no lingering elevated access.
- **SC-013**: 100% of tools that interact with live Azure resources enforce both CAC authentication and PIM elevation (read or write tier as appropriate) server-side; no unauthenticated or un-elevated Azure operations can succeed.
- **SC-014**: All four user roles (Compliance Officer, Platform Engineer, Security Lead, Auditor) can perform their permitted operations and are denied unauthorized operations with descriptive error messages stating the required role and PIM tier, 100% of the time.
- **SC-015**: The `/health` endpoint returns agent-level status within 2 seconds, and all agent invocations emit structured metrics with correlation IDs that can be traced end-to-end from request through tool execution.
- **SC-016**: All agent actions produce audit log entries with complete identity, timestamp, resource, and outcome data — verified by audit log query.

## Clarifications

### Session 2026-02-22

- Q: Should PIM elevation be mandatory for ALL Tier 2 operations, or on-demand when RBAC is insufficient? → A: On-demand PIM with enforcement — PIM is activated inline when the operation requires Azure RBAC above the user's baseline, not redundantly for every authenticated call.
- Q: What should PIM activation timeout defaults be? Current spec has 8h default, 24h max. FR-012 specifies 4h/8h. → A: Adopt FR-012 values — PIM default 4 hours, max 8 hours (stricter, IL5/IL6 appropriate).
- Q: How should read-eligible vs write-eligible PIM tiers be implemented? → A: Sub-tier model — Tier 2 splits into Tier 2a (read ops, Reader PIM sufficient) and Tier 2b (write ops, Contributor+ PIM required). Tools declare which sub-tier they need.

## Assumptions

- Azure AD (Entra ID) is configured with Privileged Identity Management (P2 license) and eligible role assignments are pre-configured for users by an Azure AD administrator outside the scope of this platform.
- CAC/PIV smart card readers and middleware (e.g., ActivClient, OpenSC) are pre-installed on user workstations. Certificate chain validation is handled by the operating system and Azure AD, not by the platform.
- The Azure Government cloud endpoint (`login.microsoftonline.us`) is used for all authentication. Commercial cloud (`login.microsoftonline.com`) is not supported in the initial implementation.
- Azure AD Conditional Access policies (requiring CAC/PIV, compliant device, approved locations) are configured in Azure AD by the organization's identity administrators. The platform validates the resulting JWT claims but does not manage Conditional Access policy configuration.
- Ticket number collection is optional and controlled by the `RequireTicketNumber` configuration flag (default: false). When enabled, the supported ticketing systems (ServiceNow, Jira, Remedy, AzureDevOps) are validated by format/prefix only. The platform does not integrate with these systems to verify ticket existence or status. Organizations that do not use a ticketing system can leave this disabled.
- The notification infrastructure from Feature 002 (email via MailKit, Teams via Adaptive Card webhook, Slack via Block Kit webhook) is available and operational.
- JIT VM access requires Azure Defender for Cloud (Standard tier) to be enabled on the target subscription for the Defender Just-in-Time API to be available.
- Session timeout defaults (8 hours for CAC, 4 hours for PIM with maximum 8 hours) align with the organization's security policy. These are configurable but policy enforcement (e.g., absolute maximum durations) is defined in Azure AD PIM settings, not in the platform.
- Azure Key Vault is provisioned in the target Azure Government environment with managed identity access configured for the application's service principal. FIPS 140-2 Level 2 validation is enabled.
- PIM role definitions for the platform are pre-configured in Azure AD with appropriate eligibility assignments for each user persona. Read-eligible and write-eligible tiers are defined.

## Dependencies

- **Feature 001 (Core Compliance)**: CAC authentication gates the existing compliance operations (assessment, remediation, evidence collection). The two-tier model must integrate with the existing tool routing in ComplianceAgent.
- **Feature 002 (Remediation Kanban)**: Notification infrastructure (NotificationService, OverdueScanHostedService) is reused for PIM event notifications.
- **Azure AD PIM API**: Microsoft Graph API endpoints for PIM role management (`/roleManagement/directory/roleEligibilityScheduleRequests`, `/roleManagement/directory/roleAssignmentScheduleRequests`).
- **Azure Defender for Cloud API**: JIT VM access API (`/jitNetworkAccessPolicies`).
- **MSAL.NET**: `Microsoft.Identity.Client` and `Microsoft.Identity.Web` for OBO flow and token management.

## Scope Boundaries

**In scope**:
- Server-side JWT validation and CAC claim enforcement
- PIM role activation/deactivation/extension via Azure AD PIM API
- PIM approval workflow for high-privilege roles
- JIT VM access via Azure Defender for Cloud API
- Audit trail recording in local database
- PIM-integrated compliance workflow hooks
- Certificate-to-role mapping storage and resolution
- Session management (timeout, status, manual termination)

**Out of scope**:
- Azure AD Conditional Access policy configuration (managed by identity admins)
- CAC reader hardware/middleware installation or troubleshooting
- Azure AD PIM eligible role assignment management (creating/removing eligibility)
- Cross-tenant PIM support (single tenant only in initial implementation)
- Smart card PIN management or certificate renewal workflows
- OCSP/CRL certificate revocation checking (delegated to Azure AD)
- Client-side UI implementation details for each surface (VS Code extension, Teams bot, web chat) — the spec covers the server-side behavior and contract that clients consume
