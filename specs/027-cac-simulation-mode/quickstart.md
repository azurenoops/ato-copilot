# Quickstart: CAC Simulation Mode

**Feature**: `027-cac-simulation-mode` | **Date**: 2026-03-12

## Enable Simulation Mode (Local Development)

### 1. Add Configuration

Add the following to `src/Ato.Copilot.Mcp/appsettings.Development.json`:

```json
{
  "CacAuth": {
    "SimulationMode": true,
    "SimulatedIdentity": {
      "UserPrincipalName": "dev.user@dev.mil",
      "DisplayName": "Dev User (Simulated)",
      "CertificateThumbprint": "ABC123DEF456",
      "Roles": ["Global Reader", "ISSO"]
    }
  }
}
```

### 2. Start the Application

```bash
cd src/Ato.Copilot.Mcp
dotnet run
```

On startup, you should see:

```
info: CAC simulation mode active. Simulated identity: dev.user@dev.mil
```

### 3. Test a CAC-Protected Operation

Invoke any MCP tool that requires CAC authentication. The simulated identity will be injected automatically — no physical smart card or JWT token is needed.

## Switch Identities

Change the `SimulatedIdentity` values in `appsettings.Development.json` and restart the application:

```json
{
  "CacAuth": {
    "SimulationMode": true,
    "SimulatedIdentity": {
      "UserPrincipalName": "test.issm@dev.mil",
      "DisplayName": "Test ISSM",
      "Roles": ["ISSM", "Global Reader"]
    }
  }
}
```

**Note**: Identity changes require an application restart. Hot-swapping identities at runtime is not supported.

## Environment Variable Override

Override specific identity fields via environment variables (useful for CI/CD):

```bash
export CacAuth__SimulationMode=true
export CacAuth__SimulatedIdentity__UserPrincipalName=ci.user@dev.mil
export CacAuth__SimulatedIdentity__DisplayName="CI Pipeline User"
export CacAuth__SimulatedIdentity__Roles__0=ISSO
export CacAuth__SimulatedIdentity__Roles__1="Global Reader"

cd src/Ato.Copilot.Mcp
dotnet run
```

Environment variables take precedence over `appsettings.Development.json` values.

## Integration Tests

Each test fixture configures its own simulated identity via DI override:

```csharp
public class IssoWorkflowTests : IAsyncLifetime
{
    private WebApplication _app = null!;
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.Configure<CacAuthOptions>(opts =>
        {
            opts.SimulationMode = true;
            opts.SimulatedIdentity = new SimulatedIdentityOptions
            {
                UserPrincipalName = "test.isso@dev.mil",
                DisplayName = "Test ISSO",
                Roles = ["ISSO"]
            };
        });
        builder.WebHost.UseTestServer();
        _app = builder.Build();
        // ... configure middleware pipeline ...
        await _app.StartAsync();
        _client = _app.GetTestClient();
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _app.DisposeAsync();
    }
}
```

No application restart needed between test classes — each fixture creates its own isolated host.

## Common Errors

| Error | Cause | Fix |
|-------|-------|-----|
| `CacAuth:SimulatedIdentity configuration is required when CacAuth:SimulationMode is true` | `SimulationMode = true` but `SimulatedIdentity` section missing | Add the `SimulatedIdentity` block to your config |
| `CacAuth:SimulatedIdentity:UserPrincipalName is required and cannot be empty` | `UserPrincipalName` not set or empty string | Set a valid UPN value |
| `CacAuth:SimulatedIdentity:DisplayName is required and cannot be empty` | `DisplayName` not set or empty string | Set a display name |
| `Simulation mode is only allowed in Development. Using real authentication.` | `SimulationMode = true` in non-Development environment | This is a safety guard; set `ASPNETCORE_ENVIRONMENT=Development` or remove `SimulationMode` |

## Disable Simulation Mode

Set `SimulationMode` to `false` (or remove it — `false` is the default):

```json
{
  "CacAuth": {
    "SimulationMode": false
  }
}
```

The application will use real JWT-based authentication on the next restart.
