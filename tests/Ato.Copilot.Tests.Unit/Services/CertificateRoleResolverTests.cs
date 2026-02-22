using Xunit;
using FluentAssertions;
using Moq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Ato.Copilot.Agents.Compliance.Services;
using Ato.Copilot.Core.Constants;
using Ato.Copilot.Core.Data.Context;
using Ato.Copilot.Core.Models.Auth;

namespace Ato.Copilot.Tests.Unit.Services;

/// <summary>
/// Tests for CertificateRoleResolver — 4-tier role resolution chain (FR-028).
/// </summary>
public class CertificateRoleResolverTests : IDisposable
{
    private readonly IDbContextFactory<AtoCopilotContext> _dbFactory;
    private readonly CertificateRoleResolver _resolver;
    private readonly string _dbName;

    public CertificateRoleResolverTests()
    {
        _dbName = $"CertResolverTests_{Guid.NewGuid()}";
        var options = new DbContextOptionsBuilder<AtoCopilotContext>()
            .UseInMemoryDatabase(_dbName)
            .Options;
        _dbFactory = new InMemoryDbContextFactory(options);
        _resolver = new CertificateRoleResolver(
            _dbFactory,
            Mock.Of<ILogger<CertificateRoleResolver>>());
    }

    public void Dispose()
    {
        using var db = _dbFactory.CreateDbContext();
        db.Database.EnsureDeleted();
    }

    // ─── ResolveRoleAsync Tests ──────────────────────────────────────────

    [Fact]
    public async Task ResolveRoleAsync_ExplicitMappingByThumbprint_ReturnsRole()
    {
        // Seed an explicit mapping
        using (var db = _dbFactory.CreateDbContext())
        {
            db.CertificateRoleMappings.Add(new CertificateRoleMapping
            {
                CertificateThumbprint = "AABB1122334455667788AABB112233445566",
                CertificateSubject = "CN=SMITH.JANE.M.1234567890",
                MappedRole = ComplianceRoles.Auditor,
                CreatedBy = "admin",
                IsActive = true
            });
            await db.SaveChangesAsync();
        }

        var role = await _resolver.ResolveRoleAsync(
            "AABB1122334455667788AABB112233445566",
            "CN=SMITH.JANE.M.1234567890",
            "user-001");

        role.Should().Be(ComplianceRoles.Auditor);
    }

    [Fact]
    public async Task ResolveRoleAsync_ExplicitMappingBySubject_ReturnsRole()
    {
        using (var db = _dbFactory.CreateDbContext())
        {
            db.CertificateRoleMappings.Add(new CertificateRoleMapping
            {
                CertificateThumbprint = "",
                CertificateSubject = "CN=DOE.JOHN.A.9876543210",
                MappedRole = ComplianceRoles.SecurityLead,
                CreatedBy = "admin",
                IsActive = true
            });
            await db.SaveChangesAsync();
        }

        var role = await _resolver.ResolveRoleAsync(
            null, "CN=DOE.JOHN.A.9876543210", "user-002");

        role.Should().Be(ComplianceRoles.SecurityLead);
    }

    [Fact]
    public async Task ResolveRoleAsync_InactiveMappingSkipped_DefaultsToPlatformEngineer()
    {
        using (var db = _dbFactory.CreateDbContext())
        {
            db.CertificateRoleMappings.Add(new CertificateRoleMapping
            {
                CertificateThumbprint = "FFEE1122334455667788",
                CertificateSubject = "CN=INACTIVE.USER",
                MappedRole = ComplianceRoles.Administrator,
                CreatedBy = "admin",
                IsActive = false // Inactive!
            });
            await db.SaveChangesAsync();
        }

        var role = await _resolver.ResolveRoleAsync(
            "FFEE1122334455667788", "CN=INACTIVE.USER", "user-003");

        // Inactive mapping should be skipped, fall through to default
        role.Should().Be(ComplianceRoles.PlatformEngineer);
    }

    [Fact]
    public async Task ResolveRoleAsync_NoMapping_NoAdGroup_NoRbac_DefaultsToPlatformEngineer()
    {
        var role = await _resolver.ResolveRoleAsync(
            "UNKNOWN_THUMBPRINT", "CN=UNKNOWN.USER", "user-unknown");

        role.Should().Be(ComplianceRoles.PlatformEngineer);
    }

    [Fact]
    public async Task ResolveRoleAsync_NullThumbprintAndSubject_DefaultsToPlatformEngineer()
    {
        var role = await _resolver.ResolveRoleAsync(null, null, "user-no-cert");

        role.Should().Be(ComplianceRoles.PlatformEngineer);
    }

    // ─── MapCertificateAsync Tests ───────────────────────────────────────

    [Fact]
    public async Task MapCertificateAsync_CreatesNewMapping()
    {
        var mapping = await _resolver.MapCertificateAsync(
            "NEW_THUMBPRINT_1234567890", "CN=NEW.USER", ComplianceRoles.Analyst, "admin-user");

        mapping.Should().NotBeNull();
        mapping.CertificateThumbprint.Should().Be("NEW_THUMBPRINT_1234567890");
        mapping.CertificateSubject.Should().Be("CN=NEW.USER");
        mapping.MappedRole.Should().Be(ComplianceRoles.Analyst);
        mapping.CreatedBy.Should().Be("admin-user");
        mapping.IsActive.Should().BeTrue();

        // Verify persisted
        using var db = _dbFactory.CreateDbContext();
        var persisted = await db.CertificateRoleMappings
            .FirstOrDefaultAsync(m => m.CertificateThumbprint == "NEW_THUMBPRINT_1234567890");
        persisted.Should().NotBeNull();
        persisted!.MappedRole.Should().Be(ComplianceRoles.Analyst);
    }

    [Fact]
    public async Task MapCertificateAsync_UpdatesExistingMapping()
    {
        // Create initial mapping
        await _resolver.MapCertificateAsync(
            "UPDATE_THUMBPRINT_1234567890", "CN=UPDATE.USER",
            ComplianceRoles.Viewer, "admin");

        // Update to a different role
        var updated = await _resolver.MapCertificateAsync(
            "UPDATE_THUMBPRINT_1234567890", "CN=UPDATE.USER",
            ComplianceRoles.Administrator, "admin");

        updated.MappedRole.Should().Be(ComplianceRoles.Administrator);

        // Verify only one mapping exists
        using var db = _dbFactory.CreateDbContext();
        var count = await db.CertificateRoleMappings
            .CountAsync(m => m.CertificateThumbprint == "UPDATE_THUMBPRINT_1234567890");
        count.Should().Be(1);
    }

    [Fact]
    public async Task MapCertificateAsync_InvalidRole_ThrowsArgumentException()
    {
        var act = async () => await _resolver.MapCertificateAsync(
            "THUMB", "CN=USER", "InvalidRole", "admin");

        await act.Should().ThrowAsync<ArgumentException>()
            .WithParameterName("role");
    }

    // ─── IsValidRole Tests ───────────────────────────────────────────────

    [Theory]
    [InlineData("Compliance.Administrator", true)]
    [InlineData("Compliance.Auditor", true)]
    [InlineData("Compliance.Analyst", true)]
    [InlineData("Compliance.Viewer", true)]
    [InlineData("Compliance.SecurityLead", true)]
    [InlineData("Compliance.PlatformEngineer", true)]
    [InlineData("InvalidRole", false)]
    [InlineData("", false)]
    public void IsValidRole_ShouldValidateCorrectly(string role, bool expected)
    {
        CertificateRoleResolver.IsValidRole(role).Should().Be(expected);
    }

    // ─── Helper ──────────────────────────────────────────────────────────

    private class InMemoryDbContextFactory : IDbContextFactory<AtoCopilotContext>
    {
        private readonly DbContextOptions<AtoCopilotContext> _options;
        public InMemoryDbContextFactory(DbContextOptions<AtoCopilotContext> options) => _options = options;
        public AtoCopilotContext CreateDbContext() => new(_options);
    }
}
