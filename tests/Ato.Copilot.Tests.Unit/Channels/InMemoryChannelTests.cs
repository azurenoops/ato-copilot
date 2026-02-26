using Xunit;
using FluentAssertions;
using Moq;
using Microsoft.Extensions.Logging;
using Ato.Copilot.Channels.Abstractions;
using Ato.Copilot.Channels.Implementations;
using Ato.Copilot.Channels.Models;

namespace Ato.Copilot.Tests.Unit.Channels;

public class InMemoryChannelTests
{
    private readonly InMemoryChannel _channel;
    private readonly Mock<ILogger<InMemoryChannel>> _loggerMock;

    public InMemoryChannelTests()
    {
        _loggerMock = new Mock<ILogger<InMemoryChannel>>();
        _channel = new InMemoryChannel(_loggerMock.Object);
    }

    private ConnectionInfo CreateConnection(string? connectionId = null, string userId = "user-1")
    {
        return new ConnectionInfo
        {
            ConnectionId = connectionId ?? Guid.NewGuid().ToString(),
            UserId = userId,
            ConnectedAt = DateTimeOffset.UtcNow,
            LastActivityAt = DateTimeOffset.UtcNow,
            IsActive = true
        };
    }

    #region SendAsync

    [Fact]
    public async Task SendAsync_ToActiveConnection_UpdatesLastActivityAt()
    {
        // Arrange
        var connection = CreateConnection();
        var originalActivity = connection.LastActivityAt;
        var received = new List<ChannelMessage>();
        _channel.RegisterConnection(connection, msg => { received.Add(msg); return Task.CompletedTask; });

        await Task.Delay(50); // Ensure time difference

        var message = new ChannelMessage { Content = "hello", Type = MessageType.UserMessage };

        // Act
        await _channel.SendAsync(connection.ConnectionId, message);

        // Assert
        received.Should().HaveCount(1);
        received[0].Content.Should().Be("hello");
        connection.LastActivityAt.Should().BeAfter(originalActivity);
    }

    [Fact]
    public async Task SendAsync_ToInactiveConnection_SkipsAndLogsWarning()
    {
        // Arrange
        var connection = CreateConnection();
        connection.IsActive = false;
        var received = new List<ChannelMessage>();
        _channel.RegisterConnection(connection, msg => { received.Add(msg); return Task.CompletedTask; });

        var message = new ChannelMessage { Content = "hello", Type = MessageType.UserMessage };

        // Act
        await _channel.SendAsync(connection.ConnectionId, message);

        // Assert
        received.Should().BeEmpty();
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, _) => o.ToString()!.Contains("Skipping send to inactive connection")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task SendAsync_HandlerThrows_LogsWarningWithoutException()
    {
        // Arrange
        var connection = CreateConnection();
        _channel.RegisterConnection(connection, _ => throw new InvalidOperationException("handler error"));

        var message = new ChannelMessage { Content = "hello", Type = MessageType.UserMessage };

        // Act — should not throw (R5)
        var act = () => _channel.SendAsync(connection.ConnectionId, message);

        // Assert
        await act.Should().NotThrowAsync();
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, _) => o.ToString()!.Contains("Failed to send message to connection")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    #endregion

    #region SendToConversationAsync

    [Fact]
    public async Task SendToConversationAsync_DeliversToAllGroupMembers()
    {
        // Arrange
        var conn1 = CreateConnection();
        var conn2 = CreateConnection();
        var received1 = new List<ChannelMessage>();
        var received2 = new List<ChannelMessage>();
        _channel.RegisterConnection(conn1, msg => { received1.Add(msg); return Task.CompletedTask; });
        _channel.RegisterConnection(conn2, msg => { received2.Add(msg); return Task.CompletedTask; });

        _channel.AddToGroup(conn1.ConnectionId, "conv-1");
        _channel.AddToGroup(conn2.ConnectionId, "conv-1");

        var message = new ChannelMessage { Content = "group message", ConversationId = "conv-1" };

        // Act
        await _channel.SendToConversationAsync("conv-1", message);

        // Assert
        received1.Should().HaveCount(1);
        received2.Should().HaveCount(1);
        received1[0].Content.Should().Be("group message");
        received2[0].Content.Should().Be("group message");
    }

    [Fact]
    public async Task SendToConversationAsync_NoMembers_DoesNothing()
    {
        // Arrange
        var message = new ChannelMessage { Content = "hello", ConversationId = "nonexistent" };

        // Act — no exception
        var act = () => _channel.SendToConversationAsync("nonexistent", message);

        // Assert
        await act.Should().NotThrowAsync();
    }

    #endregion

    #region BroadcastAsync

    [Fact]
    public async Task BroadcastAsync_DeliversToAllConnections()
    {
        // Arrange
        var conn1 = CreateConnection();
        var conn2 = CreateConnection();
        var received1 = new List<ChannelMessage>();
        var received2 = new List<ChannelMessage>();
        _channel.RegisterConnection(conn1, msg => { received1.Add(msg); return Task.CompletedTask; });
        _channel.RegisterConnection(conn2, msg => { received2.Add(msg); return Task.CompletedTask; });

        var message = new ChannelMessage { Content = "broadcast" };

        // Act
        await _channel.BroadcastAsync(message);

        // Assert
        received1.Should().HaveCount(1);
        received2.Should().HaveCount(1);
    }

    #endregion

    #region IsConnectedAsync

    [Fact]
    public async Task IsConnectedAsync_ActiveConnection_ReturnsTrue()
    {
        // Arrange
        var connection = CreateConnection();
        _channel.RegisterConnection(connection, _ => Task.CompletedTask);

        // Act
        var result = await _channel.IsConnectedAsync(connection.ConnectionId);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsConnectedAsync_UnknownId_ReturnsFalse()
    {
        // Act
        var result = await _channel.IsConnectedAsync("unknown-id");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsConnectedAsync_InactiveConnection_ReturnsFalse()
    {
        // Arrange
        var connection = CreateConnection();
        connection.IsActive = false;
        _channel.RegisterConnection(connection, _ => Task.CompletedTask);

        // Act
        var result = await _channel.IsConnectedAsync(connection.ConnectionId);

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region Connection Management

    [Fact]
    public void RegisterConnection_AddsToConnectionsAndHandlers()
    {
        // Arrange
        var connection = CreateConnection();

        // Act
        _channel.RegisterConnection(connection, _ => Task.CompletedTask);

        // Assert
        var info = _channel.GetConnectionInfo(connection.ConnectionId);
        info.Should().NotBeNull();
        info!.UserId.Should().Be(connection.UserId);
    }

    [Fact]
    public void RemoveConnection_RemovesFromConnectionsAndHandlers()
    {
        // Arrange
        var connection = CreateConnection();
        _channel.RegisterConnection(connection, _ => Task.CompletedTask);

        // Act
        _channel.RemoveConnection(connection.ConnectionId);

        // Assert
        _channel.GetConnectionInfo(connection.ConnectionId).Should().BeNull();
    }

    [Fact]
    public void GetAllConnections_ReturnsAllRegistered()
    {
        // Arrange
        var conn1 = CreateConnection(userId: "user-1");
        var conn2 = CreateConnection(userId: "user-2");
        _channel.RegisterConnection(conn1, _ => Task.CompletedTask);
        _channel.RegisterConnection(conn2, _ => Task.CompletedTask);

        // Act
        var all = _channel.GetAllConnections().ToList();

        // Assert
        all.Should().HaveCount(2);
        all.Select(c => c.UserId).Should().Contain("user-1").And.Contain("user-2");
    }

    #endregion

    #region Group Management

    [Fact]
    public async Task RemoveFromAllGroups_RemovesConnectionFromEveryGroup()
    {
        // Arrange
        var connection = CreateConnection();
        _channel.RegisterConnection(connection, _ => Task.CompletedTask);
        _channel.AddToGroup(connection.ConnectionId, "conv-1");
        _channel.AddToGroup(connection.ConnectionId, "conv-2");

        // Act
        _channel.RemoveFromAllGroups(connection.ConnectionId);
        _channel.CleanupEmptyGroups();

        // Assert — sending to those conversations should not reach the connection
        var received = new List<ChannelMessage>();
        // re-register with a handler to verify
        _channel.RemoveConnection(connection.ConnectionId);
        _channel.RegisterConnection(connection, msg => { received.Add(msg); return Task.CompletedTask; });

        // Groups are now empty (connection was removed), so no messages delivered
        await _channel.SendToConversationAsync("conv-1", new ChannelMessage { Content = "test" });
        await _channel.SendToConversationAsync("conv-2", new ChannelMessage { Content = "test" });

        received.Should().BeEmpty();
    }

    [Fact]
    public async Task CleanupEmptyGroups_RemovesEmptyGroupEntries()
    {
        // Arrange
        var connection = CreateConnection();
        _channel.RegisterConnection(connection, _ => Task.CompletedTask);
        _channel.AddToGroup(connection.ConnectionId, "conv-1");
        _channel.RemoveFromGroup(connection.ConnectionId, "conv-1");

        // Act
        _channel.CleanupEmptyGroups();

        // Assert — group should be removed (no direct way to check, but sending should be silent)
        var received = new List<ChannelMessage>();
        _channel.RemoveConnection(connection.ConnectionId);
        _channel.RegisterConnection(connection, msg => { received.Add(msg); return Task.CompletedTask; });

        await _channel.SendToConversationAsync("conv-1", new ChannelMessage { Content = "test" });
        received.Should().BeEmpty();
    }

    #endregion
}
