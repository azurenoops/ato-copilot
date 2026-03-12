# Feature Specification: CAC Simulation Mode

**Feature Branch**: `027-cac-simulation-mode`
**Created**: 2026-03-12
**Status**: Draft
**Input**: User description: "Simulation Mode Design — Implement a middleware-based simulation mode that synthesizes a configurable test identity as a ClaimsPrincipal from appsettings.json / environment variables. Simulation mode is activated by configuration (CacAuth:SimulationMode = true)."

## Clarifications

### Session 2026-03-12

- Q: Should simulated CAC sessions be explicitly marked in the database and audit logs to prevent contamination of compliance evidence? → A: Yes — mark with a distinct flag/client type and auto-exclude from compliance evidence generation.
- Q: Should simulation mode bypass JWT/MSAL token validation in the authentication middleware, or require tests to mock tokens separately? → A: Simulation mode bypasses JWT validation; the simulated identity is the sole authentication source in Development environments.
- Q: Should the simulated service support multiple identities switchable at runtime, or a single identity per startup? → A: Single identity per startup. Integration tests use DI overrides per test fixture to swap identities (logout/login as a new persona) without restarting the app. Local development requires a config change and restart to switch identities.
- Q: Should the simulated service emit structured telemetry events matching the real service, or only debug-level logs? → A: Both — debug-level logs for developer troubleshooting plus the same structured telemetry events as the real service, ensuring observability code paths are exercised during simulation.
- Q: Should the ISmartCardService interface live in Core (alongside ICacSessionService, IPimService) or in Agents alongside implementations? → A: *(Superseded)* Infrastructure analysis revealed that no smart card abstraction exists or is needed in the codebase. The authentication middleware already serves as the abstraction layer. Simulation is implemented as a middleware branch, not a separate service interface.

## User Scenarios & Testing *(mandatory)*

### User Story 1 — Simulated Identity for Local Development (Priority: P1)

A developer working on CAC-gated features without access to a physical smart card enables simulation mode through configuration. The system provides a simulated identity (user principal name, display name, certificate thumbprint, and roles) sourced from a configuration file or environment variables. The authentication middleware injects the simulated identity as a `ClaimsPrincipal`, so all downstream services receive it transparently and the developer can exercise CAC-protected workflows end-to-end on their local machine.

**Why this priority**: This is the core value of the feature. Without it, developers cannot test any CAC-authenticated workflow locally, blocking day-to-day development on all features that depend on Feature 003 (CAC Authentication & PIM).

**Independent Test**: Set `CacAuth:SimulationMode = true` and populate `CacAuth:SimulatedIdentity` in configuration. Start the application and invoke a CAC-protected operation. Verify the simulated identity is used, the operation succeeds, and no physical smart card prompt appears.

**Acceptance Scenarios**:

1. **Given** simulation mode is enabled and a simulated identity is configured, **When** the application starts, **Then** the middleware injects the simulated identity as a `ClaimsPrincipal` without any hardware interaction or JWT validation.
2. **Given** simulation mode is enabled, **When** a developer triggers a CAC-gated operation (e.g., "Run compliance assessment"), **Then** the operation executes using the simulated identity and completes successfully.
3. **Given** simulation mode is disabled (default), **When** the application starts, **Then** the real JWT-based authentication is used and no simulated identity is injected.
4. **Given** simulation mode is enabled but no simulated identity is configured, **When** the application starts, **Then** the system fails fast with a clear error message indicating the missing configuration.

---

### User Story 2 — Configurable Test Identity (Priority: P1)

A developer or QA tester configures the simulated identity to match specific test scenarios — different user principal names, display names, certificate thumbprints, and role assignments. The identity is read from the standard configuration system (configuration files or environment variables), enabling different identities for different test runs without code changes.

**Why this priority**: Configurable identity is essential for testing role-based access control, persona-specific workflows, and edge cases (e.g., users with no roles, users with high-privilege roles). Without it, simulation mode would only support a single hardcoded identity.

**Independent Test**: Configure a simulated identity with "Security Lead" roles. Invoke a high-privilege operation. Verify it succeeds. Change the simulated roles to "Platform Engineer" and restart. Verify the high-privilege operation is now blocked by authorization.

**Acceptance Scenarios**:

1. **Given** a simulated identity with `SimulatedRoles: ["Global Reader", "SharePoint Administrator"]`, **When** the system resolves the user's roles, **Then** the returned roles match the configured values exactly.
2. **Given** a simulated identity configured via environment variables (e.g., `CacAuth__SimulatedIdentity__UserPrincipalName`), **When** the application starts, **Then** the environment variable values override any values from configuration files.
3. **Given** a simulated identity with a specific certificate thumbprint, **When** a service checks the user's certificate, **Then** the configured thumbprint is returned.
4. **Given** a simulated identity with an empty roles array, **When** the system resolves roles, **Then** the user is treated as having no assigned roles and falls back to the default least-privilege behavior.

---

### User Story 3 — Automated Test Execution Without Hardware (Priority: P1)

Integration and unit tests that exercise CAC-dependent code paths run in CI/CD pipelines where no physical smart card reader is available. The test harness uses simulation mode configuration to inject deterministic identities via the middleware, ensuring tests are repeatable and environment-independent.

**Why this priority**: CI/CD pipelines cannot use physical smart cards. Without simulation mode, all CAC-dependent tests must either be skipped or require complex hardware emulation, creating a gap in test coverage for core authentication workflows.

**Independent Test**: Run the integration test suite with simulation mode enabled. Verify all CAC-dependent tests pass. Verify no test attempts to interact with smart card hardware.

**Acceptance Scenarios**:

1. **Given** a test project configured with simulation mode enabled, **When** the test suite runs, **Then** all CAC-dependent tests execute using the simulated identity and produce deterministic results.
2. **Given** a test that requires a specific role (e.g., Compliance Officer), **When** the test configures the simulated identity with that role before execution, **Then** the test passes without requiring a real CAC session.
3. **Given** a test suite that exercises multiple personas (Compliance Officer, Platform Engineer, Security Lead), **When** each test fixture registers a different simulated identity via `IOptions<CacAuthOptions>` override, **Then** each test runs under the correct persona without requiring an application restart.
4. **Given** a CI/CD pipeline with no smart card hardware, **When** the full test suite runs, **Then** zero tests fail due to missing smart card hardware.

---

### User Story 4 — Production Safety Guard (Priority: P2)

The system prevents simulation mode from being activated in production environments. Even if the configuration flag is mistakenly set, the system detects the non-development environment and refuses to use the simulated identity, logging a security warning.

**Why this priority**: Simulation mode bypasses real authentication. Accidental activation in production would be a critical security vulnerability. This guard is essential for safe deployment but is secondary to the core simulation capability.

**Independent Test**: Set `CacAuth:SimulationMode = true` in a production environment configuration. Start the application. Verify the system ignores the simulation flag, logs a security warning, and uses real JWT-based authentication.

**Acceptance Scenarios**:

1. **Given** simulation mode is enabled in a production environment, **When** the application starts, **Then** simulation mode is ignored, a security-level warning is logged, and the real JWT-based authentication is used.
2. **Given** simulation mode is enabled in a staging environment, **When** the application starts, **Then** simulation mode is ignored and a warning is logged (same behavior as production).
3. **Given** simulation mode is enabled in a development environment, **When** the application starts, **Then** simulation mode activates normally.
4. **Given** simulation mode is enabled and the environment is set to "Development", **When** the application starts, **Then** a startup log message clearly indicates that simulation mode is active and identifies the simulated user.

---

### User Story 5 — Session Lifecycle Testing (Priority: P3)

*(Descoped)* Card removal detection and certificate expiration simulation were originally designed around a `SimulatedSmartCardService` with hardware-emulation methods (`IsCardPresentAsync`, `ValidateCertificateAsync`). Under the simplified middleware approach, these physical hardware concerns are not applicable — the middleware synthesizes a `ClaimsPrincipal` once per request and does not model a persistent card connection. Session lifecycle (timeouts, expiration) is already handled by the existing `ICacSessionService` and `CacAuthOptions.DefaultSessionTimeoutHours` / `MaxSessionTimeoutHours` settings, which work identically for simulated and real sessions.

**If session lifecycle edge-case testing is needed in the future**, it can be implemented by extending `SimulatedIdentityOptions` with an optional `SessionTimeoutOverrideMinutes` property.

---

### Edge Cases

- What happens when `SimulationMode` is `true` but the `SimulatedIdentity` section is entirely missing? The system must fail fast with a descriptive error at startup, not at the point of first use.
- What happens when `SimulatedIdentity` contains an empty `UserPrincipalName`? The system must reject the configuration at startup with a validation error.
- What happens when `SimulatedRoles` is null versus an empty array? Null should default to an empty array (no roles assigned); an empty array is a valid configuration meaning the user has no elevated roles.
- What happens when environment variables partially override the simulated identity (e.g., only `UserPrincipalName` is set via env var)? The system should merge configuration sources using standard configuration precedence — environment variables override file values for the specific keys set, while file values remain for unset keys.
- What happens when the application transitions from simulation mode to real mode (configuration change without restart)? The system should require a restart; hot-swapping authentication modes at runtime is not supported.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST extend `CacAuthOptions` with a `SimulationMode` flag and a nested `SimulatedIdentityOptions` object that defines the simulated user identity (user principal name, display name, certificate thumbprint, roles).
- **FR-002**: The authentication middleware MUST bypass JWT/MSAL token validation and synthesize a `ClaimsPrincipal` from the configured `SimulatedIdentityOptions` when simulation mode is active, making the simulated identity the sole authentication source. No physical smart card hardware interaction is required, eliminating the need for a separate token mock in development and test environments.
- **FR-003**: Simulation mode MUST be activated exclusively through configuration (`CacAuth:SimulationMode = true`); there must be no code-level toggle or compile-time switch.
- **FR-004**: The simulated identity MUST be configurable with at minimum: user principal name, display name, certificate thumbprint, and a list of simulated roles.
- **FR-005**: The simulated identity MUST be readable from both configuration files and environment variables, following standard configuration source precedence (environment variables override file values).
- **FR-006**: The system MUST validate the simulated identity configuration at startup when simulation mode is enabled, and fail fast with a descriptive error if required fields (user principal name, display name) are missing or empty.
- **FR-007**: The authentication middleware MUST check the `CacAuth:SimulationMode` configuration value at startup and branch into the simulation path (synthesize claims from config) or the real authentication path (validate JWT) accordingly.
- **FR-008**: Simulation mode MUST NOT be activatable in non-development environments (Production, Staging). If the flag is set in a non-development environment, the system MUST ignore it, log a security-level warning, and use real JWT-based authentication.
- **FR-009**: When simulation mode is active, the system MUST log a startup message at Information level that clearly identifies simulation mode is enabled and displays the simulated user principal name.
- **FR-010**: *(Removed — card removal and certificate expiration are physical hardware concerns that do not apply to the middleware-based simulation approach.)*
- **FR-011**: The middleware simulation MUST integrate transparently with the existing CAC session management — downstream consumers (e.g., `ICacSessionService`, `IPimService`, `IUserContext`) must not need to know whether the identity came from a real JWT or a simulated `ClaimsPrincipal`.
- **FR-012**: The system MUST NOT include any simulated identity values in default production configuration files; simulation configuration should only appear in development-specific configuration overrides.
- **FR-013**: Simulated CAC sessions MUST be persisted with a distinct marker (e.g., a dedicated `ClientType` value such as `Simulated`) so they are distinguishable from real authentications in the session database and audit logs.
- **FR-014**: The compliance evidence generator SHOULD automatically exclude sessions marked as simulated when producing evidence artifacts for NIST controls (AC-2, AU-2, AU-3). *(Forward-looking: implementation deferred until evidence collection expands to include session data. The `ClientType.Simulated` marker enables this filter when needed.)*
- **FR-015**: *(Merged into FR-002 — JWT/MSAL bypass and sole authentication source are now part of FR-002.)*
- **FR-016**: The simulation configuration MUST support identity replacement via `IOptions<CacAuthOptions>` overrides in DI, enabling integration test fixtures to register different simulated identities per test class without requiring an application restart.
- **FR-017**: The middleware simulation branch MUST emit structured telemetry events and debug-level log entries for developer troubleshooting, ensuring observability pipelines are exercised during simulation. Required telemetry: (1) an activity span named `cac.simulation.identity_synthesis` around the claims synthesis, (2) a counter `cac.simulation.activations` incremented on each simulated request, (3) dimensions/tags: `simulated_upn` and `environment`.

### Key Entities

- **SimulatedIdentityOptions**: The configuration shape that defines the test identity for simulation mode. Bound from the `CacAuth:SimulatedIdentity` configuration section. Contains user principal name, display name, certificate thumbprint, and simulated roles.
- **CacAuthOptions (extended)**: The existing authentication configuration class, extended with a `SimulationMode` flag and a nullable `SimulatedIdentityOptions` property. Controls whether the middleware branches into simulation.
- **ClientType.Simulated**: A new enum value added to the existing `ClientType` enum. Sessions created under simulation carry this marker for audit exclusion.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Developers can exercise all CAC-protected workflows end-to-end on a local machine without a physical smart card within 2 minutes of application startup with simulation configuration present.
- **SC-002**: 100% of CAC-dependent integration tests pass in CI/CD pipelines where no smart card hardware is available.
- **SC-003**: Switching between simulated identities (different roles, different users) requires only a configuration change and application restart — zero code changes.
- **SC-004**: The production safety guard prevents simulation mode activation in non-development environments with zero false negatives (never allows simulation in production).
- **SC-005**: The middleware simulation is indistinguishable from real JWT authentication from the perspective of consuming code — all downstream services (`ICacSessionService`, `IPimService`, `IUserContext`) operate identically regardless of the identity source.
- **SC-006**: Application startup time is not measurably degraded (less than 100ms additional latency) by the simulation mode configuration check.

## Assumptions

- The application uses the standard host configuration system that supports layered configuration sources (files, environment variables, command-line arguments) with well-defined precedence.
- The existing `CacAuthOptions` configuration class in the codebase will be extended to include simulation mode settings, rather than creating a separate configuration section.
- The `ASPNETCORE_ENVIRONMENT` variable (or equivalent) reliably identifies the deployment environment for the production safety guard.
- No new service interfaces are required. The existing authentication middleware already serves as the abstraction layer between identity sources and downstream services (`ICacSessionService`, `IPimService`, `IUserContext`). Simulation is implemented as a middleware branch, not a service replacement.
- The dependency injection container is configured at startup and does not support hot-swapping service implementations at runtime.
- Development and test environments are identified by the "Development" environment name; all other environments (Production, Staging, etc.) are considered non-development.
