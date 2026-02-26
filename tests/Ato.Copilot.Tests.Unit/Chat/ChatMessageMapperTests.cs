using FluentAssertions;
using Xunit;
using Ato.Copilot.Channels.Models;
using Ato.Copilot.Chat.Channels;
using Ato.Copilot.Chat.Models;

namespace Ato.Copilot.Tests.Unit.Chat;

/// <summary>
/// Unit tests for <see cref="ChatMessageMapper"/> — verifies correct mapping
/// between Chat app DTOs and Channels library models.
/// </summary>
public class ChatMessageMapperTests
{
    // ─── ToIncomingMessage ───────────────────────────────────────

    [Fact]
    public void ToIncomingMessage_MapsAllFields()
    {
        var request = new SendMessageRequest
        {
            ConversationId = "conv-1",
            Message = "Hello",
            AttachmentIds = new List<string> { "att-1", "att-2" },
            Context = new Dictionary<string, object>
            {
                ["source"] = "web",
                ["targetAgentType"] = "compliance"
            }
        };

        var result = ChatMessageMapper.ToIncomingMessage(request, "conn-abc");

        result.ConnectionId.Should().Be("conn-abc");
        result.ConversationId.Should().Be("conv-1");
        result.Content.Should().Be("Hello");
        result.TargetAgentType.Should().Be("compliance");
        result.Metadata.Should().ContainKey("source");
        result.Metadata.Should().ContainKey("attachmentIds");
    }

    [Fact]
    public void ToIncomingMessage_UsesGetContent_ForContentAlias()
    {
        var request = new SendMessageRequest
        {
            ConversationId = "conv-1",
            Content = "Via content field"
        };

        var result = ChatMessageMapper.ToIncomingMessage(request, "conn-1");

        result.Content.Should().Be("Via content field");
    }

    [Fact]
    public void ToIncomingMessage_WithNullContext_HasEmptyMetadata()
    {
        var request = new SendMessageRequest
        {
            ConversationId = "conv-1",
            Message = "No context"
        };

        var result = ChatMessageMapper.ToIncomingMessage(request, "conn-1");

        result.Metadata.Should().NotBeNull();
        result.TargetAgentType.Should().BeNull();
    }

    // ─── ToChannelMessage ────────────────────────────────────────

    [Fact]
    public void ToChannelMessage_Success_MapsToAgentResponse()
    {
        var response = new ChatResponse
        {
            MessageId = "msg-1",
            Content = "AI says hello",
            Success = true,
            Metadata = new Dictionary<string, object> { ["intent"] = "greeting" }
        };

        var result = ChatMessageMapper.ToChannelMessage(response, "conv-1");

        result.MessageId.Should().Be("msg-1");
        result.ConversationId.Should().Be("conv-1");
        result.Type.Should().Be(MessageType.AgentResponse);
        result.Content.Should().Be("AI says hello");
        result.IsComplete.Should().BeTrue();
        result.Metadata.Should().ContainKey("intent");
    }

    [Fact]
    public void ToChannelMessage_Error_MapsToErrorType()
    {
        var response = new ChatResponse
        {
            MessageId = "msg-err",
            Content = "",
            Success = false,
            Error = "Something broke"
        };

        var result = ChatMessageMapper.ToChannelMessage(response, "conv-1");

        result.Type.Should().Be(MessageType.Error);
        result.Content.Should().Be("Something broke");
    }

    [Fact]
    public void ToChannelMessage_ErrorWithNullError_UsesDefaultMessage()
    {
        var response = new ChatResponse
        {
            MessageId = "msg-err",
            Success = false,
            Error = null
        };

        var result = ChatMessageMapper.ToChannelMessage(response, "conv-1");

        result.Content.Should().Be("The request could not be processed");
    }

    // ─── ToChatResponse ──────────────────────────────────────────

    [Fact]
    public void ToChatResponse_SuccessMessage_MapsCorrectly()
    {
        var message = new ChannelMessage
        {
            MessageId = "msg-1",
            Type = MessageType.AgentResponse,
            Content = "Response text",
            Metadata = new Dictionary<string, object> { ["key"] = "value" }
        };

        var result = ChatMessageMapper.ToChatResponse(message);

        result.MessageId.Should().Be("msg-1");
        result.Content.Should().Be("Response text");
        result.Success.Should().BeTrue();
        result.Error.Should().BeNull();
    }

    [Fact]
    public void ToChatResponse_ErrorMessage_SetsErrorAndEmptyContent()
    {
        var message = new ChannelMessage
        {
            MessageId = "msg-err",
            Type = MessageType.Error,
            Content = "Error details"
        };

        var result = ChatMessageMapper.ToChatResponse(message);

        result.Success.Should().BeFalse();
        result.Error.Should().Be("Error details");
        result.Content.Should().BeEmpty();
    }

    // ─── ToSignalRPayload ────────────────────────────────────────

    [Fact]
    public void ToSignalRPayload_ContainsExpectedShape()
    {
        var message = new ChannelMessage
        {
            MessageId = "msg-1",
            ConversationId = "conv-1",
            Type = MessageType.AgentResponse,
            Content = "Hello",
            IsComplete = true,
            Metadata = new Dictionary<string, object>()
        };

        var payload = ChatMessageMapper.ToSignalRPayload(message);

        // Verify structure via reflection (anonymous type)
        var type = payload.GetType();
        type.GetProperty("conversationId")!.GetValue(payload).Should().Be("conv-1");

        var innerMessage = type.GetProperty("message")!.GetValue(payload)!;
        var innerType = innerMessage.GetType();
        innerType.GetProperty("id")!.GetValue(innerMessage).Should().Be("msg-1");
        innerType.GetProperty("role")!.GetValue(innerMessage).Should().Be("Assistant");
        innerType.GetProperty("content")!.GetValue(innerMessage).Should().Be("Hello");
        innerType.GetProperty("status")!.GetValue(innerMessage).Should().Be("Completed");
    }

    [Fact]
    public void ToSignalRPayload_ErrorType_SetsSystemRole()
    {
        var message = new ChannelMessage
        {
            MessageId = "msg-err",
            ConversationId = "conv-1",
            Type = MessageType.Error,
            Content = "Error"
        };

        var payload = ChatMessageMapper.ToSignalRPayload(message);
        var innerMessage = payload.GetType().GetProperty("message")!.GetValue(payload)!;
        innerMessage.GetType().GetProperty("role")!.GetValue(innerMessage).Should().Be("System");
    }

    // ─── ToSignalRErrorPayload ───────────────────────────────────

    [Fact]
    public void ToSignalRErrorPayload_ContainsExpectedShape()
    {
        var message = new ChannelMessage
        {
            ConversationId = "conv-1",
            Type = MessageType.Error,
            Content = "Something failed",
            Metadata = new Dictionary<string, object> { ["errorCategory"] = "Timeout" }
        };

        var payload = ChatMessageMapper.ToSignalRErrorPayload(message, "msg-orig");

        var type = payload.GetType();
        type.GetProperty("conversationId")!.GetValue(payload).Should().Be("conv-1");
        type.GetProperty("messageId")!.GetValue(payload).Should().Be("msg-orig");
        type.GetProperty("error")!.GetValue(payload).Should().Be("Something failed");
        type.GetProperty("category")!.GetValue(payload).Should().Be("Timeout");
    }

    [Fact]
    public void ToSignalRErrorPayload_DefaultCategory_WhenNotInMetadata()
    {
        var message = new ChannelMessage
        {
            ConversationId = "conv-1",
            Type = MessageType.Error,
            Content = "Error",
            Metadata = new Dictionary<string, object>()
        };

        var payload = ChatMessageMapper.ToSignalRErrorPayload(message, "msg-1");

        payload.GetType().GetProperty("category")!.GetValue(payload).Should().Be("ProcessingError");
    }

    // ─── Round-trip ──────────────────────────────────────────────

    [Fact]
    public void RoundTrip_RequestToIncomingToResponsePreservesData()
    {
        // Request → IncomingMessage
        var request = new SendMessageRequest
        {
            ConversationId = "conv-rt",
            Message = "Round trip test"
        };
        var incoming = ChatMessageMapper.ToIncomingMessage(request, "conn-rt");

        incoming.ConversationId.Should().Be("conv-rt");
        incoming.Content.Should().Be("Round trip test");

        // Simulate handler response → ChannelMessage → ChatResponse
        var chatResponse = new ChatResponse
        {
            MessageId = "msg-rt",
            Content = "AI round trip",
            Success = true,
            Metadata = new Dictionary<string, object>()
        };
        var channelMsg = ChatMessageMapper.ToChannelMessage(chatResponse, "conv-rt");
        var backToResponse = ChatMessageMapper.ToChatResponse(channelMsg);

        backToResponse.MessageId.Should().Be("msg-rt");
        backToResponse.Content.Should().Be("AI round trip");
        backToResponse.Success.Should().BeTrue();
    }
}
