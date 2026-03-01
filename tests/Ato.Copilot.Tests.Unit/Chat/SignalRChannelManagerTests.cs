using FluentAssertions;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Ato.Copilot.Channels.Abstractions;
using Ato.Copilot.Channels.Models;
using Ato.Copilot.Chat.Channels;
using Ato.Copilot.Chat.Hubs;

namespace Ato.Copilot.Tests.Unit.Chat;

/// <summary>
/// Unit tests for <see cref="SignalRChannelManager"/> — verifies connection tracking,
/// group management delegation, and conversation membership tracking.
/// </summary>
public class SignalRChannelManagerTests
{
    private readonly SignalRConnectionTracker _tracker;
    private readonly Mock<IHubContext<ChatHub>> _hubContextMock;
    private readonly Mock<IGroupManager> _groupsMock;
    private readonly Mock<IChannel> _channelMock;
    private readonly SignalRChannelManager _manager;

    public SignalRChannelManagerTests()
    {
        _tracker = new SignalRConnectionTracker();
        _hubContextMock = new Mock<IHubContext<ChatHub>>();
        _groupsMock = new Mock<IGroupManager>();
        _channelMock = new Mock<IChannel>();

        _hubContextMock.Setup(h => h.Groups).Returns(_groupsMock.Object);

        _manager = new SignalRChannelManager(
            _tracker,
            _hubContextMock.Object,
            _channelMock.Object,
            Mock.Of<ILogger<SignalRChannelManager>>());
    }

    // ─── RegisterConnectionAsync ─────────────────────────────────

    [Fact]
    public async Task RegisterConnectionAsync_CreatesConnectionInfo()
    {
        var info = await _manager.RegisterConnectionAsync("user-1", new Dictionary<string, object>
        {
            ["signalRConnectionId"] = "conn-abc"
        });

        info.Should().NotBeNull();
        info.ConnectionId.Should().Be("conn-abc");
        info.UserId.Should().Be("user-1");
        info.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task RegisterConnectionAsync_RemovesSignalRConnectionIdFromMetadata()
    {
        var info = await _manager.RegisterConnectionAsync("user-1", new Dictionary<string, object>
        {
            ["signalRConnectionId"] = "conn-abc",
            ["source"] = "web"
        });

        info.Metadata.Should().ContainKey("source");
        info.Metadata.Should().NotContainKey("signalRConnectionId");
    }

    [Fact]
    public async Task RegisterConnectionAsync_WithoutSignalRId_GeneratesId()
    {
        var info = await _manager.RegisterConnectionAsync("user-1");

        info.Should().NotBeNull();
        info.ConnectionId.Should().NotBeNullOrEmpty();
        info.UserId.Should().Be("user-1");
    }

    // ─── UnregisterConnectionAsync ───────────────────────────────

    [Fact]
    public async Task UnregisterConnectionAsync_RemovesConnection()
    {
        _tracker.RegisterConnection("conn-1", "user-1");

        await _manager.UnregisterConnectionAsync("conn-1");

        var info = _tracker.GetConnectionInfo("conn-1");
        info.Should().BeNull();
    }

    [Fact]
    public async Task UnregisterConnectionAsync_RemovesFromAllGroups()
    {
        var connInfo = _tracker.RegisterConnection("conn-1", "user-1");
        connInfo.Conversations.Add("conv-A");
        connInfo.Conversations.Add("conv-B");

        await _manager.UnregisterConnectionAsync("conn-1");

        _groupsMock.Verify(
            g => g.RemoveFromGroupAsync("conn-1", "conv-A", default),
            Times.Once);
        _groupsMock.Verify(
            g => g.RemoveFromGroupAsync("conn-1", "conv-B", default),
            Times.Once);
    }

    [Fact]
    public async Task UnregisterConnectionAsync_HandlesUnknownConnectionGracefully()
    {
        var act = () => _manager.UnregisterConnectionAsync("unknown-conn");
        await act.Should().NotThrowAsync();
    }

    // ─── JoinConversationAsync ───────────────────────────────────

    [Fact]
    public async Task JoinConversationAsync_AddsToSignalRGroup()
    {
        _tracker.RegisterConnection("conn-1", "user-1");

        await _manager.JoinConversationAsync("conn-1", "conv-1");

        _groupsMock.Verify(
            g => g.AddToGroupAsync("conn-1", "conv-1", default),
            Times.Once);
    }

    [Fact]
    public async Task JoinConversationAsync_TracksConversationInConnectionInfo()
    {
        _tracker.RegisterConnection("conn-1", "user-1");

        await _manager.JoinConversationAsync("conn-1", "conv-1");

        var info = _tracker.GetConnectionInfo("conn-1");
        info!.Conversations.Should().Contain("conv-1");
    }

    // ─── LeaveConversationAsync ──────────────────────────────────

    [Fact]
    public async Task LeaveConversationAsync_RemovesFromSignalRGroup()
    {
        _tracker.RegisterConnection("conn-1", "user-1");

        await _manager.LeaveConversationAsync("conn-1", "conv-1");

        _groupsMock.Verify(
            g => g.RemoveFromGroupAsync("conn-1", "conv-1", default),
            Times.Once);
    }

    [Fact]
    public async Task LeaveConversationAsync_RemovesConversationFromTracking()
    {
        var connInfo = _tracker.RegisterConnection("conn-1", "user-1");
        connInfo.Conversations.Add("conv-1");

        await _manager.LeaveConversationAsync("conn-1", "conv-1");

        var info = _tracker.GetConnectionInfo("conn-1");
        info!.Conversations.Should().NotContain("conv-1");
    }

    // ─── SendToConversationAsync ─────────────────────────────────

    [Fact]
    public async Task SendToConversationAsync_DelegatesToChannel()
    {
        var message = new ChannelMessage { ConversationId = "conv-1", Content = "Test" };

        await _manager.SendToConversationAsync("conv-1", message);

        _channelMock.Verify(
            c => c.SendToConversationAsync("conv-1", message, default),
            Times.Once);
    }

    // ─── IsConnectedAsync ────────────────────────────────────────

    [Fact]
    public async Task IsConnectedAsync_ReturnsTrueForActiveConnection()
    {
        _tracker.RegisterConnection("conn-1", "user-1");

        var result = await _manager.IsConnectedAsync("conn-1");

        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsConnectedAsync_ReturnsFalseForUnknown()
    {
        var result = await _manager.IsConnectedAsync("unknown");

        result.Should().BeFalse();
    }

    // ─── GetConnectionInfoAsync ──────────────────────────────────

    [Fact]
    public async Task GetConnectionInfoAsync_ReturnsInfoForKnownConnection()
    {
        _tracker.RegisterConnection("conn-1", "user-1");

        var info = await _manager.GetConnectionInfoAsync("conn-1");

        info.Should().NotBeNull();
        info!.ConnectionId.Should().Be("conn-1");
    }

    [Fact]
    public async Task GetConnectionInfoAsync_ReturnsNullForUnknown()
    {
        var info = await _manager.GetConnectionInfoAsync("unknown");

        info.Should().BeNull();
    }

    // ─── GetAllConnections ───────────────────────────────────────

    [Fact]
    public void GetAllConnections_ReturnsAllTrackedConnections()
    {
        _tracker.RegisterConnection("conn-1", "user-1");
        _tracker.RegisterConnection("conn-2", "user-2");

        var all = _manager.GetAllConnections().ToList();

        all.Should().HaveCount(2);
    }
}
