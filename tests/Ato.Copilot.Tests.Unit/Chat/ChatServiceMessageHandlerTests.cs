using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Ato.Copilot.Channels.Models;
using Ato.Copilot.Chat.Channels;
using Ato.Copilot.Chat.Models;
using Ato.Copilot.Chat.Services;

namespace Ato.Copilot.Tests.Unit.Chat;

/// <summary>
/// Unit tests for <see cref="ChatServiceMessageHandler"/> — verifies that IMessageHandler
/// correctly wraps IChatService and maps between Channels and Chat models.
/// </summary>
public class ChatServiceMessageHandlerTests
{
    private readonly Mock<IChatService> _chatServiceMock;
    private readonly ChatServiceMessageHandler _handler;

    public ChatServiceMessageHandlerTests()
    {
        _chatServiceMock = new Mock<IChatService>();
        _handler = new ChatServiceMessageHandler(
            _chatServiceMock.Object,
            Mock.Of<ILogger<ChatServiceMessageHandler>>());
    }

    // ─── Success Path ────────────────────────────────────────────

    [Fact]
    public async Task HandleMessageAsync_DelegatesToChatService()
    {
        var incoming = new IncomingMessage
        {
            ConnectionId = "conn-1",
            ConversationId = "conv-1",
            Content = "Hello AI"
        };

        _chatServiceMock
            .Setup(s => s.SendMessageAsync(It.IsAny<SendMessageRequest>(), null))
            .ReturnsAsync(new ChatResponse
            {
                MessageId = "msg-1",
                Content = "AI response",
                Success = true
            });

        var result = await _handler.HandleMessageAsync(incoming);

        _chatServiceMock.Verify(
            s => s.SendMessageAsync(
                It.Is<SendMessageRequest>(r =>
                    r.ConversationId == "conv-1" &&
                    r.Message == "Hello AI"),
                null),
            Times.Once);
    }

    [Fact]
    public async Task HandleMessageAsync_ReturnsMappedChannelMessage()
    {
        var incoming = new IncomingMessage
        {
            ConnectionId = "conn-1",
            ConversationId = "conv-1",
            Content = "Test"
        };

        _chatServiceMock
            .Setup(s => s.SendMessageAsync(It.IsAny<SendMessageRequest>(), null))
            .ReturnsAsync(new ChatResponse
            {
                MessageId = "msg-1",
                Content = "AI response",
                Success = true,
                Metadata = new Dictionary<string, object> { ["intent"] = "test" }
            });

        var result = await _handler.HandleMessageAsync(incoming);

        result.MessageId.Should().Be("msg-1");
        result.ConversationId.Should().Be("conv-1");
        result.Type.Should().Be(MessageType.AgentResponse);
        result.Content.Should().Be("AI response");
        result.IsComplete.Should().BeTrue();
        result.Metadata.Should().ContainKey("intent");
    }

    [Fact]
    public async Task HandleMessageAsync_MapsErrorResponse()
    {
        var incoming = new IncomingMessage
        {
            ConnectionId = "conn-1",
            ConversationId = "conv-1",
            Content = "Bad query"
        };

        _chatServiceMock
            .Setup(s => s.SendMessageAsync(It.IsAny<SendMessageRequest>(), null))
            .ReturnsAsync(new ChatResponse
            {
                MessageId = "msg-err",
                Success = false,
                Error = "Invalid query"
            });

        var result = await _handler.HandleMessageAsync(incoming);

        result.Type.Should().Be(MessageType.Error);
        result.Content.Should().Be("Invalid query");
    }

    // ─── Exception Handling ──────────────────────────────────────

    [Fact]
    public async Task HandleMessageAsync_ReturnsErrorOnException()
    {
        var incoming = new IncomingMessage
        {
            ConnectionId = "conn-1",
            ConversationId = "conv-1",
            Content = "Crash"
        };

        _chatServiceMock
            .Setup(s => s.SendMessageAsync(It.IsAny<SendMessageRequest>(), null))
            .ThrowsAsync(new Exception("Service unavailable"));

        var result = await _handler.HandleMessageAsync(incoming);

        result.Type.Should().Be(MessageType.Error);
        result.Content.Should().Be("The request could not be processed");
        result.ConversationId.Should().Be("conv-1");
        result.Metadata.Should().ContainKey("errorCategory");
        result.Metadata["errorCategory"].Should().Be("ProcessingError");
    }

    // ─── Metadata Mapping ────────────────────────────────────────

    [Fact]
    public async Task HandleMessageAsync_PassesMetadataAsContext()
    {
        var incoming = new IncomingMessage
        {
            ConnectionId = "conn-1",
            ConversationId = "conv-1",
            Content = "With metadata",
            Metadata = new Dictionary<string, object>
            {
                ["source"] = "vscode",
                ["workspaceId"] = "ws-123"
            }
        };

        _chatServiceMock
            .Setup(s => s.SendMessageAsync(It.IsAny<SendMessageRequest>(), null))
            .ReturnsAsync(new ChatResponse { MessageId = "msg-1", Content = "OK", Success = true });

        await _handler.HandleMessageAsync(incoming);

        _chatServiceMock.Verify(
            s => s.SendMessageAsync(
                It.Is<SendMessageRequest>(r =>
                    r.Context != null &&
                    r.Context.ContainsKey("source") &&
                    r.Context.ContainsKey("workspaceId")),
                null),
            Times.Once);
    }

    [Fact]
    public async Task HandleMessageAsync_MapsAttachmentIds()
    {
        var incoming = new IncomingMessage
        {
            ConnectionId = "conn-1",
            ConversationId = "conv-1",
            Content = "With attachments",
            Metadata = new Dictionary<string, object>
            {
                ["attachmentIds"] = new List<string> { "att-1", "att-2" }
            }
        };

        _chatServiceMock
            .Setup(s => s.SendMessageAsync(It.IsAny<SendMessageRequest>(), null))
            .ReturnsAsync(new ChatResponse { MessageId = "msg-1", Content = "OK", Success = true });

        await _handler.HandleMessageAsync(incoming);

        _chatServiceMock.Verify(
            s => s.SendMessageAsync(
                It.Is<SendMessageRequest>(r =>
                    r.AttachmentIds != null &&
                    r.AttachmentIds.Count == 2),
                null),
            Times.Once);
    }
}
