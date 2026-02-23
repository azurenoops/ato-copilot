using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Ato.Copilot.Chat.Models;
using Ato.Copilot.Chat.Services;

namespace Ato.Copilot.Chat.Hubs;

/// <summary>
/// SignalR hub for real-time chat messaging.
/// Uses IServiceScopeFactory for scoped service resolution per research.md R-003.
/// </summary>
public class ChatHub : Hub
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ChatHub> _logger;

    public ChatHub(IServiceScopeFactory scopeFactory, ILogger<ChatHub> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <summary>
    /// Joins a conversation group to receive real-time updates.
    /// </summary>
    /// <param name="conversationId">The conversation ID to join.</param>
    public async Task JoinConversation(string conversationId)
    {
        if (string.IsNullOrWhiteSpace(conversationId))
            throw new HubException("ConversationId is required");

        await Groups.AddToGroupAsync(Context.ConnectionId, conversationId);
        _logger.LogInformation("Connection {ConnectionId} joined conversation {ConversationId}",
            Context.ConnectionId, conversationId);
    }

    /// <summary>
    /// Leaves a conversation group.
    /// </summary>
    /// <param name="conversationId">The conversation ID to leave.</param>
    public async Task LeaveConversation(string conversationId)
    {
        if (string.IsNullOrWhiteSpace(conversationId))
            throw new HubException("ConversationId is required");

        await Groups.RemoveFromGroupAsync(Context.ConnectionId, conversationId);
        _logger.LogInformation("Connection {ConnectionId} left conversation {ConversationId}",
            Context.ConnectionId, conversationId);
    }

    /// <summary>
    /// Sends a message, processes via AI, and broadcasts result to conversation group.
    /// </summary>
    /// <param name="request">The send message request.</param>
    public async Task SendMessage(SendMessageRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.ConversationId))
            throw new HubException("ConversationId is required");

        if (string.IsNullOrWhiteSpace(request.GetContent()))
            throw new HubException("Message content is required");

        if (request.GetContent().Length > 10_000)
            throw new HubException("Message content exceeds 10,000 character limit");

        var messageId = Guid.NewGuid().ToString();

        try
        {
            // Notify group: processing started
            await Clients.Group(request.ConversationId).SendAsync("MessageProcessing", new
            {
                conversationId = request.ConversationId,
                messageId,
                status = "Processing"
            });

            // Process message via scoped ChatService
            using var scope = _scopeFactory.CreateScope();
            var chatService = scope.ServiceProvider.GetRequiredService<IChatService>();
            var response = await chatService.SendMessageAsync(request);

            if (response.Success)
            {
                // Broadcast the AI response message
                await Clients.Group(request.ConversationId).SendAsync("MessageReceived", new
                {
                    conversationId = request.ConversationId,
                    message = new
                    {
                        id = response.MessageId,
                        conversationId = request.ConversationId,
                        role = "Assistant",
                        content = response.Content,
                        status = "Completed",
                        timestamp = DateTime.UtcNow,
                        metadata = response.Metadata,
                        attachments = Array.Empty<object>(),
                        toolResults = Array.Empty<object>()
                    }
                });
            }
            else
            {
                // Broadcast error
                var category = response.Metadata?.TryGetValue("errorCategory", out var cat) == true
                    ? cat.ToString()
                    : "ProcessingError";

                await Clients.Group(request.ConversationId).SendAsync("MessageError", new
                {
                    conversationId = request.ConversationId,
                    messageId,
                    error = response.Error ?? "The request could not be processed",
                    category
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing SignalR message for conversation {ConversationId}", request.ConversationId);

            await Clients.Group(request.ConversationId).SendAsync("MessageError", new
            {
                conversationId = request.ConversationId,
                messageId,
                error = "The request could not be processed",
                category = "ProcessingError"
            });
        }
    }

    /// <summary>
    /// Notifies other participants that a user is typing.
    /// </summary>
    /// <param name="conversationId">The conversation ID.</param>
    /// <param name="userId">The typing user's ID.</param>
    public async Task NotifyTyping(string conversationId, string userId)
    {
        if (string.IsNullOrWhiteSpace(conversationId) || string.IsNullOrWhiteSpace(userId))
            return;

        await Clients.OthersInGroup(conversationId).SendAsync("UserTyping", new
        {
            conversationId,
            userId
        });
    }

    /// <summary>
    /// Logs connection establishment.
    /// </summary>
    public override Task OnConnectedAsync()
    {
        _logger.LogInformation("ChatHub connection established: {ConnectionId}", Context.ConnectionId);
        return base.OnConnectedAsync();
    }

    /// <summary>
    /// Logs disconnection with reason.
    /// </summary>
    public override Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation("ChatHub connection closed: {ConnectionId}. Reason: {Reason}",
            Context.ConnectionId, exception?.Message ?? "Client disconnected");
        return base.OnDisconnectedAsync(exception);
    }
}
