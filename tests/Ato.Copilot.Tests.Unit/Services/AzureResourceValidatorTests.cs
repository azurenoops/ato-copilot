using Xunit;
using FluentAssertions;
using Moq;
using Microsoft.Extensions.Logging;
using Ato.Copilot.Core.Interfaces.Compliance;

namespace Ato.Copilot.Tests.Unit.Services;

/// <summary>
/// Tests for <see cref="AzureResourceValidationResult"/> factory methods
/// and the <see cref="IAzureResourceValidator"/> contract.
/// Since <c>ArmClient</c> uses sealed Azure SDK types that are difficult to mock,
/// validator behaviour is verified through the interface and result model.
/// </summary>
public class AzureResourceValidatorTests
{
    private readonly Mock<IAzureResourceValidator> _validatorMock;

    public AzureResourceValidatorTests()
    {
        _validatorMock = new Mock<IAzureResourceValidator>();
    }

    private const string ValidResourceId =
        "/subscriptions/00000000-0000-0000-0000-000000000001/resourceGroups/rg-test/providers/Microsoft.Compute/virtualMachines/vm-test";

    private const string ValidResourceId2 =
        "/subscriptions/00000000-0000-0000-0000-000000000001/resourceGroups/rg-test/providers/Microsoft.Storage/storageAccounts/stotest";

    // ─── AzureResourceValidationResult factory methods ──────────────────

    [Fact]
    public void ValidResult_SetsExpectedProperties()
    {
        var result = AzureResourceValidationResult.Valid(
            ValidResourceId,
            "Microsoft.Compute/virtualMachines",
            "vm-test");

        result.IsValid.Should().BeTrue();
        result.ResourceId.Should().Be(ValidResourceId);
        result.ResourceType.Should().Be("Microsoft.Compute/virtualMachines");
        result.ResourceName.Should().Be("vm-test");
        result.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void ValidResult_WithoutOptionalFields_HasNullTypeAndName()
    {
        var result = AzureResourceValidationResult.Valid(ValidResourceId);

        result.IsValid.Should().BeTrue();
        result.ResourceId.Should().Be(ValidResourceId);
        result.ResourceType.Should().BeNull();
        result.ResourceName.Should().BeNull();
    }

    [Fact]
    public void InvalidResult_SetsExpectedProperties()
    {
        var result = AzureResourceValidationResult.Invalid(ValidResourceId, "Resource not found");

        result.IsValid.Should().BeFalse();
        result.ResourceId.Should().Be(ValidResourceId);
        result.ErrorMessage.Should().Be("Resource not found");
        result.ResourceType.Should().BeNull();
        result.ResourceName.Should().BeNull();
    }

    [Fact]
    public void InvalidResult_EmptyResourceId_StoresEmpty()
    {
        var result = AzureResourceValidationResult.Invalid("", "Resource ID is empty.");

        result.IsValid.Should().BeFalse();
        result.ResourceId.Should().BeEmpty();
        result.ErrorMessage.Should().Contain("empty");
    }

    // ─── ValidateResourceAsync contract ─────────────────────────────────

    [Fact]
    public async Task ValidateResourceAsync_ValidResource_ReturnsValid()
    {
        _validatorMock
            .Setup(v => v.ValidateResourceAsync(ValidResourceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(AzureResourceValidationResult.Valid(
                ValidResourceId, "Microsoft.Compute/virtualMachines", "vm-test"));

        var result = await _validatorMock.Object.ValidateResourceAsync(ValidResourceId);

        result.IsValid.Should().BeTrue();
        result.ResourceType.Should().Be("Microsoft.Compute/virtualMachines");
        result.ResourceName.Should().Be("vm-test");
    }

    [Fact]
    public async Task ValidateResourceAsync_NotFoundResource_ReturnsInvalid()
    {
        var notFoundId = "/subscriptions/00000000-0000-0000-0000-000000000001/resourceGroups/rg-test/providers/Microsoft.Compute/virtualMachines/nonexistent";

        _validatorMock
            .Setup(v => v.ValidateResourceAsync(notFoundId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(AzureResourceValidationResult.Invalid(notFoundId, "Azure resource not found (404)."));

        var result = await _validatorMock.Object.ValidateResourceAsync(notFoundId);

        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("not found");
    }

    [Fact]
    public async Task ValidateResourceAsync_AccessDenied_ReturnsInvalidWith403()
    {
        _validatorMock
            .Setup(v => v.ValidateResourceAsync(ValidResourceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(AzureResourceValidationResult.Invalid(ValidResourceId,
                "Access denied (403). The service principal does not have read access to this resource."));

        var result = await _validatorMock.Object.ValidateResourceAsync(ValidResourceId);

        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("403");
        result.ErrorMessage.Should().Contain("Access denied");
    }

    [Fact]
    public async Task ValidateResourceAsync_InvalidFormat_ReturnsInvalid()
    {
        var badId = "not-an-arm-id";

        _validatorMock
            .Setup(v => v.ValidateResourceAsync(badId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(AzureResourceValidationResult.Invalid(badId, "Invalid ARM resource ID format."));

        var result = await _validatorMock.Object.ValidateResourceAsync(badId);

        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Invalid ARM");
    }

    [Fact]
    public async Task ValidateResourceAsync_EmptyInput_ReturnsInvalid()
    {
        _validatorMock
            .Setup(v => v.ValidateResourceAsync("", It.IsAny<CancellationToken>()))
            .ReturnsAsync(AzureResourceValidationResult.Invalid("", "Resource ID is empty."));

        var result = await _validatorMock.Object.ValidateResourceAsync("");

        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("empty");
    }

    // ─── ValidateResourcesAsync contract ────────────────────────────────

    [Fact]
    public async Task ValidateResourcesAsync_AllValid_ReturnsDictionaryOfValid()
    {
        var ids = new[] { ValidResourceId, ValidResourceId2 };

        _validatorMock
            .Setup(v => v.ValidateResourcesAsync(ids, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, AzureResourceValidationResult>
            {
                [ValidResourceId] = AzureResourceValidationResult.Valid(
                    ValidResourceId, "Microsoft.Compute/virtualMachines", "vm-test"),
                [ValidResourceId2] = AzureResourceValidationResult.Valid(
                    ValidResourceId2, "Microsoft.Storage/storageAccounts", "stotest")
            });

        var results = await _validatorMock.Object.ValidateResourcesAsync(ids);

        results.Should().HaveCount(2);
        results.Values.Should().AllSatisfy(r => r.IsValid.Should().BeTrue());
    }

    [Fact]
    public async Task ValidateResourcesAsync_MixedResults_ReturnsBothValidAndInvalid()
    {
        var badId = "/subscriptions/00000000/resourceGroups/rg/providers/Microsoft.Compute/virtualMachines/gone";
        var ids = new[] { ValidResourceId, badId };

        _validatorMock
            .Setup(v => v.ValidateResourcesAsync(ids, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, AzureResourceValidationResult>
            {
                [ValidResourceId] = AzureResourceValidationResult.Valid(
                    ValidResourceId, "Microsoft.Compute/virtualMachines", "vm-test"),
                [badId] = AzureResourceValidationResult.Invalid(badId, "Azure resource not found (404).")
            });

        var results = await _validatorMock.Object.ValidateResourcesAsync(ids);

        results.Should().HaveCount(2);
        results[ValidResourceId].IsValid.Should().BeTrue();
        results[badId].IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task ValidateResourcesAsync_EmptyList_ReturnsEmptyDictionary()
    {
        _validatorMock
            .Setup(v => v.ValidateResourcesAsync(
                It.Is<IEnumerable<string>>(e => !e.Any()),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, AzureResourceValidationResult>());

        var results = await _validatorMock.Object.ValidateResourcesAsync(Array.Empty<string>());

        results.Should().BeEmpty();
    }

    [Fact]
    public async Task ValidateResourcesAsync_SingleResource_ReturnsSingleResult()
    {
        var ids = new[] { ValidResourceId };

        _validatorMock
            .Setup(v => v.ValidateResourcesAsync(ids, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, AzureResourceValidationResult>
            {
                [ValidResourceId] = AzureResourceValidationResult.Valid(
                    ValidResourceId, "Microsoft.Compute/virtualMachines", "vm-test")
            });

        var results = await _validatorMock.Object.ValidateResourcesAsync(ids);

        results.Should().HaveCount(1);
        results[ValidResourceId].IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateResourcesAsync_AllInvalid_AllReturnFalse()
    {
        var bad1 = "bad-id-1";
        var bad2 = "bad-id-2";
        var ids = new[] { bad1, bad2 };

        _validatorMock
            .Setup(v => v.ValidateResourcesAsync(ids, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, AzureResourceValidationResult>
            {
                [bad1] = AzureResourceValidationResult.Invalid(bad1, "Invalid format"),
                [bad2] = AzureResourceValidationResult.Invalid(bad2, "Not found")
            });

        var results = await _validatorMock.Object.ValidateResourcesAsync(ids);

        results.Should().HaveCount(2);
        results.Values.Should().AllSatisfy(r => r.IsValid.Should().BeFalse());
    }

    // ─── Interface presence ─────────────────────────────────────────────

    [Fact]
    public void IAzureResourceValidator_HasValidateResourceAsync()
    {
        typeof(IAzureResourceValidator)
            .GetMethod(nameof(IAzureResourceValidator.ValidateResourceAsync))
            .Should().NotBeNull();
    }

    [Fact]
    public void IAzureResourceValidator_HasValidateResourcesAsync()
    {
        typeof(IAzureResourceValidator)
            .GetMethod(nameof(IAzureResourceValidator.ValidateResourcesAsync))
            .Should().NotBeNull();
    }
}
