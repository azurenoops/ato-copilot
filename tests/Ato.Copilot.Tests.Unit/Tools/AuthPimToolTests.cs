using System.Text.Json;
using Xunit;
using FluentAssertions;
using Moq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Ato.Copilot.Agents.Compliance.Services;
using Ato.Copilot.Agents.Compliance.Tools;
using Ato.Copilot.Core.Configuration;
using Ato.Copilot.Core.Data.Context;
using Ato.Copilot.Core.Interfaces.Auth;
using Ato.Copilot.Core.Models.Auth;
using Ato.Copilot.Agents.Common;

namespace Ato.Copilot.Tests.Unit.Tools;

public class AuthPimToolTests
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ICacSessionService _cacService;

    public AuthPimToolTests()
    {
        var options = new DbContextOptionsBuilder<AtoCopilotContext>()
            .UseInMemoryDatabase($"AuthPimToolTests_{Guid.NewGuid()}")
            .Options;
        var dbFactory = new TestDbContextFactory(options);

        _cacService = new CacSessionService(
            dbFactory,
            Options.Create(new CacAuthOptions()),
            Options.Create(new PimServiceOptions()),
            Mock.Of<ILogger<CacSessionService>>());

        // Create a real service scope factory
        var services = new ServiceCollection();
        services.AddSingleton<IDbContextFactory<AtoCopilotContext>>(dbFactory);
        services.AddScoped<ICacSessionService>(_ => _cacService);
        services.AddScoped<IPimService>(sp => new PimService(
            sp.GetRequiredService<IDbContextFactory<AtoCopilotContext>>(),
            Options.Create(new PimServiceOptions()),
            Mock.Of<ILogger<PimService>>()));
        services.AddScoped<IJitVmAccessService>(sp => new JitVmAccessService(
            sp.GetRequiredService<IDbContextFactory<AtoCopilotContext>>(),
            Options.Create(new PimServiceOptions()),
            Mock.Of<ILogger<JitVmAccessService>>()));
        services.AddScoped<ICertificateRoleResolver>(sp => new CertificateRoleResolver(
            sp.GetRequiredService<IDbContextFactory<AtoCopilotContext>>(),
            Mock.Of<ILogger<CertificateRoleResolver>>()));
        var provider = services.BuildServiceProvider();
        _scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();
    }

    // ─── CacStatusTool ──────────────────────────────────────────────────

    [Fact]
    public void CacStatusTool_ShouldHaveCorrectName()
    {
        var tool = new CacStatusTool(_scopeFactory, Mock.Of<ILogger<CacStatusTool>>());
        tool.Name.Should().Be("cac_status");
    }

    [Fact]
    public void CacStatusTool_ShouldHaveNoParameters()
    {
        var tool = new CacStatusTool(_scopeFactory, Mock.Of<ILogger<CacStatusTool>>());
        tool.Parameters.Should().BeEmpty();
    }

    [Fact]
    public async Task CacStatusTool_Unauthenticated_ShouldReturnUnauthenticatedResponse()
    {
        var tool = new CacStatusTool(_scopeFactory, Mock.Of<ILogger<CacStatusTool>>());

        var result = await tool.ExecuteAsync(new Dictionary<string, object?>());

        var doc = JsonDocument.Parse(result);
        var root = doc.RootElement;

        root.GetProperty("status").GetString().Should().Be("success");
        root.GetProperty("data").GetProperty("authenticated").GetBoolean().Should().BeFalse();
        root.GetProperty("data").GetProperty("message").GetString().Should().Contain("CAC");
        root.GetProperty("metadata").GetProperty("toolName").GetString().Should().Be("cac_status");
    }

    [Fact]
    public async Task CacStatusTool_WithNoUserId_ShouldReturnUnauthenticated()
    {
        var tool = new CacStatusTool(_scopeFactory, Mock.Of<ILogger<CacStatusTool>>());

        var result = await tool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["user_id"] = null
        });

        var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("data").GetProperty("authenticated").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task CacStatusTool_WithActiveSession_ShouldReturnAuthenticatedResponse()
    {
        // Create a session first
        await _cacService.CreateSessionAsync(
            "user-status-1", "Jane Smith", "jane@agency.mil",
            CacSessionService.ComputeTokenHash("test-token"),
            ClientType.VSCode, "10.0.0.1");

        var tool = new CacStatusTool(_scopeFactory, Mock.Of<ILogger<CacStatusTool>>());

        var result = await tool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["user_id"] = "user-status-1"
        });

        var doc = JsonDocument.Parse(result);
        var root = doc.RootElement;

        root.GetProperty("status").GetString().Should().Be("success");
        root.GetProperty("data").GetProperty("authenticated").GetBoolean().Should().BeTrue();
        root.GetProperty("data").GetProperty("identity").GetProperty("displayName").GetString()
            .Should().Be("Jane Smith");
        root.GetProperty("data").GetProperty("identity").GetProperty("email").GetString()
            .Should().Be("jane@agency.mil");
        root.GetProperty("data").GetProperty("session").GetProperty("clientType").GetString()
            .Should().Be("VSCode");
    }

    [Fact]
    public async Task CacStatusTool_ShouldIncludeRemainingMinutes()
    {
        await _cacService.CreateSessionAsync(
            "user-status-2", "Test User", "test@agency.mil",
            CacSessionService.ComputeTokenHash("test-token-2"),
            ClientType.Web, "10.0.0.2");

        var tool = new CacStatusTool(_scopeFactory, Mock.Of<ILogger<CacStatusTool>>());

        var result = await tool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["user_id"] = "user-status-2"
        });

        var doc = JsonDocument.Parse(result);
        var session = doc.RootElement.GetProperty("data").GetProperty("session");
        session.GetProperty("remainingMinutes").GetInt32().Should().BeGreaterThan(0);
    }

    // ─── CacSignOutTool ─────────────────────────────────────────────────

    [Fact]
    public void CacSignOutTool_ShouldHaveCorrectName()
    {
        var tool = new CacSignOutTool(_scopeFactory, Mock.Of<ILogger<CacSignOutTool>>());
        tool.Name.Should().Be("cac_sign_out");
    }

    [Fact]
    public async Task CacSignOutTool_WithNoSession_ShouldReturnError()
    {
        var tool = new CacSignOutTool(_scopeFactory, Mock.Of<ILogger<CacSignOutTool>>());

        var result = await tool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["user_id"] = "non-existent-user"
        });

        var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("status").GetString().Should().Be("error");
        doc.RootElement.GetProperty("data").GetProperty("errorCode").GetString()
            .Should().Be("AUTH_REQUIRED");
    }

    [Fact]
    public async Task CacSignOutTool_WithNoUserId_ShouldReturnError()
    {
        var tool = new CacSignOutTool(_scopeFactory, Mock.Of<ILogger<CacSignOutTool>>());

        var result = await tool.ExecuteAsync(new Dictionary<string, object?>());

        var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("status").GetString().Should().Be("error");
        doc.RootElement.GetProperty("data").GetProperty("errorCode").GetString()
            .Should().Be("AUTH_REQUIRED");
    }

    [Fact]
    public async Task CacSignOutTool_WithActiveSession_ShouldTerminateSession()
    {
        await _cacService.CreateSessionAsync(
            "user-signout-1", "Test User", "test@agency.mil",
            CacSessionService.ComputeTokenHash("signout-token"),
            ClientType.VSCode, "10.0.0.1");

        var tool = new CacSignOutTool(_scopeFactory, Mock.Of<ILogger<CacSignOutTool>>());

        var result = await tool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["user_id"] = "user-signout-1"
        });

        var doc = JsonDocument.Parse(result);
        var root = doc.RootElement;

        root.GetProperty("status").GetString().Should().Be("success");
        root.GetProperty("data").GetProperty("sessionTerminated").GetBoolean().Should().BeTrue();
        root.GetProperty("data").GetProperty("message").GetString().Should().Contain("terminated");
        root.GetProperty("metadata").GetProperty("toolName").GetString().Should().Be("cac_sign_out");

        // Verify session is actually terminated
        var isActive = await _cacService.IsSessionActiveAsync("user-signout-1");
        isActive.Should().BeFalse();
    }

    [Fact]
    public async Task CacSignOutTool_ResponseMatchesContractEnvelope()
    {
        await _cacService.CreateSessionAsync(
            "user-signout-2", "Test User", "test@agency.mil",
            CacSessionService.ComputeTokenHash("signout-token-2"),
            ClientType.Web, "10.0.0.2");

        var tool = new CacSignOutTool(_scopeFactory, Mock.Of<ILogger<CacSignOutTool>>());

        var result = await tool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["user_id"] = "user-signout-2"
        });

        var doc = JsonDocument.Parse(result);
        var root = doc.RootElement;

        // Verify envelope schema: status/data/metadata
        root.TryGetProperty("status", out _).Should().BeTrue();
        root.TryGetProperty("data", out _).Should().BeTrue();
        root.TryGetProperty("metadata", out _).Should().BeTrue();

        // Verify data fields per contract
        var data = root.GetProperty("data");
        data.TryGetProperty("sessionTerminated", out _).Should().BeTrue();
        data.TryGetProperty("activePimRolesDeactivated", out _).Should().BeTrue();
    }

    // ─── CacSetTimeoutTool ──────────────────────────────────────────────

    [Fact]
    public void CacSetTimeoutTool_ShouldHaveCorrectName()
    {
        var tool = new CacSetTimeoutTool(_scopeFactory, Mock.Of<ILogger<CacSetTimeoutTool>>());
        tool.Name.Should().Be("cac_set_timeout");
    }

    [Fact]
    public async Task CacSetTimeoutTool_ValidTimeout_ShouldUpdateSession()
    {
        // Create a session first
        await _cacService.CreateSessionAsync(
            "user-timeout-1", "Jane Smith", "jane@agency.mil",
            CacSessionService.ComputeTokenHash("timeout-token"),
            ClientType.VSCode, "10.0.0.1");

        var tool = new CacSetTimeoutTool(_scopeFactory, Mock.Of<ILogger<CacSetTimeoutTool>>());

        var result = await tool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["user_id"] = "user-timeout-1",
            ["timeoutHours"] = 4
        });

        var doc = JsonDocument.Parse(result);
        var root = doc.RootElement;

        root.GetProperty("status").GetString().Should().Be("success");
        root.GetProperty("data").GetProperty("previousTimeout").GetString().Should().Be("8 hours");
        root.GetProperty("data").GetProperty("newTimeout").GetString().Should().Be("4 hours");
        root.GetProperty("data").GetProperty("newExpiresAt").GetString().Should().NotBeNullOrEmpty();
        root.GetProperty("data").GetProperty("message").GetString().Should().Contain("4 hours");
        root.GetProperty("metadata").GetProperty("toolName").GetString().Should().Be("cac_set_timeout");
    }

    [Fact]
    public async Task CacSetTimeoutTool_NoAuth_ShouldReturnError()
    {
        var tool = new CacSetTimeoutTool(_scopeFactory, Mock.Of<ILogger<CacSetTimeoutTool>>());

        var result = await tool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["timeoutHours"] = 4
        });

        var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("status").GetString().Should().Be("error");
        doc.RootElement.GetProperty("data").GetProperty("errorCode").GetString().Should().Be("AUTH_REQUIRED");
    }

    [Fact]
    public async Task CacSetTimeoutTool_BelowMinimum_ShouldReturnInvalidDuration()
    {
        var tool = new CacSetTimeoutTool(_scopeFactory, Mock.Of<ILogger<CacSetTimeoutTool>>());

        var result = await tool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["user_id"] = "user-timeout-below",
            ["timeoutHours"] = 0
        });

        var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("status").GetString().Should().Be("error");
        doc.RootElement.GetProperty("data").GetProperty("errorCode").GetString().Should().Be("INVALID_TIMEOUT_DURATION");
    }

    [Fact]
    public async Task CacSetTimeoutTool_AboveMaximum_ShouldReturnInvalidDuration()
    {
        var tool = new CacSetTimeoutTool(_scopeFactory, Mock.Of<ILogger<CacSetTimeoutTool>>());

        var result = await tool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["user_id"] = "user-timeout-above",
            ["timeoutHours"] = 25
        });

        var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("status").GetString().Should().Be("error");
        doc.RootElement.GetProperty("data").GetProperty("errorCode").GetString().Should().Be("INVALID_TIMEOUT_DURATION");
    }

    [Fact]
    public async Task CacSetTimeoutTool_NoSession_ShouldReturnAuthRequired()
    {
        var tool = new CacSetTimeoutTool(_scopeFactory, Mock.Of<ILogger<CacSetTimeoutTool>>());

        var result = await tool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["user_id"] = "user-no-session",
            ["timeoutHours"] = 4
        });

        var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("status").GetString().Should().Be("error");
        doc.RootElement.GetProperty("data").GetProperty("errorCode").GetString().Should().Be("AUTH_REQUIRED");
    }

    [Fact]
    public async Task CacSetTimeoutTool_ResponseMatchesContractEnvelope()
    {
        await _cacService.CreateSessionAsync(
            "user-timeout-env", "Jane Smith", "jane@agency.mil",
            CacSessionService.ComputeTokenHash("timeout-env-token"),
            ClientType.VSCode, "10.0.0.1");

        var tool = new CacSetTimeoutTool(_scopeFactory, Mock.Of<ILogger<CacSetTimeoutTool>>());
        var result = await tool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["user_id"] = "user-timeout-env",
            ["timeoutHours"] = 12
        });

        var doc = JsonDocument.Parse(result);
        var root = doc.RootElement;

        // Verify envelope schema
        root.TryGetProperty("status", out _).Should().BeTrue();
        root.TryGetProperty("data", out _).Should().BeTrue();
        root.TryGetProperty("metadata", out _).Should().BeTrue();

        var data = root.GetProperty("data");
        data.TryGetProperty("previousTimeout", out _).Should().BeTrue();
        data.TryGetProperty("newTimeout", out _).Should().BeTrue();
        data.TryGetProperty("newExpiresAt", out _).Should().BeTrue();
    }

    // ─── PimListEligibleTool ─────────────────────────────────────────────

    [Fact]
    public void PimListEligibleTool_ShouldHaveCorrectName()
    {
        var tool = new PimListEligibleTool(_scopeFactory, Mock.Of<ILogger<PimListEligibleTool>>());
        tool.Name.Should().Be("pim_list_eligible");
    }

    [Fact]
    public async Task PimListEligibleTool_ShouldReturnRoles()
    {
        var tool = new PimListEligibleTool(_scopeFactory, Mock.Of<ILogger<PimListEligibleTool>>());
        var result = await tool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["user_id"] = "user-pim-1"
        });

        var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("status").GetString().Should().Be("success");
        doc.RootElement.GetProperty("data").GetProperty("totalCount").GetInt32().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task PimListEligibleTool_NoUser_ShouldReturnAuthRequired()
    {
        var tool = new PimListEligibleTool(_scopeFactory, Mock.Of<ILogger<PimListEligibleTool>>());
        var result = await tool.ExecuteAsync(new Dictionary<string, object?>());

        var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("status").GetString().Should().Be("error");
        doc.RootElement.GetProperty("data").GetProperty("errorCode").GetString().Should().Be("AUTH_REQUIRED");
    }

    // ─── PimActivateRoleTool ─────────────────────────────────────────────

    [Fact]
    public void PimActivateRoleTool_ShouldHaveCorrectName()
    {
        var tool = new PimActivateRoleTool(_scopeFactory, Mock.Of<ILogger<PimActivateRoleTool>>());
        tool.Name.Should().Be("pim_activate_role");
    }

    [Fact]
    public async Task PimActivateRoleTool_ShouldActivateSuccessfully()
    {
        var tool = new PimActivateRoleTool(_scopeFactory, Mock.Of<ILogger<PimActivateRoleTool>>());
        var result = await tool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["user_id"] = "user-pim-2",
            ["roleName"] = "Contributor",
            ["scope"] = "default",
            ["justification"] = "Remediating AC-2.1 finding per assessment"
        });

        var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("status").GetString().Should().Be("success");
        doc.RootElement.GetProperty("data").GetProperty("activated").GetBoolean().Should().BeTrue();
        doc.RootElement.GetProperty("data").GetProperty("roleName").GetString().Should().Be("Contributor");
    }

    [Fact]
    public async Task PimActivateRoleTool_JustificationTooShort_ShouldReturnError()
    {
        var tool = new PimActivateRoleTool(_scopeFactory, Mock.Of<ILogger<PimActivateRoleTool>>());
        var result = await tool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["user_id"] = "user-pim-3",
            ["roleName"] = "Contributor",
            ["scope"] = "default",
            ["justification"] = "Too short"
        });

        var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("status").GetString().Should().Be("error");
        doc.RootElement.GetProperty("data").GetProperty("errorCode").GetString().Should().Be("JUSTIFICATION_TOO_SHORT");
    }

    [Fact]
    public async Task PimActivateRoleTool_NotEligible_ShouldReturnError()
    {
        var tool = new PimActivateRoleTool(_scopeFactory, Mock.Of<ILogger<PimActivateRoleTool>>());
        var result = await tool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["user_id"] = "user-pim-4",
            ["roleName"] = "FakeRole",
            ["scope"] = "default",
            ["justification"] = "Remediating compliance finding in production"
        });

        var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("status").GetString().Should().Be("error");
        doc.RootElement.GetProperty("data").GetProperty("errorCode").GetString().Should().Be("NOT_ELIGIBLE");
    }

    [Fact]
    public async Task PimActivateRoleTool_MissingParams_ShouldReturnError()
    {
        var tool = new PimActivateRoleTool(_scopeFactory, Mock.Of<ILogger<PimActivateRoleTool>>());
        var result = await tool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["user_id"] = "user-pim-5"
        });

        var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("status").GetString().Should().Be("error");
        doc.RootElement.GetProperty("data").GetProperty("errorCode").GetString().Should().Be("MISSING_PARAMETERS");
    }

    // ─── PimDeactivateRoleTool ───────────────────────────────────────────

    [Fact]
    public void PimDeactivateRoleTool_ShouldHaveCorrectName()
    {
        var tool = new PimDeactivateRoleTool(_scopeFactory, Mock.Of<ILogger<PimDeactivateRoleTool>>());
        tool.Name.Should().Be("pim_deactivate_role");
    }

    [Fact]
    public async Task PimDeactivateRoleTool_ShouldDeactivateSuccessfully()
    {
        // First activate
        var activateTool = new PimActivateRoleTool(_scopeFactory, Mock.Of<ILogger<PimActivateRoleTool>>());
        await activateTool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["user_id"] = "user-pim-deact-1",
            ["roleName"] = "Reader",
            ["scope"] = "default",
            ["justification"] = "Remediating AC-2.1 finding per assessment"
        });

        var tool = new PimDeactivateRoleTool(_scopeFactory, Mock.Of<ILogger<PimDeactivateRoleTool>>());
        var result = await tool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["user_id"] = "user-pim-deact-1",
            ["roleName"] = "Reader",
            ["scope"] = "default"
        });

        var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("status").GetString().Should().Be("success");
        doc.RootElement.GetProperty("data").GetProperty("deactivated").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task PimDeactivateRoleTool_NotActive_ShouldReturnError()
    {
        var tool = new PimDeactivateRoleTool(_scopeFactory, Mock.Of<ILogger<PimDeactivateRoleTool>>());
        var result = await tool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["user_id"] = "user-pim-deact-2",
            ["roleName"] = "Contributor",
            ["scope"] = "default"
        });

        var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("status").GetString().Should().Be("error");
        doc.RootElement.GetProperty("data").GetProperty("errorCode").GetString().Should().Be("ROLE_NOT_ACTIVE");
    }

    [Fact]
    public async Task PimTools_AllResponsesMatchEnvelopeSchema()
    {
        // Each PIM tool response should have status/data/metadata
        var tools = new BaseTool[]
        {
            new PimListEligibleTool(_scopeFactory, Mock.Of<ILogger<PimListEligibleTool>>()),
            new PimActivateRoleTool(_scopeFactory, Mock.Of<ILogger<PimActivateRoleTool>>()),
            new PimDeactivateRoleTool(_scopeFactory, Mock.Of<ILogger<PimDeactivateRoleTool>>())
        };

        foreach (var tool in tools)
        {
            var result = await tool.ExecuteAsync(new Dictionary<string, object?>
            {
                ["user_id"] = "user-envelope-check"
            });

            var doc = JsonDocument.Parse(result);
            var root = doc.RootElement;
            root.TryGetProperty("status", out _).Should().BeTrue($"{tool.Name} should return status");
            root.TryGetProperty("data", out _).Should().BeTrue($"{tool.Name} should return data");
            root.TryGetProperty("metadata", out _).Should().BeTrue($"{tool.Name} should return metadata");
        }
    }

    // ─── PimListActiveTool (T088) ────────────────────────────────────────

    [Fact]
    public void PimListActiveTool_ShouldHaveCorrectName()
    {
        var tool = new PimListActiveTool(_scopeFactory, Mock.Of<ILogger<PimListActiveTool>>());
        tool.Name.Should().Be("pim_list_active");
    }

    [Fact]
    public async Task PimListActiveTool_ShouldReturnActiveRoles()
    {
        // First activate a role
        var activateTool = new PimActivateRoleTool(_scopeFactory, Mock.Of<ILogger<PimActivateRoleTool>>());
        await activateTool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["user_id"] = "user-listactive-1",
            ["roleName"] = "Contributor",
            ["scope"] = "default",
            ["justification"] = "Remediating AC-2.1 finding per assessment"
        });

        var tool = new PimListActiveTool(_scopeFactory, Mock.Of<ILogger<PimListActiveTool>>());
        var result = await tool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["user_id"] = "user-listactive-1"
        });

        var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("status").GetString().Should().Be("success");
        doc.RootElement.GetProperty("data").GetProperty("totalCount").GetInt32().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task PimListActiveTool_NoUser_ShouldReturnAuthRequired()
    {
        var tool = new PimListActiveTool(_scopeFactory, Mock.Of<ILogger<PimListActiveTool>>());
        var result = await tool.ExecuteAsync(new Dictionary<string, object?>());

        var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("status").GetString().Should().Be("error");
        doc.RootElement.GetProperty("data").GetProperty("errorCode").GetString().Should().Be("AUTH_REQUIRED");
    }

    // ─── PimExtendRoleTool (T088) ────────────────────────────────────────

    [Fact]
    public void PimExtendRoleTool_ShouldHaveCorrectName()
    {
        var tool = new PimExtendRoleTool(_scopeFactory, Mock.Of<ILogger<PimExtendRoleTool>>());
        tool.Name.Should().Be("pim_extend_role");
    }

    [Fact]
    public async Task PimExtendRoleTool_ShouldExtendSuccessfully()
    {
        // First activate
        var activateTool = new PimActivateRoleTool(_scopeFactory, Mock.Of<ILogger<PimActivateRoleTool>>());
        await activateTool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["user_id"] = "user-extend-1",
            ["roleName"] = "Reader",
            ["scope"] = "default",
            ["justification"] = "Remediating AC-2.1 finding per assessment"
        });

        var tool = new PimExtendRoleTool(_scopeFactory, Mock.Of<ILogger<PimExtendRoleTool>>());
        var result = await tool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["user_id"] = "user-extend-1",
            ["roleName"] = "Reader",
            ["scope"] = "default",
            ["additionalHours"] = 2
        });

        var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("status").GetString().Should().Be("success");
        doc.RootElement.GetProperty("data").GetProperty("extended").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task PimExtendRoleTool_ExceedsPolicy_ShouldReturnError()
    {
        var activateTool = new PimActivateRoleTool(_scopeFactory, Mock.Of<ILogger<PimActivateRoleTool>>());
        await activateTool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["user_id"] = "user-extend-2",
            ["roleName"] = "Contributor",
            ["scope"] = "default",
            ["justification"] = "Remediating AC-2.1 finding per assessment",
            ["durationHours"] = 8
        });

        var tool = new PimExtendRoleTool(_scopeFactory, Mock.Of<ILogger<PimExtendRoleTool>>());
        var result = await tool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["user_id"] = "user-extend-2",
            ["roleName"] = "Contributor",
            ["scope"] = "default",
            ["additionalHours"] = 20
        });

        var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("status").GetString().Should().Be("error");
        doc.RootElement.GetProperty("data").GetProperty("errorCode").GetString().Should().Be("DURATION_EXCEEDS_POLICY");
    }

    [Fact]
    public async Task PimExtendRoleTool_MissingParams_ShouldReturnError()
    {
        var tool = new PimExtendRoleTool(_scopeFactory, Mock.Of<ILogger<PimExtendRoleTool>>());
        var result = await tool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["user_id"] = "user-extend-3"
        });

        var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("status").GetString().Should().Be("error");
        doc.RootElement.GetProperty("data").GetProperty("errorCode").GetString().Should().Be("MISSING_PARAMETERS");
    }

    // ─── PimApproveRequestTool (T091) ────────────────────────────────────

    [Fact]
    public void PimApproveRequestTool_ShouldHaveCorrectName()
    {
        var tool = new PimApproveRequestTool(_scopeFactory, Mock.Of<ILogger<PimApproveRequestTool>>());
        tool.Name.Should().Be("pim_approve_request");
    }

    [Fact]
    public async Task PimApproveRequestTool_SecurityLead_ShouldApprove()
    {
        // Submit a pending request first
        using var scope = _scopeFactory.CreateScope();
        var pimService = scope.ServiceProvider.GetRequiredService<IPimService>();
        var request = await pimService.SubmitApprovalAsync(
            "user-approve-1", "John Doe", "Owner", "/subscriptions/sub-1",
            "Emergency production access needed", null, 4,
            Guid.NewGuid());

        var tool = new PimApproveRequestTool(_scopeFactory, Mock.Of<ILogger<PimApproveRequestTool>>());
        var result = await tool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["user_id"] = "approver-1",
            ["user_role"] = "SecurityLead",
            ["requestId"] = request.Id.ToString(),
            ["comments"] = "Approved for incident response"
        });

        var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("status").GetString().Should().Be("success");
        doc.RootElement.GetProperty("data").GetProperty("approved").GetBoolean().Should().BeTrue();
        doc.RootElement.GetProperty("data").GetProperty("requester").GetString().Should().Be("John Doe");
        doc.RootElement.GetProperty("data").GetProperty("roleName").GetString().Should().Be("Owner");
        doc.RootElement.GetProperty("metadata").GetProperty("toolName").GetString().Should().Be("pim_approve_request");
    }

    [Fact]
    public async Task PimApproveRequestTool_NonApprover_ShouldReturnInsufficientRole()
    {
        var tool = new PimApproveRequestTool(_scopeFactory, Mock.Of<ILogger<PimApproveRequestTool>>());
        var result = await tool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["user_id"] = "regular-user",
            ["user_role"] = "Contributor",
            ["requestId"] = Guid.NewGuid().ToString()
        });

        var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("status").GetString().Should().Be("error");
        doc.RootElement.GetProperty("data").GetProperty("errorCode").GetString().Should().Be("INSUFFICIENT_ROLE");
    }

    [Fact]
    public async Task PimApproveRequestTool_MissingRequestId_ShouldReturnError()
    {
        var tool = new PimApproveRequestTool(_scopeFactory, Mock.Of<ILogger<PimApproveRequestTool>>());
        var result = await tool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["user_id"] = "approver-1",
            ["user_role"] = "SecurityLead"
        });

        var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("status").GetString().Should().Be("error");
        doc.RootElement.GetProperty("data").GetProperty("errorCode").GetString().Should().Be("MISSING_PARAMETERS");
    }

    [Fact]
    public async Task PimApproveRequestTool_NoAuth_ShouldReturnAuthRequired()
    {
        var tool = new PimApproveRequestTool(_scopeFactory, Mock.Of<ILogger<PimApproveRequestTool>>());
        var result = await tool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["requestId"] = Guid.NewGuid().ToString()
        });

        var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("status").GetString().Should().Be("error");
        doc.RootElement.GetProperty("data").GetProperty("errorCode").GetString().Should().Be("AUTH_REQUIRED");
    }

    // ─── PimDenyRequestTool (T091) ───────────────────────────────────────

    [Fact]
    public void PimDenyRequestTool_ShouldHaveCorrectName()
    {
        var tool = new PimDenyRequestTool(_scopeFactory, Mock.Of<ILogger<PimDenyRequestTool>>());
        tool.Name.Should().Be("pim_deny_request");
    }

    [Fact]
    public async Task PimDenyRequestTool_SecurityLead_ShouldDeny()
    {
        using var scope = _scopeFactory.CreateScope();
        var pimService = scope.ServiceProvider.GetRequiredService<IPimService>();
        var request = await pimService.SubmitApprovalAsync(
            "user-deny-1", "Jane Smith", "Owner", "/subscriptions/sub-1",
            "Emergency production access needed", null, 4,
            Guid.NewGuid());

        var tool = new PimDenyRequestTool(_scopeFactory, Mock.Of<ILogger<PimDenyRequestTool>>());
        var result = await tool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["user_id"] = "approver-deny-1",
            ["user_role"] = "SecurityLead",
            ["requestId"] = request.Id.ToString(),
            ["reason"] = "Insufficient justification for Owner role"
        });

        var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("status").GetString().Should().Be("success");
        doc.RootElement.GetProperty("data").GetProperty("denied").GetBoolean().Should().BeTrue();
        doc.RootElement.GetProperty("data").GetProperty("requester").GetString().Should().Be("Jane Smith");
        doc.RootElement.GetProperty("data").GetProperty("reason").GetString().Should().Contain("Insufficient justification");
        doc.RootElement.GetProperty("metadata").GetProperty("toolName").GetString().Should().Be("pim_deny_request");
    }

    [Fact]
    public async Task PimDenyRequestTool_NonApprover_ShouldReturnInsufficientRole()
    {
        var tool = new PimDenyRequestTool(_scopeFactory, Mock.Of<ILogger<PimDenyRequestTool>>());
        var result = await tool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["user_id"] = "regular-user",
            ["user_role"] = "Contributor",
            ["requestId"] = Guid.NewGuid().ToString(),
            ["reason"] = "Denied"
        });

        var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("status").GetString().Should().Be("error");
        doc.RootElement.GetProperty("data").GetProperty("errorCode").GetString().Should().Be("INSUFFICIENT_ROLE");
    }

    [Fact]
    public async Task PimDenyRequestTool_MissingReason_ShouldReturnError()
    {
        var tool = new PimDenyRequestTool(_scopeFactory, Mock.Of<ILogger<PimDenyRequestTool>>());
        var result = await tool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["user_id"] = "approver-1",
            ["user_role"] = "SecurityLead",
            ["requestId"] = Guid.NewGuid().ToString()
        });

        var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("status").GetString().Should().Be("error");
        doc.RootElement.GetProperty("data").GetProperty("errorCode").GetString().Should().Be("MISSING_PARAMETERS");
    }

    [Fact]
    public async Task PimDenyRequestTool_MissingRequestId_ShouldReturnError()
    {
        var tool = new PimDenyRequestTool(_scopeFactory, Mock.Of<ILogger<PimDenyRequestTool>>());
        var result = await tool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["user_id"] = "approver-1",
            ["user_role"] = "SecurityLead",
            ["reason"] = "Not justified"
        });

        var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("status").GetString().Should().Be("error");
        doc.RootElement.GetProperty("data").GetProperty("errorCode").GetString().Should().Be("MISSING_PARAMETERS");
    }

    // ─── JitRequestAccessTool ───────────────────────────────────────────

    [Fact]
    public void JitRequestAccessTool_ShouldHaveCorrectName()
    {
        var tool = new JitRequestAccessTool(_scopeFactory, Mock.Of<ILogger<JitRequestAccessTool>>());
        tool.Name.Should().Be("jit_request_access");
    }

    [Fact]
    public async Task JitRequestAccessTool_Success_ShouldReturnConnectionCommand()
    {
        var tool = new JitRequestAccessTool(_scopeFactory, Mock.Of<ILogger<JitRequestAccessTool>>());
        var result = await tool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["user_id"] = "user-1",
            ["session_id"] = Guid.NewGuid().ToString(),
            ["vmName"] = "vm-web01",
            ["resourceGroup"] = "rg-prod",
            ["justification"] = "Need SSH access for deployment troubleshooting"
        });

        var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("status").GetString().Should().Be("success");
        doc.RootElement.GetProperty("data").GetProperty("vmName").GetString().Should().Be("vm-web01");
        doc.RootElement.GetProperty("data").GetProperty("connectionCommand").GetString().Should().NotBeNullOrEmpty();
        doc.RootElement.GetProperty("data").GetProperty("jitRequestId").GetString().Should().NotBeNullOrEmpty();
        doc.RootElement.GetProperty("metadata").GetProperty("toolName").GetString().Should().Be("jit_request_access");
    }

    [Fact]
    public async Task JitRequestAccessTool_NoAuth_ShouldReturnError()
    {
        var tool = new JitRequestAccessTool(_scopeFactory, Mock.Of<ILogger<JitRequestAccessTool>>());
        var result = await tool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["vmName"] = "vm-web01",
            ["resourceGroup"] = "rg-prod",
            ["justification"] = "Need SSH access for deployment troubleshooting"
        });

        var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("status").GetString().Should().Be("error");
        doc.RootElement.GetProperty("data").GetProperty("errorCode").GetString().Should().Be("AUTH_REQUIRED");
    }

    [Fact]
    public async Task JitRequestAccessTool_MissingJustification_ShouldReturnError()
    {
        var tool = new JitRequestAccessTool(_scopeFactory, Mock.Of<ILogger<JitRequestAccessTool>>());
        var result = await tool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["user_id"] = "user-1",
            ["vmName"] = "vm-web01",
            ["resourceGroup"] = "rg-prod"
        });

        var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("status").GetString().Should().Be("error");
        doc.RootElement.GetProperty("data").GetProperty("errorCode").GetString().Should().Be("MISSING_PARAMETERS");
    }

    [Fact]
    public async Task JitRequestAccessTool_MissingVmParams_ShouldReturnError()
    {
        var tool = new JitRequestAccessTool(_scopeFactory, Mock.Of<ILogger<JitRequestAccessTool>>());
        var result = await tool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["user_id"] = "user-1",
            ["justification"] = "Need SSH access for deployment troubleshooting"
        });

        var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("status").GetString().Should().Be("error");
        doc.RootElement.GetProperty("data").GetProperty("errorCode").GetString().Should().Be("MISSING_PARAMETERS");
    }

    // ─── JitListSessionsTool ────────────────────────────────────────────

    [Fact]
    public void JitListSessionsTool_ShouldHaveCorrectName()
    {
        var tool = new JitListSessionsTool(_scopeFactory, Mock.Of<ILogger<JitListSessionsTool>>());
        tool.Name.Should().Be("jit_list_sessions");
    }

    [Fact]
    public async Task JitListSessionsTool_ActiveSessions_ShouldReturnList()
    {
        // Create a session first
        var requestTool = new JitRequestAccessTool(_scopeFactory, Mock.Of<ILogger<JitRequestAccessTool>>());
        await requestTool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["user_id"] = "user-jit-list",
            ["session_id"] = Guid.NewGuid().ToString(),
            ["vmName"] = "vm-web01",
            ["resourceGroup"] = "rg-prod",
            ["justification"] = "Need SSH access for deployment troubleshooting"
        });

        var tool = new JitListSessionsTool(_scopeFactory, Mock.Of<ILogger<JitListSessionsTool>>());
        var result = await tool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["user_id"] = "user-jit-list"
        });

        var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("status").GetString().Should().Be("success");
        doc.RootElement.GetProperty("data").GetProperty("totalCount").GetInt32().Should().Be(1);
        doc.RootElement.GetProperty("metadata").GetProperty("toolName").GetString().Should().Be("jit_list_sessions");
    }

    [Fact]
    public async Task JitListSessionsTool_EmptyList_ShouldReturnZero()
    {
        var tool = new JitListSessionsTool(_scopeFactory, Mock.Of<ILogger<JitListSessionsTool>>());
        var result = await tool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["user_id"] = "user-no-jit"
        });

        var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("status").GetString().Should().Be("success");
        doc.RootElement.GetProperty("data").GetProperty("totalCount").GetInt32().Should().Be(0);
    }

    [Fact]
    public async Task JitListSessionsTool_NoAuth_ShouldReturnError()
    {
        var tool = new JitListSessionsTool(_scopeFactory, Mock.Of<ILogger<JitListSessionsTool>>());
        var result = await tool.ExecuteAsync(new Dictionary<string, object?> { });

        var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("status").GetString().Should().Be("error");
        doc.RootElement.GetProperty("data").GetProperty("errorCode").GetString().Should().Be("AUTH_REQUIRED");
    }

    // ─── JitRevokeAccessTool ────────────────────────────────────────────

    [Fact]
    public void JitRevokeAccessTool_ShouldHaveCorrectName()
    {
        var tool = new JitRevokeAccessTool(_scopeFactory, Mock.Of<ILogger<JitRevokeAccessTool>>());
        tool.Name.Should().Be("jit_revoke_access");
    }

    [Fact]
    public async Task JitRevokeAccessTool_Success_ShouldRevokeSession()
    {
        // Create a session first
        var requestTool = new JitRequestAccessTool(_scopeFactory, Mock.Of<ILogger<JitRequestAccessTool>>());
        await requestTool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["user_id"] = "user-jit-revoke",
            ["session_id"] = Guid.NewGuid().ToString(),
            ["vmName"] = "vm-web01",
            ["resourceGroup"] = "rg-prod",
            ["justification"] = "Need SSH access for deployment troubleshooting"
        });

        var tool = new JitRevokeAccessTool(_scopeFactory, Mock.Of<ILogger<JitRevokeAccessTool>>());
        var result = await tool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["user_id"] = "user-jit-revoke",
            ["vmName"] = "vm-web01",
            ["resourceGroup"] = "rg-prod"
        });

        var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("status").GetString().Should().Be("success");
        doc.RootElement.GetProperty("data").GetProperty("revoked").GetBoolean().Should().BeTrue();
        doc.RootElement.GetProperty("data").GetProperty("message").GetString().Should().Contain("revoked");
        doc.RootElement.GetProperty("metadata").GetProperty("toolName").GetString().Should().Be("jit_revoke_access");
    }

    [Fact]
    public async Task JitRevokeAccessTool_NoAuth_ShouldReturnError()
    {
        var tool = new JitRevokeAccessTool(_scopeFactory, Mock.Of<ILogger<JitRevokeAccessTool>>());
        var result = await tool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["vmName"] = "vm-web01",
            ["resourceGroup"] = "rg-prod"
        });

        var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("status").GetString().Should().Be("error");
        doc.RootElement.GetProperty("data").GetProperty("errorCode").GetString().Should().Be("AUTH_REQUIRED");
    }

    [Fact]
    public async Task JitRevokeAccessTool_MissingParams_ShouldReturnError()
    {
        var tool = new JitRevokeAccessTool(_scopeFactory, Mock.Of<ILogger<JitRevokeAccessTool>>());
        var result = await tool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["user_id"] = "user-1"
        });

        var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("status").GetString().Should().Be("error");
        doc.RootElement.GetProperty("data").GetProperty("errorCode").GetString().Should().Be("MISSING_PARAMETERS");
    }

    [Fact]
    public async Task JitToolResponses_ShouldFollowEnvelopeSchema()
    {
        var requestTool = new JitRequestAccessTool(_scopeFactory, Mock.Of<ILogger<JitRequestAccessTool>>());
        var result = await requestTool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["user_id"] = "user-envelope",
            ["session_id"] = Guid.NewGuid().ToString(),
            ["vmName"] = "vm-test",
            ["resourceGroup"] = "rg-test",
            ["justification"] = "Testing envelope schema compliance check"
        });

        var doc = JsonDocument.Parse(result);
        doc.RootElement.TryGetProperty("status", out _).Should().BeTrue();
        doc.RootElement.TryGetProperty("data", out _).Should().BeTrue();
        doc.RootElement.TryGetProperty("metadata", out _).Should().BeTrue();
        doc.RootElement.GetProperty("metadata").TryGetProperty("toolName", out _).Should().BeTrue();
        doc.RootElement.GetProperty("metadata").TryGetProperty("executionTimeMs", out _).Should().BeTrue();
    }

    // ─── CacMapCertificateTool ─────────────────────────────────────────

    [Fact]
    public void CacMapCertificateTool_ShouldHaveCorrectName()
    {
        var tool = new CacMapCertificateTool(_scopeFactory, Mock.Of<ILogger<CacMapCertificateTool>>());
        tool.Name.Should().Be("cac_map_certificate");
    }

    [Fact]
    public async Task CacMapCertificateTool_NoAuth_ReturnsAuthRequired()
    {
        var tool = new CacMapCertificateTool(_scopeFactory, Mock.Of<ILogger<CacMapCertificateTool>>());
        var result = await tool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["role"] = "Auditor"
        });

        var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("status").GetString().Should().Be("error");
        doc.RootElement.GetProperty("data").GetProperty("errorCode").GetString().Should().Be("AUTH_REQUIRED");
    }

    [Fact]
    public async Task CacMapCertificateTool_NoRole_ReturnsInvalidRole()
    {
        var tool = new CacMapCertificateTool(_scopeFactory, Mock.Of<ILogger<CacMapCertificateTool>>());
        var result = await tool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["user_id"] = "user-test"
        });

        var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("status").GetString().Should().Be("error");
        doc.RootElement.GetProperty("data").GetProperty("errorCode").GetString().Should().Be("INVALID_ROLE");
    }

    [Fact]
    public async Task CacMapCertificateTool_InvalidRole_ReturnsInvalidRole()
    {
        var tool = new CacMapCertificateTool(_scopeFactory, Mock.Of<ILogger<CacMapCertificateTool>>());
        var result = await tool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["user_id"] = "user-test",
            ["role"] = "SuperAdmin"
        });

        var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("status").GetString().Should().Be("error");
        doc.RootElement.GetProperty("data").GetProperty("errorCode").GetString().Should().Be("INVALID_ROLE");
    }

    [Fact]
    public async Task CacMapCertificateTool_NoActiveSession_ReturnsAuthRequired()
    {
        var tool = new CacMapCertificateTool(_scopeFactory, Mock.Of<ILogger<CacMapCertificateTool>>());
        var result = await tool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["user_id"] = "user-no-session",
            ["role"] = "Auditor"
        });

        var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("status").GetString().Should().Be("error");
        doc.RootElement.GetProperty("data").GetProperty("errorCode").GetString().Should().Be("AUTH_REQUIRED");
    }

    [Fact]
    public async Task CacMapCertificateTool_ValidMapping_ReturnsSuccess()
    {
        // Create a session for the user first
        var session = await _cacService.CreateSessionAsync(
            "user-certmap", "Cert User", "certuser@gov.mil",
            "AABBCCDD11223344556677889900AABBCCDD11223344556677889900AABBCCDD",
            ClientType.VSCode, "10.0.0.1");

        var tool = new CacMapCertificateTool(_scopeFactory, Mock.Of<ILogger<CacMapCertificateTool>>());
        var result = await tool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["user_id"] = "user-certmap",
            ["role"] = "Auditor"
        });

        var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("status").GetString().Should().Be("success");
        var data = doc.RootElement.GetProperty("data");
        data.GetProperty("mappedRole").GetString().Should().Be("Compliance.Auditor");
        data.GetProperty("certificateThumbprint").GetString().Should().NotBeNullOrEmpty();
        data.GetProperty("certificateSubject").GetString().Should().Contain("user-certmap");
        data.GetProperty("message").GetString().Should().Contain("Auditor");
    }

    [Fact]
    public async Task CacMapCertificateTool_ResponseMatchesContractEnvelope()
    {
        var session = await _cacService.CreateSessionAsync(
            "user-certenv", "Envelope User", "env@gov.mil",
            "AABBCCDD11223344556677889900AABBCCDD11223344556677889900AABBCCDD",
            ClientType.VSCode, "10.0.0.1");

        var tool = new CacMapCertificateTool(_scopeFactory, Mock.Of<ILogger<CacMapCertificateTool>>());
        var result = await tool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["user_id"] = "user-certenv",
            ["role"] = "Analyst"
        });

        var doc = JsonDocument.Parse(result);
        doc.RootElement.TryGetProperty("status", out _).Should().BeTrue();
        doc.RootElement.TryGetProperty("data", out _).Should().BeTrue();
        doc.RootElement.TryGetProperty("metadata", out _).Should().BeTrue();
        doc.RootElement.GetProperty("metadata").GetProperty("toolName").GetString().Should().Be("cac_map_certificate");
        doc.RootElement.GetProperty("metadata").TryGetProperty("executionTimeMs", out _).Should().BeTrue();
    }

    // ─── PimHistoryTool (T097) ──────────────────────────────────────────────

    [Fact]
    public void PimHistoryTool_ShouldHaveCorrectName()
    {
        var tool = new PimHistoryTool(_scopeFactory, Mock.Of<ILogger<PimHistoryTool>>());
        tool.Name.Should().Be("pim_history");
    }

    [Fact]
    public void PimHistoryTool_ShouldHaveOptionalParameters()
    {
        var tool = new PimHistoryTool(_scopeFactory, Mock.Of<ILogger<PimHistoryTool>>());
        tool.Parameters.Should().ContainKey("days");
        tool.Parameters.Should().ContainKey("roleName");
        tool.Parameters.Should().ContainKey("filterUserId");
        tool.Parameters.Should().ContainKey("scope");
        tool.Parameters["days"].Required.Should().BeFalse();
        tool.Parameters["roleName"].Required.Should().BeFalse();
    }

    [Fact]
    public async Task PimHistoryTool_NoAuth_ReturnsAuthRequired()
    {
        var tool = new PimHistoryTool(_scopeFactory, Mock.Of<ILogger<PimHistoryTool>>());
        var result = await tool.ExecuteAsync(new Dictionary<string, object?>());

        var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("status").GetString().Should().Be("error");
        doc.RootElement.GetProperty("data").GetProperty("errorCode").GetString().Should().Be("AUTH_REQUIRED");
    }

    [Fact]
    public async Task PimHistoryTool_ValidUser_ReturnsSuccess()
    {
        // Seed an activation for history
        await _cacService.CreateSessionAsync(
            "user-pimhist-1", "Hist User", "hist@gov.mil",
            "AABBCCDD11223344556677889900AABBCCDD11223344556677889900AABBCCDD",
            ClientType.VSCode, "10.0.0.1");

        // Create a PIM activation through the service to have history data
        using var scope = _scopeFactory.CreateScope();
        var pimService = scope.ServiceProvider.GetRequiredService<IPimService>();
        await pimService.ActivateRoleAsync(
            "user-pimhist-1", "Contributor", "default",
            "Compliance review task needed", null, 4, Guid.NewGuid());

        var tool = new PimHistoryTool(_scopeFactory, Mock.Of<ILogger<PimHistoryTool>>());
        var result = await tool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["user_id"] = "user-pimhist-1"
        });

        var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("status").GetString().Should().Be("success");
        var data = doc.RootElement.GetProperty("data");
        data.TryGetProperty("history", out _).Should().BeTrue();
        data.GetProperty("totalCount").GetInt32().Should().BeGreaterOrEqualTo(1);
        data.TryGetProperty("nistControlMapping", out _).Should().BeTrue();
    }

    [Fact]
    public async Task PimHistoryTool_ResponseContainsNistMapping()
    {
        using var scope = _scopeFactory.CreateScope();
        var pimService = scope.ServiceProvider.GetRequiredService<IPimService>();
        await pimService.ActivateRoleAsync(
            "user-pimhist-2", "Reader", "default",
            "Checking NIST mapping in tool", null, 2, Guid.NewGuid());

        var tool = new PimHistoryTool(_scopeFactory, Mock.Of<ILogger<PimHistoryTool>>());
        var result = await tool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["user_id"] = "user-pimhist-2"
        });

        var doc = JsonDocument.Parse(result);
        var nist = doc.RootElement.GetProperty("data").GetProperty("nistControlMapping");
        nist.GetArrayLength().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task PimHistoryTool_ResponseMatchesContractEnvelope()
    {
        var tool = new PimHistoryTool(_scopeFactory, Mock.Of<ILogger<PimHistoryTool>>());
        var result = await tool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["user_id"] = "user-pimhist-3"
        });

        var doc = JsonDocument.Parse(result);
        doc.RootElement.TryGetProperty("status", out _).Should().BeTrue();
        doc.RootElement.TryGetProperty("data", out _).Should().BeTrue();
        doc.RootElement.TryGetProperty("metadata", out _).Should().BeTrue();
        doc.RootElement.GetProperty("metadata").GetProperty("toolName").GetString().Should().Be("pim_history");
        doc.RootElement.GetProperty("metadata").TryGetProperty("executionTimeMs", out _).Should().BeTrue();
    }

    [Fact]
    public async Task PimHistoryTool_DaysClampedToValidRange()
    {
        var tool = new PimHistoryTool(_scopeFactory, Mock.Of<ILogger<PimHistoryTool>>());

        // Days = 0 should be clamped to 1, still returns success
        var result = await tool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["user_id"] = "user-pimhist-4",
            ["days"] = 0
        });

        var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("status").GetString().Should().Be("success");
    }

    private class TestDbContextFactory : IDbContextFactory<AtoCopilotContext>
    {
        private readonly DbContextOptions<AtoCopilotContext> _options;
        public TestDbContextFactory(DbContextOptions<AtoCopilotContext> options) => _options = options;
        public AtoCopilotContext CreateDbContext() => new(_options);
    }
}
