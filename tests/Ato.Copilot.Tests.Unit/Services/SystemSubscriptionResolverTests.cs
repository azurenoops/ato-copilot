using Xunit;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Moq;
using Ato.Copilot.Agents.Compliance.Services;
using Ato.Copilot.Core.Data.Context;
using Ato.Copilot.Core.Models.Compliance;

namespace Ato.Copilot.Tests.Unit.Services;

/// <summary>
/// T239 — Unit tests for SystemSubscriptionResolver (Phase 17 §9a.1).
/// Valid lookup, no match returns null, cache invalidation,
/// multiple systems sharing subscription → first match (deterministic OrderBy Id).
/// </summary>
public class SystemSubscriptionResolverTests : IDisposable
{
    private readonly DbContextOptions<AtoCopilotContext> _dbOptions;

    public SystemSubscriptionResolverTests()
    {
        _dbOptions = new DbContextOptionsBuilder<AtoCopilotContext>()
            .UseInMemoryDatabase(databaseName: $"SubResolver_{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
    }

    public void Dispose()
    {
        using var db = new AtoCopilotContext(_dbOptions);
        db.Database.EnsureDeleted();
    }

    [Fact]
    public async Task ResolveAsync_ValidSubscription_ReturnsSystemId()
    {
        // Arrange
        await SeedSystem("sys-001", new List<string> { "sub-aaa", "sub-bbb" });
        var resolver = CreateResolver();

        // Act
        var result = await resolver.ResolveAsync("sub-aaa");

        // Assert
        result.Should().Be("sys-001");
    }

    [Fact]
    public async Task ResolveAsync_NoMatch_ReturnsNull()
    {
        // Arrange
        await SeedSystem("sys-001", new List<string> { "sub-aaa" });
        var resolver = CreateResolver();

        // Act
        var result = await resolver.ResolveAsync("sub-nonexistent");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task ResolveAsync_NullOrEmpty_ReturnsNull()
    {
        var resolver = CreateResolver();

        (await resolver.ResolveAsync(null!)).Should().BeNull();
        (await resolver.ResolveAsync("")).Should().BeNull();
        (await resolver.ResolveAsync("   ")).Should().BeNull();
    }

    [Fact]
    public async Task ResolveAsync_CacheInvalidation_ReloadsFromDb()
    {
        // Arrange — seed initial data
        await SeedSystem("sys-001", new List<string> { "sub-aaa" });
        var resolver = CreateResolver();

        // First call — loads cache
        var result1 = await resolver.ResolveAsync("sub-aaa");
        result1.Should().Be("sys-001");

        // Add a new system with a different subscription
        await SeedSystem("sys-002", new List<string> { "sub-ccc" });

        // Without invalidation — stale cache
        var result2 = await resolver.ResolveAsync("sub-ccc");
        // Might be null from stale cache (within TTL), but invalidation should work:
        resolver.InvalidateCache();

        // After invalidation — reloads from DB
        var result3 = await resolver.ResolveAsync("sub-ccc");
        result3.Should().Be("sys-002");
    }

    [Fact]
    public async Task ResolveAsync_MultipleSystemsSameSubscription_ReturnsFirstByIdOrder()
    {
        // Arrange — two systems sharing the same subscription
        // OrderBy(Id) is deterministic — "sys-001" < "sys-002"
        await SeedSystem("sys-002", new List<string> { "sub-shared" });
        await SeedSystem("sys-001", new List<string> { "sub-shared" });
        var resolver = CreateResolver();

        // Act
        var result = await resolver.ResolveAsync("sub-shared");

        // Assert — first by Id order = sys-001
        result.Should().Be("sys-001");
    }

    [Fact]
    public async Task ResolveAsync_InactiveSystem_NotResolved()
    {
        // Arrange — inactive system
        await SeedSystem("sys-inactive", new List<string> { "sub-xxx" }, isActive: false);
        var resolver = CreateResolver();

        // Act
        var result = await resolver.ResolveAsync("sub-xxx");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task ResolveAsync_CaseInsensitive()
    {
        // Arrange
        await SeedSystem("sys-001", new List<string> { "Sub-AAA" });
        var resolver = CreateResolver();

        // Act
        var result = await resolver.ResolveAsync("SUB-AAA");

        // Assert
        result.Should().Be("sys-001");
    }

    [Fact]
    public async Task ResolveAsync_NoAzureProfile_Skipped()
    {
        // Arrange — system without AzureProfile
        await using var db = new AtoCopilotContext(_dbOptions);
        db.RegisteredSystems.Add(new RegisteredSystem
        {
            Id = "sys-noprofile",
            Name = "No Profile System",
            SystemType = SystemType.MajorApplication,
            MissionCriticality = MissionCriticality.MissionCritical,
            HostingEnvironment = "On-Premises",
            CreatedBy = "test",
            AzureProfile = null
        });
        await db.SaveChangesAsync();

        var resolver = CreateResolver();

        // Act
        var result = await resolver.ResolveAsync("any-sub");

        // Assert
        result.Should().BeNull();
    }

    private SystemSubscriptionResolver CreateResolver()
    {
        return new SystemSubscriptionResolver(
            new TestDbContextFactory(_dbOptions),
            Mock.Of<ILogger<SystemSubscriptionResolver>>());
    }

    private async Task SeedSystem(string systemId, List<string> subscriptionIds, bool isActive = true)
    {
        await using var db = new AtoCopilotContext(_dbOptions);
        db.RegisteredSystems.Add(new RegisteredSystem
        {
            Id = systemId,
            Name = $"System {systemId}",
            SystemType = SystemType.MajorApplication,
            MissionCriticality = MissionCriticality.MissionCritical,
            HostingEnvironment = "Azure Government",
            CreatedBy = "test",
            IsActive = isActive,
            AzureProfile = new AzureEnvironmentProfile
            {
                CloudEnvironment = AzureCloudEnvironment.Government,
                ArmEndpoint = "https://management.usgovcloudapi.net",
                AuthenticationEndpoint = "https://login.microsoftonline.us",
                SubscriptionIds = subscriptionIds
            }
        });
        await db.SaveChangesAsync();
    }

    private sealed class TestDbContextFactory : IDbContextFactory<AtoCopilotContext>
    {
        private readonly DbContextOptions<AtoCopilotContext> _options;
        public TestDbContextFactory(DbContextOptions<AtoCopilotContext> options) => _options = options;
        public AtoCopilotContext CreateDbContext() => new(_options);
    }
}
