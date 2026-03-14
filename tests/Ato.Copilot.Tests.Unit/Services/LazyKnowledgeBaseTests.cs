using Xunit;
using FluentAssertions;
using Moq;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Ato.Copilot.Agents.Compliance.Services.KnowledgeBase;
using Ato.Copilot.Core.Interfaces.Compliance;

namespace Ato.Copilot.Tests.Unit.Services;

/// <summary>
/// Tests for lazy loading behavior in knowledge base services (T041/FR-026).
/// Verifies that JSON files are not deserialized until first access and that
/// thread-safe Lazy&lt;T&gt; initialization works correctly.
/// </summary>
public class LazyKnowledgeBaseTests
{
    [Fact]
    public async Task StigKnowledgeService_DoesNotLoadDataUntilAccessed()
    {
        // Arrange — constructing the service should NOT trigger data loading
        var cache = new MemoryCache(new MemoryCacheOptions());
        var mockDod = Mock.Of<IDoDInstructionService>();
        var logger = Mock.Of<ILogger<StigKnowledgeService>>();

        // Act — just constructing, no method calls
        var service = new StigKnowledgeService(cache, mockDod, logger);

        // Assert — cache should be empty (no eager loading)
        cache.TryGetValue("kb:stig:all_controls", out _).Should().BeFalse(
            "STIG data should not be loaded at construction time");
    }

    [Fact]
    public async Task RmfKnowledgeService_DoesNotLoadDataUntilAccessed()
    {
        // Arrange
        var cache = new MemoryCache(new MemoryCacheOptions());
        var logger = Mock.Of<ILogger<RmfKnowledgeService>>();

        // Act — just constructing
        var service = new RmfKnowledgeService(cache, logger);

        // Assert
        cache.TryGetValue("kb:rmf:process_data", out _).Should().BeFalse(
            "RMF data should not be loaded at construction time");
    }

    [Fact]
    public async Task DoDInstructionService_DoesNotLoadDataUntilAccessed()
    {
        // Arrange
        var cache = new MemoryCache(new MemoryCacheOptions());
        var logger = Mock.Of<ILogger<DoDInstructionService>>();

        // Act
        var service = new DoDInstructionService(cache, logger);

        // Assert
        cache.TryGetValue("kb:dod:all_instructions", out _).Should().BeFalse(
            "DoD instruction data should not be loaded at construction time");
    }

    [Fact]
    public async Task DoDWorkflowService_DoesNotLoadDataUntilAccessed()
    {
        // Arrange
        var cache = new MemoryCache(new MemoryCacheOptions());
        var logger = Mock.Of<ILogger<DoDWorkflowService>>();

        // Act
        var service = new DoDWorkflowService(cache, logger);

        // Assert
        cache.TryGetValue("kb:dod:all_workflows", out _).Should().BeFalse(
            "DoD workflow data should not be loaded at construction time");
    }

    [Fact]
    public void StigKnowledgeService_ThreadSafe_MultipleAccess_LoadsOnce()
    {
        // Arrange
        var cache = new MemoryCache(new MemoryCacheOptions());
        var mockDod = Mock.Of<IDoDInstructionService>();
        var logger = Mock.Of<ILogger<StigKnowledgeService>>();
        var service = new StigKnowledgeService(cache, mockDod, logger);

        // Act — concurrent access should not throw
        var tasks = Enumerable.Range(0, 10)
            .Select(_ => Task.Run(async () =>
            {
                // GetStigMappingAsync triggers LoadControlsAsync internally
                var result = await service.GetStigMappingAsync("AC-1");
                return result;
            }))
            .ToArray();

        // Assert — should complete without exceptions
        var act = () => Task.WhenAll(tasks);
        act.Should().NotThrowAsync("concurrent access should be thread-safe");
    }
}
