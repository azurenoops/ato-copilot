# Data Model: Enterprise MCP Server Hardening

**Feature**: 029-enterprise-mcp-hardening  
**Date**: 2026-03-14

## Entity Inventory

### Existing Entities (Modified)

#### ToolResponse\<T\> — `src/Ato.Copilot.Core/Models/ToolResponse.cs`

**No schema changes needed.** The existing `ToolResponse<T>` already has:
- `Pagination` (`PaginationInfo?`) — used for pagination enforcement
- `Error` / `Errors` (`ErrorDetail` / `List<ErrorDetail>`) — used for structured errors
- `Metadata` (`ResponseMetadata`) — extended at runtime with cache status

The `ResponseMetadata` class gains optional runtime properties via the `McpChatResponse.Metadata` dictionary (no class modification needed).

#### PaginationInfo — `src/Ato.Copilot.Core/Models/ToolResponse.cs`

**No schema changes needed.** Already has all required fields:
- `Page` (int, 1-based, default=1)
- `PageSize` (int, default=25) — default changes to 50 via configuration
- `TotalItems` (int)
- `TotalPages` (int)
- `HasNextPage` (bool)
- `NextPageToken` (string?, nullable) — existing, supports cursor-based pagination

#### ErrorDetail — `src/Ato.Copilot.Core/Models/ToolResponse.cs`

**No schema changes needed.** Already has:
- `Message` (string) — human-readable
- `ErrorCode` (string) — machine-readable (e.g., `"RATE_LIMITED"`, `"PATH_TRAVERSAL_BLOCKED"`)
- `Suggestion` (string) — actionable guidance

New error codes introduced by this feature (used in existing `ErrorCode` field):
| Error Code | Category | Description |
|---|---|---|
| `RATE_LIMITED` | Rate Limiting | Client exceeded per-endpoint request limit |
| `DEPENDENCY_CIRCUIT_OPEN` | Resilience | Circuit breaker is open for upstream dependency |
| `DEPENDENCY_UNAVAILABLE` | Resilience | All retries exhausted for upstream dependency |
| `REQUEST_TIMEOUT` | Resilience | Per-request timeout exceeded (30s default) |
| `RETRY_AFTER_EXCEEDS_BUDGET` | Resilience | Upstream Retry-After value exceeds remaining timeout |
| `PATH_TRAVERSAL_BLOCKED` | Security | File path resolved outside allowed base directory |
| `OFFLINE_UNAVAILABLE` | Offline | Operation requires network but server is in offline mode |
| `OFFLINE_DATA_UNAVAILABLE` | Offline | Offline data source is corrupted or missing |

#### McpChatResponse — `src/Ato.Copilot.Mcp/Models/McpProtocol.cs`

**Minor extension to `Metadata` dictionary.** No new properties on the class. These keys are added at runtime:
- `metadata.cacheStatus` (`"HIT"` or `"MISS"`) — mirrors `X-Cache` header
- `metadata.cacheAge` (int, seconds) — mirrors `X-Cache-Age` header
- `metadata.pageSizeClamped` (bool) — true if client-requested page size was clamped
- `metadata.offlineCapabilities` (string[]) — list of available capabilities when offline

#### PagedResult\<T\> — `src/Ato.Copilot.Core/Interfaces/Kanban/IKanbanService.cs`

**No changes needed.** Used internally by kanban service for its own pagination.

---

### New Entities

#### ResiliencePipelineConfig

**File**: `src/Ato.Copilot.Core/Models/ResiliencePipelineConfig.cs`  
**Purpose**: Configuration for a named HTTP client resilience pipeline. Bound from `appsettings.json` `Resilience` section.

| Field | Type | Default | Description |
|---|---|---|---|
| `Name` | string | (required) | Named HTTP client this pipeline applies to |
| `MaxRetryAttempts` | int | 3 | Maximum retry attempts for transient failures |
| `BaseDelaySeconds` | double | 2.0 | Base delay for exponential backoff |
| `UseJitter` | bool | true | Whether to add jitter to retry delays |
| `CircuitBreakerFailureThreshold` | int | 5 | Failures to trigger circuit breaker open |
| `CircuitBreakerSamplingDurationSeconds` | int | 30 | Window for counting failures |
| `CircuitBreakerBreakDurationSeconds` | int | 30 | Duration circuit stays open |
| `RequestTimeoutSeconds` | int | 30 | Per-request timeout (CancellationToken) |

**Validation Rules**:
- `MaxRetryAttempts` ∈ [0, 10]
- `BaseDelaySeconds` ∈ [0.1, 60]
- `RequestTimeoutSeconds` ∈ [1, 300]
- `CircuitBreakerFailureThreshold` ≥ 1

**Configuration Binding**: `IOptions<List<ResiliencePipelineConfig>>` from `Resilience:Pipelines[]` array in appsettings.

---

#### RateLimitPolicy

**File**: `src/Ato.Copilot.Core/Models/RateLimitPolicy.cs`  
**Purpose**: Configuration for a named endpoint rate limit policy. Bound from `appsettings.json` `RateLimiting` section.

| Field | Type | Default | Description |
|---|---|---|---|
| `PolicyName` | string | (required) | Named policy identifier (e.g., `"chat"`, `"stream"`) |
| `Endpoint` | string | (required) | Route pattern this policy applies to |
| `PermitLimit` | int | 30 | Maximum requests per window |
| `WindowSeconds` | int | 60 | Sliding window duration |
| `SegmentsPerWindow` | int | 2 | Number of segments in sliding window |

**Validation Rules**:
- `PermitLimit` ≥ 1
- `WindowSeconds` ≥ 1
- `SegmentsPerWindow` ∈ [1, 10]

**Configuration Binding**: `IOptions<RateLimitingOptions>` where `RateLimitingOptions.Policies` is `List<RateLimitPolicy>`.

---

#### CachedResponse

**File**: `src/Ato.Copilot.Core/Models/CachedResponse.cs`  
**Purpose**: A cached tool response entry for both in-memory and persistent storage.

| Field | Type | Default | Description |
|---|---|---|---|
| `Id` | int | (auto) | Primary key (EF Core, persistent cache only) |
| `CacheKey` | string | (required) | Composite key: `SHA256(tool:params:subscriptionId)` |
| `ToolName` | string | (required) | Name of the tool that produced the response |
| `Response` | string | (required) | Serialized JSON of the tool response |
| `CachedAt` | DateTimeOffset | UtcNow | Timestamp when entry was cached |
| `TtlSeconds` | int | 900 | Time-to-live in seconds |
| `Source` | string | `"online"` | `"online"` or `"cached"` (origin of the data) |
| `HitCount` | int | 0 | Number of cache hits since creation |
| `SubscriptionId` | string | (required) | Azure subscription scope |

**State Transitions**:
- Created → Active (on first cache store)
- Active → Stale (when `CachedAt + TtlSeconds < UtcNow`)
- Stale → Active (after background refresh)
- Active/Stale → Evicted (by admin action, size limit, or TTL)

**EF Core Mapping** (for persistent cache in offline mode):
- Table: `CachedResponses`
- Index: Unique on `CacheKey`
- Index: Non-unique on `ToolName`, `SubscriptionId`
- `Response` column: `nvarchar(max)` / `TEXT`

---

#### OfflineCapability

**File**: `src/Ato.Copilot.Core/Models/OfflineCapability.cs`  
**Purpose**: Descriptor for an operation's offline availability. Used by `OfflineModeService` to build capability lists.

| Field | Type | Default | Description |
|---|---|---|---|
| `CapabilityName` | string | (required) | Human-readable capability name |
| `RequiresNetwork` | bool | (required) | Whether this operation requires network |
| `FallbackDescription` | string | (required) | What the user can do offline instead |
| `LastSyncedAt` | DateTimeOffset? | null | Last time this capability's data was synced |

**Not persisted to database.** Registered as a static collection in `OfflineModeService`.

---

#### RateLimitingOptions

**File**: `src/Ato.Copilot.Core/Models/RateLimitPolicy.cs` (same file)  
**Purpose**: Root configuration object for the `RateLimiting` appsettings section.

| Field | Type | Default | Description |
|---|---|---|---|
| `Policies` | List\<RateLimitPolicy\> | (see defaults) | Per-endpoint rate limit policies |
| `ExemptEndpoints` | List\<string\> | `["/health", "/mcp/tools"]` | Endpoints exempt from rate limiting |

---

#### SseEvent (internal)

**File**: `src/Ato.Copilot.Mcp/Resilience/SseEventBuffer.cs` (internal class)  
**Purpose**: Buffered SSE event for reconnection replay.

| Field | Type | Default | Description |
|---|---|---|---|
| `Id` | int | (monotonic) | Event ID (1-based, per session) |
| `EventType` | string | (required) | SSE event type (e.g., `"toolStart"`, `"complete"`) |
| `Data` | string | (required) | Serialized event JSON |
| `Timestamp` | DateTimeOffset | UtcNow | When the event was emitted |

**Not persisted.** In-memory only, per-session, bounded to 256 events.

---

## Relationships

```
ResiliencePipelineConfig ──── configures ──→ IHttpClientFactory (named clients)
RateLimitPolicy ──── configures ──→ UseRateLimiter() middleware
CachedResponse ──── stored in ──→ IMemoryCache (runtime) / CachedResponses table (offline)
CachedResponse ──── keyed by ──→ ToolName + params + SubscriptionId
OfflineCapability ──── registered in ──→ OfflineModeService (static collection)
SseEvent ──── buffered in ──→ SseEventBuffer (per streaming session)
ToolResponse<T>.Pagination ──── uses ──→ PaginationInfo (existing)
ToolResponse<T>.Error ──── uses ──→ ErrorDetail (existing, new error codes)
McpChatResponse.Metadata ──── extended with ──→ cache status, pagination clamp, offline info
```

## Configuration Schema

### appsettings.json Additions

```json
{
  "Resilience": {
    "Pipelines": [
      {
        "Name": "default",
        "MaxRetryAttempts": 3,
        "BaseDelaySeconds": 2.0,
        "UseJitter": true,
        "CircuitBreakerFailureThreshold": 5,
        "CircuitBreakerSamplingDurationSeconds": 30,
        "CircuitBreakerBreakDurationSeconds": 30,
        "RequestTimeoutSeconds": 30
      }
    ]
  },
  "RateLimiting": {
    "Policies": [
      { "PolicyName": "chat", "Endpoint": "/mcp/chat", "PermitLimit": 30, "WindowSeconds": 60, "SegmentsPerWindow": 2 },
      { "PolicyName": "stream", "Endpoint": "/mcp/chat/stream", "PermitLimit": 10, "WindowSeconds": 60, "SegmentsPerWindow": 2 },
      { "PolicyName": "jsonrpc", "Endpoint": "/mcp", "PermitLimit": 60, "WindowSeconds": 60, "SegmentsPerWindow": 2 }
    ],
    "ExemptEndpoints": ["/health", "/mcp/tools"]
  },
  "Caching": {
    "SizeLimitMb": 256,
    "DefaultTtlSeconds": 900,
    "ControlLookupTtlSeconds": 3600,
    "AssessmentTtlSeconds": 900
  },
  "Pagination": {
    "DefaultPageSize": 50,
    "MaxPageSize": 100
  },
  "Server": {
    "OfflineMode": false,
    "MaxRequestBodySizeKb": 32
  },
  "Streaming": {
    "EventBufferSize": 256,
    "KeepaliveIntervalSeconds": 15,
    "BufferEvictionTimeoutSeconds": 60
  },
  "OpenTelemetry": {
    "Enabled": true,
    "OtlpEndpoint": "http://localhost:4317",
    "PrometheusEnabled": false,
    "ServiceName": "ato-copilot-mcp"
  }
}
```
