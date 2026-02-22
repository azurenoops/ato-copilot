using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Ato.Copilot.Core.Interfaces.Kanban;
using Ato.Copilot.Core.Models.Kanban;
using TaskStatus = Ato.Copilot.Core.Models.Kanban.TaskStatus;

namespace Ato.Copilot.Agents.Compliance.Services;

/// <summary>
/// Background service that periodically scans for overdue remediation tasks
/// and enqueues TaskOverdue notifications. Creates a scoped DI per tick.
/// </summary>
public class OverdueScanHostedService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly INotificationService _notificationService;
    private readonly ILogger<OverdueScanHostedService> _logger;
    private readonly TimeSpan _interval;

    /// <summary>
    /// Initializes a new instance of <see cref="OverdueScanHostedService"/>.
    /// </summary>
    public OverdueScanHostedService(
        IServiceProvider serviceProvider,
        INotificationService notificationService,
        ILogger<OverdueScanHostedService> logger,
        IConfiguration configuration)
    {
        _serviceProvider = serviceProvider;
        _notificationService = notificationService;
        _logger = logger;

        var intervalMinutes = configuration.GetValue<int>("Agents:Kanban:OverdueScan:IntervalMinutes", 5);
        _interval = TimeSpan.FromMinutes(intervalMinutes);
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("OverdueScanHostedService started with interval {Interval}", _interval);

        using var timer = new PeriodicTimer(_interval);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await ScanForOverdueTasksAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during overdue task scan");
            }
        }

        _logger.LogInformation("OverdueScanHostedService stopped");
    }

    private async Task ScanForOverdueTasksAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var kanbanService = scope.ServiceProvider.GetRequiredService<IKanbanService>();

        // Query overdue tasks via ListTasks across all boards
        // For now, this is a simplified scan. In production, query all boards and tasks directly.
        _logger.LogDebug("Scanning for overdue tasks...");

        // Direct DB query would be more efficient but service layer enforces RBAC.
        // For background service, we query the DB context directly.
        var context = scope.ServiceProvider.GetRequiredService<Ato.Copilot.Core.Data.Context.AtoCopilotContext>();

        var overdueTasks = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions
            .ToListAsync(
                context.RemediationTasks
                    .Where(t => t.DueDate < DateTime.UtcNow
                        && t.Status != TaskStatus.Done
                        && t.Status != TaskStatus.Blocked
                        && (t.LastOverdueNotifiedAt == null || t.LastOverdueNotifiedAt < DateTime.UtcNow.AddHours(-24))),
                cancellationToken);

        if (overdueTasks.Count == 0)
        {
            _logger.LogDebug("No overdue tasks found");
            return;
        }

        _logger.LogInformation("Found {Count} overdue tasks", overdueTasks.Count);

        foreach (var task in overdueTasks)
        {
            task.LastOverdueNotifiedAt = DateTime.UtcNow;

            await _notificationService.EnqueueAsync(new NotificationMessage
            {
                EventType = NotificationEventType.TaskOverdue,
                TaskId = task.Id,
                TaskNumber = task.TaskNumber,
                BoardId = task.BoardId,
                TargetUserId = task.AssigneeId ?? "",
                Title = $"Task {task.TaskNumber} is overdue",
                Details = $"Due date was {task.DueDate:yyyy-MM-dd}. Current status: {task.Status}",
            });
        }

        await context.SaveChangesAsync(cancellationToken);
    }
}
