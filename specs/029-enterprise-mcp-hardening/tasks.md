# Tasks: Enterprise MCP Server Hardening

**Input**: Design documents from `/specs/029-enterprise-mcp-hardening/`
**Prerequisites**: plan.md (required), spec.md (required), research.md, data-model.md, contracts/

**Tests**: Included — SC-010 requires 80%+ coverage on modified files with tests covering all 9 capability areas.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions

---

## Phase 1: Setup

**Purpose**: NuGet packages, configuration models, and shared appsettings schema

- [x] T001 Add OpenTelemetry NuGet packages to src/Ato.Copilot.Mcp/Ato.Copilot.Mcp.csproj: `OpenTelemetry.Extensions.Hosting`, `OpenTelemetry.Exporter.OpenTelemetryProtocol`, `OpenTelemetry.Exporter.Prometheus.AspNetCore`, `OpenTelemetry.Instrumentation.AspNetCore`
- [x] T002 [P] Create `ResiliencePipelineConfig` configuration model in src/Ato.Copilot.Core/Models/ResiliencePipelineConfig.cs with fields: Name, MaxRetryAttempts (default 3), BaseDelaySeconds (default 2.0), UseJitter (default true), CircuitBreakerFailureThreshold (default 5), CircuitBreakerSamplingDurationSeconds (default 30), CircuitBreakerBreakDurationSeconds (default 30), RequestTimeoutSeconds (default 30). Include validation annotations and `ResilienceOptions` wrapper class
- [x] T003 [P] Create `RateLimitPolicy` and `RateLimitingOptions` configuration models in src/Ato.Copilot.Core/Models/RateLimitPolicy.cs with fields per data-model.md: PolicyName, Endpoint, PermitLimit (default 30), WindowSeconds (default 60), SegmentsPerWindow (default 2). `RateLimitingOptions` wraps a `List<RateLimitPolicy>` and `ExemptEndpoints`
- [x] T004 [P] Create `CachedResponse` model in src/Ato.Copilot.Core/Models/CachedResponse.cs with fields: Id, CacheKey, ToolName, Response (JSON string), CachedAt (DateTimeOffset), TtlSeconds (default 900), Source ("online"/"cached"), HitCount, SubscriptionId. Include `CachingOptions` wrapper class with SizeLimitMb, DefaultTtlSeconds, ControlLookupTtlSeconds, AssessmentTtlSeconds, EnableStaleWhileRevalidate
- [x] T005 [P] Create `OfflineCapability` model in src/Ato.Copilot.Core/Models/OfflineCapability.cs with fields: CapabilityName, RequiresNetwork, FallbackDescription, LastSyncedAt (nullable)
- [x] T005a [P] Create configuration option models: `PaginationOptions` in src/Ato.Copilot.Core/Models/PaginationOptions.cs (DefaultPageSize default 50, MaxPageSize default 100), `StreamingOptions` in src/Ato.Copilot.Core/Models/StreamingOptions.cs (EventBufferSize default 256, KeepaliveIntervalSeconds default 15, InactivityTimeoutSeconds default 60), `OpenTelemetryOptions` in src/Ato.Copilot.Core/Models/OpenTelemetryOptions.cs (ExporterType default "otlp", OtlpEndpoint, EnablePrometheus default false, ServiceName default "ato-copilot-mcp")
- [x] T006 Add new configuration sections to src/Ato.Copilot.Mcp/appsettings.json: `Resilience`, `RateLimiting`, `Caching`, `Pagination`, `Streaming`, `OpenTelemetry`, and new keys under existing `Server` section (`OfflineMode`, `MaxRequestBodySizeKb`) per contracts/config-schema.md

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core infrastructure that MUST be complete before ANY user story can be implemented

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [x] T007 Create `PathSanitizationService` interface `IPathSanitizationService` in src/Ato.Copilot.Core/Interfaces/IPathSanitizationService.cs with methods: `PathValidationResult ValidatePathWithinBase(string candidatePath, string baseDirectory)` returning a result with IsValid, Reason, CanonicalPath
- [x] T008 [P] Create `HttpMetrics` class in src/Ato.Copilot.Core/Observability/HttpMetrics.cs. Define a new `Meter("ato.copilot.http")` with instruments: `ato.copilot.http.request.duration` (histogram), `ato.copilot.http.request.total` (counter), `ato.copilot.cache.hits` (counter), `ato.copilot.cache.misses` (counter) per FR-022
- [x] T009 Register new configuration options in src/Ato.Copilot.Core/Extensions/CoreServiceExtensions.cs: bind `ResilienceOptions`, `RateLimitingOptions`, `CachingOptions`, `PaginationOptions`, `StreamingOptions`, `OpenTelemetryOptions` from `IConfiguration`. Register `IPathSanitizationService`, `HttpMetrics` as singletons
- [x] T010 Augment `IMemoryCache` registration in src/Ato.Copilot.Core/Extensions/CoreServiceExtensions.cs: add `MemoryCacheOptions.SizeLimit` (from `CachingOptions.SizeLimitMb` × 1024 × 1024) and eviction callback that logs at Debug level per FR-020a

**Checkpoint**: Foundation ready — configuration models bound, services registered, metrics instruments created

---

## Phase 3: User Story 1 — Resilient Retry Logic (Priority: P1) 🎯 MVP

**Goal**: All HTTP clients have retry + circuit breaker + timeout policies. Transient failures auto-recover without user intervention.

**Independent Test**: Mock HttpClient returns 503 twice then 200 → request succeeds with 3 logged retries.

### Tests for User Story 1

- [x] T011 [P] [US1] Create unit tests in tests/Ato.Copilot.Tests.Unit/Resilience/ResiliencePipelineTests.cs: test retry on 503 (3 attempts, exponential backoff), test Retry-After header honored, test circuit breaker opens after threshold, test timeout cancellation, test structured error codes (DEPENDENCY_CIRCUIT_OPEN, REQUEST_TIMEOUT, RETRY_AFTER_EXCEEDS_BUDGET)
- [x] T012 [P] [US1] Create integration tests in tests/Ato.Copilot.Tests.Integration/Resilience/RetryIntegrationTests.cs using WebApplicationFactory with mock DelegatingHandler: end-to-end retry verification, circuit breaker state transition logging

### Implementation for User Story 1

- [x] T013 [US1] Create shared resilience pipeline builder extension method `ConfigureResiliencePipeline(this IHttpClientBuilder builder, ResiliencePipelineConfig config)` in src/Ato.Copilot.Agents/Extensions/ServiceCollectionExtensions.cs. Pipeline order: Retry (with Retry-After support via ShouldRetryAfterHeader) → Circuit Breaker → Timeout. Log all retry attempts, circuit state transitions, and timeouts at Warning level with dependency name, attempt number, delay, status code, and correlation ID per FR-001 through FR-005
- [x] T014 [US1] Refactor existing NistControlsService HttpClient registration in src/Ato.Copilot.Agents/Extensions/ServiceCollectionExtensions.cs to use the shared `ConfigureResiliencePipeline` method with default config (3 retries, 2s base delay) per FR-005a. Verify existing NIST retry behavior is preserved
- [x] T015 [P] [US1] Apply shared resilience pipeline to `McpServer` named HttpClient in src/Ato.Copilot.Chat/Program.cs (currently line 85, 180s timeout, no resilience). Use default pipeline config with RequestTimeoutSeconds matching the existing 180s timeout
- [x] T016 [P] [US1] Apply shared resilience pipeline to generic HttpClient factory in src/Ato.Copilot.Core/Extensions/CoreServiceExtensions.cs per FR-001

**Checkpoint**: All 3 HTTP clients have retry + circuit breaker + timeout. Tests verify 503→retry→success and circuit breaker open→fast-fail.

---

## Phase 4: User Story 2 — API Rate Limiting (Priority: P1)

**Goal**: Per-endpoint rate limits prevent a single client from overwhelming the server or exhausting Azure API quotas.

**Independent Test**: Send 31 requests to `/mcp/chat` in 60s → request 31 returns HTTP 429 with `Retry-After` header.

### Tests for User Story 2

- [x] T017 [P] [US2] Create unit tests in tests/Ato.Copilot.Tests.Unit/Middleware/RateLimitingTests.cs: test sliding window enforcement, test per-client isolation (different clients have independent limits), test endpoint exemption (/health, /mcp/tools are not limited), test structured 429 response body with ErrorDetail, test environment variable override of limits
- [x] T018 [P] [US2] Create integration tests in tests/Ato.Copilot.Tests.Integration/McpEndpoints/RateLimitIntegrationTests.cs using WebApplicationFactory: send 31 rapid requests to `/mcp/chat`, verify 31st returns 429 with Retry-After header, verify /health is exempt

### Implementation for User Story 2

- [x] T019 [US2] Register ASP.NET Core rate limiting middleware in src/Ato.Copilot.Mcp/Program.cs: call `builder.Services.AddRateLimiter()` with named policies from `RateLimitingOptions` configuration. Each policy uses `SlidingWindowRateLimiter` with PermitLimit, Window, and SegmentsPerWindow from config. Client partitioning by authenticated user identity (CAC NameIdentifier claim) with IP address fallback per FR-006, FR-008, FR-010a
- [x] T020 [US2] Add `app.UseRateLimiter()` to middleware pipeline in src/Ato.Copilot.Mcp/Program.cs — positioned after `UseCors()` and before `CacAuthenticationMiddleware` per contracts/mcp-endpoints.md middleware order. Apply named policies to endpoints: `.RequireRateLimiting("chat")` on `/mcp/chat`, `.RequireRateLimiting("stream")` on `/mcp/chat/stream`, `.RequireRateLimiting("jsonrpc")` on `/mcp`. Mark `/health` and `/mcp/tools` with `.DisableRateLimiting()` per FR-007
- [x] T021 [US2] Implement rate-limit rejected response handler in src/Ato.Copilot.Mcp/Program.cs `OnRejected` callback: return HTTP 429 with `Retry-After` header and `ErrorDetail` body (`errorCode: "RATE_LIMITED"`, message with endpoint and limit, suggestion to reduce frequency). Log at Warning level with client identifier, endpoint, and window utilization per FR-009

**Checkpoint**: All chat/stream/JSON-RPC endpoints enforce per-client rate limits. Health and tools endpoints are exempt.

---

## Phase 5: User Story 3 — Path Sanitization & Input Validation (Priority: P1)

**Goal**: All file path inputs validated against base directories. Oversized payloads rejected before processing.

**Independent Test**: `../../../etc/passwd` filename rejected with `PATH_TRAVERSAL_BLOCKED`; 33 KB body returns HTTP 413.

### Tests for User Story 3

- [x] T022 [P] [US3] Create unit tests in tests/Ato.Copilot.Tests.Unit/Services/PathSanitizationServiceTests.cs: test `../` traversal rejection, test `%2e%2e%2f` URL-encoded traversal, test null byte rejection, test UNC path rejection (`\\server\share`), test valid path acceptance, test cross-platform path separators, test path within base directory passes validation
- [x] T023 [P] [US3] Create unit tests in tests/Ato.Copilot.Tests.Unit/Middleware/RequestSizeLimitTests.cs: test 32 KB body accepted, test 33 KB body returns 413, test configurable limit via options, test non-chat endpoints not affected

### Implementation for User Story 3

- [x] T024 [US3] Implement `PathSanitizationService` in src/Ato.Copilot.Core/Services/PathSanitizationService.cs: `ValidatePathWithinBase()` canonicalizes both paths via `Path.GetFullPath()`, checks `StartsWith()` containment, detects null bytes, URL-decodes then re-validates, rejects UNC prefixes. Returns `PathValidationResult` with IsValid, Reason, CanonicalPath per FR-011
- [x] T025 [US3] Update `ChatService.SaveAttachmentAsync` in src/Ato.Copilot.Chat/Services/ChatService.cs: inject `IPathSanitizationService`, validate resolved storage path against `uploads/` base directory before writing. Throw `InvalidOperationException` with `errorCode: "PATH_TRAVERSAL_BLOCKED"` on failure per FR-012
- [x] T026 [P] [US3] Create `RequestSizeLimitMiddleware` in src/Ato.Copilot.Mcp/Middleware/RequestSizeLimitMiddleware.cs: read `Server:MaxRequestBodySizeKb` from options (default 32), reject requests with `Content-Length` exceeding limit with HTTP 413. Apply to `/mcp/chat` and `/mcp/chat/stream` routes per FR-013
- [x] T027 [US3] Register `RequestSizeLimitMiddleware` in src/Ato.Copilot.Mcp/Program.cs middleware pipeline — after `UseRateLimiter()`, before `CacAuthenticationMiddleware` per middleware order
- [x] T027a [US3] Integrate `PathSanitizationService` validation into `McpServer.ProcessChatRequestAsync` in src/Ato.Copilot.Mcp/Server/McpServer.cs: for all tool invocations that accept file path parameters, validate paths against the configurable base directory allowlist before executing the tool per FR-014
- [x] T027b [US3] Integrate `ScriptSanitizationService` as a complementary validation layer alongside `PathSanitizationService` in src/Ato.Copilot.Core/Services/PathSanitizationService.cs: path validation covers file access boundaries while script sanitization covers content safety. Both are required for remediation workflows per FR-015

**Checkpoint**: Path traversal attacks blocked on all file operations. Tool invocation paths validated. Oversized payloads rejected at middleware layer.

---

## Phase 6: User Story 4 — Performance Caching (Priority: P2)

**Goal**: Repeated queries return from cache in <500ms. Per-subscription scope. Mutations bypass and invalidate cache.

**Independent Test**: Same control lookup twice in 30s → second returns `X-Cache: HIT` in <500ms.

### Tests for User Story 4

- [x] T028 [P] [US4] Create unit tests in tests/Ato.Copilot.Tests.Unit/Services/ResponseCacheServiceTests.cs: test cache miss on first request, test cache hit on second identical request, test TTL expiration triggers fresh request, test mutation bypasses cache, test mutation invalidates related entries, test stale-while-revalidate serves stale during refresh, test composite key includes subscription ID, test cache size limit eviction, test ClearCache by scope filter
- [x] T029 [P] [US4] Create integration tests in tests/Ato.Copilot.Tests.Integration/McpEndpoints/CacheIntegrationTests.cs: verify `X-Cache: HIT/MISS` headers on `/mcp/chat`, verify `X-Cache-Age` header present on hits, verify different subscription IDs have independent cache entries

### Implementation for User Story 4

- [x] T030 [US4] Implement `ResponseCacheService` in src/Ato.Copilot.Core/Services/ResponseCacheService.cs: wrap `IMemoryCache`, composite key via `SHA256(toolName:sortedParamsJson:subscriptionId)`, TTL registry per tool category (15min assessments, 60min lookups, never for mutations), `SemaphoreSlim` per key for stale-while-revalidate, `HitCount` tracking, inject `HttpMetrics` for cache hit/miss counters per FR-016, FR-019
- [x] T031 [US4] Integrate `ResponseCacheService` into `McpServer.ProcessChatRequestAsync` in src/Ato.Copilot.Mcp/Server/McpServer.cs: check cache before agent dispatch (using tool name + params + subscription from request context), store result after successful execution, skip cache for mutation operations per FR-018. Add `cacheStatus` and `cacheAge` to `McpChatResponse.Metadata` dictionary
- [x] T032 [US4] Add `X-Cache` and `X-Cache-Age` response headers in src/Ato.Copilot.Mcp/Server/McpHttpBridge.cs `HandleChatRequestAsync`: read cache status from `McpChatResponse.Metadata` and set HTTP headers per FR-017. For streaming responses, include cache status in the `complete` event metadata
- [x] T033 [US4] Create `ClearCacheTool` extending `BaseTool` in src/Ato.Copilot.Agents/Tools/ClearCacheTool.cs: accepts optional scope filter (subscription, tool name), calls `ResponseCacheService.ClearByScope()`, returns count of evicted entries per FR-020. Register tool via `RegisterTool()` in the appropriate agent constructor

**Checkpoint**: Repeated lookups served from cache. Cache headers visible. Mutations invalidate. Admin can clear cache.

---

## Phase 7: User Story 5 — Enterprise Monitoring & Metrics (Priority: P2)

**Goal**: All 6+ metric instruments exported via OTLP. Distributed tracing spans request lifecycle. Enhanced health endpoint.

**Independent Test**: Send 10 requests, query `/metrics` endpoint → counters reflect traffic.

### Tests for User Story 5

- [x] T034 [P] [US5] Create unit tests in tests/Ato.Copilot.Tests.Unit/Observability/HttpMetricsTests.cs: verify `ato.copilot.http.request.duration` histogram records values, verify `ato.copilot.http.request.total` counter increments, verify tag dimensions (endpoint, method, status_code) applied correctly, verify cache hit/miss counters increment
- [x] T035 [P] [US5] Create unit tests for `RequestMetricsMiddleware` in tests/Ato.Copilot.Tests.Unit/Middleware/RequestMetricsMiddlewareTests.cs: verify Information-level log emitted on request completion with endpoint, method, status code, processing time, agent used, tools count, cache hit/miss, and correlation ID per FR-045

### Implementation for User Story 5

- [x] T036 [US5] Register OpenTelemetry in src/Ato.Copilot.Mcp/Program.cs: `AddOpenTelemetry().WithMetrics()` connecting existing `Meter` instances (`ato.copilot.tools`, `ato.copilot.compliance`) and new `ato.copilot.http`. Add `.WithTracing()` with `ActivitySource("Ato.Copilot.Mcp")`. Configure OTLP exporter from `OpenTelemetryOptions`. Optionally register Prometheus scrape endpoint at `/metrics` behind CAC auth per FR-021. When Prometheus exporter is disabled, requests to `/metrics` MUST return HTTP 404 with a structured error suggesting configuration steps
- [x] T037 [US5] Create `RequestMetricsMiddleware` in src/Ato.Copilot.Mcp/Middleware/RequestMetricsMiddleware.cs: record start time, invoke next, record `HttpMetrics` histogram and counter on completion. Log summary event at Information level per FR-045. Register in pipeline after `ComplianceAuthorizationMiddleware` per middleware order
- [x] T038 [US5] Enable distributed tracing in src/Ato.Copilot.Mcp/Server/McpServer.cs: create `ActivitySource("Ato.Copilot.Mcp")`, start root `Activity` in `ProcessChatRequestAsync`, create child activities for each tool execution. Propagate W3C `traceparent` to downstream HTTP calls per FR-024
- [x] T039 [US5] Enrich `CorrelationIdMiddleware` in src/Ato.Copilot.Core/Observability/CorrelationIdMiddleware.cs: add `Activity.Current?.TraceId` and `SpanId` to Serilog LogContext alongside existing `CorrelationId` per FR-044
- [x] T040 [US5] Enhance `/health` endpoint in src/Ato.Copilot.Mcp/Server/McpHttpBridge.cs `HandleHealthAsync`: add server uptime (from `Process.GetCurrentProcess().StartTime`), build version (from `Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>`), total requests served (from `HttpMetrics` counter), individual check durations per FR-023

**Checkpoint**: Metrics exported to OTLP/Prometheus. Traces span full request lifecycle. Health endpoint enriched.

---

## Phase 8: User Story 6 — Lazy-Loaded Parsing (Priority: P2)

**Goal**: Knowledge base files parsed on first access. Large assessments streamed incrementally. Memory under 512 MB.

**Independent Test**: Start server, first KB access triggers parse. 200-finding assessment streams `partial` events within 5s.

### Tests for User Story 6

- [x] T041 [P] [US6] Create unit tests for lazy loading in tests/Ato.Copilot.Tests.Unit/Services/LazyKnowledgeBaseTests.cs: verify JSON files not deserialized until first access, verify thread-safe Lazy<T> initialization, verify warmup service triggers lazy init but doesn't eagerly parse all sub-datasets
- [x] T041a [P] [US6] Create integration tests in tests/Ato.Copilot.Tests.Integration/McpEndpoints/LazyLoadingIntegrationTests.cs using WebApplicationFactory: verify streaming assessment via `/mcp/chat/stream` emits `partial` events within 5s for large result sets, verify peak memory stays within budget

### Implementation for User Story 6

- [x] T042 [US6] Refactor `NistControlsService` in src/Ato.Copilot.Agents/Compliance/Services/NistControlsService.cs: wrap catalog deserialization in `Lazy<Task<NistCatalogRoot>>` per dataset. `LoadAndCacheCatalogAsync` triggers lazy init on access. `NistControlsCacheWarmupService` continues to call `GetCatalogAsync()` which triggers init but individual sub-datasets defer parsing per FR-026
- [x] T043 [P] [US6] Refactor knowledge base file loading in src/Ato.Copilot.Agents/KnowledgeBase/Services/KnowledgeBaseService.cs: wrap each of the 7 JSON files in `Lazy<T>` with `LazyThreadSafetyMode.ExecutionAndPublication`. Files parsed on first access instead of eagerly per FR-026
- [x] T044 [US6] Implement `IAsyncEnumerable<T>` streaming for compliance assessment tools: modify compliance assessment tool response to yield `ComplianceFinding` items incrementally when count exceeds streaming threshold (default 50). Update `McpServer` to serialize yielded items into SSE `partial` events per FR-027
- [x] T045 [US6] Implement streaming builder for POA&M/SSP generation: modify document generation to write sections incrementally to the response stream, flushing each section independently per FR-028

**Checkpoint**: KB files lazy-loaded. Large assessments streamed. Memory stays under 512 MB for 200-finding scans.

---

## Phase 9: User Story 7 — Pagination Protection (Priority: P2)

**Goal**: All collection responses paginated. No unbounded result sets. Existing `PaginationInfo` envelope enforced.

**Independent Test**: Request 200 findings → response contains 50 items with `hasNextPage: true`.

### Tests for User Story 7

- [x] T046 [P] [US7] Create integration tests in tests/Ato.Copilot.Tests.Integration/McpEndpoints/PaginationIntegrationTests.cs: verify default page size (50) applied when no param, verify maxPageSize (100) clamped with `metadata.pageSizeClamped: true`, verify `nextPageToken` returns next page, verify `pageSize: 0` clamped to 1, verify `/mcp/tools` paginated

### Implementation for User Story 7

- [x] T047 [US7] Implement server-side pagination enforcement in src/Ato.Copilot.Mcp/Server/McpServer.cs `ProcessChatRequestAsync`: after tool execution, if `response.Data` is `IEnumerable` with count > configured page size, slice to requested page + populate `PaginationInfo` (page, pageSize, totalItems, totalPages, hasNextPage, nextPageToken as base64 offset). Clamp pageSize to maxPageSize with `metadata.pageSizeClamped: true`. Read defaults from `PaginationOptions` per FR-029, FR-030, FR-031
- [x] T048 [US7] Add pagination support to `/mcp/tools` endpoint in src/Ato.Copilot.Mcp/Server/McpHttpBridge.cs `HandleToolsListAsync`: accept `page` and `pageSize` query parameters, paginate tool list when count exceeds page size, return `PaginationInfo` envelope per FR-032
- [x] T049 [US7] Implement cursor-based pagination for stateful tools in src/Ato.Copilot.Mcp/Server/McpServer.cs: for tools returning `PagedResult<T>` (kanban, audit logs), map `PagedResult.HasMore` to `PaginationInfo.HasNextPage` and generate opaque `nextPageToken` cursor per FR-033

**Checkpoint**: All collection responses bounded. Pagination metadata in every response. No unbounded result sets.

---

## Phase 10: User Story 8 — IL6 Offline Mode (Priority: P3)

**Goal**: Server operates without network. Deterministic ops from embedded/cached data. AI-dependent ops return `OFFLINE_UNAVAILABLE`.

**Independent Test**: Set `ATO_SERVER__OFFLINEMODE=true` → control lookups succeed, chat returns `OFFLINE_UNAVAILABLE`.

### Tests for User Story 8

- [x] T050 [P] [US8] Create unit tests in tests/Ato.Copilot.Tests.Unit/Services/OfflineModeServiceTests.cs: test `IsOffline` flag reads config, test `GetAvailableCapabilities()` returns deterministic operations, test `GetUnavailableCapabilities()` returns AI-dependent ops, test `NistControls:EnableOfflineFallback` auto-forced when offline
- [x] T050a [P] [US8] Create integration tests in tests/Ato.Copilot.Tests.Integration/McpEndpoints/OfflineModeIntegrationTests.cs using WebApplicationFactory with `OfflineMode=true`: verify NIST control lookups succeed without network, verify AI-dependent operations return `OFFLINE_UNAVAILABLE`, verify `/health` reports `Degraded` status

### Implementation for User Story 8

- [x] T051 [US8] Implement `OfflineModeService` in src/Ato.Copilot.Core/Services/OfflineModeService.cs: singleton reading `Server:OfflineMode` from `IOptions`. Expose `IsOffline`, `GetAvailableCapabilities()`, `GetUnavailableCapabilities()`. Register static `OfflineCapability` list (control lookups: offline, cached assessments: offline, doc generation: offline, AI chat: online, ARM scan: online, live assessment: online) per FR-034
- [x] T052 [US8] Integrate offline guard in src/Ato.Copilot.Mcp/Server/McpServer.cs `ProcessChatRequestAsync`: when `OfflineModeService.IsOffline` and the operation requires network, return `ErrorDetail` with `errorCode: "OFFLINE_UNAVAILABLE"` and suggestion listing available offline capabilities. Force `NistControls:EnableOfflineFallback = true` per FR-035
- [x] T053 [US8] Create `CacheRepository` in src/Ato.Copilot.State/Repositories/CacheRepository.cs: EF Core entity mapping for `CachedResponse` to `CachedResponses` table (unique index on CacheKey, non-unique on ToolName+SubscriptionId). Implement `SaveAsync`, `GetByKeyAsync`, `GetStaleEntriesAsync`, `DeleteByScopeAsync`. Use Data Protection API for at-rest encryption per FR-036, FR-039
- [x] T054 [US8] Add EF Core migration for `CachedResponses` table in src/Ato.Copilot.State/ — create migration with `CacheKey` (nvarchar 256, unique), `ToolName`, `Response` (nvarchar max), `CachedAt`, `TtlSeconds`, `Source`, `HitCount`, `SubscriptionId`
- [x] T055 [US8] Implement offline-to-online sync in src/Ato.Copilot.Core/Services/OfflineModeService.cs: when mode transitions from offline to online, perform background sync refreshing stale cached data. Log at Info level with counts of refreshed, unchanged, and failed entries per FR-037
- [x] T056 [US8] Update `/health` endpoint in src/Ato.Copilot.Mcp/Server/McpHttpBridge.cs: when offline, return `Degraded` status with `availableCapabilities` and `unavailableCapabilities` arrays per FR-038

**Checkpoint**: Offline mode gates network calls. NIST lookups work offline. Cached data persisted. Sync on reconnect.

---

## Phase 11: User Story 9 — Streaming Resilience & Reconnection (Priority: P3)

**Goal**: SSE events have IDs. Client reconnects via `Last-Event-ID`. Keepalive prevents proxy timeout.

**Independent Test**: Start stream → close after 3 events → reconnect with `Last-Event-ID: 3` → missed events replayed.

### Tests for User Story 9

- [x] T057 [P] [US9] Create unit tests in tests/Ato.Copilot.Tests.Unit/Resilience/SseEventBufferTests.cs: test monotonic ID assignment, test buffer stores events up to max size (256), test oldest events evicted when buffer full, test replay returns events after specified ID, test buffer cleanup after session complete, test cleanup after inactivity timeout (60s), test keepalive timer fires after 15s inactivity
- [x] T057a [P] [US9] Create integration tests in tests/Ato.Copilot.Tests.Integration/McpEndpoints/SseReconnectionIntegrationTests.cs using WebApplicationFactory: start streaming request, capture event IDs, reconnect with `Last-Event-ID` header, verify missed events replayed in order before live events

### Implementation for User Story 9

- [x] T058 [US9] Implement `SseEventBuffer` in src/Ato.Copilot.Mcp/Resilience/SseEventBuffer.cs: `ConcurrentDictionary<string, SessionBuffer>` keyed by conversation ID. Each `SessionBuffer` has `ConcurrentQueue<SseEvent>` with max size from `StreamingOptions.EventBufferSize` (default 256), monotonic ID counter, last-activity timestamp, keepalive `Timer` per FR-041, FR-043
- [x] T059 [US9] Update `HandleChatStreamRequestAsync` in src/Ato.Copilot.Mcp/Server/McpHttpBridge.cs: write `id: N\n` before each `data:` line per FR-040. Buffer each event in `SseEventBuffer`. On `Last-Event-ID` header, replay events from buffer before starting live stream. Start keepalive timer sending `: keepalive\n\n` every 15s of inactivity per FR-042. Evict buffer after `complete` event or 60s post-disconnect per FR-043
- [x] T060 [US9] Register `SseEventBuffer` as singleton in src/Ato.Copilot.Mcp/Program.cs and inject into `McpHttpBridge`

**Checkpoint**: SSE events have IDs. Reconnection replays from buffer. Keepalive prevents proxy timeouts.

---

## Phase 12: Polish & Cross-Cutting Concerns

**Purpose**: Documentation, validation, and final integration

### Documentation Updates

- [x] T061 [P] Update docs/api/mcp-server.md with new endpoint behaviors: rate limiting responses (429 + Retry-After), cache headers (X-Cache, X-Cache-Age), pagination parameters and PaginationInfo envelope, offline mode responses (OFFLINE_UNAVAILABLE), SSE reconnection protocol (event IDs, Last-Event-ID, keepalive), /metrics endpoint (authenticated, OTLP/Prometheus)
- [x] T062 [P] Update docs/architecture/security.md with path sanitization approach (PathSanitizationService + Path.GetFullPath canonicalization), rate limiting architecture (per-endpoint sliding window, per-client partitioning), input validation layer (RequestSizeLimitMiddleware, 32KB limit), ScriptSanitizationService integration for remediation workflows
- [x] T062a [P] Update docs/architecture/overview.md with enterprise hardening layer: resilience pipelines across all HTTP clients, caching architecture (IMemoryCache + per-subscription scope), OpenTelemetry integration, offline mode architecture
- [x] T062b [P] Update docs/architecture/data-model.md with new entities: ResiliencePipelineConfig, RateLimitPolicy, CachedResponse (including EF Core mapping for persistent cache), OfflineCapability
- [x] T062c [P] Update docs/dev/contributing.md with new configuration sections (Resilience, RateLimiting, Caching, Pagination, Streaming, OpenTelemetry), environment variable override patterns, and new NuGet package dependencies
- [x] T062d [P] Update docs/getting-started/engineer.md with new MCP server capabilities: resilience configuration, cache management (ClearCache tool), offline mode setup, monitoring endpoint access, pagination query parameters
- [x] T062e [P] Update docs/guides/engineer-guide.md with enterprise hardening operational guidance: configuring rate limits for deployment, setting up OpenTelemetry/Prometheus monitoring, enabling IL6 offline mode, interpreting cache headers and metrics
- [x] T062f [P] Create or update docs/release-notes/ entry for Feature 029 summarizing all 9 capability areas with migration notes for any breaking changes (e.g., paginated responses for previously unbounded endpoints)

### Validation
- [x] T063 Run `dotnet build Ato.Copilot.sln --warnaserrors` and fix all warnings in modified files
- [x] T064 Run `dotnet test Ato.Copilot.sln` and verify all existing + new tests pass (zero regressions per SC-010)
- [x] T065 Run quickstart.md validation: execute each verification command from specs/029-enterprise-mcp-hardening/quickstart.md and confirm expected outcomes

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — can start immediately
- **Foundational (Phase 2)**: Depends on Phase 1 completion — BLOCKS all user stories
- **US1 Resilience (Phase 3)**: Depends on Phase 2 (config models, service registration)
- **US2 Rate Limiting (Phase 4)**: Depends on Phase 2 (config models)
- **US3 Path Sanitization (Phase 5)**: Depends on Phase 2 (IPathSanitizationService interface)
- **US4 Caching (Phase 6)**: Depends on Phase 2 (CachingOptions, HttpMetrics). Benefits from US5 metrics but not required
- **US5 Monitoring (Phase 7)**: Depends on Phase 2 (HttpMetrics instruments)
- **US6 Lazy Loading (Phase 8)**: Depends on Phase 2 only — independent of other stories
- **US7 Pagination (Phase 9)**: Depends on Phase 2 (PaginationOptions)
- **US8 Offline (Phase 10)**: Depends on Phase 2 + benefits from US4 caching (persistent cache). Can implement CacheRepository independently
- **US9 SSE Reconnection (Phase 11)**: Depends on Phase 2 (StreamingOptions). Independent of all other stories
- **Polish (Phase 12)**: Depends on all desired user stories being complete

### User Story Dependencies

```
Phase 1 (Setup)
    └── Phase 2 (Foundational) ─── BLOCKS ALL ───┐
         ├── US1 (Resilience)  ─── P1 ───┐       │
         ├── US2 (Rate Limiting) ─ P1 ───┤       │
         ├── US3 (Path Sanitization) P1 ─┤       │
         ├── US4 (Caching) ──── P2 ──────┤       │
         ├── US5 (Monitoring) ─ P2 ──────┤       │
         ├── US6 (Lazy Loading) P2 ──────┤       │
         ├── US7 (Pagination) ─ P2 ──────┤       │
         ├── US8 (Offline) ──── P3 ──────┤       │
         └── US9 (SSE Reconnect) P3 ─────┘       │
              └── Phase 12 (Polish) ─────────────┘
```

- **No inter-story dependencies**: All 9 user stories can execute in parallel after Phase 2
- **Recommended order**: P1 stories first (US1→US2→US3), then P2 (US4→US5→US6→US7), then P3 (US8→US9)

### Within Each User Story

- Tests MUST be written and FAIL before implementation
- Models/interfaces before services
- Services before middleware/endpoint integration
- Core implementation before integration points

### Parallel Opportunities

**Phase 1 (all [P] tasks):** T002, T003, T004, T005, T005a can run in parallel (different files)

**Phase 2:** T008 can run in parallel with T007, T009, T010

**P1 Stories (after Phase 2):** US1, US2, US3 touch different files and can proceed in parallel:
- US1 modifies `ServiceCollectionExtensions.cs`, `Program.cs` (Chat)
- US2 modifies `Program.cs` (Mcp)
- US3 creates new `PathSanitizationService`, modifies `ChatService.cs`, integrates tool-level path validation

**P2 Stories:** US4, US5, US6, US7 can all proceed in parallel:
- US4 creates `ResponseCacheService`, modifies `McpServer.cs`
- US5 modifies `Program.cs` (OTel), creates `RequestMetricsMiddleware`
- US6 modifies `NistControlsService`, `KnowledgeBaseService`
- US7 modifies `McpServer.cs` (shared with US4 — sequence US4 before US7)

**P3 Stories:** US8 and US9 touch completely different files — fully parallel:
- US8 creates `OfflineModeService`, `CacheRepository`
- US9 creates `SseEventBuffer`, modifies `McpHttpBridge.cs`

---

## Implementation Strategy

### MVP First (P1 Stories Only)

1. Complete Phase 1: Setup (T001–T006)
2. Complete Phase 2: Foundational (T007–T010)
3. Complete Phase 3: US1 Resilience (T011–T016)
4. Complete Phase 4: US2 Rate Limiting (T017–T021)
5. Complete Phase 5: US3 Path Sanitization (T022–T027b)
6. **STOP and VALIDATE**: All P1 capabilities functional — server retries, rate-limits, and validates input

### Incremental Delivery

1. **Increment 1 (P1)**: Resilience + Rate Limiting + Path Sanitization → core security hardening
2. **Increment 2 (P2)**: Caching + Monitoring + Lazy Loading + Pagination → performance and visibility
3. **Increment 3 (P3)**: Offline Mode + SSE Reconnection → advanced resilience
4. **Polish**: Documentation, final validation, quickstart verification

### Parallel Team Strategy

With 3 developers after Phase 2 completion:
- **Developer A**: US1 → US4 → US8 (resilience → caching → offline)
- **Developer B**: US2 → US5 → US9 (rate limiting → monitoring → SSE)
- **Developer C**: US3 → US6 → US7 (security → performance → pagination)

---

## Notes

- [P] tasks = different files, no dependencies
- [Story] label maps task to specific user story for traceability
- Each user story is independently completable and testable
- Verify tests fail before implementing
- Commit after each task or logical group
- Run `dotnet build --warnaserrors` after each phase
- Run `dotnet test` after each user story completion
