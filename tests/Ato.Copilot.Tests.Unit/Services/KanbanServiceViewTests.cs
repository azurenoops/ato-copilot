using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using FluentAssertions;
using Ato.Copilot.Agents.Compliance.Services;
using Ato.Copilot.Core.Interfaces.Kanban;
using Ato.Copilot.Core.Models.Kanban;
using Ato.Copilot.State.Abstractions;

namespace Ato.Copilot.Tests.Unit.Services;

/// <summary>
/// Unit tests for KanbanService view operations (SavedView CRUD via IAgentStateManager):
/// SaveViewAsync, GetViewAsync, ListViewsAsync, DeleteViewAsync.
/// </summary>
public class KanbanServiceViewTests : IDisposable
{
    private readonly Mock<IAgentStateManager> _stateMock = new();
    private readonly KanbanService _service;

    // In-memory store for state manager
    private readonly Dictionary<string, object?> _stateStore = new();

    public KanbanServiceViewTests()
    {
        // Simulate IAgentStateManager behavior
        _stateMock.Setup(s => s.SetStateAsync<It.IsAnyType>(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<It.IsAnyType>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, object?, CancellationToken>((agentId, key, value, ct) =>
            {
                _stateStore[$"{agentId}:{key}"] = value;
            })
            .Returns(Task.CompletedTask);

        _stateMock.Setup(s => s.GetStateAsync<SavedView>(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string agentId, string key, CancellationToken ct) =>
            {
                _stateStore.TryGetValue($"{agentId}:{key}", out var val);
                return val as SavedView;
            });

        _stateMock.Setup(s => s.GetStateAsync<List<string>>(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string agentId, string key, CancellationToken ct) =>
            {
                _stateStore.TryGetValue($"{agentId}:{key}", out var val);
                return val as List<string>;
            });

        // We need a context for constructor but won't use it in view tests
        var options = new Microsoft.EntityFrameworkCore.DbContextOptionsBuilder<Ato.Copilot.Core.Data.Context.AtoCopilotContext>()
            .UseInMemoryDatabase($"KanbanView_{Guid.NewGuid()}")
            .Options;
        var context = new Ato.Copilot.Core.Data.Context.AtoCopilotContext(options);

        _service = new KanbanService(
            context,
            Mock.Of<ILogger<KanbanService>>(),
            Mock.Of<INotificationService>(),
            _stateMock.Object,
            Mock.Of<Ato.Copilot.Core.Interfaces.Compliance.IAtoComplianceEngine>(),
            Mock.Of<Ato.Copilot.Core.Interfaces.Compliance.IRemediationEngine>());
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task SaveView_StoresView()
    {
        var view = new SavedView
        {
            Name = "My Critical",
            OwnerId = "user1",
            BoardId = "board1",
            Filters = new ViewFilters { IsOverdue = true }
        };

        var result = await _service.SaveViewAsync(view);

        result.Name.Should().Be("My Critical");

        // Verify stored
        var stored = await _service.GetViewAsync("user1", "My Critical");
        stored.Should().NotBeNull();
        stored!.Filters.IsOverdue.Should().BeTrue();
    }

    [Fact]
    public async Task SaveView_UpdatesIndex()
    {
        var view = new SavedView { Name = "View1", OwnerId = "user1", BoardId = "b1" };

        await _service.SaveViewAsync(view);

        // Check the index was updated
        _stateStore.TryGetValue("kanban:view-index:user1", out var indexObj);
        var index = indexObj as List<string>;
        index.Should().Contain("View1");
    }

    [Fact]
    public async Task SaveView_DuplicateName_NoIndexDuplicate()
    {
        var view1 = new SavedView { Name = "View1", OwnerId = "user1", BoardId = "b1" };
        var view2 = new SavedView { Name = "View1", OwnerId = "user1", BoardId = "b2" };

        await _service.SaveViewAsync(view1);
        await _service.SaveViewAsync(view2);

        _stateStore.TryGetValue("kanban:view-index:user1", out var indexObj);
        var index = indexObj as List<string>;
        index!.Count(n => n == "View1").Should().Be(1);
    }

    [Fact]
    public async Task GetView_Nonexistent_ReturnsNull()
    {
        var result = await _service.GetViewAsync("user1", "nonexistent");

        result.Should().BeNull();
    }

    [Fact]
    public async Task ListViews_ReturnsAllForUser()
    {
        await _service.SaveViewAsync(new SavedView { Name = "V1", OwnerId = "user1", BoardId = "b1" });
        await _service.SaveViewAsync(new SavedView { Name = "V2", OwnerId = "user1", BoardId = "b1" });
        await _service.SaveViewAsync(new SavedView { Name = "V3", OwnerId = "user2", BoardId = "b1" });

        var result = await _service.ListViewsAsync("user1");

        result.Should().HaveCount(2);
        result.Select(v => v.Name).Should().Contain("V1").And.Contain("V2");
    }

    [Fact]
    public async Task ListViews_NoViews_ReturnsEmpty()
    {
        var result = await _service.ListViewsAsync("user1");

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task DeleteView_RemovesFromIndex()
    {
        await _service.SaveViewAsync(new SavedView { Name = "V1", OwnerId = "user1", BoardId = "b1" });
        await _service.SaveViewAsync(new SavedView { Name = "V2", OwnerId = "user1", BoardId = "b1" });

        await _service.DeleteViewAsync("user1", "V1");

        var views = await _service.ListViewsAsync("user1");
        views.Should().HaveCount(1);
        views[0].Name.Should().Be("V2");
    }

    [Fact]
    public async Task DeleteView_SetsViewNull()
    {
        await _service.SaveViewAsync(new SavedView { Name = "V1", OwnerId = "user1", BoardId = "b1" });

        await _service.DeleteViewAsync("user1", "V1");

        var view = await _service.GetViewAsync("user1", "V1");
        view.Should().BeNull();
    }
}
