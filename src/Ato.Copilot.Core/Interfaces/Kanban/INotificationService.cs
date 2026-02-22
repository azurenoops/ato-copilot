using Ato.Copilot.Core.Models.Kanban;

namespace Ato.Copilot.Core.Interfaces.Kanban;

/// <summary>
/// Service interface for asynchronous notification dispatch.
/// Registered as Singleton — maintains bounded channel and HTTP clients.
/// </summary>
public interface INotificationService
{
    /// <summary>
    /// Enqueues a notification for asynchronous delivery.
    /// This method does not block on delivery — notifications are dispatched in background.
    /// </summary>
    /// <param name="message">The notification message to enqueue.</param>
    /// <returns>A task that completes when the message is enqueued (not delivered).</returns>
    Task EnqueueAsync(NotificationMessage message);
}

/// <summary>
/// Notification message payload for async dispatch.
/// </summary>
public record NotificationMessage
{
    /// <summary>Type of notification event.</summary>
    public NotificationEventType EventType { get; init; }

    /// <summary>Task ID that triggered the notification.</summary>
    public string TaskId { get; init; } = "";

    /// <summary>Human-readable task number (e.g., REM-001).</summary>
    public string TaskNumber { get; init; } = "";

    /// <summary>Board ID containing the task.</summary>
    public string BoardId { get; init; } = "";

    /// <summary>Target user ID for the notification.</summary>
    public string TargetUserId { get; init; } = "";

    /// <summary>Notification title/subject.</summary>
    public string Title { get; init; } = "";

    /// <summary>Notification body/details.</summary>
    public string Details { get; init; } = "";
}
