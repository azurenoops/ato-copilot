# Feature Specification: Enterprise MCP Server Hardening

**Feature Branch**: `029-enterprise-mcp-hardening`  
**Created**: 2026-03-14  
**Status**: Draft  
**Input**: User description: "Enterprise ATO MCP Server with resilient retry logic, rate-limit and pagination protection, path sanitization, lazy-loaded parsing, structured logging, streaming support, performance caching, enterprise monitoring, and IL6 offline support."

## User Scenarios & Testing *(mandatory)*

### User Story 1 — Resilient Retry Logic Across All External Calls (Priority: P1)

As a compliance officer running an ATO assessment, I need the MCP server to automatically retry transient failures (Azure API throttling, network blips, AI service 503s) with exponential backoff so that my assessment completes reliably without manual resubmission.

Currently, Polly retry with exponential backoff is configured only for the NIST Controls Service HttpClient (3 retries, 2s base delay, jitter enabled). No retry logic exists for Azure AI calls, Azure Resource Manager (ARM) calls, Foundry agent calls, or the MCP server's own `ProcessChatRequestAsync` agent dispatch. If Azure OpenAI returns a 429 or 503, the request fails immediately. There is no circuit breaker anywhere in the system to prevent cascading failures when an upstream dependency is down.

**Why this priority**: Transient failures are the most common cause of user-visible errors in government cloud environments. Without system-wide resilience, every 429 from Azure OpenAI or brief ARM API outage produces a user-facing error, undermining trust in the platform.

**Independent Test**: Can be tested by configuring a mock HttpClient that returns 503 on the first two calls and 200 on the third, then verifying the request succeeds with 3 logged retry attempts and a total elapsed time consistent with exponential backoff.

**Acceptance Scenarios**:

1. **Given** the Azure AI service returns HTTP 503 on the first call, **When** the MCP server processes a compliance chat request, **Then** it retries automatically up to the configured maximum (default: 3) with exponential backoff and jitter, and returns a successful response if any retry succeeds.
2. **Given** the Azure AI service returns HTTP 429 (Too Many Requests) with a `Retry-After` header of 5 seconds, **When** the MCP server retries, **Then** it honors the `Retry-After` delay before the next attempt rather than using its own calculated backoff.
3. **Given** an ARM API call fails 4 consecutive times (exceeding the retry limit), **When** the circuit breaker threshold is reached (5 failures in 30 seconds), **Then** subsequent calls to that dependency are short-circuited for a configurable recovery window (default: 30 seconds) and the server returns a structured error with `errorCode: "DEPENDENCY_UNAVAILABLE"` and a `suggestion` explaining the upstream service is temporarily down.
4. **Given** all retries and circuit-breaker attempts are exhausted, **When** the MCP server returns an error, **Then** the response includes a structured `ErrorDetail` with the original error code, the number of retry attempts made, and a user-facing suggestion.
5. **Given** repeated transient failures on a dependency, **When** the circuit breaker opens, **Then** the server emits a structured log event at Warning level including the dependency name, failure count, and circuit state transition.

---

### User Story 2 — API Rate Limiting and Throttle Protection (Priority: P1)

As a platform operator, I need the MCP server to enforce per-endpoint rate limits so that a single misbehaving client cannot overwhelm the server or exhaust Azure API quotas that affect all users. "Without degradation" means p95 response time remains under 5 seconds for simple queries and error rate stays under 1% at 50–100 concurrent clients.

Currently, `SlidingWindowRateLimiter` is used only inside `AlertNotificationService` to throttle notification dispatch (10/minute per channel). No rate limiting exists at the HTTP endpoint level — `/mcp/chat`, `/mcp/chat/stream`, `/mcp/tools`, and `/mcp` accept unlimited requests. There is no `UseRateLimiter()` middleware in the pipeline.

**Why this priority**: Without endpoint-level throttling, a single client can consume all Azure OpenAI tokens/minute, causing 429 errors for all concurrent users. Government environments require capacity protection to maintain SLA commitments.

**Independent Test**: Can be tested by sending 60 rapid requests to `/mcp/chat` within 1 minute (exceeding a 30 req/min limit) and verifying that request 31+ receives HTTP 429 with a `Retry-After` header and structured error body.

**Acceptance Scenarios**:

1. **Given** the rate limiting middleware is configured with a sliding window of 30 requests per minute per client, **When** a client sends 31 requests within 60 seconds to `/mcp/chat`, **Then** the 31st request returns HTTP 429 with a `Retry-After` header indicating seconds until the next permit and a structured `ErrorDetail` body with `errorCode: "RATE_LIMITED"`.
2. **Given** the rate limit is configured per-endpoint, **When** a client is rate-limited on `/mcp/chat`, **Then** requests to `/health` and `/mcp/tools` are NOT affected (health and tool-listing endpoints are exempt from rate limiting).
3. **Given** the rate limit configuration specifies different limits per endpoint, **When** `/mcp/chat/stream` has a lower limit than `/mcp/chat` (because SSE connections hold server resources longer), **Then** the streaming endpoint enforces its own independent rate limit.
4. **Given** the rate limiter is active, **When** a request is throttled, **Then** a structured log event at Warning level is emitted with the client identifier (derived from correlation ID or authenticated user), endpoint, and current window utilization.
5. **Given** the server is deployed with environment variable overrides, **When** `ATO_RATELIMIT__CHATLIMIT` is set, **Then** the per-minute limit for `/mcp/chat` uses the overridden value.

---

### User Story 3 — Path Sanitization and Input Validation (Priority: P1)

As a security engineer, I need all file path inputs and user-supplied strings to be validated and sanitized before processing so that the server is protected against path traversal, injection, and oversized payload attacks.

Currently, `ScriptSanitizationService` blocks 23 destructive command patterns in remediation scripts (comprehensive for script content). However, `ChatService.SaveAttachmentAsync` uses `Path.Combine()` without validating that the resolved path stays within the `uploads/` directory. The `/mcp/chat` endpoint does not enforce request body size limits. No consistent input validation layer exists across all MCP endpoints.

**Why this priority**: Path traversal and injection are OWASP Top 10 vulnerabilities. Government compliance (NIST 800-53 SI-10: Information Input Validation) requires explicit input validation at all system boundaries.

**Independent Test**: Can be tested by sending a `fileName` parameter containing `../../../etc/passwd` to the attachment endpoint and verifying it is rejected with a structured error, and by sending a request body exceeding the configured maximum size to `/mcp/chat` and verifying HTTP 413.

**Acceptance Scenarios**:

1. **Given** a file path input containing directory traversal sequences (`../`, `..\\`, `%2e%2e%2f`), **When** any file operation resolves the path, **Then** the path is canonicalized via `Path.GetFullPath()` and rejected if it resolves outside the designated base directory, returning `errorCode: "PATH_TRAVERSAL_BLOCKED"`.
2. **Given** a chat message exceeding the configured maximum size (default: 32 KB), **When** submitted to `/mcp/chat`, **Then** the server returns HTTP 413 (Payload Too Large) with a structured error before any agent processing occurs.
3. **Given** a request body containing null bytes or non-UTF-8 sequences, **When** submitted to any MCP endpoint, **Then** those sequences are rejected or sanitized before reaching agent processing.
4. **Given** any endpoint receiving a tool invocation with a file path parameter, **When** the path is processed, **Then** the path is validated against a configurable allowlist of base directories (default: application working directory).
5. **Given** the `SaveAttachmentAsync` method receives a file name, **When** the final storage path is computed, **Then** the method verifies the resolved absolute path starts with the `uploads/` base directory path before writing.

---

### User Story 4 — Performance Caching for Repeated Queries (Priority: P2)

As a compliance analyst running multiple assessments against the same system boundary, I need frequently accessed data (compliance baselines, control lookups, recent assessment results) to be served from cache so that repeated queries return in sub-second time instead of re-executing full AI processing.

Currently, `IMemoryCache` is registered and used exclusively for NIST catalog data (controls, impact levels, FedRAMP templates) with 24-hour absolute / 6-hour sliding expiration and a background warmup service. No caching exists for tool responses, assessment results, or frequently requested compliance data from Azure APIs.

**Why this priority**: Constitution VIII mandates simple queries complete in 5 seconds. Caching eliminates redundant Azure API calls, reduces token consumption, and improves perceived responsiveness for common compliance lookups.

**Independent Test**: Can be tested by sending the same compliance control lookup twice in 30 seconds and verifying the second response returns in under 500ms with a `X-Cache: HIT` header, compared to the first response which takes several seconds.

**Acceptance Scenarios**:

1. **Given** a NIST control lookup (e.g., "Tell me about AC-2") has been served within the cache window, **When** the same lookup is requested again, **Then** the cached result is returned without invoking the AI agent, and the response includes a `X-Cache: HIT` response header.
2. **Given** a compliance assessment has been computed for a specific scope within the past 15 minutes, **When** the user requests the same assessment scope, **Then** the server returns the cached assessment with a notice that it reflects data as of the cached timestamp, and offers a "Refresh Assessment" follow-up action.
3. **Given** a cache-eligible response is returned, **When** the response is inspected, **Then** it includes `X-Cache` (HIT or MISS) and `X-Cache-Age` (seconds since cached) response headers for diagnostic transparency.
4. **Given** the cache window has expired, **When** the next identical request arrives, **Then** the request is processed fresh and the result is cached for the next window.
5. **Given** an administrator triggers a cache invalidation action, **When** the invalidation completes, **Then** all cached entries for the specified scope are removed and subsequent requests produce fresh results.

---

### User Story 5 — Enterprise Monitoring and Metrics Export (Priority: P2)

As a platform operations team, I need the MCP server to export structured metrics and health telemetry in industry-standard formats so that government SOC dashboards can monitor server health, request latency, error rates, and resource utilization in real time.

Currently, `System.Diagnostics.Metrics` instruments are defined in `ToolMetrics` (4 instruments: tool invocations, duration, errors, active sessions) and `ComplianceMetricsService` (2 instruments: NIST API calls, duration). Health checks exist for compliance-agent and NIST controls. Serilog structured logging is configured with correlation IDs and sensitive data redaction. However, no metrics exporter (OpenTelemetry, Prometheus, Application Insights) is registered for the MCP server — the metrics are recorded but never exported. No endpoint latency histograms exist. Health checks lack build version and uptime data.

**Why this priority**: Government SOCs require real-time visibility. The metrics infrastructure is already instrumented but not connected to any backend. Wiring the export path is high-value, low-risk work.

**Independent Test**: Can be tested by sending 10 requests to `/mcp/chat`, then querying the `/metrics` endpoint and verifying counters for `ato.copilot.tool.invocations`, `ato.copilot.http.request.duration`, and active session count reflect the test traffic.

**Acceptance Scenarios**:

1. **Given** the MCP server is running, **When** an operator queries `/health`, **Then** the response includes all registered health checks with pass/fail status, individual check durations, server uptime since last restart, build version, and an overall aggregate status.
2. **Given** the metrics exporter is configured, **When** 10 chat requests are processed, **Then** the `ato.copilot.tool.invocations` counter reflects the actual tool calls, and the `ato.copilot.http.request.duration` histogram captures per-endpoint latency distributions.
3. **Given** the OpenTelemetry exporter is registered, **When** a compliance assessment runs with 5 tool calls, **Then** a distributed trace spans the full request lifecycle from HTTP ingress through agent selection, each tool execution, and response serialization.
4. **Given** an error occurs during request processing, **When** the error is logged, **Then** the log entry includes the correlation ID, conversation ID, error code, agent used, and W3C trace ID for cross-system correlation.
5. **Given** the health check endpoint is queried by a load balancer, **When** any registered health check reports Unhealthy, **Then** the aggregate endpoint returns HTTP 503 with the failing check details.

---

### User Story 6 — Lazy-Loaded Parsing for Large Payloads (Priority: P2)

As an engineer running compliance scans across a large system boundary (50+ Azure resources), I need the server to process and return results incrementally rather than buffering the entire result set in memory before responding, so that I see findings as they are discovered and the server stays within its 512 MB memory budget.

Currently, all compliance findings are collected into a `List<ComplianceFinding>` in memory before the full result is serialized and returned. POA&M and SSP document generation builds the entire document content in memory before returning. No `IAsyncEnumerable` or streaming result patterns are used in tool responses. The 9 static JSON knowledge base files are fully deserialized into memory at startup.

**Why this priority**: Constitution VIII mandates a 512 MB memory budget. Large assessments can exceed this if findings are buffered. Lazy parsing also reduces time-to-first-byte for the user.

**Independent Test**: Can be tested by running a compliance scan against a 100-resource scope via `/mcp/chat/stream` and verifying that SSE `partial` events begin arriving within 5 seconds while the full assessment continues, and peak memory usage stays under 512 MB.

**Acceptance Scenarios**:

1. **Given** the MCP server processes a compliance assessment with 200+ findings, **When** streaming via `/mcp/chat/stream`, **Then** the first `toolProgress` SSE event with partial findings is emitted within 5 seconds, rather than waiting for all tools to complete.
2. **Given** a tool returns a paginated result set exceeding 100 items, **When** the server serializes the response, **Then** items are serialized incrementally (yielded from an enumerable) rather than buffered into a complete list before serialization.
3. **Given** the server starts in HTTP mode, **When** the NIST knowledge base JSON files are loaded, **Then** each file is parsed on first access (lazy initialization) rather than all files being parsed eagerly at startup, reducing startup memory footprint by deferring parsing until the respective data is requested.
4. **Given** a POA&M document generation request for a scope with 500 findings, **When** the server processes the request, **Then** peak memory usage for the generation operation does not exceed 256 MB (half the total budget), measured via diagnostic counters.

---

### User Story 7 — Pagination Protection for Tool Result Sets (Priority: P2)

As a compliance analyst querying the kanban board or listing compliance findings, I need MCP tool responses to enforce pagination with bounded page sizes so that the server never returns unbounded result sets that blow up client memory or network bandwidth.

Currently, `PagedResult<T>` and `PaginationInfo` classes exist in the data model. The kanban service (`IKanbanService`) accepts `page` and `pageSize` parameters. However, pagination is not exposed via MCP HTTP endpoints — the `/mcp/chat` and `/mcp/tools` responses return all items. There is no server-side maximum page size enforcement. Tool listing at `/mcp/tools` returns all tools in a single response.

**Why this priority**: Constitution VIII requires bounded result sets with configurable default page size (50). Tools can return hundreds of findings; sending them all in a single response violates performance requirements.

**Independent Test**: Can be tested by requesting a compliance finding list for a scope with 200 findings and verifying the response contains at most 50 items with `pagination.hasNextPage: true` and a `nextPageToken` for retrieval of subsequent pages.

**Acceptance Scenarios**:

1. **Given** a tool returns a result set exceeding the maximum page size (default: 50), **When** the MCP server builds the response, **Then** the response includes only the first page of results and a `pagination` object with `totalItems`, `totalPages`, `hasNextPage: true`, and `nextPageToken`.
2. **Given** a request includes a `pageSize` parameter exceeding the configured maximum (default: 100), **When** the server processes the request, **Then** the page size is clamped to the configured maximum and a `metadata.pageSizeClamped: true` indicator is included in the response.
3. **Given** a paginated first page has been returned with a `nextPageToken`, **When** the client sends a follow-up request with that token, **Then** the server returns the next page of results with updated pagination metadata.
4. **Given** the `/mcp/tools` endpoint lists all available tools, **When** the total tool count exceeds the page size limit, **Then** the response is paginated with the same `PaginationInfo` envelope.
5. **Given** no `page` or `pageSize` parameter is provided, **When** the server returns a collection response, **Then** the default page size (configurable, default: 50) is applied.

---

### User Story 8 — IL6 Offline and Air-Gapped Operation (Priority: P3)

As a compliance officer operating in an IL6 (Impact Level 6) air-gapped environment with no internet connectivity, I need the MCP server to function fully offline using cached and embedded data so that I can perform control lookups, retrieve cached assessments, and generate documents without any external network calls. Chat queries requiring AI processing return a structured error listing available offline capabilities.

Currently, `EnableOfflineFallback: true` is configured in `appsettings.json` for NIST controls, and the `NistControlsService` falls back to an embedded OSCAL resource when online fetch fails. `CloudEnvironment` is configurable for sovereign clouds. However, there is no system-wide offline mode — Azure AI calls, ARM API calls, and most agent operations require network connectivity. No local/embedded AI model support exists. No sync-when-reconnected capability exists.

**Why this priority**: IL6 environments are critical for DoD classified workloads but are the most constrained. While P3 priority, this directly addresses the core user requirement and must be architecturally planned even if implementation is phased.

**Independent Test**: Can be tested by setting `ATO_SERVER__OFFLINEMODE=true`, disconnecting from the network, and verifying that NIST control lookups, cached assessment queries, and document generation all succeed while Azure-dependent operations return a structured error with `errorCode: "OFFLINE_UNAVAILABLE"` and a list of capabilities available offline.

**Acceptance Scenarios**:

1. **Given** the server is started with `OfflineMode: true`, **When** any operation that requires Azure AI or Azure ARM is invoked, **Then** the server returns a structured error with `errorCode: "OFFLINE_UNAVAILABLE"` and a `suggestion` listing the capabilities that are available offline (control lookups, cached assessments, document generation from cached data).
2. **Given** offline mode is enabled, **When** a user requests a NIST control lookup (e.g., "What is AC-2?"), **Then** the embedded OSCAL data and cached control definitions serve the response without any network call.
3. **Given** offline mode is enabled, **When** the server starts, **Then** all cached data from the last online session (assessments, baselines, control mappings) is available from local persistent storage, not just in-memory cache.
4. **Given** offline mode is enabled and the user has previously run assessments, **When** the user requests a POA&M or SSP document, **Then** the document is generated from the most recent cached assessment data with a timestamp disclaimer indicating the data freshness.
5. **Given** the server transitions from offline to online (network restored), **When** the server detects connectivity, **Then** it performs a background sync of stale cached data against live sources and logs the sync result.
6. **Given** the server operates in IL6 mode, **When** processing data at rest, **Then** all locally persisted cache data is encrypted using FIPS 140-2 validated modules consistent with the platform's existing authentication chain.

---

### User Story 9 — Streaming Resilience and Reconnection (Priority: P3)

As a user running a long compliance scan via the VS Code extension, I need the SSE streaming connection to recover via automatic event replay from the server-side buffer after network interruptions so that I don't lose progress on a 30-second assessment if a brief network blip occurs.

Currently, `/mcp/chat/stream` emits typed SSE events (8 event types including `agentRouted`, `toolStart`, `toolProgress`, `toolComplete`, `partial`, `complete`). The VS Code SSE client uses native fetch with `ReadableStream`. However, SSE events have no `id` field for reconnection. There is no server-side event buffer. Client disconnection is silently caught but not recoverable.

**Why this priority**: Streaming is fully implemented but lacks resilience. Network interruptions on government networks are common due to VPN and proxy configurations.

**Independent Test**: Can be tested by starting a streaming request, forcibly closing the client connection after 3 events, reconnecting with `Last-Event-ID: 3`, and verifying the server replays missed events from its buffer.

**Acceptance Scenarios**:

1. **Given** the MCP server sends SSE events, **When** each event is emitted, **Then** it includes an `id: <monotonically-increasing-integer>` field for reconnection tracking.
2. **Given** a client disconnects during a streaming response, **When** the client reconnects with a `Last-Event-ID` header within the event buffer window (default: 60 seconds), **Then** the server replays all events after the specified ID before continuing with live events.
3. **Given** a client reconnects after the event buffer window has expired, **When** the reconnection occurs, **Then** the server returns the final `complete` event if available, or a structured error indicating the stream session has expired.
4. **Given** a streaming connection is idle for more than 30 seconds between events, **When** the server detects idle time, **Then** it sends a keepalive comment (`: keepalive\n\n`) to prevent proxy/load-balancer timeout.

---

### Edge Cases

- What happens when a retry succeeds but takes longer than the Constitution VIII 30-second limit? → The response is returned successfully with `processingTimeMs` reflecting actual time. A warning-level log is emitted noting the SLA breach.
- What happens when the circuit breaker is open and the user sends a new request? → The server returns immediately with `errorCode: "DEPENDENCY_CIRCUIT_OPEN"` and a `suggestion` indicating the estimated recovery time. No call is made to the failing dependency.
- What happens when a rate-limited client sends a request to an exempt endpoint (`/health`)? → The request is processed normally. Rate limits apply only to chat and tool endpoints.
- What happens when a cache entry is being refreshed and a concurrent request arrives for the same key? → The concurrent request receives the stale cached value (stale-while-revalidate pattern). The refresh completes in the background and subsequent requests get the fresh value.
- What happens when the server starts in offline mode but the embedded OSCAL data is corrupted or missing? → The NIST controls health check reports Unhealthy with a specific error. The server starts but compliance tools that depend on control data return `errorCode: "OFFLINE_DATA_UNAVAILABLE"`.
- What happens when the SSE event buffer fills up and older events are evicted? → A buffer overflow warning is logged with the count of evicted events. Reconnecting clients that request an evicted event ID receive the oldest available event with a `retry` field suggesting a shorter reconnect interval.
- What happens when an extremely large chat message (>32 KB) is sent as a valid UTF-8 string? → The server rejects it with HTTP 413 before JSON deserialization occurs. The rejection is logged at Info level with the message size and client identifier.
- What happens when a client requests `pageSize: 0` or a negative page size? → The server clamps to a minimum page size of 1 and includes `metadata.pageSizeClamped: true` in the response.
- What happens when the path sanitization rejects a valid cross-platform path format? → Path validation uses `Path.GetFullPath()` for canonicalization, which handles platform-specific separators. Only the canonical result is checked against the base directory.
- What happens when the Prometheus/OpenTelemetry metrics endpoint is requested but the exporter is not configured? → The `/metrics` endpoint returns HTTP 404 with a structured error suggesting configuration steps.
- What happens when an unauthenticated client requests `/metrics`? → The request is rejected with HTTP 401, consistent with all other MCP endpoints. The `/metrics` endpoint is behind the same CAC authentication middleware.
- What happens when the offline-to-online sync detects conflicting changes? → The server uses a "last write wins" strategy for cached data and logs the conflict details. Assessments are always refreshed from live data when online; cached assessments are treated as read-only snapshots.
- What happens when a retry delay from a `Retry-After` header exceeds the total timeout budget? → The server abandons the retry and returns immediately with `errorCode: "RETRY_AFTER_EXCEEDS_BUDGET"` and the `Retry-After` value in the error details.

## Requirements *(mandatory)*

### Functional Requirements

#### Resilient Retry Logic

- **FR-001**: All HTTP clients registered via `IHttpClientFactory` MUST have a resilience pipeline configured with exponential backoff retry (configurable max retries, default: 3; base delay, default: 2 seconds; jitter: enabled). This extends the existing NIST-only Polly configuration to all named HTTP clients including Azure AI, ARM, and any future external service clients.
- **FR-002**: The retry pipeline MUST honor `Retry-After` response headers. When a 429 or 503 response includes `Retry-After`, the next retry delay MUST use the server-specified value instead of the calculated exponential backoff, provided the value does not exceed the remaining timeout budget.
- **FR-003**: A circuit breaker MUST be configured for each resilience pipeline with: failure threshold (default: 5 failures in 30 seconds), break duration (default: 30 seconds), and sampling duration matching the failure window. When the circuit opens, all subsequent calls MUST fail fast with `errorCode: "DEPENDENCY_CIRCUIT_OPEN"` until the recovery window elapses.
- **FR-004**: A timeout policy MUST wrap each resilience pipeline with a configurable per-request timeout (default: 30 seconds per Constitution VIII). Requests exceeding the timeout MUST be cancelled via `CancellationToken` and return `errorCode: "REQUEST_TIMEOUT"`.
- **FR-005**: All retry attempts, circuit breaker state transitions, and timeout cancellations MUST be logged as structured events at Warning level with: dependency name, attempt number, delay applied, HTTP status code, and correlation ID.
- **FR-005a**: The existing `NistControlsService` Polly configuration MUST be refactored to use a shared resilience pipeline builder so that retry, circuit breaker, and timeout policies are consistent across all HTTP clients. The NIST-specific configuration (3 retries, 2s base delay) is preserved as the shared default.

#### API Rate Limiting

- **FR-006**: The MCP server MUST register and apply ASP.NET Core rate limiting middleware (`UseRateLimiter()`) in the middleware pipeline, positioned after CORS and before authentication.
- **FR-007**: Rate limits MUST be configurable per endpoint via a named policy model: `/mcp/chat` (default: 30 req/min), `/mcp/chat/stream` (default: 10 req/min), `/mcp` JSON-RPC (default: 60 req/min). The `/health` and `/mcp/tools` endpoints MUST be exempt from rate limiting.
- **FR-008**: Rate limiting MUST use the sliding window algorithm with configurable window size (default: 1 minute) and segment count (default: 2), consistent with the existing `AlertNotificationService` pattern.
- **FR-009**: When a request is rate-limited, the server MUST return HTTP 429 with a `Retry-After` header (seconds until next permit) and a response body containing an `ErrorDetail` with `errorCode: "RATE_LIMITED"`, a `message` indicating the limit, and a `suggestion` advising the client to reduce request frequency.
- **FR-010**: Rate limit thresholds MUST be configurable via `appsettings.json` under a `RateLimiting` section and overridable via environment variables (`ATO_RATELIMITING__CHATLIMIT`, etc.).
- **FR-010a**: Rate limiting MUST identify clients by correlation ID or authenticated user identity (from CAC claims). Unauthenticated requests MUST be rate-limited by source IP address.

#### Path Sanitization and Input Validation

- **FR-011**: A `PathSanitizationService` MUST be introduced with a `ValidatePathWithinBase(string candidatePath, string baseDirectory)` method that canonicalizes both paths via `Path.GetFullPath()` and returns a result indicating whether the candidate path is within the base directory. The service MUST also detect and reject null bytes, URL-encoded traversal sequences (`%2e%2e`), and UNC/network path prefixes.
- **FR-012**: `ChatService.SaveAttachmentAsync` MUST be updated to validate the resolved storage path against the `uploads/` base directory using `PathSanitizationService` before writing any file. Validation failure MUST throw an `InvalidOperationException` with `errorCode: "PATH_TRAVERSAL_BLOCKED"`.
- **FR-013**: The `/mcp/chat` and `/mcp/chat/stream` endpoints MUST enforce a configurable maximum request body size (default: 32 KB). Requests exceeding this limit MUST be rejected with HTTP 413 before JSON deserialization.
- **FR-014**: All file path parameters received in tool invocations MUST be validated via `PathSanitizationService` against a configurable base directory allowlist before file operations.
- **FR-015**: The `ScriptSanitizationService` MUST be integrated into the `PathSanitizationService` as a complementary layer — path validation covers file access boundaries while script sanitization covers content safety. Both are required for remediation workflows.

#### Performance Caching

- **FR-016**: A response-level caching layer MUST be introduced that caches tool responses by a composite key of: tool name, normalized input parameters (sorted and hashed), and subscription scope (Azure subscription ID from the user's configured environment). Users authorized to the same subscription share cached results; authorization is enforced before cache lookup via the existing CAC authentication and compliance authorization middleware. Cache entries MUST have configurable TTL (default: 15 minutes for assessments, 60 minutes for control lookups, no caching for mutations).
- **FR-017**: Cached responses MUST include `X-Cache` (HIT or MISS) and `X-Cache-Age` (seconds since cached) response headers on HTTP responses. SSE streaming responses MUST include cache status in the `complete` event metadata.
- **FR-018**: Mutation operations (remediation execution, configuration changes, finding status updates, evidence collection) MUST bypass the cache entirely and invalidate any related cached entries upon completion.
- **FR-019**: The caching layer MUST implement stale-while-revalidate semantics: when a cache entry is expired but a refresh is in progress, concurrent requests receive the stale value while the background refresh completes.
- **FR-020**: Cache entries MUST be evictable via an administrative action (tool call or configuration endpoint). A `ClearCache` tool MUST be registered that accepts an optional scope filter and reports the number of entries evicted.
- **FR-020a**: The existing `IMemoryCache` registration (`services.AddMemoryCache()`) MUST be augmented with a configurable size limit (default: 256 MB) and eviction callback that logs evicted entries at Debug level.

#### Enterprise Monitoring and Metrics

- **FR-021**: The MCP server MUST register an OpenTelemetry metrics exporter that connects the existing `System.Diagnostics.Metrics` instruments (`ToolMetrics`, `ComplianceMetricsService`) to a configurable backend. Default exporter: OTLP (OpenTelemetry Protocol). Optional: Prometheus scrape endpoint at `/metrics`, which MUST require the same CAC authentication as other MCP endpoints (not publicly accessible). Monitoring systems use authenticated scraping or OTLP push export to avoid exposing operational data.
- **FR-022**: New HTTP-level metrics MUST be added: `ato.copilot.http.request.duration` (histogram, tags: endpoint, method, status_code), `ato.copilot.http.request.total` (counter, tags: endpoint, method, status_code), `ato.copilot.cache.hits` (counter, tags: cache_name), `ato.copilot.cache.misses` (counter, tags: cache_name).
- **FR-023**: The `/health` endpoint response MUST be enriched to include: server uptime (time since process start), build version (from assembly metadata), total requests served since start, and individual health check response times.
- **FR-024**: Distributed tracing MUST be enabled via `System.Diagnostics.ActivitySource`. Each request MUST create a root `Activity` spanning the full request lifecycle. Tool executions MUST create child activities. W3C `traceparent` header MUST be propagated to downstream HTTP calls.
- **FR-025**: *(Consolidated into FR-044)* — See FR-044 under Structured Logging Enhancements for W3C TraceId and SpanId enrichment requirements.

#### Lazy-Loaded Parsing

- **FR-026**: Knowledge base JSON files MUST be loaded lazily — each file is parsed on first access via `Lazy<T>` initialization rather than all files being parsed at startup. The `NistControlsCacheWarmupService` continues to trigger warmup but defers individual file parsing until the specific dataset is requested.
- **FR-027**: Compliance assessment tool responses exceeding the configurable streaming threshold (default: 50 items) MUST use `IAsyncEnumerable<T>` internally to yield results incrementally. The MCP server MUST serialize yielded items into the SSE stream as `partial` events containing batches of findings.
- **FR-028**: POA&M and SSP document generation MUST use a streaming builder pattern that writes sections incrementally to the response stream rather than buffering the complete document in memory. Each section MUST be flushable independently.

#### Pagination Protection

- **FR-029**: All MCP tool responses returning collections MUST include a `PaginationInfo` envelope (using the existing `PaginationInfo` class from `ToolResponse.cs`) with `page`, `pageSize`, `totalItems`, `totalPages`, `hasNextPage`, and optional `nextPageToken`.
- **FR-030**: A configurable maximum page size (default: 100) MUST be enforced server-side. Client-requested page sizes exceeding the maximum MUST be clamped silently with a `metadata.pageSizeClamped: true` indicator.
- **FR-031**: The default page size (default: 50) MUST be configurable via `appsettings.json` (`Pagination:DefaultPageSize`) and overridable via environment variable.
- **FR-032**: The `/mcp/tools` endpoint MUST support pagination when the total tool count exceeds the configured page size. The default response MUST return the first page with pagination metadata.
- **FR-033**: Continuation token (cursor-based) pagination MUST be supported for stateful tools (kanban task lists, audit log queries) where offset-based pagination is inefficient. The `nextPageToken` field on `PaginationInfo` carries the opaque cursor.

#### IL6 Offline Support

- **FR-034**: A server-wide `OfflineMode` configuration flag MUST be introduced (`appsettings.json` `Server:OfflineMode`, environment variable `ATO_SERVER__OFFLINEMODE`). When enabled, the server MUST NOT make any outbound network calls. All operations requiring network access MUST return `errorCode: "OFFLINE_UNAVAILABLE"` with a list of online-dependent capabilities.
- **FR-035**: In offline mode, the server MUST serve all responses from local data sources: embedded OSCAL resources, locally cached assessments (persisted to disk or database), and static knowledge base files. The existing `EnableOfflineFallback` for NIST controls MUST be automatically activated.
- **FR-036**: Cached assessment data MUST be persistable to the local database (SQLite or SQL Server) so that data survives server restarts in offline mode. Each cached entry MUST include a `cachedAt` timestamp and `source` indicator (online/cached).
- **FR-037**: When transitioning from offline to online mode, the server MUST perform a background sync that refreshes stale cached data from live sources. Sync results MUST be logged at Info level with counts of refreshed, unchanged, and failed entries.
- **FR-038**: In offline mode, the `/health` endpoint MUST report `Degraded` status (not Unhealthy) with a list of unavailable capabilities and available offline capabilities.
- **FR-039**: All locally persisted data in offline/IL6 mode MUST be encrypted at rest using the platform's data protection API, consistent with FIPS 140-2 requirements.

#### Streaming Resilience

- **FR-040**: All SSE events emitted by `/mcp/chat/stream` MUST include an `id` field with a monotonically increasing integer, starting at 1 for each streaming session.
- **FR-041**: The server MUST maintain a bounded event buffer (default: 256 events, configurable) per active streaming session. When a client reconnects with `Last-Event-ID`, the server MUST replay events after the specified ID from the buffer.
- **FR-042**: When no SSE event is emitted for 15 seconds during an active stream, the server MUST send a keepalive comment (`: keepalive\n\n`) to prevent proxy/load-balancer timeout.
- **FR-043**: The event buffer MUST be evicted when the streaming session completes (after the `complete` event is sent) or after a configurable inactivity timeout (default: 60 seconds after disconnect).

#### Structured Logging Enhancements

- **FR-044**: All structured log events emitted during request processing MUST include the W3C `TraceId` and `SpanId` from `Activity.Current` alongside the existing `CorrelationId`, enabling correlation between Serilog logs and distributed traces.
- **FR-045**: A new `RequestMetricsMiddleware` MUST log a summary event at Information level upon request completion containing: endpoint, HTTP method, status code, processing time (ms), agent used, tools executed count, cache hit/miss, and correlation ID.

### Key Entities

- **ResiliencePipelineConfig**: Configuration for a named resilience pipeline — `name` (string), `maxRetryAttempts` (int, default 3), `baseDelaySeconds` (double, default 2.0), `useJitter` (bool, default true), `circuitBreakerFailureThreshold` (int, default 5), `circuitBreakerSamplingDurationSeconds` (int, default 30), `circuitBreakerBreakDurationSeconds` (int, default 30), `requestTimeoutSeconds` (int, default 30).
- **RateLimitPolicy**: Configuration for a named endpoint rate limit — `policyName` (string), `permitLimit` (int), `windowSeconds` (int), `segmentsPerWindow` (int), `endpoint` (string).
- **CachedResponse**: A cached tool response entry — `cacheKey` (string, composite of tool + params + scope), `response` (serialized result), `cachedAt` (timestamp), `ttlSeconds` (int), `source` ("online" or "cached"), `hitCount` (int).
- **OfflineCapability**: A descriptor for an offline-available operation — `capabilityName` (string), `requiresNetwork` (bool), `fallbackDescription` (string), `lastSyncedAt` (timestamp, null if never synced).

## Clarifications

### Session 2026-03-14

- Q: How many simultaneous MCP clients should the server support without degradation? → A: 50–100 concurrent clients (organization-wide shared service)
- Q: Should cached responses be isolated per-user, shared per-subscription, or globally shared across all users? → A: Per-subscription — users authorized to the same Azure subscription share cached results. Authorization is enforced at the Azure API layer before results enter the cache, and CAC authentication validates identity before cache lookup.
- Q: Should offline mode include a local AI model for chat processing, or be limited to deterministic operations only? → A: Deterministic only — offline mode serves cached/embedded data (control lookups, cached assessments, document generation). Chat queries requiring AI return `OFFLINE_UNAVAILABLE`. Local AI model integration is deferred to a future feature.
- Q: Should the `/metrics` endpoint require authentication or be accessible without credentials? → A: Authenticated — the `/metrics` endpoint requires the same CAC authentication as other MCP endpoints. Monitoring systems use authenticated scraping or OTLP push export.
- Q: What is the acceptable startup time impact from adding rate limiting, OpenTelemetry, and resilience pipelines? → A: No more than 2 seconds additional startup time, keeping total under the Constitution VIII 10-second target. Lazy-loaded parsing (FR-026) offsets new middleware registrations.

## Assumptions

- The server MUST support 50–100 concurrent clients without degradation. This organization-wide deployment target informs rate limit defaults (per-client, not global), SSE event buffer memory budget (256 events × up to 100 sessions ≈ 25 MB), and validates that the 512 MB memory ceiling is achievable with response caching only if cache size limits are enforced (FR-020a: 256 MB default).
- The existing `Microsoft.Extensions.Http.Resilience` (Polly 8.4.2) dependency is already in the project. This feature extends its use from the single NIST HttpClient to all HTTP clients.
- ASP.NET Core's built-in rate limiting middleware (`Microsoft.AspNetCore.RateLimiting`) is available in .NET 9 and does not require a new NuGet dependency. The `SlidingWindowRateLimiter` already used in `AlertNotificationService` validates the pattern.
- `System.Diagnostics.Metrics` is already used for instrumentation. This feature adds an exporter (OpenTelemetry OTLP or Prometheus) that requires a new NuGet reference (`OpenTelemetry.Exporter.OpenTelemetryProtocol` or `OpenTelemetry.Exporter.Prometheus.AspNetCore`).
- The `IMemoryCache` registered in `ServiceCollectionExtensions` is sufficient for performance caching. No distributed cache is needed because the MCP server is single-process per Constitution.
- Offline/IL6 mode is an additive capability. When `OfflineMode: false` (default), all existing behavior is unchanged.
- Offline mode is scoped to deterministic operations only (NIST control lookups from embedded OSCAL, cached assessment retrieval, document generation from cached data). Natural language chat queries requiring Azure AI processing are NOT available offline and return `OFFLINE_UNAVAILABLE`. Local/embedded AI model integration is out of scope for this feature and deferred to a future feature.
- The existing `SaveAttachmentAsync` path vulnerability is mitigated by GUID-based filename generation (the actual file name on disk is always a random GUID). The path sanitization improvement adds defense-in-depth rather than fixing an exploitable vulnerability.
- Pagination for tool responses requires changes to the `BaseTool` response handling in `McpServer.ProcessChatRequestAsync`. Individual tools already support pagination internally (kanban service) — this feature exposes it through the MCP response envelope.
- The SSE event buffer is per-session and in-memory. No cross-server session replay is needed because the MCP server runs as a single instance.
- Constitution VIII performance targets (5s simple, 30s complex, 512 MB memory, 10s startup) serve as the validation benchmarks for caching and lazy-loading improvements.
- The addition of rate limiting middleware, OpenTelemetry exporters, and resilience pipeline registrations MUST NOT increase server startup time by more than 2 seconds beyond the current baseline. Lazy-loaded parsing (FR-026) is expected to offset the new registrations, maintaining net startup time under the 10-second Constitution VIII ceiling.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 95% of requests to external dependencies (Azure AI, ARM, NIST API) that encounter transient errors (429, 503, timeout) succeed on automatic retry without user intervention.
- **SC-002**: Zero requests per hour are lost due to a single misbehaving client, verified by rate limiting enforcement at the configured threshold (default: 30 req/min per client for `/mcp/chat`). Server sustains 50–100 concurrent clients without degradation (p95 response time ≤5s for simple queries, error rate ≤1%).
- **SC-003**: 100% of file path inputs processed by the server are validated against base directory boundaries, with zero path traversal bypasses in penetration testing.
- **SC-004**: Repeated identical queries (same control lookup, same assessment scope within TTL) return in under 500 ms from cache, versus the uncached baseline.
- **SC-005**: All 6 existing metric instruments are exported to at least one monitoring backend (OTLP or Prometheus), with new HTTP-level metrics capturing per-endpoint latency distributions.
- **SC-006**: Peak memory usage during a 200-finding assessment stays under 512 MB with lazy parsing and streaming results, validated by diagnostic memory counters. Server startup time remains under 10 seconds (no more than 2 seconds increase from new middleware registrations).
- **SC-007**: 100% of tool responses returning collections include pagination metadata, with no unbounded result sets exceeding the configured maximum page size.
- **SC-008**: In offline mode, NIST control lookups and cached assessment queries succeed with zero network calls, verified by network traffic monitoring.
- **SC-009**: SSE streaming sessions recover from client disconnection within the buffer window (60 seconds) via `Last-Event-ID` reconnection, replaying missed events correctly.
- **SC-010**: All existing tests continue to pass (zero regressions) and new tests cover resilience, rate limiting, path sanitization, caching, pagination, offline mode, and streaming reconnection with 80%+ coverage on modified files.
