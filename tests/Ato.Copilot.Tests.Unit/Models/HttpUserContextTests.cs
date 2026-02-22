using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Moq;
using Xunit;
using FluentAssertions;
using Ato.Copilot.Core.Constants;
using Ato.Copilot.Mcp.Models;

namespace Ato.Copilot.Tests.Unit.Models;

/// <summary>
/// Unit tests for <see cref="HttpUserContext"/> verifying claim extraction,
/// lazy caching, role priority resolution, and fallback defaults.
/// </summary>
public class HttpUserContextTests
{
    // ── Authenticated user ────────────────────────────────────────────────

    [Fact]
    public void AuthenticatedUser_ReturnsOidAsUserId()
    {
        var ctx = CreateAuthenticatedContext(
            oid: "user-oid-123",
            name: "Jane Doe",
            roles: ComplianceRoles.Analyst);

        var sut = new HttpUserContext(Accessor(ctx));

        sut.UserId.Should().Be("user-oid-123");
        sut.IsAuthenticated.Should().BeTrue();
    }

    [Fact]
    public void AuthenticatedUser_FallsBackToSubWhenOidMissing()
    {
        var claims = new List<Claim>
        {
            new("sub", "user-sub-456"),
            new(ClaimTypes.Name, "John"),
            new(ClaimTypes.Role, ComplianceRoles.Viewer),
        };
        var ctx = CreateContext(claims, isAuthenticated: true, name: "John");

        var sut = new HttpUserContext(Accessor(ctx));

        sut.UserId.Should().Be("user-sub-456");
    }

    [Fact]
    public void AuthenticatedUser_ReturnsAnonymousWhenNoOidOrSub()
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, "NoId User"),
            new(ClaimTypes.Role, ComplianceRoles.Viewer),
        };
        var ctx = CreateContext(claims, isAuthenticated: true, name: "NoId User");

        var sut = new HttpUserContext(Accessor(ctx));

        sut.UserId.Should().Be("anonymous");
    }

    [Fact]
    public void AuthenticatedUser_ReturnsDisplayName()
    {
        var ctx = CreateAuthenticatedContext(
            oid: "u1", name: "Alice Admin", roles: ComplianceRoles.Administrator);

        var sut = new HttpUserContext(Accessor(ctx));

        sut.DisplayName.Should().Be("Alice Admin");
    }

    [Fact]
    public void AuthenticatedUser_DisplayNameFallsBackToUserId()
    {
        var claims = new List<Claim>
        {
            new("oid", "user-123"),
            new(ClaimTypes.Role, ComplianceRoles.Viewer),
        };
        // No name claim, identity has no name
        var ctx = CreateContext(claims, isAuthenticated: true, name: null);

        var sut = new HttpUserContext(Accessor(ctx));

        sut.DisplayName.Should().Be("user-123");
    }

    // ── Role priority ─────────────────────────────────────────────────────

    [Theory]
    [InlineData(ComplianceRoles.Administrator)]
    [InlineData(ComplianceRoles.SecurityLead)]
    [InlineData(ComplianceRoles.Analyst)]
    [InlineData(ComplianceRoles.Auditor)]
    [InlineData(ComplianceRoles.PlatformEngineer)]
    [InlineData(ComplianceRoles.Viewer)]
    public void SingleRole_ReturnsCorrectRole(string role)
    {
        var ctx = CreateAuthenticatedContext(oid: "u1", name: "User", roles: role);
        var sut = new HttpUserContext(Accessor(ctx));

        sut.Role.Should().Be(role);
    }

    [Fact]
    public void MultipleRoles_ReturnsHighestPriority()
    {
        var claims = new List<Claim>
        {
            new("oid", "u1"),
            new(ClaimTypes.Name, "Multi"),
            new(ClaimTypes.Role, ComplianceRoles.Analyst),
            new(ClaimTypes.Role, ComplianceRoles.SecurityLead),
            new(ClaimTypes.Role, ComplianceRoles.Viewer),
        };
        var ctx = CreateContext(claims, isAuthenticated: true, name: "Multi");

        var sut = new HttpUserContext(Accessor(ctx));

        sut.Role.Should().Be(ComplianceRoles.SecurityLead);
    }

    [Fact]
    public void AuthenticatedUser_NoRoleClaims_DefaultsToViewer()
    {
        var claims = new List<Claim>
        {
            new("oid", "u1"),
            new(ClaimTypes.Name, "NoRole"),
        };
        var ctx = CreateContext(claims, isAuthenticated: true, name: "NoRole");

        var sut = new HttpUserContext(Accessor(ctx));

        sut.Role.Should().Be(ComplianceRoles.Viewer);
    }

    // ── Unauthenticated / null context ────────────────────────────────────

    [Fact]
    public void UnauthenticatedUser_ReturnsDefaults()
    {
        var ctx = CreateContext(new List<Claim>(), isAuthenticated: false, name: null);

        var sut = new HttpUserContext(Accessor(ctx));

        sut.UserId.Should().Be("anonymous");
        sut.DisplayName.Should().Be("anonymous");
        sut.Role.Should().Be(ComplianceRoles.Viewer);
        sut.IsAuthenticated.Should().BeFalse();
    }

    [Fact]
    public void NullHttpContext_ReturnsDefaults()
    {
        var accessor = new Mock<IHttpContextAccessor>();
        accessor.Setup(a => a.HttpContext).Returns((HttpContext?)null);

        var sut = new HttpUserContext(accessor.Object);

        sut.UserId.Should().Be("anonymous");
        sut.DisplayName.Should().Be("anonymous");
        sut.Role.Should().Be(ComplianceRoles.Viewer);
        sut.IsAuthenticated.Should().BeFalse();
    }

    // ── Lazy caching ──────────────────────────────────────────────────────

    [Fact]
    public void Properties_AreCachedAfterFirstAccess()
    {
        var ctx = CreateAuthenticatedContext(
            oid: "cached-user", name: "Cache Test", roles: ComplianceRoles.Analyst);
        var accessor = Accessor(ctx);

        var sut = new HttpUserContext(accessor);

        // First access
        var userId1 = sut.UserId;
        var name1 = sut.DisplayName;
        var role1 = sut.Role;
        var auth1 = sut.IsAuthenticated;

        // Second access — same values
        sut.UserId.Should().Be(userId1);
        sut.DisplayName.Should().Be(name1);
        sut.Role.Should().Be(role1);
        sut.IsAuthenticated.Should().Be(auth1);
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private static IHttpContextAccessor Accessor(HttpContext ctx)
    {
        var mock = new Mock<IHttpContextAccessor>();
        mock.Setup(a => a.HttpContext).Returns(ctx);
        return mock.Object;
    }

    private static HttpContext CreateAuthenticatedContext(string oid, string name, params string[] roles)
    {
        var claims = new List<Claim>
        {
            new("oid", oid),
            new(ClaimTypes.Name, name),
        };
        foreach (var role in roles)
            claims.Add(new Claim(ClaimTypes.Role, role));

        return CreateContext(claims, isAuthenticated: true, name: name);
    }

    private static HttpContext CreateContext(List<Claim> claims, bool isAuthenticated, string? name)
    {
        var identity = isAuthenticated
            ? new ClaimsIdentity(claims, "TestScheme", ClaimTypes.Name, ClaimTypes.Role)
            : new ClaimsIdentity(claims);

        var principal = new ClaimsPrincipal(identity);

        var httpContext = new DefaultHttpContext
        {
            User = principal,
        };
        return httpContext;
    }
}
