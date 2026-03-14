# Quickstart: Enterprise MCP Server Hardening

**Feature**: 029-enterprise-mcp-hardening  
**Date**: 2026-03-14

## Prerequisites

- .NET 9.0 SDK
- Access to the `029-enterprise-mcp-hardening` branch
- Solution builds clean: `dotnet build Ato.Copilot.sln`
- All existing tests pass: `dotnet test Ato.Copilot.sln`

## Branch Setup

```bash
git checkout 029-enterprise-mcp-hardening
dotnet restore Ato.Copilot.sln
dotnet build Ato.Copilot.sln
```

## New NuGet Packages

Added to `src/Ato.Copilot.Mcp/Ato.Copilot.Mcp.csproj`:

```xml
<PackageReference Include="OpenTelemetry.Extensions.Hosting" Version="1.9.0" />
<PackageReference Include="OpenTelemetry.Exporter.OpenTelemetryProtocol" Version="1.9.0" />
<PackageReference Include="OpenTelemetry.Exporter.Prometheus.AspNetCore" Version="1.9.0-beta.2" />
<PackageReference Include="OpenTelemetry.Instrumentation.AspNetCore" Version="1.9.0" />
```

No other project picks up new dependencies.

## Key Implementation Areas

### 1. Resilience Pipelines

**What changed**: All named HTTP clients now have retry + circuit breaker + timeout policies.

**Where**: `src/Ato.Copilot.Agents/Extensions/ServiceCollectionExtensions.cs`

**Test**: 
```bash
dotnet test tests/Ato.Copilot.Tests.Unit/ --filter "FullyQualifiedName~Resilience"
```

**Verify**: Mock a 503 response and confirm 3 retries with exponential backoff.

### 2. Rate Limiting

**What changed**: `/mcp/chat`, `/mcp/chat/stream`, `/mcp` enforce per-client request limits.

**Where**: `src/Ato.Copilot.Mcp/Program.cs` (middleware registration)

**Test**:
```bash
dotnet test tests/Ato.Copilot.Tests.Integration/ --filter "FullyQualifiedName~RateLimit"
```

**Verify manually**: Send 31 rapid requests to `/mcp/chat` — request 31 should return HTTP 429.

### 3. Path Sanitization

**What changed**: New `PathSanitizationService` validates all file paths against base directories.

**Where**: `src/Ato.Copilot.Core/Services/PathSanitizationService.cs`

**Test**:
```bash
dotnet test tests/Ato.Copilot.Tests.Unit/ --filter "FullyQualifiedName~PathSanitization"
```

**Verify**: `../../../etc/passwd` is rejected with `PATH_TRAVERSAL_BLOCKED`.

### 4. Response Caching

**What changed**: Tool responses are cached per-subscription with configurable TTL.

**Where**: `src/Ato.Copilot.Core/Services/ResponseCacheService.cs`

**Test**:
```bash
dotnet test tests/Ato.Copilot.Tests.Unit/ --filter "FullyQualifiedName~ResponseCache"
```

**Verify**: Same request twice in 30s → second response has `X-Cache: HIT` header.

### 5. OpenTelemetry & Metrics

**What changed**: Existing metrics instruments are exported via OTLP. New HTTP metrics added.

**Where**: `src/Ato.Copilot.Mcp/Program.cs` (OTel registration), `src/Ato.Copilot.Core/Observability/HttpMetrics.cs`

**Test with local collector**:
```bash
# Start OTLP collector (if available)
docker run -p 4317:4317 otel/opentelemetry-collector

# Run server and send requests, then check collector output
```

### 6. Pagination

**What changed**: All collection responses are paginated with `PaginationInfo` envelope.

**Where**: `src/Ato.Copilot.Mcp/Server/McpServer.cs`

**Test**:
```bash
dotnet test tests/Ato.Copilot.Tests.Integration/ --filter "FullyQualifiedName~Pagination"
```

### 7. Offline Mode

**What changed**: Offline mode gates all outbound network calls and serves from local data.

**Where**: `src/Ato.Copilot.Core/Services/OfflineModeService.cs`

**Test**:
```bash
ATO_SERVER__OFFLINEMODE=true dotnet run --project src/Ato.Copilot.Mcp/
# Verify /health returns "Degraded" with offline capability list
```

### 8. SSE Reconnection

**What changed**: SSE events have `id` fields, event buffer supports `Last-Event-ID` replay.

**Where**: `src/Ato.Copilot.Mcp/Resilience/SseEventBuffer.cs`, `src/Ato.Copilot.Mcp/Server/McpHttpBridge.cs`

**Test**:
```bash
dotnet test tests/Ato.Copilot.Tests.Unit/ --filter "FullyQualifiedName~SseEventBuffer"
```

## Configuration

All new features are configured in `appsettings.json`. See [config-schema.md](contracts/config-schema.md) for full schema.

Minimal configuration for development (defaults are sensible):
```json
{
  "OpenTelemetry": {
    "Enabled": false,
    "PrometheusEnabled": false
  }
}
```

## Running All Tests

```bash
# Unit tests
dotnet test tests/Ato.Copilot.Tests.Unit/ --verbosity normal

# Integration tests
dotnet test tests/Ato.Copilot.Tests.Integration/ --verbosity normal

# All tests with coverage
dotnet test Ato.Copilot.sln --collect:"XPlat Code Coverage"
```

## Build & Verify

```bash
dotnet build Ato.Copilot.sln --warnaserrors
dotnet test Ato.Copilot.sln
```

Both commands MUST produce zero warnings and zero test failures.
