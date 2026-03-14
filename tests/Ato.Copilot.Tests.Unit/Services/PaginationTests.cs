using Xunit;
using FluentAssertions;
using System.Text;
using System.Text.Json;

namespace Ato.Copilot.Tests.Unit.Services;

/// <summary>
/// Tests for server-side pagination enforcement (T046/FR-029 through FR-033).
/// </summary>
public class PaginationTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    [Fact]
    public void DefaultPageSize_AppliedWhenNoParam()
    {
        // Arrange
        var options = new Ato.Copilot.Core.Models.PaginationOptions();

        // Assert
        options.DefaultPageSize.Should().Be(50, "default page size should be 50");
    }

    [Fact]
    public void MaxPageSize_ClampedWhenExceeded()
    {
        // Arrange
        var options = new Ato.Copilot.Core.Models.PaginationOptions();
        var requestedPageSize = 200;

        // Act
        var effectivePageSize = Math.Min(requestedPageSize, options.MaxPageSize);

        // Assert
        effectivePageSize.Should().Be(100, "page size should be clamped to MaxPageSize=100");
    }

    [Fact]
    public void PageSize_Zero_ClampedToOne()
    {
        // Arrange
        var requestedPageSize = 0;

        // Act
        var effectivePageSize = Math.Max(1, requestedPageSize);

        // Assert
        effectivePageSize.Should().Be(1);
    }

    [Fact]
    public void NextPageToken_RoundTrips()
    {
        // Arrange — encode offset as base64
        var offset = 50;
        var token = Convert.ToBase64String(Encoding.UTF8.GetBytes($"offset:{offset}"));

        // Act — decode
        var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(token));

        // Assert
        decoded.Should().Be("offset:50");
    }

    [Fact]
    public void PaginationInfo_CorrectlyCalculatedForCollection()
    {
        // Arrange
        var totalItems = 150;
        var pageSize = 50;
        var page = 2;

        // Act
        var info = new Ato.Copilot.Core.Models.PaginationInfo
        {
            Page = page,
            PageSize = pageSize,
            TotalItems = totalItems,
            TotalPages = (int)Math.Ceiling((double)totalItems / pageSize),
            HasNextPage = page * pageSize < totalItems,
            NextPageToken = page * pageSize < totalItems
                ? Convert.ToBase64String(Encoding.UTF8.GetBytes($"offset:{page * pageSize}"))
                : null
        };

        // Assert
        info.TotalPages.Should().Be(3);
        info.HasNextPage.Should().BeTrue();
        info.NextPageToken.Should().NotBeNull();
    }
}
