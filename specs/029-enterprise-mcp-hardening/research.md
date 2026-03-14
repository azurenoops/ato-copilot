# Research: Enterprise MCP Server Hardening

**Feature**: 029-enterprise-mcp-hardening  
**Date**: 2026-03-14

## R1: Resilience Pipeline Strategy

**Decision**: Extend the existing `Microsoft.Extensions.Http.Resilience` (Polly 8.4.2) configuration from the NIST HttpClient to all named HTTP clients using a shared pipeline builder method.

**Rationale**: The NIST client already uses `AddResilienceHandler()` with `HttpRetryStrategyOptions` (3 retries, 2s exponential backoff, jitter). This is the standard .NET 9 pattern. Extending it avoids introducing a second resilience library and keeps configuration consistent.

**Alternatives Considered**:
- **Raw `HttpClient` retry loops**: Rejected — no circuit breaker support, duplicated logic across clients.
- **Third-party library (e.g., Refit with Polly)**: Rejected — adds unnecessary dependency when `Microsoft.Extensions.Http.Resilience` already wraps Polly 8.x and is referenced in `Ato.Copilot.Core.csproj`.

**Implementation Approach**:
- Create a shared `ConfigureResiliencePipeline(IHttpClientBuilder, ResiliencePipelineConfig)` extension method.
- Apply to all 3 named clients: `NistControlsService`, `McpServer`, generic factory.
- Pipeline order: Retry → Circuit Breaker → Timeout (outer to inner per Polly best practices).
- `Retry-After` header handling via `HttpRetryStrategyOptions.ShouldRetryAfterHeader = true`.
- Circuit breaker uses `CircuitBreakerStrategyOptions<HttpResponseMessage>` with configurable thresholds.

**Key Finding**: The `McpServer` client in `Ato.Copilot.Chat/Program.cs` (line 85) has a 180s timeout but no resilience handler. This is the highest-risk client as it proxies all chat requests.

---

## R2: ASP.NET Core Rate Limiting Middleware

**Decision**: Use ASP.NET Core's built-in `Microsoft.AspNetCore.RateLimiting` middleware with named policies per endpoint, using the `SlidingWindowRateLimiter` algorithm.

**Rationale**: .NET 9 includes rate limiting middleware natively (`AddRateLimiter()` / `UseRateLimiter()`). No additional NuGet package needed. The `SlidingWindowRateLimiter` is already proven in `AlertNotificationService` (10/min/channel). Using the same algorithm at the middleware level provides consistency.

**Alternatives Considered**:
- **Custom middleware with `SemaphoreSlim`**: Rejected — reinvents what the framework already provides. No per-client tracking.
- **Reverse proxy rate limiting (NGINX/APIM)**: Not rejected but out of scope — application-level rate limiting provides defense-in-depth independent of infrastructure.
- **`TokenBucketRateLimiter`**: Rejected — sliding window better matches the per-minute rate limit semantics and is already used in the codebase.

**Implementation Approach**:
- Register named policies: `"chat"` (30/min), `"stream"` (10/min), `"jsonrpc"` (60/min).
- Client identification: `httpContext.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value` for authenticated users, IP address fallback for unauthenticated.
- Position in pipeline: After CORS, before `CacAuthenticationMiddleware` (so rate-limited requests don't consume auth processing).
- `/health` and `/mcp/tools` endpoints use `DisableRateLimiting()` attribute.
- Configuration via `RateLimiting` section in `appsettings.json`.

---

## R3: OpenTelemetry Exporter Selection

**Decision**: Use OTLP (OpenTelemetry Protocol) as the primary exporter with optional Prometheus scrape endpoint. New NuGet packages: `OpenTelemetry.Extensions.Hosting`, `OpenTelemetry.Exporter.OpenTelemetryProtocol`, `OpenTelemetry.Exporter.Prometheus.AspNetCore`.

**Rationale**: OTLP is the standard for government SOC dashboards (supports Jaeger, Grafana, Azure Monitor). Prometheus is optional for environments with existing Prometheus infrastructure. Push-based OTLP avoids exposing an unauthenticated scrape endpoint (though per clarification Q4, our `/metrics` endpoint requires CAC auth anyway).

**Alternatives Considered**:
- **Prometheus only**: Rejected — pull-based scraping requires network access from monitoring server to the application, which may not work in IL6 environments.
- **Application Insights SDK directly**: Rejected — ties to Azure-specific telemetry. OTLP is cloud-agnostic and supports Azure Monitor via OTLP-to-AppInsights bridge.
- **Custom metrics endpoint**: Rejected — reinvents OpenTelemetry.

**Implementation Approach**:
- Register `AddOpenTelemetry().WithMetrics()` and `.WithTracing()` in `Program.cs`.
- Connect existing `Meter` instances (`ato.copilot.tools`, `ato.copilot.compliance`) to OTLP exporter.
- Add new `Meter` for HTTP metrics (`ato.copilot.http`).
- `ActivitySource` for distributed tracing: `"Ato.Copilot.Mcp"` root source.
- Prometheus endpoint at `/metrics` behind CAC auth middleware.

**New NuGet References** (added to `Ato.Copilot.Mcp.csproj`):
- `OpenTelemetry.Extensions.Hosting` ~1.9.0
- `OpenTelemetry.Exporter.OpenTelemetryProtocol` ~1.9.0
- `OpenTelemetry.Exporter.Prometheus.AspNetCore` ~1.9.0-beta (optional)
- `OpenTelemetry.Instrumentation.AspNetCore` ~1.9.0

---

## R4: Response Caching Strategy

**Decision**: Use the existing `IMemoryCache` with a new `ResponseCacheService` wrapper that provides composite keying (tool + params + subscription), TTL management, stale-while-revalidate, and cache headers.

**Rationale**: The MCP server runs as a single process (per Constitution). `IMemoryCache` is already registered and proven for NIST data with 24h TTL. A distributed cache (`IDistributedCache`, Redis) adds infrastructure complexity without benefit for the single-instance architecture.

**Alternatives Considered**:
- **`IDistributedCache` with Redis**: Rejected — single-process architecture makes Redis unnecessary overhead. Constitution does not require multi-instance deployments.
- **HTTP response caching middleware (`ResponseCachingMiddleware`)**: Rejected — caches at the HTTP level which doesn't understand tool semantics. The caching must be tool-aware (skip mutations, per-subscription scoping).
- **Output caching (`AddOutputCache`)**: Rejected — too coarse-grained for MCP request routing where the same endpoint handles different tools.

**Implementation Approach**:
- `ResponseCacheService` wraps `IMemoryCache` with:
  - Composite key: `SHA256($"{toolName}:{sortedParamsJson}:{subscriptionId}")`.
  - TTL registry: per-tool-category defaults (15min assessments, 60min lookups, never for mutations).
  - Stale-while-revalidate: `SemaphoreSlim` per cache key to prevent thundering herd; stale value served while refresh runs.
  - Size limit: `MemoryCacheOptions.SizeLimit` set to 256 MB with callback logging.
- `ClearCache` registered as a `BaseTool` with optional scope filter.
- Cache integration point: `McpServer.ProcessChatRequestAsync` checks cache before agent dispatch, stores result after.

---

## R5: Lazy-Loaded Knowledge Base Parsing

**Decision**: Wrap each knowledge-base JSON file in `Lazy<T>` with thread-safe initialization. The `NistControlsCacheWarmupService` continues to run but triggers lazy initialization rather than eager deserialization.

**Rationale**: Currently 7 JSON files in `KnowledgeBase/Data/` are copied to output via `.csproj` `Content` directives but loaded on-demand by agents. The NIST catalog (embedded resource) is loaded by `NistControlsCacheWarmupService` at startup. Converting to `Lazy<T>` defers parsing until first access, reducing startup memory by ~20-40 MB depending on file sizes.

**Alternatives Considered**:
- **`System.Text.Json` streaming with `Utf8JsonReader`**: Rejected for knowledge base files — they're moderate-sized reference data that benefits from full deserialization into typed models. Streaming parsers add complexity without proportional benefit for files under 10 MB.
- **Memory-mapped files**: Rejected — requires custom serialization format. JSON is the existing format and changing it adds migration risk.

**Implementation Approach**:
- `NistControlsService`: Replace direct `JsonSerializer.DeserializeAsync` with `Lazy<Task<NistCatalogRoot>>` per dataset.
- `KnowledgeBaseService`: Wrap file loading in `Lazy<T>` per file.
- `NistControlsCacheWarmupService`: Calls `GetCatalogAsync()` which triggers lazy init. No change to warmup timing, but individual sub-datasets defer parsing.
- `IAsyncEnumerable<T>` for compliance assessments: `ComplianceAssessmentTool` yields `ComplianceFinding` items as they're evaluated rather than collecting into `List<T>`.

---

## R6: SSE Reconnection Buffer Design

**Decision**: Per-session `ConcurrentQueue<SseEvent>` with monotonic event IDs and bounded size (256 events). `Last-Event-ID` header triggers replay from buffer.

**Rationale**: SSE spec defines `id` fields and `Last-Event-ID` reconnection natively. The buffer must be per-session (not global) because each stream has its own event sequence. 256 events × ~2 KB/event × 100 concurrent sessions = ~50 MB worst case, well within the 512 MB memory budget.

**Alternatives Considered**:
- **Redis Streams**: Rejected — single-process architecture; adds infrastructure dependency.
- **File-based buffer**: Rejected — adds I/O latency for a transient buffer that only lives during the streaming session.
- **No buffer, re-execute from scratch**: Rejected — 30-second assessments would restart from zero on reconnection, terrible UX.

**Implementation Approach**:
- `SseEventBuffer` class: `ConcurrentDictionary<string, SessionBuffer>` keyed by session/conversation ID.
- Each `SessionBuffer`: `ConcurrentQueue<(int Id, string Data)>` with max size enforcement (drop oldest on overflow).
- Keepalive: `Timer` per active session sending `: keepalive\n\n` every 15 seconds of inactivity.
- Cleanup: Buffer evicted 60 seconds after client disconnect or after `complete` event.
- `McpHttpBridge.HandleChatStreamRequestAsync`: Check `Last-Event-ID` header → replay from buffer → continue live.

---

## R7: Path Sanitization Best Practices

**Decision**: New `PathSanitizationService` using `Path.GetFullPath()` canonicalization with explicit base directory comparison via `StartsWith()` on the canonical path.

**Rationale**: `Path.Combine("uploads", "../../../etc/passwd")` resolves to `/etc/passwd` — the current `SaveAttachmentAsync` is vulnerable to this (mitigated only by GUID filename generation). `Path.GetFullPath()` canonicalizes the path, then `StartsWith(basePath)` verifies containment. This is the OWASP-recommended pattern for .NET.

**Alternatives Considered**:
- **Regex-based path blocking**: Rejected — insufficient. URL-encoded traversal (`%2e%2e%2f`) and null bytes bypass regex.
- **`SafeFileHandle` only**: Rejected — doesn't prevent path resolution before the file is opened.

**Implementation Approach**:
- `ValidatePathWithinBase(string candidatePath, string baseDirectory)` returns `PathValidationResult` (valid/invalid + reason).
- Pre-checks: null byte detection, URL-decode then re-canonicalize, UNC path rejection (`\\server\share`).
- Integration: `ChatService.SaveAttachmentAsync`, tool file parameters, remediation script paths.
- Complements (not replaces) existing `ScriptSanitizationService` which handles content-level safety.

---

## R8: Offline Mode Architecture

**Decision**: Server-wide `OfflineMode` flag that gates all outbound HTTP calls. Deterministic operations only (no local AI model). Persistent cache via EF Core `CacheRepository`.

**Rationale**: Per clarification Q3, offline mode is scoped to deterministic operations: NIST control lookups from embedded OSCAL, cached assessment retrieval, document generation from cached data. This avoids the massive complexity of local AI model deployment while still providing core compliance utility in air-gapped environments.

**Alternatives Considered**:
- **Local ONNX/GGUF model for chat**: Deferred to future feature per clarification. Adds 2-4 GB model download, GPU requirements, and quality parity challenges.
- **No offline mode**: Rejected — IL6 DoD environments require air-gapped operation per spec.
- **Offline-only binary (separate deployment)**: Rejected — adds build/deploy complexity. Single binary with mode flag is simpler.

**Implementation Approach**:
- `OfflineModeService`: Singleton that reads `Server:OfflineMode` config. Exposes `IsOffline`, `GetAvailableCapabilities()`, `GetUnavailableCapabilities()`.
- HTTP client interception: When offline, all `HttpMessageHandler` instances return `OFFLINE_UNAVAILABLE` without making network calls.
- `CacheRepository`: EF Core entity with `CacheKey`, `Response` (JSON), `CachedAt`, `Source`, `TtlSeconds`. SQLite for IL6 deployments.
- Data Protection API for at-rest encryption: `Microsoft.AspNetCore.DataProtection` with DPAPI (Windows) or key file (Linux), FIPS 140-2 compliant when OS FIPS mode is enabled.

---

## R9: Pagination Enforcement Strategy

**Decision**: Server-side pagination enforcement in `McpServer.ProcessChatRequestAsync` response handling, using the existing `PaginationInfo` model.

**Rationale**: `PaginationInfo` already has all required fields (`Page`, `PageSize`, `TotalItems`, `TotalPages`, `HasNextPage`, `NextPageToken`). `PagedResult<T>` exists in `IKanbanService`. The gap is that MCP response serialization doesn't enforce pagination — tools return full collections. Adding a pagination enforcement layer in `McpServer` keeps individual tools unchanged.

**Alternatives Considered**:
- **Per-tool pagination (each tool manages its own)**: Rejected — inconsistent. Some tools already paginate (kanban), most don't. Server-side enforcement ensures universality.
- **HTTP-level pagination middleware**: Rejected — MCP responses are JSON-RPC wrapped; standard HTTP pagination middleware can't understand the envelope.

**Implementation Approach**:
- `McpServer.ProcessChatRequestAsync`: After tool execution, if `response.Data` is `IEnumerable` with count > `maxPageSize`, slice to page and populate `PaginationInfo`.
- Page token: Base64-encoded offset for simple offset-based. Opaque cursor for stateful tools.
- `/mcp/tools` endpoint: Paginate tool listing if count exceeds page size.
- Configuration: `Pagination:DefaultPageSize` (50), `Pagination:MaxPageSize` (100) in `appsettings.json`.
