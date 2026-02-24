# Research: NIST Controls Knowledge Foundation

**Feature Branch**: `007-nist-controls` | **Date**: 2026-02-23

## R1: OSCAL Catalog JSON Structure

**Decision**: Typed C# record models with `[JsonPropertyName]` attributes for every property.

**Rationale**: The NIST OSCAL catalog uses **kebab-case exclusively** (`last-modified`, `oscal-version`, `back-matter`, `sort-id`, `assessment-objective`). `System.Text.Json` has no built-in kebab-case naming policy — `CamelCase` policy would fail silently, producing empty objects. Explicit `[JsonPropertyName]` attributes give compile-time control over mapping and are immune to naming policy changes.

**Alternatives considered**:
- `PropertyNamingPolicy = CamelCase` — rejected: doesn't handle kebab-case, would silently fail
- Custom `JsonNamingPolicy` subclass — rejected: fragile, hard to test, adds maintenance burden vs. attributes
- `JsonDocument` (current approach) — rejected: inline parsing with string lookups is error-prone and doesn't enable `GetControlEnhancementAsync` cleanly

**Key structural findings** (from reading the embedded OSCAL catalog):

```
root.catalog
  ├── uuid (string)
  ├── metadata { title, last-modified, version, oscal-version, revisions[], props[], links[], roles[], parties[], responsible-parties[], remarks }
  ├── groups[] (20 families)
  │   ├── id, class ("family"), title, props[]
  │   └── controls[]
  │       ├── id, class ("SP800-53"), title
  │       ├── params[] { id, props[], label, guidelines[].prose }
  │       ├── props[] { name, value, class?, ns? }
  │       ├── links[] { href, rel }
  │       ├── parts[] (recursive)
  │       │   ├── id, name (discriminator: "statement"|"guidance"|"assessment-objective"|"assessment-method"|"item")
  │       │   ├── prose (text content)
  │       │   └── parts[] (nested sub-items)
  │       └── controls[] (enhancements, e.g., ac-2.1, ac-2.2)
  └── back-matter.resources[] (200 items)
```

- **20 control families** (not 18 — spec was slightly off; actual count: AC, AT, AU, CA, CM, CP, IA, IR, MA, MP, PE, PL, PM, PS, PT, RA, SA, SC, SI, SR)
- **Control IDs**: non-zero-padded (`ac-2`), **label props** are zero-padded (`AC-02`)
- **Enhancement IDs**: dot notation (`ac-2.1`, `sc-7.3`)
- **Part `name`** discriminates content type: `statement`, `guidance`, `assessment-objective`, `assessment-method`, `item`
- **Version**: `5.2.0`, **OSCAL version**: `1.1.3`

## R2: IMemoryCache Integration with Singleton Service

**Decision**: Inject `IMemoryCache` into `NistControlsService` constructor; replace the private `_controls` list + `_loaded` flag + `SemaphoreSlim` with cache-backed loading using `GetOrCreateAsync`.

**Rationale**: `IMemoryCache` is registered as Singleton by `services.AddMemoryCache()` (already present in `Ato.Copilot.State`). It provides built-in absolute/sliding expiration, `CacheItemPriority`, and thread-safe get-or-create semantics. The existing `SemaphoreSlim` pattern duplicates what `IMemoryCache` provides natively.

**Alternatives considered**:
- Keep `SemaphoreSlim` with manual TTL check — rejected: manual expiration tracking is error-prone; doesn't support sliding expiration or priority
- `IDistributedCache` — rejected: overkill for single-process; adds serialization overhead; catalog is ~50MB in memory
- `LazyCache` (third-party) — rejected: adds dependency; `IMemoryCache` is built-in and sufficient

**Implementation notes**:
- Cache keys: `NistControls:Catalog` (typed catalog object), `NistControls:Version` (version string)
- Absolute expiration: 24h (from `NistControlsOptions.CacheDurationHours`)
- Sliding expiration: 25% of absolute (6h default)
- Priority: `CacheItemPriority.High`
- `services.AddMemoryCache()` must be called in `AddComplianceAgent()` DI registration

## R3: Polly Resilience via Microsoft.Extensions.Http.Resilience

**Decision**: Use `Microsoft.Extensions.Http.Resilience` (already referenced in `Ato.Copilot.Core.csproj`) with `AddStandardResilienceHandler()` or custom retry pipeline on the `AddHttpClient<NistControlsService>()` registration.

**Rationale**: The project already depends on `Microsoft.Extensions.Http.Resilience` (version 9.0.0) in Core — this is the .NET 9 in-box Polly 8 integration. Using it on the `HttpClient` registration keeps resilience declarative and outside business logic.

**Alternatives considered**:
- Raw `Polly` NuGet — rejected: project already uses the in-box integration; adding raw Polly creates two resilience patterns
- Manual retry loop in `NistControlsService` — rejected: mixes concerns; harder to test; doesn't leverage `HttpClient` pipeline

**Implementation**: Configure on `AddHttpClient<NistControlsService>()` in `ServiceCollectionExtensions.cs`:
```csharp
services.AddHttpClient<NistControlsService>()
    .AddResilienceHandler("nist-catalog", builder =>
    {
        builder.AddRetry(new HttpRetryStrategyOptions
        {
            MaxRetryAttempts = 3,
            Delay = TimeSpan.FromSeconds(2),
            BackoffType = DelayBackoffType.Exponential,
            ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                .Handle<HttpRequestException>()
                .Handle<TaskCanceledException>()
                .HandleResult(r => !r.IsSuccessStatusCode)
        });
    });
```

## R4: BackgroundService Pattern for Cache Warmup

**Decision**: Follow the existing `ComplianceWatchHostedService` pattern — `BackgroundService` with `PeriodicTimer`, `IOptions<T>` injection, consecutive failure tracking, and structured logging.

**Rationale**: The project already has 5 `BackgroundService` implementations. The `ComplianceWatchHostedService` (336 lines) is the closest analog — it uses `PeriodicTimer`, `IOptions<MonitoringOptions>`, exponential backoff on failures, and `stoppingToken` cancellation. Following this pattern ensures consistency.

**Alternatives considered**:
- `IHostedService` with `Timer` — rejected: `BackgroundService` is the recommended .NET abstraction; all existing hosted services use it
- Hangfire/Quartz — rejected: heavyweight; project doesn't use job schedulers

**Implementation notes**:
- Initial delay: 10s (`await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken)`)
- Refresh interval: 90% of `CacheDurationHours` (default: 21.6h)
- On failure: log + wait 5 min + retry
- After each successful warmup: call `ComplianceValidationService.ValidateControlMappingsAsync()`
- Register via `services.AddHostedService<NistControlsCacheWarmupService>()`

## R5: Health Check Pattern

**Decision**: Follow the existing `AgentHealthCheck` pattern — implement `IHealthCheck`, inject dependencies, return structured `HealthCheckResult` with data dictionary.

**Rationale**: `AgentHealthCheck` (103 lines) already exists in `Ato.Copilot.Core.Observability`. It uses `IServiceProvider.CreateScope()`, checks service resolvability, and includes error descriptions. The new `NistControlsHealthCheck` follows this pattern but probes specific NIST catalog data.

**Alternatives considered**:
- EF Core health check (`AddDbContextCheck`) — rejected: NIST data is in-memory cache, not database
- Custom middleware — rejected: `IHealthCheck` is the standard ASP.NET Core pattern with built-in endpoint support

## R6: Metrics Pattern (System.Diagnostics.Metrics)

**Decision**: Create a static `NistControlsMetrics` class following the existing `ToolMetrics` pattern — static `Meter`, `Counter<long>`, `Histogram<double>`, convenience methods.

**Rationale**: `ToolMetrics` (101 lines) already establishes the project's metrics pattern: static `Meter` named `"Ato.Copilot"`, typed instruments with unit and description, convenience methods like `RecordSuccess()`. FR-039 specifies `nist_api_calls_total` (counter) and `nist_api_call_duration_seconds` (histogram) — these map directly to the existing pattern.

**Alternatives considered**:
- OpenTelemetry SDK directly — rejected: `System.Diagnostics.Metrics` is the .NET-native API; OpenTelemetry exporters pick up the `Meter` automatically
- Instance-based metrics service — rejected: spec calls for `Singleton` `ComplianceMetricsService`, but static instruments are simpler and match `ToolMetrics` pattern

## R7: BaseTool Pattern for Knowledge Base Tools

**Decision**: `NistControlSearchTool` and `NistControlExplainerTool` must extend `BaseTool` (Constitution Principle II). They inject `INistControlsService` and implement `ExecuteCoreAsync`.

**Rationale**: All 10+ existing tools extend `BaseTool`, which provides instrumented `ExecuteAsync` wrapper with `Stopwatch` timing and `ToolMetrics` recording. The `Parameters` property defines MCP-compatible parameter schemas. `GetArg<T>()` helper extracts typed arguments from the MCP dictionary.

**Implementation pattern** (from existing `ControlFamilyTool`):
- Constructor: `base(logger)` + inject `INistControlsService`
- `Name`/`Description`/`Parameters`: static strings
- `ExecuteCoreAsync`: extract args, call service, format result as JSON string
- Register as `Singleton` in `ServiceCollectionExtensions.cs`
- Wire into `ComplianceAgent.RegisterTool()` in agent constructor

## R8: Configuration Migration (NistCatalog → Agents:Compliance:NistControls)

**Decision**: Migrate from raw `IConfiguration` keys (`NistCatalog:PreferOnline`, `NistCatalog:CachePath`, etc.) to `IOptions<NistControlsOptions>` bound from `Agents:Compliance:NistControls`.

**Rationale**: The existing `NistControlsOptions` class is dead code — its properties don't match the `IConfiguration` keys actually used. This migration unifies configuration under the `Agents:Compliance` section (matching `ComplianceAgentOptions` binding) and enables validation attributes.

**Breaking change mitigation**:
- Update `appsettings.json` in both `Ato.Copilot.Mcp` and any test configuration
- Add `services.Configure<NistControlsOptions>(config.GetSection("Agents:Compliance:NistControls"))` in `AddComplianceAgent()`
- Remove old `NistCatalog:*` keys from appsettings after migration
- Existing tests may reference old config keys — must update

## R9: Existing NistControl Entity vs. OSCAL Deserialization Models

**Decision**: Keep existing `NistControl` EF Core entity for database persistence. Create separate OSCAL deserialization types (`OscalCatalog`, `OscalGroup`, `OscalControl`, etc.) in the Agents project for JSON deserialization. The service maps between them.

**Rationale**: The existing `NistControl` entity is used by EF Core for database storage (has `[Key]` attribute, relationship navigation properties). Breaking it would require database migration. The OSCAL models are transient — used only for deserialization and in-memory caching.

**Naming note**: Using `Oscal` prefix for deserialization models to distinguish from the EF `NistControl` entity. The `NistCatalog` / `NistCatalogRoot` names from the spec map to `OscalCatalog` / `OscalCatalogRoot` for clarity, but we'll use the spec's names since the `Nist` prefix is already established in the codebase.
