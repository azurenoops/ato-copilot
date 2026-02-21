# Test Checklist: Core Compliance Capabilities

**Purpose**: Validate completeness and clarity of test strategy, coverage targets, and acceptance criteria
**Created**: 2026-02-21
**Feature**: [spec.md](../spec.md)

## Coverage & Strategy

- [x] CHK001 Is the 80% unit test coverage target (SC-009) defined per service or as an aggregate across all 9 services? [Clarity, Spec §SC-009]
- [x] CHK002 Are integration test coverage requirements defined separately from unit test coverage? [Completeness, Gap]
- [x] CHK003 Is the test strategy for Azure SDK mocking specified (mock `ArmClient`, `HttpPipeline`, or use recorded playback)? [Clarity, Gap]
- [x] CHK004 Are test data requirements defined for the NIST OSCAL catalog (full 1,189 controls or representative subset)? [Clarity, Gap]
- [x] CHK005 Is the test database strategy specified (in-memory SQLite, shared fixture, per-test isolation)? [Clarity, Gap]

## Acceptance Scenario Quality

- [x] CHK006 Are all 7 user story acceptance scenarios independently testable without requiring prior story completion? [Consistency, Spec §US1-US7]
- [x] CHK007 Is the "60 seconds for 50-200 resources" benchmark (US2-4, SC-002) defined with specific test environment specs? [Measurability, Spec §SC-002]
- [x] CHK008 Are negative acceptance scenarios (invalid input, missing config, API failure) defined for each user story? [Coverage, Gap]
- [x] CHK009 Is the acceptance criteria for "streaming progress updates" (FR-015) measurable — what constitutes a progress update? [Clarity, Spec §FR-015]
- [x] CHK010 Are acceptance criteria defined for the dual-source NIST catalog (FR-017) — online success, offline fallback, cache expiry? [Completeness, Spec §FR-017]

## Boundary & Edge Cases

- [x] CHK011 Are test requirements defined for subscriptions with 0 resources? [Coverage, Spec §Edge Cases]
- [x] CHK012 Are test requirements defined for Azure API rate limiting (15 req/5s for Resource Graph)? [Coverage, Spec §Edge Cases]
- [x] CHK013 Are test requirements defined for the same control violated by resource AND policy scans with conflicting severity? [Coverage, Spec §Edge Cases]
- [x] CHK014 Are test requirements defined for interrupted batch remediation (process crash mid-batch)? [Coverage, Spec §Edge Cases]
- [x] CHK015 Are test requirements defined for concurrent assessment requests on the same subscription? [Coverage, Spec §Edge Cases]
- [x] CHK016 Are test requirements defined for evidence collection on controls with no applicable Azure resources? [Coverage, Spec §Edge Cases]

## Error Path Testing

- [x] CHK017 Are all 14 error codes from contracts/compliance-tools.md mapped to specific test cases? [Completeness, Spec §Error Codes]
- [x] CHK018 Are test requirements defined for partial scan failures (resource scan succeeds but policy scan fails in combined mode)? [Coverage, Gap]
- [x] CHK019 Are test requirements defined for `CancellationToken` timeout behavior at the 60-second boundary? [Coverage, Gap]
- [x] CHK020 Are test requirements defined for database migration failures at startup? [Coverage, Gap]

## Integration Testing

- [x] CHK021 Is the `WebApplicationFactory` test strategy defined — full pipeline or isolated controller tests? [Clarity, Gap]
- [x] CHK022 Are integration test requirements for MCP JSON-RPC protocol (stdio mode) defined separately from HTTP mode? [Completeness, Gap]
- [x] CHK023 Are requirements defined for end-to-end flows crossing agent boundaries (ConfigurationAgent → ComplianceAgent handoff)? [Coverage, Gap]
- [x] CHK024 Are test requirements defined for middleware ordering (auth before audit logging)? [Coverage, Gap]

## Regression & Non-Functional

- [x] CHK025 Are performance regression test requirements defined (baseline measurement, threshold for failure)? [Completeness, Gap]
- [x] CHK026 Are memory usage test requirements defined against the 512MB steady-state budget? [Completeness, Spec §Plan]
- [x] CHK027 Are startup time test requirements defined against the 10-second target? [Completeness, Spec §Plan]

## Notes

- Check items off as completed: `[x]`
- Items tagged `[Gap]` indicate missing test strategy that should be addressed
- Items tagged with `[Spec §...]` reference existing spec sections
- Prioritize boundary condition tests — these are where compliance systems typically fail
