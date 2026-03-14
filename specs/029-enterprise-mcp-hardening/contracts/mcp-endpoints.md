# MCP Endpoint Contracts: Enterprise MCP Server Hardening

**Feature**: 029-enterprise-mcp-hardening  
**Date**: 2026-03-14

## Endpoint Inventory

All endpoints are served by `McpHttpBridge` on the MCP Kestrel host. Base URL: `http(s)://<host>:<port>`.

---

### POST `/mcp/chat`

**Purpose**: Process a chat request via agent routing.  
**Rate Limit Policy**: `chat` (30 req/min per client, default)  
**Max Request Body**: 32 KB (FR-013)  
**Authentication**: CAC required

**Request** (unchanged):
```json
{
  "message": "string",
  "conversationId": "string?",
  "subscriptionId": "string?"
}
```

**Response** (enhanced):
```json
{
  "success": true,
  "response": "string",
  "conversationId": "string",
  "agentUsed": "string",
  "intentType": "string",
  "processingTimeMs": 1234.5,
  "toolsExecuted": [
    { "toolName": "string", "executionTimeMs": 100, "success": true }
  ],
  "errors": [],
  "suggestedActions": [],
  "metadata": {
    "cacheStatus": "HIT|MISS",
    "cacheAge": 45,
    "pageSizeClamped": false
  },
  "data": { "pagination": { "page": 1, "pageSize": 50, "totalItems": 200, "totalPages": 4, "hasNextPage": true, "nextPageToken": "eyJ..." } }
}
```

**New Response Headers**:
| Header | Value | Condition |
|---|---|---|
| `X-Cache` | `HIT` or `MISS` | Always present for cacheable requests |
| `X-Cache-Age` | Integer (seconds) | Present when `X-Cache: HIT` |

**Error Responses**:
| Status | Error Code | Condition |
|---|---|---|
| 413 | (plain text) | Request body > 32 KB |
| 429 | `RATE_LIMITED` | Client exceeded rate limit |
| 503 | `DEPENDENCY_CIRCUIT_OPEN` | Upstream circuit breaker open |
| 504 | `REQUEST_TIMEOUT` | Request exceeded 30s timeout |

---

### POST `/mcp/chat/stream`

**Purpose**: Process a chat request with SSE streaming response.  
**Rate Limit Policy**: `stream` (10 req/min per client, default)  
**Max Request Body**: 32 KB (FR-013)  
**Authentication**: CAC required

**Request**: Same as `/mcp/chat`.

**SSE Response Format** (enhanced):
```
id: 1
data: {"type":"agentRouted","agentName":"ComplianceAgent","timestamp":"..."}

id: 2
data: {"type":"toolStart","toolName":"assess-compliance","timestamp":"..."}

id: 3
data: {"type":"toolProgress","progress":"Scanning AC-2...","timestamp":"..."}

id: 4
data: {"type":"partial","findings":[...],"timestamp":"..."}

id: 5
data: {"type":"toolComplete","toolName":"assess-compliance","success":true,"executionTimeMs":1500,"timestamp":"..."}

id: 6
data: {"type":"complete","response":"...","metadata":{"cacheStatus":"MISS"},"timestamp":"..."}

```

**New SSE Behaviors**:
- Every event includes `id: <monotonic-integer>` field (FR-040)
- Keepalive comment `: keepalive\n\n` sent after 15s of inactivity (FR-042)
- `Last-Event-ID` header on reconnect triggers replay from buffer (FR-041)

**Reconnection Request**:
```http
POST /mcp/chat/stream HTTP/1.1
Last-Event-ID: 3
Content-Type: application/json

{ "message": "...", "conversationId": "existing-conv-id" }
```

**Reconnection Response**: Events with `id > 3` replayed from buffer, then live events continue.

---

### POST `/mcp`

**Purpose**: JSON-RPC endpoint for MCP protocol.  
**Rate Limit Policy**: `jsonrpc` (60 req/min per client, default)  
**Max Request Body**: 32 KB (FR-013)  
**Authentication**: CAC required

**Contract**: Unchanged. Rate limiting and input validation applied as middleware.

---

### GET `/mcp/tools`

**Purpose**: List available MCP tools.  
**Rate Limit**: Exempt  
**Authentication**: CAC required

**Response** (enhanced with pagination):
```json
{
  "tools": [ ... ],
  "pagination": {
    "page": 1,
    "pageSize": 50,
    "totalItems": 75,
    "totalPages": 2,
    "hasNextPage": true,
    "nextPageToken": "eyJ..."
  }
}
```

**Query Parameters** (new):
| Parameter | Type | Default | Description |
|---|---|---|---|
| `page` | int | 1 | Page number (1-based) |
| `pageSize` | int | 50 | Items per page (max: 100) |

---

### GET `/health`

**Purpose**: Health check endpoint.  
**Rate Limit**: Exempt  
**Authentication**: None (for load balancer probes)

**Response** (enhanced):
```json
{
  "status": "Healthy|Degraded|Unhealthy",
  "checks": {
    "compliance-agent": { "status": "Healthy", "durationMs": 15 },
    "nist-controls": { "status": "Healthy", "durationMs": 8 }
  },
  "uptime": "02:15:30.123",
  "buildVersion": "1.29.0",
  "totalRequestsServed": 4521,
  "offlineMode": false
}
```

**Offline Mode Response** (`Server:OfflineMode=true`):
```json
{
  "status": "Degraded",
  "checks": { ... },
  "offlineMode": true,
  "availableCapabilities": ["control-lookup", "cached-assessments", "document-generation"],
  "unavailableCapabilities": ["azure-ai-chat", "arm-resource-scan", "live-assessment"]
}
```

---

### GET `/metrics` (new, optional)

**Purpose**: Prometheus metrics scrape endpoint.  
**Rate Limit**: Exempt  
**Authentication**: CAC required (FR-021, clarification Q4)  
**Condition**: Only available when `OpenTelemetry:PrometheusEnabled=true`

**Response**: Prometheus text exposition format (`text/plain; version=0.0.4`).

```
# HELP ato_copilot_tool_invocations_total Total tool invocations
# TYPE ato_copilot_tool_invocations_total counter
ato_copilot_tool_invocations_total{tool_name="assess-compliance"} 42

# HELP ato_copilot_http_request_duration_seconds HTTP request duration
# TYPE ato_copilot_http_request_duration_seconds histogram
ato_copilot_http_request_duration_seconds_bucket{endpoint="/mcp/chat",method="POST",le="0.5"} 15
...

# HELP ato_copilot_cache_hits_total Cache hit count
# TYPE ato_copilot_cache_hits_total counter
ato_copilot_cache_hits_total{cache_name="response"} 128
```

**Error**: Returns 404 when `PrometheusEnabled=false` with structured error body.

---

## Middleware Pipeline Order (Updated)

```
1. CorrelationIdMiddleware        ← existing (adds W3C TraceId enrichment)
2. SerilogRequestLogging          ← existing
3. UseCors                        ← existing
4. UseRateLimiter                 ← NEW (FR-006)
5. RequestSizeLimitMiddleware     ← NEW (FR-013)
6. CacAuthenticationMiddleware    ← existing
7. ComplianceAuthorizationMiddleware ← existing
8. RequestMetricsMiddleware       ← NEW (FR-045)
9. AuditLoggingMiddleware         ← existing
10. Endpoint routing              ← existing
```

**Rationale for order**:
- Rate limiter before auth: reject over-limit requests before consuming auth processing.
- Body size limit before auth: reject oversized payloads before deserialization.
- Request metrics after auth: capture authenticated user identity in metrics.

---

## Common Error Response Envelope

All error responses (except 413 which may be raw) follow this schema:

```json
{
  "success": false,
  "response": "",
  "errors": [
    {
      "errorCode": "RATE_LIMITED",
      "message": "Request rate limit exceeded for /mcp/chat (30 requests per minute)",
      "suggestion": "Reduce request frequency or wait 12 seconds before retrying"
    }
  ],
  "metadata": {
    "correlationId": "abc-123",
    "traceId": "0af7651916cd43dd8448eb211c80319c"
  }
}
```
