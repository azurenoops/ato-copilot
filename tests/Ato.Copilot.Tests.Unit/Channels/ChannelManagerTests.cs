using Xunit;
using FluentAssertions;
using Moq;
using Microsoft.Extensions.Logging;
using Ato.Copilot.Channels.Abstractions;
using Ato.Copilot.Channels.Implementations;
using Ato.Copilot.Channels.Models;

namespace Ato.Copilot.Tests.Unit.Channels;

public class ChannelManagerTests
{
    private readonly ChannelManager _manager;
    private readonly InMemoryChannel _channel;
    private readonly Mock<ILogger<ChannelManager>> _managerLoggerMock;
    private readonly List<ChannelMessage> _deliveredMessages = new();

    public ChannelManagerTests()
    {
        var channelLogger = Mock.Of<ILogger<InMemoryChannel>>();
        _channel = new InMemoryChannel(channelLogger);
        _managerLoggerMock = new Mock<ILogger<ChannelManager>>();
        _manager = new ChannelManager(_channel, _managerLoggerMock.Object);
    }

    #region RegisterConnectionAsync

    [Fact]
    public async Task RegisterConnectionAsync_ReturnsConnectionInfoWithGeneratedId()
    {
        // Act
        var connection = await _manager.RegisterConnectionAsync("user-1");

        // Assert
        connection.Should().NotBeNull();
        connection.ConnectionId.Should().NotBeNullOrWhiteSpace();
        Guid.TryParse(connection.ConnectionId, out _).Should().BeTrue("ConnectionId should be a valid GUID");
        connection.UserId.Should().Be("user-1");
        connection.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task RegisterConnectionAsync_WithMetadata_IncludesMetadata()
    {
        // Arrange
        var metadata = new Dictionary<string, object> { ["source"] = "test", ["version"] = 1 };

        // Act
        var connection = await _manager.RegisterConnectionAsync("user-1", metadata);

        // Assert
        connection.Metadata.Should().ContainKey("source").WhoseValue.Should().Be("test");
    }

    [Fact]
    public async Task RegisterConnectionAsync_MultipleConnections_GenerateUniqueIds()
    {
        // Act
        var conn1 = await _manager.RegisterConnectionAsync("user-1");
        var conn2 = await _manager.RegisterConnectionAsync("user-1");

        // Assert
        conn1.ConnectionId.Should().NotBe(conn2.ConnectionId);
    }

    #endregion

    #region UnregisterConnectionAsync

    [Fact]
    public async Task UnregisterConnectionAsync_RemovesFromAllGroups()
    {
        // Arrange
        var conn = await _manager.RegisterConnectionAsync("user-1");
        await _manager.JoinConversationAsync(conn.ConnectionId, "conv-1");
        await _manager.JoinConversationAsync(conn.ConnectionId, "conv-2");

        // Act
        await _manager.UnregisterConnectionAsync(conn.ConnectionId);

        // Assert
        var isConnected = await _manager.IsConnectedAsync(conn.ConnectionId);
        isConnected.Should().BeFalse();
    }

    [Fact]
    public async Task UnregisterConnectionAsync_ConnectionInfoNoLongerAvailable()
    {
        // Arrange
        var conn = await _manager.RegisterConnectionAsync("user-1");

        // Act
        await _manager.UnregisterConnectionAsync(conn.ConnectionId);

        // Assert
        var info = await _manager.GetConnectionInfoAsync(conn.ConnectionId);
        info.Should().BeNull();
    }

    #endregion

    #region JoinConversationAsync / LeaveConversationAsync

    [Fact]
    public async Task JoinConversationAsync_AddsConnectionToGroup()
    {
        // Arrange
        var conn = await _manager.RegisterConnectionAsync("user-1");
        // Re-register with a message catcher
        _channel.RemoveConnection(conn.ConnectionId);
        _channel.RegisterConnection(
            new ConnectionInfo
            {
                ConnectionId = conn.ConnectionId,
                UserId = "user-1",
                IsActive = true,
                ConnectedAt = DateTimeOffset.UtcNow,
                LastActivityAt = DateTimeOffset.UtcNow
            },
            msg => { _deliveredMessages.Add(msg); return Task.CompletedTask; });

        await _manager.JoinConversationAsync(conn.ConnectionId, "conv-1");

        // Act
        await _manager.SendToConversationAsync("conv-1", new ChannelMessage { Content = "hello" });

        // Assert
        _deliveredMessages.Should().HaveCount(1);
        _deliveredMessages[0].Content.Should().Be("hello");
    }

    [Fact]
    public async Task LeaveConversationAsync_RemovesConnectionFromGroup()
    {
        // Arrange
        var conn = await _manager.RegisterConnectionAsync("user-1");
        _channel.RemoveConnection(conn.ConnectionId);
        _channel.RegisterConnection(
            new ConnectionInfo
            {
                ConnectionId = conn.ConnectionId,
                UserId = "user-1",
                IsActive = true,
                ConnectedAt = DateTimeOffset.UtcNow,
                LastActivityAt = DateTimeOffset.UtcNow
            },
            msg => { _deliveredMessages.Add(msg); return Task.CompletedTask; });

        await _manager.JoinConversationAsync(conn.ConnectionId, "conv-1");
        await _manager.LeaveConversationAsync(conn.ConnectionId, "conv-1");

        // Act
        await _manager.SendToConversationAsync("conv-1", new ChannelMessage { Content = "hello" });

        // Assert
        _deliveredMessages.Should().BeEmpty();
    }

    [Fact]
    public async Task LeaveConversationAsync_LastMember_CleansEmptyGroup()
    {
        // Arrange
        var conn = await _manager.RegisterConnectionAsync("user-1");
        await _manager.JoinConversationAsync(conn.ConnectionId, "conv-1");

        // Act
        await _manager.LeaveConversationAsync(conn.ConnectionId, "conv-1");

        // Assert — group should be cleaned up, no exception when sending
        var act = () => _manager.SendToConversationAsync("conv-1", new ChannelMessage { Content = "test" });
        await act.Should().NotThrowAsync();
    }

    #endregion

    #region GetConnectionInfoAsync / GetAllConnections

    [Fact]
    public async Task GetConnectionInfoAsync_ExistingConnection_ReturnsInfo()
    {
        // Arrange
        var conn = await _manager.RegisterConnectionAsync("user-1");

        // Act
        var info = await _manager.GetConnectionInfoAsync(conn.ConnectionId);

        // Assert
        info.Should().NotBeNull();
        info!.UserId.Should().Be("user-1");
    }

    [Fact]
    public async Task GetConnectionInfoAsync_UnknownConnection_ReturnsNull()
    {
        // Act
        var info = await _manager.GetConnectionInfoAsync("unknown-id");

        // Assert
        info.Should().BeNull();
    }

    [Fact]
    public async Task GetAllConnections_ReturnsAllRegisteredConnections()
    {
        // Arrange
        await _manager.RegisterConnectionAsync("user-1");
        await _manager.RegisterConnectionAsync("user-2");
        await _manager.RegisterConnectionAsync("user-3");

        // Act
        var all = _manager.GetAllConnections().ToList();

        // Assert
        all.Should().HaveCount(3);
        all.Select(c => c.UserId).Should().Contain(new[] { "user-1", "user-2", "user-3" });
    }

    #endregion

    #region IsConnectedAsync

    [Fact]
    public async Task IsConnectedAsync_RegisteredConnection_ReturnsTrue()
    {
        // Arrange
        var conn = await _manager.RegisterConnectionAsync("user-1");

        // Act
        var result = await _manager.IsConnectedAsync(conn.ConnectionId);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsConnectedAsync_AfterUnregister_ReturnsFalse()
    {
        // Arrange
        var conn = await _manager.RegisterConnectionAsync("user-1");
        await _manager.UnregisterConnectionAsync(conn.ConnectionId);

        // Act
        var result = await _manager.IsConnectedAsync(conn.ConnectionId);

        // Assert
        result.Should().BeFalse();
    }

    #endregion
}
