# Configuration Schema: Enterprise MCP Server Hardening

**Feature**: 029-enterprise-mcp-hardening  
**Date**: 2026-03-14

## New Configuration Sections

All new configuration sections are additive to the existing `appsettings.json`. Existing sections are not modified.

### `Resilience` Section

Controls retry, circuit breaker, and timeout policies for HTTP clients.

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
  }
}
```

**Environment Variable Overrides**:
| Variable | Maps To |
|---|---|
| `ATO_RESILIENCE__PIPELINES__0__MAXRETRYATTEMPTS` | `Resilience:Pipelines:0:MaxRetryAttempts` |
| `ATO_RESILIENCE__PIPELINES__0__REQUESTTIMEOUTSECONDS` | `Resilience:Pipelines:0:RequestTimeoutSeconds` |

**Binding**: `IOptions<ResilienceOptions>` registered via `services.Configure<ResilienceOptions>(configuration.GetSection("Resilience"))`.

---

### `RateLimiting` Section

Controls per-endpoint rate limiting policies.

```json
{
  "RateLimiting": {
    "Policies": [
      {
        "PolicyName": "chat",
        "Endpoint": "/mcp/chat",
        "PermitLimit": 30,
        "WindowSeconds": 60,
        "SegmentsPerWindow": 2
      },
      {
        "PolicyName": "stream",
        "Endpoint": "/mcp/chat/stream",
        "PermitLimit": 10,
        "WindowSeconds": 60,
        "SegmentsPerWindow": 2
      },
      {
        "PolicyName": "jsonrpc",
        "Endpoint": "/mcp",
        "PermitLimit": 60,
        "WindowSeconds": 60,
        "SegmentsPerWindow": 2
      }
    ],
    "ExemptEndpoints": ["/health", "/mcp/tools"]
  }
}
```

**Environment Variable Overrides**:
| Variable | Maps To |
|---|---|
| `ATO_RATELIMITING__POLICIES__0__PERMITLIMIT` | `RateLimiting:Policies:0:PermitLimit` |
| `ATO_RATELIMITING__CHATLIMIT` | Custom override → `RateLimiting:Policies[chat]:PermitLimit` |

---

### `Caching` Section

Controls response-level caching behavior.

```json
{
  "Caching": {
    "SizeLimitMb": 256,
    "DefaultTtlSeconds": 900,
    "ControlLookupTtlSeconds": 3600,
    "AssessmentTtlSeconds": 900,
    "EnableStaleWhileRevalidate": true
  }
}
```

**Binding**: `IOptions<CachingOptions>`.

---

### `Pagination` Section

Controls default and maximum page sizes for collection responses.

```json
{
  "Pagination": {
    "DefaultPageSize": 50,
    "MaxPageSize": 100
  }
}
```

**Environment Variable Overrides**:
| Variable | Maps To |
|---|---|
| `ATO_PAGINATION__DEFAULTPAGESIZE` | `Pagination:DefaultPageSize` |
| `ATO_PAGINATION__MAXPAGESIZE` | `Pagination:MaxPageSize` |

---

### `Server` Section Extensions

New keys added to the existing `Server` section.

```json
{
  "Server": {
    "OfflineMode": false,
    "MaxRequestBodySizeKb": 32
  }
}
```

**Environment Variable Overrides**:
| Variable | Maps To |
|---|---|
| `ATO_SERVER__OFFLINEMODE` | `Server:OfflineMode` |
| `ATO_SERVER__MAXREQUESTBODYSIZEKB` | `Server:MaxRequestBodySizeKb` |

---

### `Streaming` Section

Controls SSE event buffer and keepalive behavior.

```json
{
  "Streaming": {
    "EventBufferSize": 256,
    "KeepaliveIntervalSeconds": 15,
    "BufferEvictionTimeoutSeconds": 60
  }
}
```

---

### `OpenTelemetry` Section

Controls metrics and tracing export.

```json
{
  "OpenTelemetry": {
    "Enabled": true,
    "OtlpEndpoint": "http://localhost:4317",
    "PrometheusEnabled": false,
    "ServiceName": "ato-copilot-mcp"
  }
}
```

**Environment Variable Overrides**:
| Variable | Maps To |
|---|---|
| `OTEL_EXPORTER_OTLP_ENDPOINT` | Standard OTLP env var (takes precedence) |
| `ATO_OPENTELEMETRY__ENABLED` | `OpenTelemetry:Enabled` |
| `ATO_OPENTELEMETRY__PROMETHEUSENABLED` | `OpenTelemetry:PrometheusEnabled` |

---

## Existing Sections (Unchanged)

The following existing `appsettings.json` sections are NOT modified:

- `AzureAi` — Azure AI configuration
- `NistControls` — NIST catalog configuration (including `EnableOfflineFallback`)
- `AuditLogging` — Audit logging configuration
- `Cors` — CORS policy configuration
- `Authentication` — CAC/PIM authentication configuration

**Note**: When `Server:OfflineMode=true`, the existing `NistControls:EnableOfflineFallback` is automatically forced to `true` regardless of its configured value.
