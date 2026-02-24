# Quickstart: ATO Compliance Engine — Production Readiness

**Feature Branch**: `008-compliance-engine` | **Date**: 2026-02-23

## Prerequisites

- .NET 9.0 SDK
- Azure CLI authenticated (`az login` or `az login --use-device-code`)
- Azure subscription with Reader access (for ARM SDK resource queries)
- All prior features merged (007-nist-controls is critical dependency)

## Build & Test

```bash
# Build the full solution
dotnet build Ato.Copilot.sln

# Run all unit tests
dotnet test tests/Ato.Copilot.Tests.Unit/Ato.Copilot.Tests.Unit.csproj

# Run only compliance engine tests
dotnet test tests/Ato.Copilot.Tests.Unit/Ato.Copilot.Tests.Unit.csproj --filter "FullyQualifiedName~AtoComplianceEngine"

# Run scanner tests
dotnet test tests/Ato.Copilot.Tests.Unit/Ato.Copilot.Tests.Unit.csproj --filter "FullyQualifiedName~Scanner"
```

## Key Entry Points

### Running a Compliance Assessment

The primary entry point is `IAtoComplianceEngine.RunComprehensiveAssessmentAsync`:

```csharp
var engine = serviceProvider.GetRequiredService<IAtoComplianceEngine>();

// Single subscription assessment
var assessment = await engine.RunComprehensiveAssessmentAsync(
    subscriptionId: "your-subscription-id",
    progress: new Progress<AssessmentProgress>(p =>
        Console.WriteLine($"[{p.PercentComplete:F0}%] Scanning {p.CurrentFamily}...")));

Console.WriteLine($"Score: {assessment.ComplianceScore}%");
Console.WriteLine($"Findings: {assessment.Findings.Count}");
Console.WriteLine(assessment.ExecutiveSummary);
```

### MCP Tool Usage

Via the MCP server (stdio or HTTP), the `ComplianceAssessmentTool` wraps the engine:

```json
{
  "tool": "compliance_assessment",
  "arguments": {
    "subscriptionId": "your-subscription-id",
    "resourceGroup": null
  }
}
```

### Scanner Architecture

Scanners implement `IComplianceScanner` and are dispatched via `IScannerRegistry`:

```csharp
// The engine dispatches automatically, but you can also call directly:
var registry = serviceProvider.GetRequiredService<IScannerRegistry>();
var scanner = registry.GetScanner("AC"); // Returns AccessControlScanner

var controls = await nistService.GetControlFamilyAsync("AC", false, ct);
var result = await scanner.ScanAsync(subscriptionId, null, controls, ct);

Console.WriteLine($"AC: {result.ComplianceScore}% ({result.PassedControls}/{result.TotalControls})");
```

### Evidence Collection

```csharp
var evidence = await engine.CollectEvidenceAsync("AC", subscriptionId, ct: ct);
Console.WriteLine($"Completeness: {evidence.CompletenessScore}%");
Console.WriteLine($"Types collected: {evidence.CollectedEvidenceTypes}/{evidence.ExpectedEvidenceTypes}");
```

### Certificate Generation

```csharp
try
{
    var cert = await engine.GenerateCertificateAsync(subscriptionId, "John Doe", ct);
    Console.WriteLine($"Certificate: {cert.CertificateId}");
    Console.WriteLine($"Valid until: {cert.ExpiresAt:yyyy-MM-dd}");
}
catch (InvalidOperationException ex)
{
    Console.WriteLine($"Cannot certify: {ex.Message}"); // Score < 80%
}
```

## DI Registration

All services are registered via `ServiceCollectionExtensions.AddComplianceAgent()` which has been updated to include:

```csharp
// In AddComplianceAgent():
services.AddSingleton<IAzureResourceService, AzureResourceService>();
services.AddSingleton<IAssessmentPersistenceService, AssessmentPersistenceService>();
services.AddComplianceScanners();      // 11 scanners + registry
services.AddEvidenceCollectors();      // 11 collectors + registry
services.AddKnowledgeBaseServices();   // 5 stub interfaces
```

## File Layout

```
src/Ato.Copilot.Agents/Compliance/
├── Services/
│   ├── AtoComplianceEngine.cs            # Core engine (enhanced)
│   ├── AzureResourceService.cs           # NEW: ARM SDK wrapper
│   ├── AssessmentPersistenceService.cs   # NEW: EF Core persistence
│   └── KnowledgeBase/                    # NEW: 5 stub services
├── Scanners/                             # NEW: 11 + base + default
│   ├── BaseComplianceScanner.cs
│   ├── AccessControlScanner.cs
│   └── ...
├── EvidenceCollectors/                   # NEW: 11 + base + default
│   ├── BaseEvidenceCollector.cs
│   └── ...
└── Tools/
    └── ComplianceTools.cs                # Updated to new API

tests/Ato.Copilot.Tests.Unit/
├── Services/AtoComplianceEngineTests.cs  # Expanded (14 → ~80+ tests)
├── Scanners/                             # NEW: 11 test files
└── EvidenceCollectors/                   # NEW: 11 test files
```

## Dependencies on Prior Features

| Feature | What This Feature Consumes |
|---------|---------------------------|
| 007 (NIST Controls) | `INistControlsService` (7 methods), `ControlFamilies` constants, OSCAL models, cache warming, health check, validation, metrics |
| 005 (Compliance Watch) | `IComplianceWatchService` (drift, baselines, monitoring), `IAlertManager` (alerts), `IComplianceEventSource` (events) |
| Existing Engine | `IAzurePolicyComplianceService`, `IDefenderForCloudService`, `IEvidenceStorageService`, `AtoCopilotContext` |
