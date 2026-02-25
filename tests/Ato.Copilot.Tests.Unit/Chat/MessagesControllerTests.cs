using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Ato.Copilot.Chat.Controllers;
using Ato.Copilot.Chat.Models;
using Ato.Copilot.Chat.Services;

namespace Ato.Copilot.Tests.Unit.Chat;

/// <summary>
/// Unit tests for MessagesController covering positive, negative, and boundary scenarios.
/// </summary>
public class MessagesControllerTests
{
    private readonly Mock<IChatService> _chatServiceMock;
    private readonly Mock<ILogger<MessagesController>> _loggerMock;
    private readonly MessagesController _controller;

    public MessagesControllerTests()
    {
        _chatServiceMock = new Mock<IChatService>();
        _loggerMock = new Mock<ILogger<MessagesController>>();
        _controller = new MessagesController(_chatServiceMock.Object, _loggerMock.Object);
    }

    // ─── Positive Tests ──────────────────────────────────────────

    [Fact]
    public async Task PostMessage_WithValidRequest_ReturnsOkWithChatResponse()
    {
        // Arrange
        var request = new SendMessageRequest
        {
            ConversationId = Guid.NewGuid().ToString(),
            Message = "Hello, AI!"
        };

        var expectedResponse = new ChatResponse
        {
            MessageId = Guid.NewGuid().ToString(),
            Content = "Hello! How can I help?",
            Success = true
        };

        _chatServiceMock
            .Setup(s => s.SendMessageAsync(It.IsAny<SendMessageRequest>(), It.IsAny<IProgress<string>?>()))
            .ReturnsAsync(expectedResponse);

        // Act
        var result = await _controller.PostMessage(request);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var chatResponse = okResult.Value.Should().BeOfType<ChatResponse>().Subject;
        chatResponse.Success.Should().BeTrue();
        chatResponse.Content.Should().Be("Hello! How can I help?");
    }

    [Fact]
    public async Task GetMessages_WithValidConversationId_ReturnsOkWithMessages()
    {
        // Arrange
        var conversationId = Guid.NewGuid().ToString();
        var messages = new List<ChatMessage>
        {
            new() { Id = Guid.NewGuid().ToString(), ConversationId = conversationId, Content = "Message 1", Role = MessageRole.User, Timestamp = DateTime.UtcNow, Status = MessageStatus.Completed },
            new() { Id = Guid.NewGuid().ToString(), ConversationId = conversationId, Content = "Response 1", Role = MessageRole.Assistant, Timestamp = DateTime.UtcNow.AddSeconds(1), Status = MessageStatus.Completed }
        };

        _chatServiceMock
            .Setup(s => s.GetMessagesAsync(conversationId, 0, 100))
            .ReturnsAsync(messages);

        // Act
        var result = await _controller.GetMessages(conversationId, 0, 100);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var returnedMessages = okResult.Value.Should().BeAssignableTo<List<ChatMessage>>().Subject;
        returnedMessages.Should().HaveCount(2);
    }

    // ─── Negative Tests ──────────────────────────────────────────

    [Fact]
    public async Task PostMessage_WithEmptyMessage_ReturnsBadRequest()
    {
        // Arrange
        var request = new SendMessageRequest
        {
            ConversationId = Guid.NewGuid().ToString(),
            Message = ""
        };

        // Act
        var result = await _controller.PostMessage(request);

        // Assert
        var badRequest = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var error = badRequest.Value.Should().BeOfType<ErrorResponse>().Subject;
        error.Message.Should().Contain("required");
    }

    [Fact]
    public async Task PostMessage_WithNullMessage_ReturnsBadRequest()
    {
        // Arrange
        var request = new SendMessageRequest
        {
            ConversationId = Guid.NewGuid().ToString(),
            Message = null!
        };

        // Act
        var result = await _controller.PostMessage(request);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task PostMessage_WithEmptyConversationId_ReturnsBadRequest()
    {
        // Arrange
        var request = new SendMessageRequest
        {
            ConversationId = "",
            Message = "Hello"
        };

        // Act
        var result = await _controller.PostMessage(request);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task GetMessages_WithEmptyConversationId_ReturnsBadRequest()
    {
        // Act
        var result = await _controller.GetMessages("", 0, 100);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    // ─── Boundary Tests ──────────────────────────────────────────

    [Fact]
    public async Task GetMessages_WithDefaultPagination_UsesSkip0Take100()
    {
        // Arrange
        var conversationId = Guid.NewGuid().ToString();
        _chatServiceMock
            .Setup(s => s.GetMessagesAsync(conversationId, 0, 100))
            .ReturnsAsync(new List<ChatMessage>());

        // Act
        await _controller.GetMessages(conversationId);

        // Assert
        _chatServiceMock.Verify(s => s.GetMessagesAsync(conversationId, 0, 100), Times.Once);
    }

    [Fact]
    public async Task GetMessages_WithCustomPagination_PassesCorrectValues()
    {
        // Arrange
        var conversationId = Guid.NewGuid().ToString();
        _chatServiceMock
            .Setup(s => s.GetMessagesAsync(conversationId, 10, 50))
            .ReturnsAsync(new List<ChatMessage>());

        // Act
        await _controller.GetMessages(conversationId, skip: 10, take: 50);

        // Assert
        _chatServiceMock.Verify(s => s.GetMessagesAsync(conversationId, 10, 50), Times.Once);
    }

    [Fact]
    public async Task PostMessage_WhenServiceReturnsError_ReturnsOkWithErrorResponse()
    {
        // Arrange
        var request = new SendMessageRequest
        {
            ConversationId = Guid.NewGuid().ToString(),
            Message = "Test message"
        };

        _chatServiceMock
            .Setup(s => s.SendMessageAsync(It.IsAny<SendMessageRequest>(), It.IsAny<IProgress<string>?>()))
            .ReturnsAsync(new ChatResponse
            {
                MessageId = Guid.NewGuid().ToString(),
                Content = "",
                Success = false,
                Error = "Service unavailable"
            });

        // Act
        var result = await _controller.PostMessage(request);

        // Assert — service errors still return 200 OK with error in response body
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var chatResponse = okResult.Value.Should().BeOfType<ChatResponse>().Subject;
        chatResponse.Success.Should().BeFalse();
        chatResponse.Error.Should().Be("Service unavailable");
    }
}
