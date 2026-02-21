<!--
  SYNC IMPACT REPORT
  ==================
  Version change: 1.0.0 → 1.1.0 (MINOR)
  Bump rationale: Three new principles added, governance section
  materially expanded with decision-making guidance. No existing
  principles removed or redefined.

  Modified principles:
    - III. Test-First Development → III. Testing Standards (expanded
      with mutation testing, boundary coverage, flaky-test policy)

  Added principles:
    - VI. Code Quality & Maintainability
    - VII. User Experience Consistency
    - VIII. Performance Requirements

  Added sections:
    - "Technical Decision-Making Framework" under Governance

  Removed sections: None

  Templates requiring updates:
    ✅ .specify/templates/plan-template.md - No changes needed;
       Constitution Check is dynamically filled and Technical Context
       already has Performance Goals / Constraints fields.
    ✅ .specify/templates/spec-template.md - No changes needed;
       Success Criteria / Measurable Outcomes already accommodate
       performance and UX metrics.
    ✅ .specify/templates/tasks-template.md - No changes needed;
       Polish phase includes performance optimization; test phases
       align with updated testing standards.
    ✅ .specify/templates/commands/ - Directory does not exist; N/A.

  Follow-up TODOs: None
-->

# ATO Copilot Constitution

## Core Principles

### I. Documentation as Source of Truth

All design and code changes MUST follow guidance documented under `/docs/`. If guidance conflicts
with "general best practices," repo guidance wins. Missing guidance MUST NOT be invented; instead,
propose an ADR or new document in `/docs/standards`.

**Rationale**: Ensures consistency across contributors and AI-assisted development. Every
recommendation MUST cite the doc path + section heading. If no rule exists, state so and suggest
where it should live.

### II. BaseAgent/BaseTool Architecture

All agents MUST extend `BaseAgent`. All tools MUST extend `BaseTool`. This pattern is
NON-NEGOTIABLE and applies to every feature touching the agent layer.

- Agents MUST implement: `AgentId`, `AgentName`, `Description`, `GetSystemPrompt()`
- Tools MUST implement: `Name`, `Description`, `Parameters`, `ExecuteAsync()`
- System prompts MUST be externalized in `*.prompt.txt` files
- Tools MUST be registered via `RegisterTool()` in agent constructors

**Rationale**: Standardized abstractions enable multi-agent orchestration, consistent function
calling, and maintainable code.

### III. Testing Standards (NON-NEGOTIABLE)

All behavior changes MUST include corresponding test changes. Target coverage: 80%+ for
unit tests. Test quality is as important as test quantity.

- **Unit Tests** (`Tests.Unit/`): Isolated component tests with mocked dependencies using
  xUnit, FluentAssertions, and Moq. Each public method MUST have at least one positive and
  one negative test case.
- **Integration Tests** (`Tests.Integration/`): WebApplicationFactory-based API and database
  tests. Every MCP tool endpoint MUST have an integration test covering the happy path and
  at least one error path.
- **Manual Tests** (`Tests.Manual/`): Documented verification scenarios for complex workflows.
  Each scenario MUST include preconditions, steps, and expected outcomes.
- **Boundary & Edge-Case Coverage**: All numeric inputs, collection parameters, and nullable
  fields MUST have boundary-value tests (empty, null, max-length, overflow).
- **Flaky-Test Policy**: A test that fails intermittently MUST be quarantined within 24 hours,
  root-caused within one sprint, and either fixed or removed.
- **Regression Tests**: Every bug fix MUST include a regression test that reproduces the
  original defect before verifying the fix.

Build/test discipline: Every change proposal MUST include exact `dotnet build` and `dotnet test`
commands with expected outcomes.

**Rationale**: Tests are the contract. Code without tests is incomplete. Comprehensive test
coverage across boundaries and edge cases prevents compliance-critical defects from reaching
production.

### IV. Azure Government & Compliance First

This platform targets Azure Government with NIST 800-53 compliance. All Azure interactions MUST:

- Support dual cloud environments: `AzureUSGovernment` (primary) and `AzureCloud`
- Use `DefaultAzureCredential` chain (Azure CLI for dev, Managed Identity for prod)
- Never hardcode credentials; use Key Vault or environment variables
- Validate against FedRAMP High and NIST 800-53 control families where applicable

**Rationale**: Government workloads require strict compliance boundaries.

### V. Observability & Structured Logging

All services MUST implement structured logging with Serilog. Logging requirements:

- Console + file sinks MUST be configured for development
- Application Insights sink MUST be configured for production
- Tool executions MUST log: input parameters, execution duration, success/failure
- Agent invocations MUST log: selected agent, tool chain, termination reason

**Rationale**: Compliance operations require full traceability. Logs enable auditing
tool execution chains and compliance assessment history.

### VI. Code Quality & Maintainability

All code MUST meet the following quality standards to ensure long-term maintainability of
compliance-critical software.

- **Single Responsibility**: Each method MUST serve a single purpose. No method SHOULD exceed
  50 lines of logic (excluding braces and blank lines). Methods exceeding this limit MUST be
  refactored or justified in a code-review comment.
- **Dependency Injection**: All dependencies MUST be injected via constructor injection. The
  service-locator pattern is prohibited. `IServiceProvider` MUST NOT be passed as a
  constructor parameter except in composition roots.
- **XML Documentation**: All public types and members MUST have XML documentation comments
  (`<summary>`, `<param>`, `<returns>`). Internal types SHOULD have documentation when their
  purpose is non-obvious.
- **No Magic Values**: Magic numbers and string literals MUST be extracted to named constants,
  enums, or configuration values. Exception: `0`, `1`, `-1`, `string.Empty`, and `null` in
  idiomatic C# patterns.
- **Code Duplication**: Duplicated logic spanning 5+ lines MUST be extracted into a shared
  method or abstraction. Copy-paste code MUST be flagged during review.
- **Warnings-as-Errors**: `dotnet build` MUST produce zero warnings in modified files. New
  `#pragma warning disable` directives MUST include a justification comment.
- **Naming Conventions**: Follow .NET naming guidelines — `PascalCase` for public members,
  `_camelCase` for private fields, `I`-prefix for interfaces. Acronyms of 3+ characters
  MUST use PascalCase (e.g., `McpServer`, not `MCPServer`).

**Rationale**: Consistent code quality reduces cognitive load, accelerates onboarding, and
minimizes defect density in compliance-critical software where correctness is paramount.

### VII. User Experience Consistency

All user-facing interfaces (MCP tools, HTTP endpoints, CLI output) MUST deliver a predictable,
accessible experience across interaction modes.

- **Consistent Response Schema**: All MCP tool responses MUST follow a uniform envelope
  structure containing: `status` (success/error), `data` (result payload), `metadata`
  (execution time, tool name, timestamp).
- **Actionable Error Messages**: Error responses MUST include: a human-readable `message`,
  a machine-readable `errorCode`, and a `suggestion` field with corrective guidance. Stack
  traces MUST NOT be exposed to end users in production.
- **Mode Parity**: Stdio and HTTP modes MUST produce functionally equivalent results for
  identical inputs. Any mode-specific behavior MUST be documented in `/docs/`.
- **Compliance Context**: All compliance-related output MUST include the relevant control
  family identifier, framework reference (e.g., NIST 800-53 Rev 5), and assessment scope.
- **Progress Feedback**: Operations exceeding 2 seconds MUST provide progress indicators
  (streaming updates in stdio mode, polling status in HTTP mode).
- **Accessibility**: All documentation, error messages, and user guidance MUST use plain
  language. Jargon MUST be accompanied by a brief definition on first use within a session.

**Rationale**: Government users require predictable, auditable interfaces that reduce
training burden and ensure consistent compliance reporting regardless of integration mode.

### VIII. Performance Requirements

All components MUST meet baseline performance targets to ensure responsiveness during
time-sensitive ATO and compliance workflows.

- **MCP Tool Response Time**: Simple queries (status, control lookup, audit log retrieval)
  MUST complete within 5 seconds. Complex operations (full compliance assessment, document
  generation) MUST complete within 30 seconds for a single-subscription scope.
- **HTTP Endpoint Latency**: Health and status endpoints MUST respond within 200ms (p95).
  Tool invocation endpoints MUST respond within the tool-specific time limits above.
- **Memory Budget**: Steady-state memory consumption MUST remain under 512MB for standard
  operations. Bulk operations (multi-subscription scans, large document generation) MUST
  NOT exceed 1GB.
- **Bounded Result Sets**: All database queries and API responses returning collections
  MUST support pagination. Unbounded `SELECT *` queries are prohibited. Default page size
  MUST be configurable (default: 50 items).
- **Cancellation Support**: All async operations MUST accept and honor `CancellationToken`.
  Long-running operations MUST check for cancellation at meaningful intervals.
- **Startup Time**: The MCP server MUST be ready to accept requests within 10 seconds of
  process start in both stdio and HTTP modes.

**Rationale**: Compliance tooling is time-sensitive during ATO processes. Predictable
performance builds user trust and ensures the platform remains viable under operational load.

## Azure Government & Compliance Requirements

All code interacting with Azure services MUST adhere to:

| Requirement | Standard |
|-------------|----------|
| Authentication | Managed Identity (prod), Azure CLI (dev) |
| Secrets | Azure Key Vault only; no hardcoded values |
| Networking | Private endpoints preferred; firewall rules documented |
| Compliance | NIST 800-53 control mapping for security-relevant features |
| Data Residency | US regions only (usgovvirginia, usgovarizona, usgovtexas) |

Infrastructure code (Bicep/Terraform) MUST include compliance annotations and policy assignments.

## Development Workflow & Quality Gates

### Required Output Format

For any change proposal, output:

1. **Guidance Compliance Report**: PASS/FAIL with rule-by-rule citations
2. **Architecture Decision**: If architecture/design is impacted, document rationale
3. **Code Changes**: Files changed + why + build/test commands + rollback procedure

### Quality Gates

| Gate | Requirement |
|------|-------------|
| Build | `dotnet build Ato.Copilot.sln` MUST pass with zero warnings |
| Unit Tests | `dotnet test` MUST pass with 80%+ coverage |
| Linting | No new warnings in modified files |
| Performance | New endpoints MUST include response-time assertions |
| UX Consistency | Tool responses MUST conform to standard envelope schema |
| Documentation | New features MUST update relevant `/docs/*.md` |

### Branch Strategy

- Feature branches: `###-feature-name` format (e.g., `001-add-evidence-api`)
- All changes via pull request with minimum 1 reviewer
- CI pipeline MUST pass before merge

## Governance

This constitution supersedes all other development practices for the ATO Copilot project.
Amendments follow this process:

1. **Proposal**: Open issue or PR with proposed change and rationale
2. **Review**: Minimum 1 maintainer approval required
3. **Migration**: Breaking changes MUST include migration plan
4. **Version Bump**: Follow semantic versioning (MAJOR.MINOR.PATCH)

All PRs and code reviews MUST verify compliance with this constitution.

### Technical Decision-Making Framework

When making implementation choices, principles MUST be applied in the following priority order:

1. **Compliance & Security** (Principles IV, I): Government compliance requirements and
   documented guidance always take precedence. If a performance optimization or code-quality
   improvement conflicts with compliance, compliance wins.
2. **Correctness & Testing** (Principles III, VI): All code MUST be provably correct through
   tests before optimizations are considered. Untested performance improvements are rejected.
3. **User Experience** (Principle VII): Consistent UX MUST NOT be sacrificed for internal
   code elegance or marginal performance gains.
4. **Performance** (Principle VIII): Performance targets MUST be met, but MUST NOT justify
   skipping tests, violating architecture patterns, or degrading UX.
5. **Code Quality** (Principles II, VI): Clean code and architecture patterns are mandatory
   but MUST yield to higher-priority principles when genuine trade-offs arise.
6. **Observability** (Principle V): Logging and tracing MUST be included in every feature
   but MUST NOT introduce measurable performance degradation (>5% latency impact).

When trade-offs arise between principles at the same priority level, the decision MUST be
documented as an Architecture Decision Record (ADR) in `/docs/standards/` and cited in the
pull request description.

**Version**: 1.1.0 | **Ratified**: 2025-01-01 | **Last Amended**: 2026-02-21
