using Xunit;
using FluentAssertions;
using Moq;
using Microsoft.Extensions.Logging;
using Ato.Copilot.Channels.Abstractions;
using Ato.Copilot.Channels.Implementations;
using Ato.Copilot.Channels.Models;

namespace Ato.Copilot.Tests.Unit.Channels;

public class StreamContextTests
{
    private readonly Mock<IChannel> _channelMock;
    private readonly StreamContext _context;
    private readonly List<ChannelMessage> _sentMessages = new();

    public StreamContextTests()
    {
        _channelMock = new Mock<IChannel>();
        _channelMock
            .Setup(c => c.SendToConversationAsync(It.IsAny<string>(), It.IsAny<ChannelMessage>(), It.IsAny<CancellationToken>()))
            .Callback<string, ChannelMessage, CancellationToken>((_, msg, _) => _sentMessages.Add(msg))
            .Returns(Task.CompletedTask);

        _context = new StreamContext(
            "conv-1",
            "ComplianceAgent",
            _channelMock.Object,
            Mock.Of<ILogger>());
    }

    #region WriteAsync

    [Fact]
    public async Task WriteAsync_String_IncrementsSequenceNumber()
    {
        // Act
        await _context.WriteAsync("chunk1");
        await _context.WriteAsync("chunk2");
        await _context.WriteAsync("chunk3");

        // Assert
        _sentMessages.Should().HaveCount(3);
        _sentMessages[0].SequenceNumber.Should().Be(1);
        _sentMessages[1].SequenceNumber.Should().Be(2);
        _sentMessages[2].SequenceNumber.Should().Be(3);
    }

    [Fact]
    public async Task WriteAsync_String_SendsStreamChunkType()
    {
        // Act
        await _context.WriteAsync("content");

        // Assert
        _sentMessages.Should().HaveCount(1);
        _sentMessages[0].Type.Should().Be(MessageType.StreamChunk);
        _sentMessages[0].Content.Should().Be("content");
        _sentMessages[0].IsStreaming.Should().BeTrue();
        _sentMessages[0].IsComplete.Should().BeFalse();
        _sentMessages[0].AgentType.Should().Be("ComplianceAgent");
    }

    [Fact]
    public async Task WriteAsync_StreamChunk_IncrementsSequenceNumber()
    {
        // Act
        await _context.WriteAsync(new StreamChunk { Content = "a", ContentType = StreamContentType.Markdown });
        await _context.WriteAsync(new StreamChunk { Content = "b", ContentType = StreamContentType.Markdown });

        // Assert
        _sentMessages.Should().HaveCount(2);
        _sentMessages[0].SequenceNumber.Should().Be(1);
        _sentMessages[1].SequenceNumber.Should().Be(2);
    }

    [Fact]
    public async Task WriteAsync_AfterComplete_IsIgnored()
    {
        // Arrange
        await _context.WriteAsync("chunk1");
        await _context.CompleteAsync();
        var countAfterComplete = _sentMessages.Count;

        // Act
        await _context.WriteAsync("should be ignored");

        // Assert
        _sentMessages.Should().HaveCount(countAfterComplete);
    }

    #endregion

    #region CompleteAsync

    [Fact]
    public async Task CompleteAsync_SendsAggregatedContent()
    {
        // Arrange
        await _context.WriteAsync("Hello ");
        await _context.WriteAsync("World");

        // Act
        await _context.CompleteAsync();

        // Assert
        var completion = _sentMessages.Last();
        completion.Type.Should().Be(MessageType.AgentResponse);
        completion.Content.Should().Be("Hello World");
        completion.IsComplete.Should().BeTrue();
        completion.IsStreaming.Should().BeFalse();
    }

    [Fact]
    public async Task CompleteAsync_SetsStreamIdInMetadata()
    {
        // Arrange
        await _context.WriteAsync("data");

        // Act
        await _context.CompleteAsync();

        // Assert
        var completion = _sentMessages.Last();
        completion.Metadata.Should().ContainKey("streamId");
        completion.Metadata["streamId"].Should().Be(_context.StreamId);
    }

    [Fact]
    public async Task CompleteAsync_CalledTwice_IsIdempotent()
    {
        // Arrange
        await _context.WriteAsync("data");

        // Act
        await _context.CompleteAsync();
        var countAfterFirst = _sentMessages.Count;
        await _context.CompleteAsync();

        // Assert
        _sentMessages.Should().HaveCount(countAfterFirst, "second CompleteAsync should be no-op");
    }

    #endregion

    #region AbortAsync

    [Fact]
    public async Task AbortAsync_SendsErrorTypeMessage()
    {
        // Act
        await _context.AbortAsync("Something went wrong");

        // Assert
        _sentMessages.Should().HaveCount(1);
        _sentMessages[0].Type.Should().Be(MessageType.Error);
        _sentMessages[0].Content.Should().Be("Something went wrong");
        _sentMessages[0].IsComplete.Should().BeTrue();
        _sentMessages[0].Metadata.Should().ContainKey("aborted").WhoseValue.Should().Be(true);
    }

    [Fact]
    public async Task AbortAsync_AfterComplete_IsIgnored()
    {
        // Arrange
        await _context.CompleteAsync();
        var countAfterComplete = _sentMessages.Count;

        // Act
        await _context.AbortAsync("error");

        // Assert
        _sentMessages.Should().HaveCount(countAfterComplete);
    }

    #endregion

    #region DisposeAsync

    [Fact]
    public async Task DisposeAsync_AutoCompletesIfNotFinalized()
    {
        // Arrange
        await _context.WriteAsync("pending data");

        // Act
        await _context.DisposeAsync();

        // Assert — should have sent a completion message
        var completion = _sentMessages.Last();
        completion.Type.Should().Be(MessageType.AgentResponse);
        completion.Content.Should().Be("pending data");
        completion.IsComplete.Should().BeTrue();
    }

    [Fact]
    public async Task DisposeAsync_AfterComplete_DoesNotSendDuplicate()
    {
        // Arrange
        await _context.WriteAsync("data");
        await _context.CompleteAsync();
        var countAfterComplete = _sentMessages.Count;

        // Act
        await _context.DisposeAsync();

        // Assert
        _sentMessages.Should().HaveCount(countAfterComplete, "DisposeAsync should not duplicate completion");
    }

    [Fact]
    public async Task DisposeAsync_AfterAbort_DoesNotSendDuplicate()
    {
        // Arrange
        await _context.WriteAsync("data");
        await _context.AbortAsync("error");
        var countAfterAbort = _sentMessages.Count;

        // Act
        await _context.DisposeAsync();

        // Assert
        _sentMessages.Should().HaveCount(countAfterAbort, "DisposeAsync should not duplicate after abort");
    }

    #endregion

    #region StreamId

    [Fact]
    public void StreamId_IsValidGuid()
    {
        Guid.TryParse(_context.StreamId, out _).Should().BeTrue();
    }

    [Fact]
    public void StreamId_IsUniquePerInstance()
    {
        var context2 = new StreamContext("conv-1", null, _channelMock.Object, Mock.Of<ILogger>());
        _context.StreamId.Should().NotBe(context2.StreamId);
    }

    #endregion
}
