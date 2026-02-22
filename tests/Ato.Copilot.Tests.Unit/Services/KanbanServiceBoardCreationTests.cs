using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using FluentAssertions;
using Ato.Copilot.Agents.Compliance.Services;
using Ato.Copilot.Core.Constants;
using Ato.Copilot.Core.Data.Context;
using Ato.Copilot.Core.Interfaces.Compliance;
using Ato.Copilot.Core.Interfaces.Kanban;
using Ato.Copilot.Core.Models.Compliance;
using Ato.Copilot.Core.Models.Kanban;
using Ato.Copilot.State.Abstractions;
using TaskStatus = Ato.Copilot.Core.Models.Kanban.TaskStatus;

namespace Ato.Copilot.Tests.Unit.Services;

/// <summary>
/// Unit tests for KanbanService board creation operations:
/// CreateBoardAsync, CreateBoardFromAssessmentAsync.
/// </summary>
public class KanbanServiceBoardCreationTests : IDisposable
{
    private readonly AtoCopilotContext _context;
    private readonly KanbanService _service;
    private readonly Mock<INotificationService> _notificationMock = new();
    private readonly Mock<IAgentStateManager> _stateMock = new();
    private readonly Mock<IAtoComplianceEngine> _complianceMock = new();
    private readonly Mock<IRemediationEngine> _remediationMock = new();
    private readonly Mock<ILogger<KanbanService>> _loggerMock = new();

    public KanbanServiceBoardCreationTests()
    {
        var options = new DbContextOptionsBuilder<AtoCopilotContext>()
            .UseInMemoryDatabase($"KanbanBoardCreation_{Guid.NewGuid()}")
            .Options;
        _context = new AtoCopilotContext(options);

        _service = new KanbanService(
            _context,
            _loggerMock.Object,
            _notificationMock.Object,
            _stateMock.Object,
            _complianceMock.Object,
            _remediationMock.Object);
    }

    public void Dispose()
    {
        _context.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task CreateBoardAsync_WithValidInput_CreatesBoard()
    {
        var board = await _service.CreateBoardAsync("Test Board", "sub-123", "owner@test.com");

        board.Should().NotBeNull();
        board.Name.Should().Be("Test Board");
        board.SubscriptionId.Should().Be("sub-123");
        board.Owner.Should().Be("owner@test.com");
        board.IsArchived.Should().BeFalse();
        board.NextTaskNumber.Should().Be(1);
        board.Tasks.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateBoardAsync_PersistsToDatabase()
    {
        var board = await _service.CreateBoardAsync("Persisted Board", "sub-456", "user@test.com");

        var retrieved = await _context.RemediationBoards.FindAsync(board.Id);
        retrieved.Should().NotBeNull();
        retrieved!.Name.Should().Be("Persisted Board");
    }

    [Fact]
    public async Task CreateBoardFromAssessmentAsync_WithFindings_CreatesTasksPerFinding()
    {
        // Arrange: seed an assessment with 3 findings
        var assessment = new ComplianceAssessment
        {
            SubscriptionId = "sub-test",
            Findings = new List<ComplianceFinding>
            {
                new()
                {
                    ControlId = "AC-2",
                    ControlFamily = "AC",
                    Title = "MFA not enabled",
                    Severity = FindingSeverity.Critical,
                    Status = FindingStatus.Open,
                    ResourceId = "/subscriptions/sub/resourceGroups/rg/providers/Microsoft.Compute/vms/vm1",
                },
                new()
                {
                    ControlId = "AC-3",
                    ControlFamily = "AC",
                    Title = "Access enforcement gap",
                    Severity = FindingSeverity.High,
                    Status = FindingStatus.Open,
                    ResourceId = "/subscriptions/sub/resourceGroups/rg/providers/Microsoft.Storage/storageAccounts/sa1",
                },
                new()
                {
                    ControlId = "AU-6",
                    ControlFamily = "AU",
                    Title = "Audit log review",
                    Severity = FindingSeverity.Medium,
                    Status = FindingStatus.InProgress,
                    ResourceId = "/subscriptions/sub/resourceGroups/rg/providers/Microsoft.Sql/servers/sql1",
                },
            }
        };

        _context.Assessments.Add(assessment);
        await _context.SaveChangesAsync();

        // Act
        var board = await _service.CreateBoardFromAssessmentAsync(
            assessment.Id, "Assessment Board", "sub-test", "co@test.com");

        // Assert
        board.Should().NotBeNull();
        board.Name.Should().Be("Assessment Board");
        board.AssessmentId.Should().Be(assessment.Id);
        board.Tasks.Should().HaveCount(3);
    }

    [Fact]
    public async Task CreateBoardFromAssessmentAsync_TasksInBacklogStatus()
    {
        var assessment = SeedAssessmentWithFinding(FindingSeverity.High, FindingStatus.Open);

        var board = await _service.CreateBoardFromAssessmentAsync(
            assessment.Id, "Board", "sub", "owner");

        board.Tasks.Should().AllSatisfy(t => t.Status.Should().Be(TaskStatus.Backlog));
    }

    [Fact]
    public async Task CreateBoardFromAssessmentAsync_SetsSequentialTaskNumbers()
    {
        var assessment = new ComplianceAssessment
        {
            SubscriptionId = "sub",
            Findings = new List<ComplianceFinding>
            {
                CreateFinding("AC-1", FindingSeverity.High),
                CreateFinding("AC-2", FindingSeverity.Medium),
                CreateFinding("AC-3", FindingSeverity.Low),
            }
        };
        _context.Assessments.Add(assessment);
        await _context.SaveChangesAsync();

        var board = await _service.CreateBoardFromAssessmentAsync(
            assessment.Id, "Seq Board", "sub", "owner");

        board.Tasks.Select(t => t.TaskNumber).Should().BeEquivalentTo(
            new[] { "REM-001", "REM-002", "REM-003" });
    }

    [Fact]
    public async Task CreateBoardFromAssessmentAsync_CriticalSeverity_DueDateWithin24Hours()
    {
        var assessment = SeedAssessmentWithFinding(FindingSeverity.Critical, FindingStatus.Open);
        var before = DateTime.UtcNow;

        var board = await _service.CreateBoardFromAssessmentAsync(
            assessment.Id, "Board", "sub", "owner");

        var task = board.Tasks.Single();
        task.DueDate.Should().BeCloseTo(before.AddHours(KanbanConstants.DefaultCriticalHours), TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task CreateBoardFromAssessmentAsync_HighSeverity_DueDateWithin7Days()
    {
        var assessment = SeedAssessmentWithFinding(FindingSeverity.High, FindingStatus.Open);
        var before = DateTime.UtcNow;

        var board = await _service.CreateBoardFromAssessmentAsync(
            assessment.Id, "Board", "sub", "owner");

        var task = board.Tasks.Single();
        task.DueDate.Should().BeCloseTo(before.AddDays(KanbanConstants.DefaultHighDays), TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task CreateBoardFromAssessmentAsync_MediumSeverity_DueDateWithin30Days()
    {
        var assessment = SeedAssessmentWithFinding(FindingSeverity.Medium, FindingStatus.Open);
        var before = DateTime.UtcNow;

        var board = await _service.CreateBoardFromAssessmentAsync(
            assessment.Id, "Board", "sub", "owner");

        var task = board.Tasks.Single();
        task.DueDate.Should().BeCloseTo(before.AddDays(KanbanConstants.DefaultMediumDays), TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task CreateBoardFromAssessmentAsync_LowSeverity_DueDateWithin90Days()
    {
        var assessment = SeedAssessmentWithFinding(FindingSeverity.Low, FindingStatus.Open);
        var before = DateTime.UtcNow;

        var board = await _service.CreateBoardFromAssessmentAsync(
            assessment.Id, "Board", "sub", "owner");

        var task = board.Tasks.Single();
        task.DueDate.Should().BeCloseTo(before.AddDays(KanbanConstants.DefaultLowDays), TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task CreateBoardFromAssessmentAsync_ZeroFindings_ReturnsEmptyBoard()
    {
        var assessment = new ComplianceAssessment
        {
            SubscriptionId = "sub",
            Findings = new List<ComplianceFinding>
            {
                // Only remediated findings — should be excluded
                new()
                {
                    ControlId = "AC-1",
                    ControlFamily = "AC",
                    Title = "Already fixed",
                    Status = FindingStatus.Remediated,
                    Severity = FindingSeverity.High,
                }
            }
        };
        _context.Assessments.Add(assessment);
        await _context.SaveChangesAsync();

        var board = await _service.CreateBoardFromAssessmentAsync(
            assessment.Id, "Empty Board", "sub", "owner");

        board.Tasks.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateBoardFromAssessmentAsync_AssessmentNotFound_Throws()
    {
        var act = () => _service.CreateBoardFromAssessmentAsync(
            "nonexistent", "Board", "sub", "owner");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not found*");
    }

    [Fact]
    public async Task CreateBoardFromAssessmentAsync_MapsControlIdAndFamily()
    {
        var assessment = SeedAssessmentWithFinding(FindingSeverity.High, FindingStatus.Open, "SC-7");

        var board = await _service.CreateBoardFromAssessmentAsync(
            assessment.Id, "Board", "sub", "owner");

        var task = board.Tasks.Single();
        task.ControlId.Should().Be("SC-7");
        task.ControlFamily.Should().Be("SC");
    }

    [Fact]
    public async Task CreateBoardFromAssessmentAsync_MapsAffectedResources()
    {
        var resourceId = "/subscriptions/sub/resourceGroups/rg/providers/Microsoft.Compute/vms/vm1";
        var assessment = new ComplianceAssessment
        {
            SubscriptionId = "sub",
            Findings = new List<ComplianceFinding>
            {
                new()
                {
                    ControlId = "AC-2",
                    ControlFamily = "AC",
                    Title = "Finding",
                    Status = FindingStatus.Open,
                    Severity = FindingSeverity.High,
                    ResourceId = resourceId,
                }
            }
        };
        _context.Assessments.Add(assessment);
        await _context.SaveChangesAsync();

        var board = await _service.CreateBoardFromAssessmentAsync(
            assessment.Id, "Board", "sub", "owner");

        board.Tasks.Single().AffectedResources.Should().Contain(resourceId);
    }

    [Fact]
    public async Task CreateBoardFromAssessmentAsync_MapsRemediationScript()
    {
        var assessment = new ComplianceAssessment
        {
            SubscriptionId = "sub",
            Findings = new List<ComplianceFinding>
            {
                new()
                {
                    ControlId = "AC-2",
                    ControlFamily = "AC",
                    Title = "Finding",
                    Status = FindingStatus.Open,
                    Severity = FindingSeverity.High,
                    RemediationScript = "Set-AzPolicy -Enable $true",
                }
            }
        };
        _context.Assessments.Add(assessment);
        await _context.SaveChangesAsync();

        var board = await _service.CreateBoardFromAssessmentAsync(
            assessment.Id, "Board", "sub", "owner");

        board.Tasks.Single().RemediationScript.Should().Be("Set-AzPolicy -Enable $true");
    }

    [Fact]
    public async Task CreateBoardFromAssessmentAsync_ExcludesRemediatedFindings()
    {
        var assessment = new ComplianceAssessment
        {
            SubscriptionId = "sub",
            Findings = new List<ComplianceFinding>
            {
                CreateFinding("AC-1", FindingSeverity.High, FindingStatus.Remediated),
                CreateFinding("AC-2", FindingSeverity.High, FindingStatus.Open),
                CreateFinding("AC-3", FindingSeverity.High, FindingStatus.FalsePositive),
            }
        };
        _context.Assessments.Add(assessment);
        await _context.SaveChangesAsync();

        var board = await _service.CreateBoardFromAssessmentAsync(
            assessment.Id, "Board", "sub", "owner");

        board.Tasks.Should().HaveCount(1);
        board.Tasks.Single().ControlId.Should().Be("AC-2");
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private ComplianceAssessment SeedAssessmentWithFinding(
        FindingSeverity severity, FindingStatus status, string controlId = "AC-2")
    {
        var assessment = new ComplianceAssessment
        {
            SubscriptionId = "sub",
            Findings = new List<ComplianceFinding>
            {
                CreateFinding(controlId, severity, status),
            }
        };
        _context.Assessments.Add(assessment);
        _context.SaveChanges();
        return assessment;
    }

    private static ComplianceFinding CreateFinding(
        string controlId, FindingSeverity severity, FindingStatus status = FindingStatus.Open)
    {
        return new ComplianceFinding
        {
            ControlId = controlId,
            ControlFamily = controlId.Length >= 2 ? controlId[..2] : controlId,
            Title = $"Finding for {controlId}",
            Severity = severity,
            Status = status,
            ResourceId = $"/subscriptions/sub/providers/test/{controlId.ToLower()}",
        };
    }
}
