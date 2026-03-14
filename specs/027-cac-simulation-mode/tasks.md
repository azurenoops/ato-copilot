# Tasks: CAC Simulation Mode

**Input**: Design documents from `/specs/027-cac-simulation-mode/`
**Prerequisites**: plan.md (required), spec.md (required for user stories), research.md, data-model.md, quickstart.md

**Tests**: Included — plan.md Constitution Check III commits to unit tests (positive + negative per public method), integration tests, and boundary/edge-case tests. User Story 3 is specifically about automated test execution.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3, US4)
- Include exact file paths in descriptions

---

## Phase 1: Setup

**Purpose**: Development configuration for simulation mode

- [X] T001 Create `src/Ato.Copilot.Mcp/appsettings.Development.json` with `CacAuth:SimulationMode` flag set to `true` and a complete `CacAuth:SimulatedIdentity` block containing `UserPrincipalName`, `DisplayName`, `CertificateThumbprint`, and `Roles` array (see quickstart.md for exact shape)

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Configuration model and enum value that ALL user stories depend on

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [X] T002 Add `SimulatedIdentityOptions` config POCO class (properties: `UserPrincipalName`, `DisplayName`, `CertificateThumbprint`, `Roles`) and extend `CacAuthOptions` with `SimulationMode` bool + nullable `SimulatedIdentity` property in `src/Ato.Copilot.Core/Configuration/GatewayOptions.cs` (see research.md R2 for exact property definitions and data-model.md Entity 1 and 2 for field specs)
- [X] T003 [P] Add `Simulated` value with XML doc comment (`/// <summary>Simulated CAC session for development/testing. Excluded from compliance evidence per FR-014.</summary>`) to `ClientType` enum in `src/Ato.Copilot.Core/Models/Auth/AuthEnums.cs` (currently has: VSCode, Teams, Web, CLI)

**Checkpoint**: Configuration model ready — user story implementation can now begin

---

## Phase 3: User Story 1 — Simulated Identity for Local Development (Priority: P1) 🎯 MVP

**Goal**: Developer enables simulation mode via config; middleware injects a simulated `ClaimsPrincipal` so all CAC-protected workflows work locally without a physical smart card

**Independent Test**: Set `CacAuth:SimulationMode = true` + populate `CacAuth:SimulatedIdentity` in config. Start the app. Invoke a CAC-protected operation. Verify simulated identity is used, operation succeeds, no hardware prompt appears.

### Implementation for User Story 1

- [X] T004 [US1] Replace the existing Development bypass block (lines ~51–56) in `CacAuthenticationMiddleware.InvokeAsync` with a simulation branch: when `SimulationMode == true` and `ASPNETCORE_ENVIRONMENT == "Development"`, synthesize a `ClaimsPrincipal` from `SimulatedIdentityOptions` (claims: `NameIdentifier` → UPN, `Name` → DisplayName, `preferred_username` → UPN, `amr` → mfa + rsa, `Role` per configured role, thumbprint claim if configured), set `context.Items["ClientType"] = ClientType.Simulated`, emit debug log and structured telemetry span, then call `_next(context)` and return. When `SimulationMode == true` but environment is NOT Development, log a security-level warning and fall through to real JWT auth. Inject `IOptions<CacAuthOptions>` and `IHostEnvironment` into the middleware constructor. See research.md R3 for exact claims mapping pattern in `src/Ato.Copilot.Mcp/Middleware/CacAuthenticationMiddleware.cs`
- [X] T005 [US1] Add startup validation in `src/Ato.Copilot.Mcp/Program.cs` after service registration: when `CacAuth:SimulationMode == true` and `ASPNETCORE_ENVIRONMENT == "Development"`, validate that `SimulatedIdentity` is not null, `UserPrincipalName` is not empty, and `DisplayName` is not empty — throw `InvalidOperationException` with descriptive message on failure. On success, log at Information level: `"CAC simulation mode active. Simulated identity: {UserPrincipalName}"`. When `SimulationMode == true` and environment is NOT Development, log warning: `"CacAuth:SimulationMode is enabled but environment is {Environment}. Simulation mode will be ignored."`
- [X] T006 [P] [US1] Write unit tests for core simulation scenarios in `tests/Ato.Copilot.Tests.Unit/Middleware/CacAuthSimulationTests.cs` (create file and `Middleware/` directory if needed). Test cases: (1) SimulationMode enabled + Development → ClaimsPrincipal synthesized with correct claims, (2) SimulationMode disabled → middleware passes through to JWT auth, (3) SimulationMode enabled + missing SimulatedIdentity config → startup throws InvalidOperationException, (4) SimulationMode enabled + empty UPN → startup throws with descriptive message, (5) startup info log emitted with simulated UPN. Use xUnit + FluentAssertions + Moq. Mock IOptions<CacAuthOptions>, IHostEnvironment, ILogger.

**Checkpoint**: At this point, simulation mode works end-to-end for local development. This is the MVP.

---

## Phase 4: User Story 2 — Configurable Test Identity (Priority: P1)

**Goal**: Different identity configurations (roles, thumbprints, empty roles) produce correct behavior — all driven by config with no code changes

**Independent Test**: Configure identity with "Security Lead" roles → invoke high-privilege operation → succeeds. Change roles to "Platform Engineer" → restart → high-privilege operation blocked.

> **Note**: US2 implementation is embedded in Phase 2 (config POCO) and Phase 3 (middleware claims synthesis). This phase validates configurability through focused unit tests.

### Tests for User Story 2

- [X] T007 [US2] Write unit tests for identity configuration variations in `tests/Ato.Copilot.Tests.Unit/Middleware/CacAuthSimulationTests.cs`. Test cases: (1) configured roles map exactly to `ClaimTypes.Role` claims on synthesized principal, (2) multiple roles → multiple role claims, (3) empty roles array → zero role claims → least privilege, (4) null `Roles` property defaults to empty list (no role claims), (5) `CertificateThumbprint` when configured → present as claim on principal, (6) `CertificateThumbprint` when null → claim absent, (7) `IOptions<CacAuthOptions>` override in DI replaces configured identity values, (8) environment variable override of `CacAuth__SimulatedIdentity__UserPrincipalName` takes precedence over JSON config value (FR-005)

**Checkpoint**: Identity configurability verified — all config variations produce correct claims

---

## Phase 5: User Story 3 — Automated Test Execution Without Hardware (Priority: P1)

**Goal**: Integration tests run in CI/CD with simulation mode, producing deterministic results without smart card hardware

**Independent Test**: Run integration test suite with simulation mode enabled. All CAC-dependent tests pass. No test interacts with smart card hardware.

### Implementation for User Story 3

- [X] T008 [US3] Create integration test fixture base in `tests/Ato.Copilot.Tests.Integration/SimulationModeIntegrationTests.cs` using the existing `WebApplication.CreateBuilder()` + `UseTestServer()` pattern (see research.md R5). Each test class configures its own `CacAuthOptions` with a distinct simulated identity via `builder.Services.Configure<CacAuthOptions>(...)`. Implement `IAsyncLifetime` for proper setup/teardown.
- [X] T009 [US3] Write integration tests in `tests/Ato.Copilot.Tests.Integration/SimulationModeIntegrationTests.cs`. Test cases: (1) ISSO persona fixture — invoke protected endpoint → succeeds with ISSO identity, (2) Platform Engineer persona fixture — invoke endpoint → succeeds with engineer identity, (3) different test classes use different personas without app restart, (4) simulated session created with `ClientType.Simulated` marker in database, (5) no test interacts with smart card hardware (no `SmartCard`/`SCARD` references in test output)

**Checkpoint**: CI/CD pipeline can run all CAC-dependent tests using simulation mode

---

## Phase 6: User Story 4 — Production Safety Guard (Priority: P2)

**Goal**: Simulation mode is impossible to activate in non-Development environments, even if misconfigured

**Independent Test**: Set `CacAuth:SimulationMode = true` in production config. Start the app. Verify simulation is ignored, security warning logged, real JWT auth used.

> **Note**: The environment guard implementation is in T004 (middleware branch). This phase verifies config hygiene and tests the guard behavior.

### Implementation for User Story 4

- [X] T010 [US4] Audit `src/Ato.Copilot.Mcp/appsettings.json` to confirm it contains no `CacAuth:SimulationMode` or `CacAuth:SimulatedIdentity` keys — simulation config must only appear in `appsettings.Development.json` (FR-012). If any simulation keys are present, remove them.

### Tests for User Story 4

- [X] T011 [P] [US4] Write unit tests for production safety guard in `tests/Ato.Copilot.Tests.Unit/Middleware/CacAuthSimulationTests.cs`. Test cases: (1) SimulationMode enabled + Production environment → simulation ignored, real auth used, (2) SimulationMode enabled + Staging environment → simulation ignored, (3) SimulationMode enabled + Development → simulation activates, (4) security warning logged when SimulationMode enabled in non-Development, (5) warning log includes environment name

**Checkpoint**: Production safety guard verified — zero risk of accidental simulation in production

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: Documentation, code quality, and end-to-end validation

- [X] T012 [P] Add XML doc comments on `SimulatedIdentityOptions` class, new `CacAuthOptions.SimulationMode` and `CacAuthOptions.SimulatedIdentity` properties, and document the `ClientType.Simulated` evidence exclusion contract (FR-014 forward-looking guard) in `src/Ato.Copilot.Core/Configuration/GatewayOptions.cs` and `src/Ato.Copilot.Core/Models/Auth/AuthEnums.cs`
- [X] T013 [P] Update feature documentation — add CAC simulation mode developer guide section to `docs/dev/` or `docs/getting-started/engineer.md` covering: enabling simulation, switching identities, CI/CD usage, production safety
- [X] T014 Run `quickstart.md` end-to-end validation — start the application with `appsettings.Development.json` simulation config, verify startup log, invoke a CAC-protected MCP tool, confirm simulated identity flows through, check all error scenarios from quickstart.md Common Errors table

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — can start immediately
- **Foundational (Phase 2)**: No code dependencies on Phase 1 — can start in parallel, but logically comes second
- **User Story 1 (Phase 3)**: Depends on Phase 2 completion (needs `SimulatedIdentityOptions`, `CacAuthOptions` extensions, `ClientType.Simulated`)
- **User Stories 2, 3, 4 (Phases 4–6)**: All depend on Phase 3 (US1) completion — then can proceed **in parallel**
- **Polish (Phase 7)**: Depends on all user story phases being complete

### User Story Dependencies

```
Phase 1 (Setup) ─────────┐
                          ├──▶ Phase 3 (US1) ──┬──▶ Phase 4 (US2) ──┐
Phase 2 (Foundational) ──┘          MVP        ├──▶ Phase 5 (US3) ──┼──▶ Phase 7 (Polish)
                                               └──▶ Phase 6 (US4) ──┘
```

- **US1 (P1)**: MVP — can start after Foundational. No dependencies on other stories.
- **US2 (P1)**: Can start after US1. Tests verify configurability implemented in US1.
- **US3 (P1)**: Can start after US1. Independent — builds integration test fixtures.
- **US4 (P2)**: Can start after US1. Independent — verifies guard implemented in US1.

### Within Each User Story

- Models/config (Phase 2) before middleware (Phase 3)
- Middleware implementation before startup validation
- Implementation before tests (tests validate behavior)
- Core path before edge cases

### Parallel Opportunities

After US1 (Phase 3) completes, **three workstreams** can proceed in parallel:
- **Stream A**: US2 unit tests (T007) — different test cases in same file
- **Stream B**: US3 integration tests (T008, T009) — different test file entirely
- **Stream C**: US4 verification + tests (T010, T011) — config audit + different test cases

---

## Parallel Example: Foundational Phase

```bash
# T002 and T003 touch different files — run in parallel:
Task: T002 "Add SimulatedIdentityOptions + extend CacAuthOptions in GatewayOptions.cs"
Task: T003 "Add Simulated to ClientType enum in AuthEnums.cs"
```

## Parallel Example: Post-MVP (after US1)

```bash
# US2, US3, US4 are independent — run in parallel:
Task: T007 "Unit tests for identity config variations in CacAuthSimulationTests.cs"
Task: T008 "Integration test fixtures in SimulationModeIntegrationTests.cs"
Task: T010 "Audit appsettings.json for production config cleanliness"
Task: T011 "Unit tests for production safety guard in CacAuthSimulationTests.cs"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup (T001)
2. Complete Phase 2: Foundational (T002, T003)
3. Complete Phase 3: User Story 1 (T004, T005, T006)
4. **STOP and VALIDATE**: Run `dotnet build`, run unit tests, start app with simulation config, invoke a CAC-protected endpoint
5. MVP is deployable — developers can use simulation mode for local development

### Incremental Delivery

1. Setup + Foundational → Config model ready
2. US1 → MVP! Simulation mode works end-to-end
3. US2 → Configurable identity verified with edge-case tests
4. US3 → CI/CD test execution validated with integration tests
5. US4 → Production safety guard verified
6. Polish → Documentation and end-to-end validation

### Key Implementation Notes

- **No new interfaces**: The middleware IS the abstraction. See research.md R1 (superseded) for rationale.
- **No new services**: Simulation is a middleware branch (~30 lines), not a service replacement. See research.md R6 (superseded).
- **FR-014 (evidence exclusion)**: Forward-looking guard. `ClientType.Simulated` is the marker. The actual `.Where(s => s.ClientType != ClientType.Simulated)` filter will be added when evidence collection expands to include session data. See research.md R4.
- **US5 (Session Lifecycle Testing)**: Descoped — card removal and certificate expiration are physical hardware concerns not applicable to middleware approach. See spec.md US5 for future extension option.

---

## Notes

- [P] tasks = different files, no dependencies on incomplete work
- [Story] label maps task to specific user story for traceability
- Each user story is independently testable after completion
- Commit after each task or logical group
- Stop at any checkpoint to validate story independently
- Total: 14 tasks across 7 phases (4 user stories + setup + foundational + polish)
