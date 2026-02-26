using FluentAssertions;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Ato.Copilot.Channels.Models;
using Ato.Copilot.Chat.Channels;
using Ato.Copilot.Chat.Hubs;

namespace Ato.Copilot.Tests.Unit.Chat;

/// <summary>
/// Unit tests for <see cref="SignalRChannel"/> — verifies that IChannel methods
/// correctly delegate to IHubContext&lt;ChatHub&gt; with the right event names and payloads.
/// </summary>
public class SignalRChannelTests
{
    private readonly Mock<IHubContext<ChatHub>> _hubContextMock;
    private readonly Mock<IHubClients> _hubClientsMock;
    private readonly Mock<ISingleClientProxy> _clientProxyMock;
    private readonly Mock<IClientProxy> _groupProxyMock;
    private readonly Mock<IClientProxy> _allProxyMock;
    private readonly SignalRConnectionTracker _tracker;
    private readonly SignalRChannel _channel;

    public SignalRChannelTests()
    {
        _hubContextMock = new Mock<IHubContext<ChatHub>>();
        _hubClientsMock = new Mock<IHubClients>();
        _clientProxyMock = new Mock<ISingleClientProxy>();
        _groupProxyMock = new Mock<IClientProxy>();
        _allProxyMock = new Mock<IClientProxy>();
        _tracker = new SignalRConnectionTracker();

        _hubClientsMock.Setup(c => c.Client(It.IsAny<string>())).Returns(_clientProxyMock.Object);
        _hubClientsMock.Setup(c => c.Group(It.IsAny<string>())).Returns(_groupProxyMock.Object);
        _hubClientsMock.Setup(c => c.All).Returns(_allProxyMock.Object);
        _hubContextMock.Setup(h => h.Clients).Returns(_hubClientsMock.Object);

        _channel = new SignalRChannel(
            _hubContextMock.Object,
            _tracker,
            Mock.Of<ILogger<SignalRChannel>>());
    }

    // ─── SendAsync ───────────────────────────────────────────────

    [Fact]
    public async Task SendAsync_SendsMessageReceivedToClient()
    {
        var message = new ChannelMessage
        {
            MessageId = "msg-1",
            ConversationId = "conv-1",
            Type = MessageType.AgentResponse,
            Content = "Hello"
        };

        await _channel.SendAsync("conn-1", message);

        _hubClientsMock.Verify(c => c.Client("conn-1"), Times.Once);
        _clientProxyMock.Verify(
            p => p.SendCoreAsync("MessageReceived", It.IsAny<object?[]>(), default),
            Times.Once);
    }

    [Fact]
    public async Task SendAsync_SendsMessageErrorForErrorType()
    {
        var message = new ChannelMessage
        {
            MessageId = "msg-err",
            ConversationId = "conv-1",
            Type = MessageType.Error,
            Content = "Something went wrong"
        };

        await _channel.SendAsync("conn-1", message);

        _clientProxyMock.Verify(
            p => p.SendCoreAsync("MessageError", It.IsAny<object?[]>(), default),
            Times.Once);
    }

    [Fact]
    public async Task SendAsync_SendsMessageProgressForProgressType()
    {
        var message = new ChannelMessage
        {
            MessageId = "msg-prog",
            ConversationId = "conv-1",
            Type = MessageType.ProgressUpdate,
            Content = "Step 2 of 5"
        };

        await _channel.SendAsync("conn-1", message);

        _clientProxyMock.Verify(
            p => p.SendCoreAsync("MessageProgress", It.IsAny<object?[]>(), default),
            Times.Once);
    }

    [Fact]
    public async Task SendAsync_SendsMessageProcessingForThinkingType()
    {
        var message = new ChannelMessage
        {
            MessageId = "msg-think",
            ConversationId = "conv-1",
            Type = MessageType.AgentThinking,
            Content = "Processing"
        };

        await _channel.SendAsync("conn-1", message);

        _clientProxyMock.Verify(
            p => p.SendCoreAsync("MessageProcessing", It.IsAny<object?[]>(), default),
            Times.Once);
    }

    // ─── SendToConversationAsync ─────────────────────────────────

    [Fact]
    public async Task SendToConversationAsync_SendsToGroup()
    {
        var message = new ChannelMessage
        {
            MessageId = "msg-2",
            ConversationId = "conv-2",
            Type = MessageType.AgentResponse,
            Content = "Group message"
        };

        await _channel.SendToConversationAsync("conv-2", message);

        _hubClientsMock.Verify(c => c.Group("conv-2"), Times.Once);
        _groupProxyMock.Verify(
            p => p.SendCoreAsync("MessageReceived", It.IsAny<object?[]>(), default),
            Times.Once);
    }

    // ─── BroadcastAsync ──────────────────────────────────────────

    [Fact]
    public async Task BroadcastAsync_SendsToAll()
    {
        var message = new ChannelMessage
        {
            MessageId = "msg-3",
            ConversationId = "conv-3",
            Type = MessageType.SystemNotification,
            Content = "System broadcast"
        };

        await _channel.BroadcastAsync(message);

        _allProxyMock.Verify(
            p => p.SendCoreAsync("MessageReceived", It.IsAny<object?[]>(), default),
            Times.Once);
    }

    // ─── IsConnectedAsync ────────────────────────────────────────

    [Fact]
    public async Task IsConnectedAsync_ReturnsTrueForRegisteredConnection()
    {
        _tracker.RegisterConnection("conn-active", "user-1");

        var result = await _channel.IsConnectedAsync("conn-active");

        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsConnectedAsync_ReturnsFalseForUnknownConnection()
    {
        var result = await _channel.IsConnectedAsync("conn-unknown");

        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsConnectedAsync_ReturnsFalseForUnregisteredConnection()
    {
        _tracker.RegisterConnection("conn-removed", "user-1");
        _tracker.UnregisterConnection("conn-removed");

        var result = await _channel.IsConnectedAsync("conn-removed");

        result.Should().BeFalse();
    }
}
