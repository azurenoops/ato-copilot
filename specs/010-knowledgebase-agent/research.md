# Research: KnowledgeBase Agent — "Compliance Library"

**Feature**: `010-knowledgebase-agent` | **Date**: 2026-02-24

## Research Topic 1: Multi-Agent Orchestrator Design

### Decision: Confidence-scored orchestrator with `CanHandle` on `BaseAgent`

### Rationale

The existing `McpServer.ClassifyAndRouteAgent()` uses hard-coded keyword if/else routing with concrete agent injection. The `BaseAgent` alias pattern (`services.AddSingleton<BaseAgent>(sp => sp.GetRequiredService<ComplianceAgent>())`) already exists for both agents — meaning `IEnumerable<BaseAgent>` resolves all registered agents without DI changes. Adding `abstract double CanHandle(string message)` to `BaseAgent` makes every agent self-describing for routing.

### Design

```csharp
// BaseAgent gains:
public abstract double CanHandle(string message);

// New class in Ato.Copilot.Mcp/Server/AgentOrchestrator.cs
public class AgentOrchestrator
{
    private readonly IEnumerable<BaseAgent> _agents;
    private readonly double _minimumThreshold;     // default 0.3
    private readonly ILogger<AgentOrchestrator> _logger;

    public BaseAgent? SelectAgent(string message)
    {
        var scored = _agents
            .Select(a => (agent: a, score: a.CanHandle(message)))
            .Where(x => x.score >= _minimumThreshold)
            .OrderByDescending(x => x.score)
            .FirstOrDefault();
        return scored.agent;  // null if none above threshold
    }
}
```

### CanHandle scoring approach per agent

| Agent | High-confidence keywords (≥0.8) | Medium-confidence (0.4-0.6) | Default |
|-------|--------------------------------|---------------------------|---------|
| KnowledgeBaseAgent | "explain", "what is", "tell me about", "define", "describe" + domain terms ("nist", "stig", "rmf", "cci", "impact level", "fedramp", "il2-il6", "cat i/ii/iii") | Domain terms alone without action verbs | 0.1 |
| ComplianceAgent | "scan", "assess", "check", "validate", "run", "execute", "monitor", "generate", "create", "remediate" + compliance terms | Compliance terms alone without action verbs | 0.2 (catch-more) |
| ConfigurationAgent | "configure", "set", "subscription", "framework", "baseline", "settings", "setup", "config" | — | 0.0 |

**Key rule**: Action keywords ("scan", "assess", "run") take precedence over knowledge keywords when both are present. Example: "Scan my subscription for AC-2" → ComplianceAgent scores higher because "scan" is an action verb.

### Impact on McpServer

- `ClassifyAndRouteAgent()` method is removed
- `McpServer` constructor changes: replace concrete `ComplianceAgent` + `ConfigurationAgent` with `AgentOrchestrator`
- `ProcessChatRequestAsync` calls `_orchestrator.SelectAgent(message)` instead of `ClassifyAndRouteAgent(message)`
- If `SelectAgent` returns null → return graceful "I'm not sure how to help" response
- MCP tool dispatch (`HandleToolCallAsync`) is unchanged — tools are dispatched directly by name

### Alternatives Considered

1. **Extend keyword if/else**: Simple but unscalable — each new agent requires modifying `McpServer`. Rejected because user chose Option C (full orchestrator).
2. **IAgentRouter strategy pattern**: Interface-based but still requires manual registration. Rejected — `BaseAgent` abstract method is simpler since all agents already extend `BaseAgent`.
3. **Separate `IRoutableAgent` interface**: Would allow opt-in routing but creates risk of agents not implementing it. Rejected — user explicitly chose `abstract` on `BaseAgent` to guarantee participation.

---

## Research Topic 2: JSON Data File Loading & Caching

### Decision: File-system loading with `IMemoryCache` and 24-hour TTL

### Rationale

The existing `NistControlsService` uses embedded resources for its OSCAL catalog. The KB data files use a different pattern: shipped as content files in the build output (`CopyToOutputDirectory`), loaded via `Path.Combine(AppContext.BaseDirectory, "KnowledgeBase/Data/{file}.json")`, and cached in `IMemoryCache`. This pattern is well-suited for curated static files that may be updated between deployments.

### Design

```csharp
// Base pattern for all data-backed KB services
protected async Task<T?> LoadDataFileAsync<T>(string fileName, CancellationToken ct)
{
    var cacheKey = $"kb_data_{fileName}";
    if (_cache.TryGetValue(cacheKey, out T? cached))
        return cached;

    var path = Path.Combine(AppContext.BaseDirectory, "KnowledgeBase", "Data", fileName);
    if (!File.Exists(path))
    {
        _logger.LogWarning("KB data file not found: {Path}", path);
        return default;
    }

    try
    {
        var json = await File.ReadAllTextAsync(path, ct);
        var data = JsonSerializer.Deserialize<T>(json, _jsonOptions);
        _cache.Set(cacheKey, data, TimeSpan.FromHours(24));
        return data;
    }
    catch (JsonException ex)
    {
        _logger.LogError(ex, "Failed to deserialize KB data file: {Path}", path);
        return default;
    }
}
```

### csproj configuration

```xml
<ItemGroup>
  <Content Include="KnowledgeBase\Data\*.json">
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
  </Content>
</ItemGroup>
```

### Alternatives Considered

1. **Embedded resources**: Requires `Assembly.GetManifestResourceStream()`. Works but makes JSON files harder to inspect/update without recompiling. Rejected — static files are more operationally flexible.
2. **Build-time OSCAL/XCCDF parsing**: Would require an OSCAL parser and build pipeline. Rejected — user chose Option B (manually curated).
3. **Lazy loading on first access**: Considered but rejected in favor of eager caching on first query because startup penalty is acceptable (<10s target) and avoids cold-start latency during assessment workflows.

---

## Research Topic 3: Backward Compatibility with Existing Service Consumers

### Decision: Expand interfaces with new methods; preserve existing method signatures exactly

### Findings

| Interface | Existing Method | Production Callers | Compatibility Risk |
|-----------|----------------|-------------------|-------------------|
| `IStigValidationService` | `ValidateAsync(familyCode, controls, subscriptionId, ct)` | `AtoComplianceEngine.cs` (line 608) — merges returned findings into assessment | **HIGH** — signature must not change |
| `IRmfKnowledgeService` | `GetGuidanceAsync(controlId, ct)` | None (registered in DI only) | LOW |
| `IStigKnowledgeService` | `GetStigMappingAsync(controlId, ct)` | None (registered in DI only) | LOW |
| `IDoDInstructionService` | `GetInstructionAsync(controlId, ct)` | None (registered in DI only) | LOW |
| `IDoDWorkflowService` | `GetWorkflowAsync(assessmentType, ct)` | None (registered in DI only) | LOW |

### Strategy

- **All 5 interfaces**: Keep existing method signatures unchanged. Add new methods alongside.
- **`IStigValidationService`**: Special care — `ValidateAsync` return type (`List<ComplianceFinding>`) and parameters must be preserved. New STIG methods (`GetStigControlAsync`, `SearchStigsAsync`, `GetStigCrossReferenceAsync`) are added to the same interface or to `IStigKnowledgeService` depending on domain fit:
  - `IStigKnowledgeService` gets the knowledge methods (lookup, search, cross-reference)
  - `IStigValidationService` keeps validation-only methods (unchanged)
- **4 uncalled interfaces**: Existing methods stay but may now return richer data from JSON files instead of stubs. Return types are unchanged (string/List<string>), so callers (if any appear) get better data transparently.

### INistControlsService consumption

The KB agent consumes `INistControlsService` — it does NOT replace it. Methods used:
- `GetControlAsync(controlId, ct)` — for `explain_nist_control` tool
- `SearchControlsAsync(query, family, impactLevel, maxResults, ct)` — for `search_nist_controls` tool
- `GetControlEnhancementAsync(controlId, ct)` — for enhancement fallback in `explain_nist_control`

No modifications needed to `INistControlsService`.

---

## Research Topic 4: MCP Tool Registration & Dispatch

### Decision: Follow existing `ComplianceMcpTools` pattern with `KnowledgeBaseMcpTools` class

### Findings

The existing MCP tool dispatch in `McpServer` uses:
1. A `ComplianceMcpTools` class that wraps agent tool calls
2. A giant `switch` statement in `HandleToolCallAsync` matching tool name strings
3. Tool definitions returned by `ListToolsAsync`

### Design

- Create `KnowledgeBaseMcpTools` class in `Ato.Copilot.Mcp/Tools/`
- Register 7 tools with `kb_` prefix (e.g., `kb_explain_nist_control`, `kb_search_nist_controls`, etc.)
- Add `switch` cases in `McpServer.HandleToolCallAsync` for all `kb_*` tools
- Add tool definitions in `McpServer.ListToolsAsync`
- `KnowledgeBaseMcpTools` delegates to `KnowledgeBaseAgent.ProcessAsync` or individual tool `ExecuteAsync` calls

### Alternatives Considered

1. **Convention-based auto-registration**: Scanning for tools automatically. Rejected — too complex for this iteration; the switch pattern is consistent with existing code.
2. **Direct tool dispatch (bypass agent)**: MCP tools call services directly, skipping the agent. Rejected — goes through the agent to maintain query tracking, state sharing, and consistent error handling.

---

## Research Topic 5: Test Architecture

### Decision: Follow existing test organization — unit in `Tests.Unit/`, integration in `Tests.Integration/`

### Findings

Existing test organization:
- `Tests.Unit/Agents/` — agent-level tests (e.g., `ComplianceAgentTests.cs`)
- `Tests.Unit/Services/` — service-level tests (e.g., `NistControlsServiceTests.cs`, `AtoComplianceEngineTests.cs`)
- `Tests.Unit/Tools/` — tool-level tests (e.g., `NistControlToolTests.cs`)
- `Tests.Integration/` — MCP endpoint tests (e.g., `McpToolEndpointTests.cs`)
- Integration tests use `Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory`

### New test file plan

| Category | File | Test Count (est.) |
|----------|------|-------------------|
| Agent | `KnowledgeBaseAgentTests.cs` | ~15 (ProcessAsync, query classification, state tracking, error handling) |
| Orchestrator | `AgentOrchestratorTests.cs` | ~20 (confidence scoring, threshold, tiebreaking, fallback, multi-agent) |
| Tools (7) | `Explain*ToolTests.cs`, `Search*ToolTests.cs`, `Get*ToolTests.cs` | ~10 each × 7 = ~70 |
| Services (6) | `*ServiceTests.cs` | ~8 each × 6 = ~48 |
| Integration | `KnowledgeBaseMcpToolEndpointTests.cs`, `OrchestratorRoutingIntegrationTests.cs` | ~15 |
| **Total** | | **~168** |

### Test patterns

- Services: mock `IMemoryCache`, test file loading with temp files, test caching behavior, test missing/malformed files
- Tools: mock service interfaces, test input normalization, test error responses, test response formatting
- Orchestrator: mock `BaseAgent` implementations with controlled `CanHandle` scores, test all routing scenarios
- Integration: `WebApplicationFactory` with real DI, test MCP request → response through full stack
