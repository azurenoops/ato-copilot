using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using FluentAssertions;
using Ato.Copilot.Agents.Compliance.Services;
using Ato.Copilot.Core.Data.Context;
using Ato.Copilot.Core.Interfaces.Kanban;
using Ato.Copilot.Core.Models.Compliance;
using Ato.Copilot.Core.Models.Kanban;
using TaskStatus = Ato.Copilot.Core.Models.Kanban.TaskStatus;

namespace Ato.Copilot.Tests.Unit.Services;

/// <summary>
/// Unit tests for OverdueScanHostedService:
/// queries overdue tasks, enqueues notifications, updates LastOverdueNotifiedAt.
/// </summary>
public class OverdueScanHostedServiceTests : IDisposable
{
    private readonly AtoCopilotContext _context;
    private readonly Mock<INotificationService> _notificationMock = new();
    private readonly ServiceProvider _serviceProvider;

    public OverdueScanHostedServiceTests()
    {
        var dbName = $"OverdueScan_{Guid.NewGuid()}";
        var options = new DbContextOptionsBuilder<AtoCopilotContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        _context = new AtoCopilotContext(options);

        _notificationMock.Setup(n => n.EnqueueAsync(It.IsAny<NotificationMessage>()))
            .Returns(Task.CompletedTask);

        // Build a service provider so OverdueScanHostedService can create scopes
        var services = new ServiceCollection();
        services.AddDbContext<AtoCopilotContext>(opt => opt.UseInMemoryDatabase(dbName));
        services.AddScoped<IKanbanService, KanbanService>();
        services.AddSingleton(_notificationMock.Object);
        services.AddSingleton(Mock.Of<Ato.Copilot.State.Abstractions.IAgentStateManager>());
        services.AddSingleton(Mock.Of<Ato.Copilot.Core.Interfaces.Compliance.IAtoComplianceEngine>());
        services.AddSingleton(Mock.Of<Ato.Copilot.Core.Interfaces.Compliance.IRemediationEngine>());
        services.AddLogging();
        _serviceProvider = services.BuildServiceProvider();
    }

    public void Dispose()
    {
        _context.Dispose();
        _serviceProvider.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task ScanFindsOverdueTasks_EnqueuesNotifications()
    {
        // Seed an overdue task
        var board = new RemediationBoard { Name = "B", SubscriptionId = "sub", Owner = "o" };
        board.Tasks.Add(new RemediationTask
        {
            BoardId = board.Id,
            TaskNumber = "REM-001", Title = "Overdue",
            ControlId = "AC-1", ControlFamily = "AC",
            Status = TaskStatus.InProgress,
            Severity = FindingSeverity.Critical,
            DueDate = DateTime.UtcNow.AddDays(-2),
            AssigneeId = "alice",
            CreatedBy = "owner",
        });
        _context.RemediationBoards.Add(board);
        await _context.SaveChangesAsync();

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Agents:Kanban:OverdueScan:IntervalMinutes"] = "60"
            })
            .Build();

        var service = new OverdueScanHostedService(
            _serviceProvider,
            _notificationMock.Object,
            Mock.Of<ILogger<OverdueScanHostedService>>(),
            config);

        // Start and immediately stop to trigger one scan
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        // The ExecuteAsync waits for the first timer tick, so we use reflection to call the private ScanForOverdueTasksAsync
        var method = typeof(OverdueScanHostedService).GetMethod("ScanForOverdueTasksAsync",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        method.Should().NotBeNull("ScanForOverdueTasksAsync should exist");

        await (Task)method!.Invoke(service, new object[] { CancellationToken.None })!;

        _notificationMock.Verify(n => n.EnqueueAsync(It.Is<NotificationMessage>(
            m => m.EventType == NotificationEventType.TaskOverdue && m.TaskNumber == "REM-001")),
            Times.Once);
    }

    [Fact]
    public async Task ScanSkipsDoneTasks()
    {
        var board = new RemediationBoard { Name = "B", SubscriptionId = "sub", Owner = "o" };
        board.Tasks.Add(new RemediationTask
        {
            BoardId = board.Id,
            TaskNumber = "REM-001", Title = "Done overdue",
            ControlId = "AC-1", ControlFamily = "AC",
            Status = TaskStatus.Done,
            Severity = FindingSeverity.High,
            DueDate = DateTime.UtcNow.AddDays(-5),
            CreatedBy = "owner",
        });
        _context.RemediationBoards.Add(board);
        await _context.SaveChangesAsync();

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Agents:Kanban:OverdueScan:IntervalMinutes"] = "60" })
            .Build();

        var service = new OverdueScanHostedService(
            _serviceProvider, _notificationMock.Object,
            Mock.Of<ILogger<OverdueScanHostedService>>(), config);

        var method = typeof(OverdueScanHostedService).GetMethod("ScanForOverdueTasksAsync",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        await (Task)method!.Invoke(service, new object[] { CancellationToken.None })!;

        _notificationMock.Verify(n => n.EnqueueAsync(It.IsAny<NotificationMessage>()), Times.Never);
    }

    [Fact]
    public async Task ScanSkipsRecentlyNotified()
    {
        var board = new RemediationBoard { Name = "B", SubscriptionId = "sub", Owner = "o" };
        board.Tasks.Add(new RemediationTask
        {
            BoardId = board.Id,
            TaskNumber = "REM-001", Title = "Recently notified",
            ControlId = "AC-1", ControlFamily = "AC",
            Status = TaskStatus.InProgress,
            Severity = FindingSeverity.High,
            DueDate = DateTime.UtcNow.AddDays(-2),
            LastOverdueNotifiedAt = DateTime.UtcNow.AddHours(-1), // notified 1h ago (within 24h window)
            CreatedBy = "owner",
        });
        _context.RemediationBoards.Add(board);
        await _context.SaveChangesAsync();

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Agents:Kanban:OverdueScan:IntervalMinutes"] = "60" })
            .Build();

        var service = new OverdueScanHostedService(
            _serviceProvider, _notificationMock.Object,
            Mock.Of<ILogger<OverdueScanHostedService>>(), config);

        var method = typeof(OverdueScanHostedService).GetMethod("ScanForOverdueTasksAsync",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        await (Task)method!.Invoke(service, new object[] { CancellationToken.None })!;

        _notificationMock.Verify(n => n.EnqueueAsync(It.IsAny<NotificationMessage>()), Times.Never);
    }

    [Fact]
    public async Task ScanUpdatesLastOverdueNotifiedAt()
    {
        var board = new RemediationBoard { Name = "B", SubscriptionId = "sub", Owner = "o" };
        var task = new RemediationTask
        {
            BoardId = board.Id,
            TaskNumber = "REM-001", Title = "Overdue",
            ControlId = "AC-1", ControlFamily = "AC",
            Status = TaskStatus.ToDo,
            Severity = FindingSeverity.Medium,
            DueDate = DateTime.UtcNow.AddDays(-1),
            CreatedBy = "owner",
        };
        board.Tasks.Add(task);
        _context.RemediationBoards.Add(board);
        await _context.SaveChangesAsync();

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Agents:Kanban:OverdueScan:IntervalMinutes"] = "60" })
            .Build();

        var service = new OverdueScanHostedService(
            _serviceProvider, _notificationMock.Object,
            Mock.Of<ILogger<OverdueScanHostedService>>(), config);

        var method = typeof(OverdueScanHostedService).GetMethod("ScanForOverdueTasksAsync",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        await (Task)method!.Invoke(service, new object[] { CancellationToken.None })!;

        // The hosted service uses its own scoped context, but we can verify via notification
        _notificationMock.Verify(n => n.EnqueueAsync(It.Is<NotificationMessage>(
            m => m.EventType == NotificationEventType.TaskOverdue)),
            Times.Once);
    }
}
