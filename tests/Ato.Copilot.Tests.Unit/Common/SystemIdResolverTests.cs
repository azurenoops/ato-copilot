// ─────────────────────────────────────────────────────────────────────────────
// Feature 020 · SystemIdResolver — Auto-resolve system names/acronyms → GUIDs
// Tests for SystemIdResolver + BaseTool.TryResolveSystemIdAsync integration
// ─────────────────────────────────────────────────────────────────────────────

using Xunit;
using FluentAssertions;
using Moq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Ato.Copilot.Agents.Common;
using Ato.Copilot.Core.Data.Context;
using Ato.Copilot.Core.Interfaces.Compliance;
using Ato.Copilot.Core.Models.Compliance;
using Microsoft.Extensions.Logging.Abstractions;

namespace Ato.Copilot.Tests.Unit.Common;

public class SystemIdResolverTests
{
    private readonly string _testSystemId = Guid.NewGuid().ToString();
    private const string TestSystemName = "Eagles Nest";
    private const string TestSystemAcronym = "EN";

    // ─── SystemIdResolver.ResolveAsync ───────────────────────────────────────

    [Fact]
    public async Task ResolveAsync_GuidInput_ReturnsSameGuid()
    {
        var resolver = CreateResolver(Array.Empty<RegisteredSystem>());
        var guid = Guid.NewGuid().ToString();

        var result = await resolver.ResolveAsync(guid);

        result.Should().Be(guid, "GUID values should pass through without DB lookup");
    }

    [Theory]
    [InlineData("  {0}  ")]   // Whitespace-padded GUID
    [InlineData("{0}")]        // Normal GUID
    public async Task ResolveAsync_GuidWithWhitespace_ReturnsTrimmedGuid(string template)
    {
        var resolver = CreateResolver(Array.Empty<RegisteredSystem>());
        var guid = Guid.NewGuid().ToString();
        var input = string.Format(template, guid);

        var result = await resolver.ResolveAsync(input);

        result.Should().Be(input.Trim());
    }

    [Fact]
    public async Task ResolveAsync_SystemName_ResolvesToGuid()
    {
        var system = CreateSystem();
        var resolver = CreateResolver(new[] { system });

        var result = await resolver.ResolveAsync(TestSystemName);

        result.Should().Be(_testSystemId);
    }

    [Fact]
    public async Task ResolveAsync_SystemName_CaseInsensitive()
    {
        var system = CreateSystem();
        var resolver = CreateResolver(new[] { system });

        var result = await resolver.ResolveAsync("eagles nest");

        result.Should().Be(_testSystemId);
    }

    [Fact]
    public async Task ResolveAsync_SystemName_UpperCase()
    {
        var system = CreateSystem();
        var resolver = CreateResolver(new[] { system });

        var result = await resolver.ResolveAsync("EAGLES NEST");

        result.Should().Be(_testSystemId);
    }

    [Fact]
    public async Task ResolveAsync_Acronym_ResolvesToGuid()
    {
        var system = CreateSystem();
        var resolver = CreateResolver(new[] { system });

        var result = await resolver.ResolveAsync(TestSystemAcronym);

        result.Should().Be(_testSystemId);
    }

    [Fact]
    public async Task ResolveAsync_Acronym_CaseInsensitive()
    {
        var system = CreateSystem();
        var resolver = CreateResolver(new[] { system });

        var result = await resolver.ResolveAsync("en");

        result.Should().Be(_testSystemId);
    }

    [Fact]
    public async Task ResolveAsync_NoMatch_ThrowsInvalidOperation()
    {
        var resolver = CreateResolver(Array.Empty<RegisteredSystem>());

        var act = async () => await resolver.ResolveAsync("NonexistentSystem");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*No registered system found*NonexistentSystem*");
    }

    [Fact]
    public async Task ResolveAsync_AmbiguousMatch_ThrowsInvalidOperation()
    {
        var system1 = new RegisteredSystem
        {
            Id = Guid.NewGuid().ToString(),
            Name = "Shared Name",
            Acronym = "SN",
            SystemType = SystemType.MajorApplication,
            MissionCriticality = MissionCriticality.MissionCritical
        };
        var system2 = new RegisteredSystem
        {
            Id = Guid.NewGuid().ToString(),
            Name = "Another System",
            Acronym = "Shared Name",   // Acronym matches system1's name
            SystemType = SystemType.MajorApplication,
            MissionCriticality = MissionCriticality.MissionCritical
        };
        var resolver = CreateResolver(new[] { system1, system2 });

        var act = async () => await resolver.ResolveAsync("Shared Name");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*matches 2 systems*");
    }

    [Fact]
    public async Task ResolveAsync_EmptyInput_ThrowsInvalidOperation()
    {
        var resolver = CreateResolver(Array.Empty<RegisteredSystem>());

        var act = async () => await resolver.ResolveAsync("");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*system_id*required*");
    }

    [Fact]
    public async Task ResolveAsync_WhitespaceOnly_ThrowsInvalidOperation()
    {
        var resolver = CreateResolver(Array.Empty<RegisteredSystem>());

        var act = async () => await resolver.ResolveAsync("   ");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*system_id*required*");
    }

    [Fact]
    public async Task ResolveAsync_NullAcronym_MatchesByNameOnly()
    {
        var system = new RegisteredSystem
        {
            Id = _testSystemId,
            Name = "No Acronym System",
            Acronym = null,
            SystemType = SystemType.MajorApplication,
            MissionCriticality = MissionCriticality.MissionCritical
        };
        var resolver = CreateResolver(new[] { system });

        var result = await resolver.ResolveAsync("No Acronym System");

        result.Should().Be(_testSystemId);
    }

    [Fact]
    public async Task ResolveAsync_CancellationRequested_Throws()
    {
        var system = CreateSystem();
        var resolver = CreateResolver(new[] { system });
        var cts = new CancellationTokenSource();
        cts.Cancel();

        var act = async () => await resolver.ResolveAsync("Eagles Nest", cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    // ─── BaseTool.TryResolveSystemIdAsync (integration via ExecuteAsync) ─────

    [Fact]
    public async Task BaseTool_NoResolver_SkipsResolution()
    {
        var tool = new FakeSystemIdTool();
        // SystemIdResolver is null by default
        var args = new Dictionary<string, object?> { ["system_id"] = "Eagles Nest" };

        await tool.ExecuteAsync(args);

        // The name should remain unchanged
        args["system_id"].Should().Be("Eagles Nest");
    }

    [Fact]
    public async Task BaseTool_WithResolver_ResolvesNameToGuid()
    {
        var resolvedGuid = Guid.NewGuid().ToString();
        var mockResolver = new Mock<ISystemIdResolver>();
        mockResolver.Setup(r => r.ResolveAsync("Eagles Nest", It.IsAny<CancellationToken>()))
            .ReturnsAsync(resolvedGuid);

        var tool = new FakeSystemIdTool { SystemIdResolver = mockResolver.Object };
        var args = new Dictionary<string, object?> { ["system_id"] = "Eagles Nest" };

        await tool.ExecuteAsync(args);

        args["system_id"].Should().Be(resolvedGuid);
    }

    [Fact]
    public async Task BaseTool_WithResolver_GuidPassesThrough()
    {
        var guid = Guid.NewGuid().ToString();
        var mockResolver = new Mock<ISystemIdResolver>();

        var tool = new FakeSystemIdTool { SystemIdResolver = mockResolver.Object };
        var args = new Dictionary<string, object?> { ["system_id"] = guid };

        await tool.ExecuteAsync(args);

        args["system_id"].Should().Be(guid);
        // Resolver should never have been called for a GUID
        mockResolver.Verify(r => r.ResolveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task BaseTool_WithResolver_NoSystemIdParam_Skips()
    {
        var mockResolver = new Mock<ISystemIdResolver>();

        var tool = new FakeNoSystemIdTool { SystemIdResolver = mockResolver.Object };
        var args = new Dictionary<string, object?> { ["other_param"] = "value" };

        await tool.ExecuteAsync(args);

        // Resolver should not be called — tool has no system_id parameter
        mockResolver.Verify(r => r.ResolveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task BaseTool_WithResolver_NullSystemId_Skips()
    {
        var mockResolver = new Mock<ISystemIdResolver>();

        var tool = new FakeSystemIdTool { SystemIdResolver = mockResolver.Object };
        var args = new Dictionary<string, object?> { ["system_id"] = null };

        await tool.ExecuteAsync(args);

        mockResolver.Verify(r => r.ResolveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task BaseTool_WithResolver_EmptyStringSystemId_Skips()
    {
        var mockResolver = new Mock<ISystemIdResolver>();

        var tool = new FakeSystemIdTool { SystemIdResolver = mockResolver.Object };
        var args = new Dictionary<string, object?> { ["system_id"] = "" };

        await tool.ExecuteAsync(args);

        mockResolver.Verify(r => r.ResolveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task BaseTool_WithResolver_JsonElement_ResolvesCorrectly()
    {
        var resolvedGuid = Guid.NewGuid().ToString();
        var mockResolver = new Mock<ISystemIdResolver>();
        mockResolver.Setup(r => r.ResolveAsync("Eagles Nest", It.IsAny<CancellationToken>()))
            .ReturnsAsync(resolvedGuid);

        // Simulate how MCP protocol delivers values — as JsonElement
        var json = System.Text.Json.JsonDocument.Parse("\"Eagles Nest\"");
        var jsonElement = json.RootElement.Clone();

        var tool = new FakeSystemIdTool { SystemIdResolver = mockResolver.Object };
        var args = new Dictionary<string, object?> { ["system_id"] = jsonElement };

        await tool.ExecuteAsync(args);

        args["system_id"].Should().Be(resolvedGuid);
    }

    [Fact]
    public async Task BaseTool_WithResolver_JsonElementGuid_SkipsResolution()
    {
        var guid = Guid.NewGuid().ToString();
        var mockResolver = new Mock<ISystemIdResolver>();

        var json = System.Text.Json.JsonDocument.Parse($"\"{guid}\"");
        var jsonElement = json.RootElement.Clone();

        var tool = new FakeSystemIdTool { SystemIdResolver = mockResolver.Object };
        var args = new Dictionary<string, object?> { ["system_id"] = jsonElement };

        await tool.ExecuteAsync(args);

        // Value remains as JsonElement since no resolution was needed
        args["system_id"].Should().BeOfType<System.Text.Json.JsonElement>();
        ((System.Text.Json.JsonElement)args["system_id"]!).GetString().Should().Be(guid);
        mockResolver.Verify(r => r.ResolveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task BaseTool_WithResolver_ResolutionFails_PropagatesException()
    {
        var mockResolver = new Mock<ISystemIdResolver>();
        mockResolver.Setup(r => r.ResolveAsync("Unknown", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("No registered system found matching 'Unknown'."));

        var tool = new FakeSystemIdTool { SystemIdResolver = mockResolver.Object };
        var args = new Dictionary<string, object?> { ["system_id"] = "Unknown" };

        var act = async () => await tool.ExecuteAsync(args);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*No registered system found*");
    }

    [Fact]
    public async Task BaseTool_WithResolver_MissingSystemIdArg_Skips()
    {
        var mockResolver = new Mock<ISystemIdResolver>();

        var tool = new FakeSystemIdTool { SystemIdResolver = mockResolver.Object };
        // Tool declares system_id in Parameters, but caller didn't provide it
        var args = new Dictionary<string, object?> { ["other_param"] = "value" };

        await tool.ExecuteAsync(args);

        mockResolver.Verify(r => r.ResolveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private RegisteredSystem CreateSystem() => new()
    {
        Id = _testSystemId,
        Name = TestSystemName,
        Acronym = TestSystemAcronym,
        SystemType = SystemType.MajorApplication,
        MissionCriticality = MissionCriticality.MissionCritical
    };

    private SystemIdResolver CreateResolver(IEnumerable<RegisteredSystem> systems)
    {
        var dbName = $"ResolverTest_{Guid.NewGuid()}";
        var options = new DbContextOptionsBuilder<AtoCopilotContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        // Seed the database
        using (var ctx = new AtoCopilotContext(options))
        {
            ctx.RegisteredSystems.AddRange(systems);
            ctx.SaveChanges();
        }

        // Wire up a real IServiceScopeFactory so SystemIdResolver can create scopes
        var services = new ServiceCollection();
        services.AddDbContext<AtoCopilotContext>(o => o.UseInMemoryDatabase(dbName));
        var sp = services.BuildServiceProvider();
        var scopeFactory = sp.GetRequiredService<IServiceScopeFactory>();

        return new SystemIdResolver(scopeFactory, Mock.Of<ILogger<SystemIdResolver>>());
    }

    // ─── Fake tools for BaseTool integration tests ───────────────────────────

    /// <summary>A minimal tool that declares a system_id parameter.</summary>
    private class FakeSystemIdTool : BaseTool
    {
        public FakeSystemIdTool() : base(NullLogger.Instance) { }

        public override string Name => "fake_tool_with_system_id";
        public override string Description => "Fake tool for testing system_id resolution";

        public override IReadOnlyDictionary<string, ToolParameter> Parameters =>
            new Dictionary<string, ToolParameter>
            {
                ["system_id"] = new() { Name = "system_id", Description = "System GUID, name, or acronym", Type = "string", Required = true },
                ["other_param"] = new() { Name = "other_param", Description = "Some other param", Type = "string" }
            };

        public override Task<string> ExecuteCoreAsync(Dictionary<string, object?> arguments, CancellationToken cancellationToken = default)
            => Task.FromResult("{\"status\":\"ok\"}");
    }

    /// <summary>A minimal tool that does NOT declare a system_id parameter.</summary>
    private class FakeNoSystemIdTool : BaseTool
    {
        public FakeNoSystemIdTool() : base(NullLogger.Instance) { }

        public override string Name => "fake_tool_without_system_id";
        public override string Description => "Fake tool without system_id";

        public override IReadOnlyDictionary<string, ToolParameter> Parameters =>
            new Dictionary<string, ToolParameter>
            {
                ["other_param"] = new() { Name = "other_param", Description = "Some param", Type = "string" }
            };

        public override Task<string> ExecuteCoreAsync(Dictionary<string, object?> arguments, CancellationToken cancellationToken = default)
            => Task.FromResult("{\"status\":\"ok\"}");
    }
}
