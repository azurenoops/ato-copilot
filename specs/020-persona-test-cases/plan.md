# Implementation Plan: Persona End-to-End Test Cases

**Branch**: `020-persona-test-cases` | **Date**: 2026-03-05 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/020-persona-test-cases/spec.md`

## Summary

Define and organize 147 manual end-to-end test cases (+3 cross-persona scenarios) that exercise all 118 MCP tools across 5 RMF personas (ISSM, ISSO, SCA, AO, Engineer). Each test case specifies the exact natural language input, expected tool resolution, and expected output for a human tester using VS Code `@ato` or Microsoft Teams. Tests use a single cumulative "Eagle Eye" system that progresses through the full RMF lifecycle. The implementation is **documentation-only** — no new source code, models, or APIs. The deliverable is a structured test script document with preconditions, execution order, and acceptance criteria.

## Technical Context

**Language/Version**: N/A — documentation-only feature (manual test scripts)  
**Primary Dependencies**: Requires all 118 MCP tools from Features 001–019 to be deployed and functional  
**Storage**: N/A — no data model changes  
**Testing**: Manual test scripts (human tester executes NL queries, visually verifies output)  
**Target Platform**: VS Code (`@ato` chat participant) + Microsoft Teams (Adaptive Cards)  
**Project Type**: Manual test documentation  
**Performance Goals**: Each test case should complete within 10 seconds (simple queries ≤5s, complex operations ≤30s per Constitution VIII)  
**Constraints**: Mandatory cumulative execution order (ISSM → ISSO → SCA → AO → Engineer); single "Eagle Eye" system instance; all 5 personas must be tested sequentially  
**Scale/Scope**: 147 test cases + 3 cross-persona scenarios covering 118 tools, 5 personas, 7 RBAC roles

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Applicability | Status | Notes |
|-----------|--------------|--------|-------|
| I. Documentation as Source of Truth | **Applies** | ✅ PASS | Test scripts follow documented tool specs from Features 001–019; all NL inputs reference registered MCP tools |
| II. BaseAgent/BaseTool Architecture | N/A | ✅ PASS | No new agents or tools created — tests exercise existing tools only |
| III. Testing Standards | **Applies** | ✅ PASS | Constitution III requires manual tests to include "preconditions, steps, and expected outcomes" — spec provides all three for every test case |
| IV. Azure Government & Compliance | **Applies** | ✅ PASS | Test data uses Azure Government environment; NIST 800-53 Moderate baseline |
| V. Observability & Structured Logging | N/A | ✅ PASS | No new services or instrumentation — tests exercise existing logging |
| VI. Code Quality & Maintainability | N/A | ✅ PASS | No new code — this is documentation only |
| VII. User Experience Consistency | **Applies** | ✅ PASS | Test cases verify consistent response schema (status/data/metadata envelope) and actionable error messages |
| VIII. Performance Requirements | **Applies** | ✅ PASS | Acceptance criteria #1 requires 10-second response time, aligning with Constitution VIII tool response targets |

**Gate Result**: ✅ ALL PASS — no violations. Proceed to Phase 0.

## Project Structure

### Documentation (this feature)

```text
specs/020-persona-test-cases/
├── plan.md              # This file
├── spec.md              # Feature specification (147 test cases)
├── research.md          # Phase 0: tool verification & gap analysis
├── data-model.md        # Phase 1: test execution data model
├── quickstart.md        # Phase 1: how to run the manual test suite
└── tasks.md             # Phase 2 output (/speckit.tasks command)
```

### Source Code (repository root)

```text
# No source code changes — documentation-only feature.
# All 118 MCP tools are already implemented in:
src/
├── Ato.Copilot.Agents/Compliance/Tools/   # 118 Agent Framework tools
├── Ato.Copilot.Mcp/Tools/                 # ComplianceMcpTools.cs (MCP wrappers)
└── Ato.Copilot.Core/Constants/            # ComplianceRoles.cs (RBAC roles)
```

**Structure Decision**: No new source directories needed. Feature 020 is entirely contained in `specs/020-persona-test-cases/`.

## Complexity Tracking

> No constitution violations — table not applicable.
