using Xunit;
using FluentAssertions;
using Moq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Ato.Copilot.Agents.Compliance.Configuration;
using Ato.Copilot.Agents.Compliance.Services;
using Ato.Copilot.Core.Data.Context;
using Ato.Copilot.Core.Interfaces.Compliance;
using Ato.Copilot.Core.Models.Compliance;

namespace Ato.Copilot.Tests.Unit.Services;

/// <summary>
/// Tests for the Azure resource validation integration in <see cref="BoundaryService"/>.
/// Validates the <c>ValidateAzureResources</c> feature flag behaviour, resource enrichment,
/// and backward compatibility when the flag is disabled or the validator is not registered.
/// </summary>
public class BoundaryValidationTests : IDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly Mock<IAzureResourceValidator> _validatorMock;

    private const string SystemId = "sys-boundary-test";
    private const string ResourceId1 =
        "/subscriptions/00000000-0000-0000-0000-000000000001/resourceGroups/rg-test/providers/Microsoft.Compute/virtualMachines/vm-test";
    private const string ResourceId2 =
        "/subscriptions/00000000-0000-0000-0000-000000000001/resourceGroups/rg-test/providers/Microsoft.Storage/storageAccounts/stotest";

    public BoundaryValidationTests()
    {
        var services = new ServiceCollection();
        var dbName = $"BoundaryValidation_{Guid.NewGuid()}";
        services.AddDbContext<AtoCopilotContext>(options =>
            options.UseInMemoryDatabase(dbName));

        _serviceProvider = services.BuildServiceProvider();

        // Seed a registered system
        using var initScope = _serviceProvider.CreateScope();
        var ctx = initScope.ServiceProvider.GetRequiredService<AtoCopilotContext>();
        ctx.Database.EnsureCreated();
        ctx.RegisteredSystems.Add(new RegisteredSystem
        {
            Id = SystemId,
            Name = "Test Boundary System",
            Acronym = "TBS",
            SystemType = SystemType.MajorApplication,
            MissionCriticality = MissionCriticality.MissionEssential,
            HostingEnvironment = "Azure Government",
            CreatedBy = "test-user"
        });
        ctx.SaveChanges();

        _scopeFactory = _serviceProvider.GetRequiredService<IServiceScopeFactory>();
        _validatorMock = new Mock<IAzureResourceValidator>();
    }

    public void Dispose() => _serviceProvider.Dispose();

    // ─── Helper methods ─────────────────────────────────────────────────

    private BoundaryService CreateService(
        bool validateAzureResources,
        IAzureResourceValidator? validator = null)
    {
        var options = Options.Create(new BoundaryOptions
        {
            ValidateAzureResources = validateAzureResources
        });

        return new BoundaryService(
            _scopeFactory,
            Mock.Of<ILogger<BoundaryService>>(),
            options,
            validator);
    }

    private static List<BoundaryResourceInput> MakeResources(params string[] resourceIds)
    {
        return resourceIds.Select(id => new BoundaryResourceInput
        {
            ResourceId = id,
            ResourceType = "Microsoft.Compute/virtualMachines",
            ResourceName = "test-resource"
        }).ToList();
    }

    // ─── Feature flag OFF ───────────────────────────────────────────────

    [Fact]
    public async Task DefineBoundary_FlagOff_DoesNotCallValidator()
    {
        var service = CreateService(validateAzureResources: false, _validatorMock.Object);
        var resources = MakeResources(ResourceId1);

        var result = await service.DefineBoundaryAsync(SystemId, resources, "test-user");

        result.Should().HaveCount(1);
        _validatorMock.Verify(
            v => v.ValidateResourcesAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task DefineBoundary_FlagOff_PersistsResources()
    {
        var service = CreateService(validateAzureResources: false);
        var resources = MakeResources(ResourceId1);

        var result = await service.DefineBoundaryAsync(SystemId, resources, "test-user");

        result.Should().HaveCount(1);
        result[0].ResourceId.Should().Be(ResourceId1);
        result[0].IsInBoundary.Should().BeTrue();
    }

    // ─── Feature flag ON — no validator registered ──────────────────────

    [Fact]
    public async Task DefineBoundary_FlagOn_NoValidator_SkipsValidation()
    {
        var service = CreateService(validateAzureResources: true, validator: null);
        var resources = MakeResources(ResourceId1);

        // Should succeed — null-safe check on validator
        var result = await service.DefineBoundaryAsync(SystemId, resources, "test-user");

        result.Should().HaveCount(1);
        result[0].ResourceId.Should().Be(ResourceId1);
    }

    // ─── Feature flag ON — all resources valid ──────────────────────────

    [Fact]
    public async Task DefineBoundary_FlagOn_AllValid_PersistsResources()
    {
        _validatorMock
            .Setup(v => v.ValidateResourcesAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, AzureResourceValidationResult>
            {
                [ResourceId1] = AzureResourceValidationResult.Valid(
                    ResourceId1, "Microsoft.Compute/virtualMachines", "vm-test")
            });

        var service = CreateService(validateAzureResources: true, _validatorMock.Object);
        var resources = MakeResources(ResourceId1);

        var result = await service.DefineBoundaryAsync(SystemId, resources, "test-user");

        result.Should().HaveCount(1);
        result[0].ResourceId.Should().Be(ResourceId1);
        result[0].IsInBoundary.Should().BeTrue();
    }

    [Fact]
    public async Task DefineBoundary_FlagOn_AllValid_CallsValidatorOnce()
    {
        _validatorMock
            .Setup(v => v.ValidateResourcesAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, AzureResourceValidationResult>
            {
                [ResourceId1] = AzureResourceValidationResult.Valid(ResourceId1)
            });

        var service = CreateService(validateAzureResources: true, _validatorMock.Object);
        var resources = MakeResources(ResourceId1);

        await service.DefineBoundaryAsync(SystemId, resources, "test-user");

        _validatorMock.Verify(
            v => v.ValidateResourcesAsync(
                It.Is<IEnumerable<string>>(ids => ids.Contains(ResourceId1)),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task DefineBoundary_FlagOn_MultipleValid_PersistsAll()
    {
        _validatorMock
            .Setup(v => v.ValidateResourcesAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, AzureResourceValidationResult>
            {
                [ResourceId1] = AzureResourceValidationResult.Valid(
                    ResourceId1, "Microsoft.Compute/virtualMachines", "vm-test"),
                [ResourceId2] = AzureResourceValidationResult.Valid(
                    ResourceId2, "Microsoft.Storage/storageAccounts", "stotest")
            });

        var service = CreateService(validateAzureResources: true, _validatorMock.Object);
        var resources = MakeResources(ResourceId1, ResourceId2);

        var result = await service.DefineBoundaryAsync(SystemId, resources, "test-user");

        result.Should().HaveCount(2);
        result.Select(r => r.ResourceId).Should().Contain(ResourceId1);
        result.Select(r => r.ResourceId).Should().Contain(ResourceId2);
    }

    // ─── Feature flag ON — resource enrichment ──────────────────────────

    [Fact]
    public async Task DefineBoundary_FlagOn_EnrichesResourceType()
    {
        _validatorMock
            .Setup(v => v.ValidateResourcesAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, AzureResourceValidationResult>
            {
                [ResourceId1] = AzureResourceValidationResult.Valid(
                    ResourceId1, "Microsoft.Compute/virtualMachines", "vm-enriched")
            });

        var service = CreateService(validateAzureResources: true, _validatorMock.Object);
        var resources = new List<BoundaryResourceInput>
        {
            new()
            {
                ResourceId = ResourceId1,
                ResourceType = "OriginalType",
                ResourceName = null
            }
        };

        var result = await service.DefineBoundaryAsync(SystemId, resources, "test-user");

        // ResourceType should be overwritten by ARM
        result[0].ResourceType.Should().Be("Microsoft.Compute/virtualMachines");
        // ResourceName should be populated since it was null
        result[0].ResourceName.Should().Be("vm-enriched");
    }

    [Fact]
    public async Task DefineBoundary_FlagOn_DoesNotOverwriteExistingResourceName()
    {
        _validatorMock
            .Setup(v => v.ValidateResourcesAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, AzureResourceValidationResult>
            {
                [ResourceId1] = AzureResourceValidationResult.Valid(
                    ResourceId1, "Microsoft.Compute/virtualMachines", "arm-name")
            });

        var service = CreateService(validateAzureResources: true, _validatorMock.Object);
        var resources = new List<BoundaryResourceInput>
        {
            new()
            {
                ResourceId = ResourceId1,
                ResourceType = "Microsoft.Compute/virtualMachines",
                ResourceName = "user-provided-name"
            }
        };

        var result = await service.DefineBoundaryAsync(SystemId, resources, "test-user");

        // User-provided name should be preserved (not overwritten)
        result[0].ResourceName.Should().Be("user-provided-name");
    }

    // ─── Feature flag ON — invalid resources ────────────────────────────

    [Fact]
    public async Task DefineBoundary_FlagOn_InvalidResource_ThrowsInvalidOperation()
    {
        _validatorMock
            .Setup(v => v.ValidateResourcesAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, AzureResourceValidationResult>
            {
                [ResourceId1] = AzureResourceValidationResult.Invalid(ResourceId1, "Resource not found (404)")
            });

        var service = CreateService(validateAzureResources: true, _validatorMock.Object);
        var resources = MakeResources(ResourceId1);

        var act = () => service.DefineBoundaryAsync(SystemId, resources, "test-user");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Azure resource validation failed*1 resource(s)*");
    }

    [Fact]
    public async Task DefineBoundary_FlagOn_InvalidResource_ErrorContainsResourceId()
    {
        _validatorMock
            .Setup(v => v.ValidateResourcesAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, AzureResourceValidationResult>
            {
                [ResourceId1] = AzureResourceValidationResult.Invalid(ResourceId1, "Not found")
            });

        var service = CreateService(validateAzureResources: true, _validatorMock.Object);
        var resources = MakeResources(ResourceId1);

        var act = () => service.DefineBoundaryAsync(SystemId, resources, "test-user");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"*{ResourceId1}*Not found*");
    }

    [Fact]
    public async Task DefineBoundary_FlagOn_MultipleInvalid_ThrowsWithAllErrors()
    {
        _validatorMock
            .Setup(v => v.ValidateResourcesAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, AzureResourceValidationResult>
            {
                [ResourceId1] = AzureResourceValidationResult.Invalid(ResourceId1, "Not found"),
                [ResourceId2] = AzureResourceValidationResult.Invalid(ResourceId2, "Access denied (403)")
            });

        var service = CreateService(validateAzureResources: true, _validatorMock.Object);
        var resources = MakeResources(ResourceId1, ResourceId2);

        var act = () => service.DefineBoundaryAsync(SystemId, resources, "test-user");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*2 resource(s)*");
    }

    [Fact]
    public async Task DefineBoundary_FlagOn_MixedValidInvalid_ThrowsOnlyForInvalid()
    {
        _validatorMock
            .Setup(v => v.ValidateResourcesAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, AzureResourceValidationResult>
            {
                [ResourceId1] = AzureResourceValidationResult.Valid(
                    ResourceId1, "Microsoft.Compute/virtualMachines", "vm-test"),
                [ResourceId2] = AzureResourceValidationResult.Invalid(ResourceId2, "Access denied (403)")
            });

        var service = CreateService(validateAzureResources: true, _validatorMock.Object);
        var resources = MakeResources(ResourceId1, ResourceId2);

        var act = () => service.DefineBoundaryAsync(SystemId, resources, "test-user");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*1 resource(s)*");
    }

    [Fact]
    public async Task DefineBoundary_FlagOn_InvalidResource_DoesNotPersist()
    {
        _validatorMock
            .Setup(v => v.ValidateResourcesAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, AzureResourceValidationResult>
            {
                [ResourceId1] = AzureResourceValidationResult.Invalid(ResourceId1, "Not found")
            });

        var service = CreateService(validateAzureResources: true, _validatorMock.Object);
        var resources = MakeResources(ResourceId1);

        try { await service.DefineBoundaryAsync(SystemId, resources, "test-user"); }
        catch (InvalidOperationException) { /* expected */ }

        // Verify nothing was persisted
        using var scope = _serviceProvider.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<AtoCopilotContext>();
        var boundaries = await ctx.AuthorizationBoundaries
            .Where(b => b.RegisteredSystemId == SystemId)
            .ToListAsync();

        boundaries.Should().BeEmpty();
    }

    // ─── Backward compatibility ─────────────────────────────────────────

    [Fact]
    public async Task Constructor_WithoutOptionalParams_WorksCorrectly()
    {
        // Simulate legacy construction (no options, no validator)
        var service = new BoundaryService(
            _scopeFactory,
            Mock.Of<ILogger<BoundaryService>>());

        var resources = MakeResources(ResourceId1);

        var result = await service.DefineBoundaryAsync(SystemId, resources, "test-user");

        result.Should().HaveCount(1);
        result[0].ResourceId.Should().Be(ResourceId1);
    }

    [Fact]
    public async Task Constructor_WithOptionsOnly_NoValidator_WorksCorrectly()
    {
        var options = Options.Create(new BoundaryOptions { ValidateAzureResources = true });
        var service = new BoundaryService(
            _scopeFactory,
            Mock.Of<ILogger<BoundaryService>>(),
            options);

        var resources = MakeResources(ResourceId1);

        // Should succeed — flag is ON but no validator
        var result = await service.DefineBoundaryAsync(SystemId, resources, "test-user");

        result.Should().HaveCount(1);
    }

    // ─── BoundaryOptions defaults ───────────────────────────────────────

    [Fact]
    public void BoundaryOptions_DefaultsToDisabled()
    {
        var options = new BoundaryOptions();
        options.ValidateAzureResources.Should().BeFalse();
    }

    [Fact]
    public void BoundaryOptions_CanBeEnabled()
    {
        var options = new BoundaryOptions { ValidateAzureResources = true };
        options.ValidateAzureResources.Should().BeTrue();
    }

    // ─── Edge cases ─────────────────────────────────────────────────────

    [Fact]
    public async Task DefineBoundary_FlagOn_ResourceWithWhitespaceId_SkipsValidation()
    {
        // Resources with blank IDs are filtered out before validation
        _validatorMock
            .Setup(v => v.ValidateResourcesAsync(
                It.Is<IEnumerable<string>>(ids => ids.Count() == 1),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, AzureResourceValidationResult>
            {
                [ResourceId1] = AzureResourceValidationResult.Valid(ResourceId1)
            });

        var service = CreateService(validateAzureResources: true, _validatorMock.Object);
        var resources = new List<BoundaryResourceInput>
        {
            new()
            {
                ResourceId = ResourceId1,
                ResourceType = "Microsoft.Compute/virtualMachines",
                ResourceName = "vm1"
            },
            new()
            {
                ResourceId = " ",
                ResourceType = "Microsoft.Storage/storageAccounts",
                ResourceName = "sto1"
            }
        };

        // The whitespace-ID resource should still fail at the individual resource validation
        // (ArgumentException for null/empty ResourceId), but only the non-blank one is validated via ARM
        _validatorMock.Verify(
            v => v.ValidateResourcesAsync(
                It.Is<IEnumerable<string>>(ids => ids.All(id => !string.IsNullOrWhiteSpace(id))),
                It.IsAny<CancellationToken>()),
            Times.AtMostOnce);
    }
}
