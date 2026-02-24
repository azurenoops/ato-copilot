using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using FluentAssertions;
using Ato.Copilot.Agents.Compliance.Services;
using Ato.Copilot.Core.Interfaces.Compliance;
using Ato.Copilot.Core.Models.Compliance;

namespace Ato.Copilot.Tests.Unit.Services;

/// <summary>
/// Unit tests for <see cref="ComplianceValidationService"/>: all 11 controls valid,
/// missing controls produce warnings, empty catalog handling.
/// </summary>
public class ComplianceValidationServiceTests
{
    private readonly Mock<INistControlsService> _nistServiceMock = new();
    private readonly Mock<ILogger<ComplianceValidationService>> _loggerMock = new();

    private ComplianceValidationService CreateService()
    {
        return new ComplianceValidationService(
            _nistServiceMock.Object,
            _loggerMock.Object);
    }

    // ─── ValidateControlMappingsAsync ────────────────────────────────────────

    [Fact]
    public async Task ValidateControlMappings_AllControlsPresent_LogsSuccess()
    {
        _nistServiceMock.Setup(s => s.ValidateControlIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var service = CreateService();
        await service.ValidateControlMappingsAsync();

        // Should not log any warnings
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never);
    }

    [Fact]
    public async Task ValidateControlMappings_MissingControls_LogsWarning()
    {
        // All controls valid except SC-28
        _nistServiceMock.Setup(s => s.ValidateControlIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _nistServiceMock.Setup(s => s.ValidateControlIdAsync("SC-28", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var service = CreateService();
        await service.ValidateControlMappingsAsync();

        // Should log a warning about the missing control
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task ValidateControlMappings_AllMissing_LogsWarning()
    {
        _nistServiceMock.Setup(s => s.ValidateControlIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var service = CreateService();
        await service.ValidateControlMappingsAsync();

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task ValidateControlMappings_Checks11Controls()
    {
        _nistServiceMock.Setup(s => s.ValidateControlIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var service = CreateService();
        await service.ValidateControlMappingsAsync();

        // Should validate exactly 11 control IDs
        _nistServiceMock.Verify(
            s => s.ValidateControlIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Exactly(11));
    }

    // ─── ValidateConfigurationAsync ──────────────────────────────────────────

    [Fact]
    public async Task ValidateConfiguration_NullCatalog_LogsWarning()
    {
        _nistServiceMock.Setup(s => s.GetCatalogAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((NistCatalog?)null);

        var service = CreateService();
        await service.ValidateConfigurationAsync();

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task ValidateConfiguration_20Groups_NoWarning()
    {
        var catalog = new NistCatalog
        {
            Metadata = new CatalogMetadata { Title = "Test", Version = "5.2.0" },
            Groups = Enumerable.Range(1, 20)
                .Select(i => new ControlGroup { Id = $"g{i}", Title = $"Group {i}" })
                .ToList()
        };

        _nistServiceMock.Setup(s => s.GetCatalogAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(catalog);

        var service = CreateService();
        await service.ValidateConfigurationAsync();

        // No warnings for 20 groups
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never);
    }

    [Fact]
    public async Task ValidateConfiguration_WrongGroupCount_LogsWarning()
    {
        var catalog = new NistCatalog
        {
            Metadata = new CatalogMetadata { Title = "Test", Version = "5.2.0" },
            Groups = new List<ControlGroup>
            {
                new() { Id = "ac", Title = "Access Control" }
            }
        };

        _nistServiceMock.Setup(s => s.GetCatalogAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(catalog);

        var service = CreateService();
        await service.ValidateConfigurationAsync();

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}
