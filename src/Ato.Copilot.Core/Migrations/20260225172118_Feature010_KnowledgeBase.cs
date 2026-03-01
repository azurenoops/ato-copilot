using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ato.Copilot.Core.Migrations
{
    /// <inheritdoc />
    public partial class Feature010_KnowledgeBase : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ControlDescription",
                table: "Findings",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ControlTitle",
                table: "Findings",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "RemediatedAt",
                table: "Findings",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RemediatedBy",
                table: "Findings",
                type: "TEXT",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RemediationTrackingStatus",
                table: "Findings",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "StigFinding",
                table: "Findings",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "StigId",
                table: "Findings",
                type: "TEXT",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SubscriptionId",
                table: "Findings",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<TimeSpan>(
                name: "AssessmentDuration",
                table: "Assessments",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ControlFamilyResults",
                table: "Assessments",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "EnvironmentName",
                table: "Assessments",
                type: "TEXT",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExecutiveSummary",
                table: "Assessments",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ResourceGroupFilter",
                table: "Assessments",
                type: "TEXT",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RiskProfile",
                table: "Assessments",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ScanPillarResults",
                table: "Assessments",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SubscriptionIds",
                table: "Assessments",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "AlertIdCounters",
                columns: table => new
                {
                    Date = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    LastSequence = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AlertIdCounters", x => x.Date);
                });

            migrationBuilder.CreateTable(
                name: "AlertRules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    SubscriptionId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    ResourceGroupName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    ResourceType = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    ResourceId = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    ControlFamily = table.Column<string>(type: "TEXT", maxLength: 10, nullable: true),
                    ControlId = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    TriggerCondition = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: true),
                    SeverityOverride = table.Column<int>(type: "INTEGER", nullable: true),
                    RecipientOverrides = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: false),
                    IsDefault = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AlertRules", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AutoRemediationRules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    SubscriptionId = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    ResourceGroupName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    ControlFamily = table.Column<string>(type: "TEXT", maxLength: 10, nullable: true),
                    ControlId = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    Action = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    ApprovalMode = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    ExecutionCount = table.Column<int>(type: "INTEGER", nullable: false),
                    LastExecutedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    CreatedBy = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AutoRemediationRules", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CacSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Email = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    TokenHash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    SessionStart = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    ClientType = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    IpAddress = table.Column<string>(type: "TEXT", maxLength: 45, nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CacSessions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CertificateRoleMappings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CertificateThumbprint = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    CertificateSubject = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    MappedRole = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CertificateRoleMappings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ComplianceAlerts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    AlertId = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Type = table.Column<int>(type: "INTEGER", nullable: false),
                    Severity = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: false),
                    SubscriptionId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    AffectedResources = table.Column<string>(type: "TEXT", nullable: false),
                    ControlId = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    ControlFamily = table.Column<string>(type: "TEXT", maxLength: 5, nullable: true),
                    ChangeDetails = table.Column<string>(type: "TEXT", nullable: true),
                    ActorId = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    RecommendedAction = table.Column<string>(type: "TEXT", nullable: true),
                    AssignedTo = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    DismissalJustification = table.Column<string>(type: "TEXT", nullable: true),
                    DismissedBy = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    GroupedAlertId = table.Column<Guid>(type: "TEXT", nullable: true),
                    IsGrouped = table.Column<bool>(type: "INTEGER", nullable: false),
                    ChildAlertCount = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    AcknowledgedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    AcknowledgedBy = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    ResolvedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    EscalatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    SlaDeadline = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ComplianceAlerts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ComplianceAlerts_ComplianceAlerts_GroupedAlertId",
                        column: x => x.GroupedAlertId,
                        principalTable: "ComplianceAlerts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ComplianceBaselines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    SubscriptionId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    ResourceId = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    ResourceType = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    ConfigurationHash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    ConfigurationSnapshot = table.Column<string>(type: "TEXT", nullable: false),
                    PolicyComplianceState = table.Column<string>(type: "TEXT", nullable: true),
                    AssessmentId = table.Column<Guid>(type: "TEXT", nullable: true),
                    CapturedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ComplianceBaselines", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ComplianceSnapshots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    SubscriptionId = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    ComplianceScore = table.Column<double>(type: "REAL", nullable: false),
                    TotalControls = table.Column<int>(type: "INTEGER", nullable: false),
                    PassedControls = table.Column<int>(type: "INTEGER", nullable: false),
                    FailedControls = table.Column<int>(type: "INTEGER", nullable: false),
                    TotalResources = table.Column<int>(type: "INTEGER", nullable: false),
                    CompliantResources = table.Column<int>(type: "INTEGER", nullable: false),
                    NonCompliantResources = table.Column<int>(type: "INTEGER", nullable: false),
                    ActiveAlertCount = table.Column<int>(type: "INTEGER", nullable: false),
                    CriticalAlertCount = table.Column<int>(type: "INTEGER", nullable: false),
                    HighAlertCount = table.Column<int>(type: "INTEGER", nullable: false),
                    ControlFamilyBreakdown = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: true),
                    CapturedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    IsWeeklySnapshot = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ComplianceSnapshots", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EscalationPaths",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    TriggerSeverity = table.Column<int>(type: "INTEGER", nullable: false),
                    EscalationDelayMinutes = table.Column<int>(type: "INTEGER", nullable: false),
                    Recipients = table.Column<string>(type: "TEXT", nullable: false),
                    Channel = table.Column<int>(type: "INTEGER", nullable: false),
                    RepeatIntervalMinutes = table.Column<int>(type: "INTEGER", nullable: false),
                    MaxEscalations = table.Column<int>(type: "INTEGER", nullable: false),
                    WebhookUrl = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EscalationPaths", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MonitoringConfigurations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    SubscriptionId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    ResourceGroupName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    Mode = table.Column<int>(type: "INTEGER", nullable: false),
                    Frequency = table.Column<int>(type: "INTEGER", nullable: false),
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    NextRunAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    LastRunAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    LastEventCheckAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    CreatedBy = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MonitoringConfigurations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RemediationBoards",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    SubscriptionId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    AssessmentId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Owner = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    IsArchived = table.Column<bool>(type: "INTEGER", nullable: false),
                    NextTaskNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    RowVersion = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RemediationBoards", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RemediationBoards_Assessments_AssessmentId",
                        column: x => x.AssessmentId,
                        principalTable: "Assessments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SuppressionRules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    SubscriptionId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    ResourceGroupName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    ResourceId = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    ControlFamily = table.Column<string>(type: "TEXT", maxLength: 10, nullable: true),
                    ControlId = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    Type = table.Column<int>(type: "INTEGER", nullable: false),
                    Justification = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    QuietHoursStart = table.Column<TimeOnly>(type: "TEXT", nullable: true),
                    QuietHoursEnd = table.Column<TimeOnly>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SuppressionRules", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "JitRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    RequestType = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    PimRequestId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    UserId = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    UserDisplayName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    ConversationId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    SessionId = table.Column<Guid>(type: "TEXT", nullable: true),
                    RoleName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Scope = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    ScopeDisplayName = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    Justification = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    TicketNumber = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    TicketSystem = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    Status = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    DurationHours = table.Column<int>(type: "INTEGER", nullable: false),
                    RequestedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    ActivatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    DeactivatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    ActualDuration = table.Column<TimeSpan>(type: "TEXT", nullable: true),
                    ApproverId = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    ApproverDisplayName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    ApproverComments = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    ApprovalDecisionAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    VmName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    ResourceGroup = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    SubscriptionId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Port = table.Column<int>(type: "INTEGER", nullable: true),
                    Protocol = table.Column<string>(type: "TEXT", maxLength: 10, nullable: true),
                    SourceIp = table.Column<string>(type: "TEXT", maxLength: 45, nullable: true),
                    RowVersion = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JitRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_JitRequests_CacSessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "CacSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AlertNotifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    AlertId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Channel = table.Column<int>(type: "INTEGER", nullable: false),
                    Recipient = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    Subject = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    Body = table.Column<string>(type: "TEXT", nullable: true),
                    IsDelivered = table.Column<bool>(type: "INTEGER", nullable: false),
                    DeliveryError = table.Column<string>(type: "TEXT", nullable: true),
                    SentAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    DeliveredAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AlertNotifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AlertNotifications_ComplianceAlerts_AlertId",
                        column: x => x.AlertId,
                        principalTable: "ComplianceAlerts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RemediationTasks",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    TaskNumber = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    BoardId = table.Column<string>(type: "TEXT", nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: false),
                    ControlId = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    ControlFamily = table.Column<string>(type: "TEXT", maxLength: 5, nullable: false),
                    Severity = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    AssigneeId = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    AssigneeName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    DueDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    AffectedResources = table.Column<string>(type: "TEXT", nullable: false),
                    RemediationScript = table.Column<string>(type: "TEXT", maxLength: 8000, nullable: true),
                    ValidationCriteria = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    FindingId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    LinkedAlertId = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedBy = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    LastOverdueNotifiedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    RowVersion = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RemediationTasks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RemediationTasks_RemediationBoards_BoardId",
                        column: x => x.BoardId,
                        principalTable: "RemediationBoards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TaskComments",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    TaskId = table.Column<string>(type: "TEXT", nullable: false),
                    AuthorId = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    AuthorName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Content = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    EditedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    IsEdited = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsSystemComment = table.Column<bool>(type: "INTEGER", nullable: false),
                    ParentCommentId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Mentions = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaskComments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TaskComments_RemediationTasks_TaskId",
                        column: x => x.TaskId,
                        principalTable: "RemediationTasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TaskHistoryEntries",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    TaskId = table.Column<string>(type: "TEXT", nullable: false),
                    EventType = table.Column<int>(type: "INTEGER", nullable: false),
                    OldValue = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    NewValue = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    ActingUserId = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    ActingUserName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Timestamp = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Details = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaskHistoryEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TaskHistoryEntries_RemediationTasks_TaskId",
                        column: x => x.TaskId,
                        principalTable: "RemediationTasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AlertNotification_Alert_Channel",
                table: "AlertNotifications",
                columns: new[] { "AlertId", "Channel" });

            migrationBuilder.CreateIndex(
                name: "IX_AlertNotification_SentAt",
                table: "AlertNotifications",
                column: "SentAt");

            migrationBuilder.CreateIndex(
                name: "IX_AlertRule_IsDefault",
                table: "AlertRules",
                column: "IsDefault");

            migrationBuilder.CreateIndex(
                name: "IX_AlertRule_Sub_Family_Enabled",
                table: "AlertRules",
                columns: new[] { "SubscriptionId", "ControlFamily", "IsEnabled" });

            migrationBuilder.CreateIndex(
                name: "IX_AutoRemediationRule_Enabled",
                table: "AutoRemediationRules",
                column: "IsEnabled");

            migrationBuilder.CreateIndex(
                name: "IX_AutoRemediationRule_Sub_Family_Enabled",
                table: "AutoRemediationRules",
                columns: new[] { "SubscriptionId", "ControlFamily", "IsEnabled" });

            migrationBuilder.CreateIndex(
                name: "IX_CacSession_ExpiresAt",
                table: "CacSessions",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_CacSession_UserId_Status",
                table: "CacSessions",
                columns: new[] { "UserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_CertMapping_Subject",
                table: "CertificateRoleMappings",
                column: "CertificateSubject",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CertMapping_Thumbprint",
                table: "CertificateRoleMappings",
                column: "CertificateThumbprint",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ComplianceAlert_AlertId",
                table: "ComplianceAlerts",
                column: "AlertId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ComplianceAlert_Assignee_Status",
                table: "ComplianceAlerts",
                columns: new[] { "AssignedTo", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ComplianceAlert_ControlFamily",
                table: "ComplianceAlerts",
                column: "ControlFamily");

            migrationBuilder.CreateIndex(
                name: "IX_ComplianceAlert_GroupedAlertId",
                table: "ComplianceAlerts",
                column: "GroupedAlertId");

            migrationBuilder.CreateIndex(
                name: "IX_ComplianceAlert_Sla_Status",
                table: "ComplianceAlerts",
                columns: new[] { "SlaDeadline", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ComplianceAlert_Status_Severity",
                table: "ComplianceAlerts",
                columns: new[] { "Status", "Severity" });

            migrationBuilder.CreateIndex(
                name: "IX_ComplianceAlert_Sub_Created",
                table: "ComplianceAlerts",
                columns: new[] { "SubscriptionId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ComplianceBaseline_Resource_Active",
                table: "ComplianceBaselines",
                columns: new[] { "ResourceId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_ComplianceBaseline_Sub_Captured",
                table: "ComplianceBaselines",
                columns: new[] { "SubscriptionId", "CapturedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ComplianceSnapshot_Sub_CapturedAt",
                table: "ComplianceSnapshots",
                columns: new[] { "SubscriptionId", "CapturedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ComplianceSnapshot_Weekly_CapturedAt",
                table: "ComplianceSnapshots",
                columns: new[] { "IsWeeklySnapshot", "CapturedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_EscalationPath_Severity_Enabled",
                table: "EscalationPaths",
                columns: new[] { "TriggerSeverity", "IsEnabled" });

            migrationBuilder.CreateIndex(
                name: "IX_JitRequest_RequestedAt",
                table: "JitRequests",
                column: "RequestedAt");

            migrationBuilder.CreateIndex(
                name: "IX_JitRequest_RoleName_Scope",
                table: "JitRequests",
                columns: new[] { "RoleName", "Scope" });

            migrationBuilder.CreateIndex(
                name: "IX_JitRequest_SessionId",
                table: "JitRequests",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "IX_JitRequest_Status_ExpiresAt",
                table: "JitRequests",
                columns: new[] { "Status", "ExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "IX_JitRequest_UserId_Status",
                table: "JitRequests",
                columns: new[] { "UserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_MonitoringConfig_NextRun_Enabled",
                table: "MonitoringConfigurations",
                columns: new[] { "NextRunAt", "IsEnabled" });

            migrationBuilder.CreateIndex(
                name: "IX_MonitoringConfig_Sub_RG",
                table: "MonitoringConfigurations",
                columns: new[] { "SubscriptionId", "ResourceGroupName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RemediationBoards_AssessmentId",
                table: "RemediationBoards",
                column: "AssessmentId");

            migrationBuilder.CreateIndex(
                name: "IX_RemediationBoards_SubscriptionId",
                table: "RemediationBoards",
                column: "SubscriptionId");

            migrationBuilder.CreateIndex(
                name: "IX_RemediationBoards_SubscriptionId_IsArchived",
                table: "RemediationBoards",
                columns: new[] { "SubscriptionId", "IsArchived" });

            migrationBuilder.CreateIndex(
                name: "IX_RemediationTasks_AssigneeId",
                table: "RemediationTasks",
                column: "AssigneeId");

            migrationBuilder.CreateIndex(
                name: "IX_RemediationTasks_BoardId",
                table: "RemediationTasks",
                column: "BoardId");

            migrationBuilder.CreateIndex(
                name: "IX_RemediationTasks_BoardId_ControlFamily",
                table: "RemediationTasks",
                columns: new[] { "BoardId", "ControlFamily" });

            migrationBuilder.CreateIndex(
                name: "IX_RemediationTasks_BoardId_Status",
                table: "RemediationTasks",
                columns: new[] { "BoardId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_RemediationTasks_ControlId",
                table: "RemediationTasks",
                column: "ControlId");

            migrationBuilder.CreateIndex(
                name: "IX_RemediationTasks_DueDate",
                table: "RemediationTasks",
                column: "DueDate");

            migrationBuilder.CreateIndex(
                name: "IX_RemediationTasks_Status",
                table: "RemediationTasks",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_SuppressionRule_Active_Expires",
                table: "SuppressionRules",
                columns: new[] { "IsActive", "ExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SuppressionRule_Sub_Resource",
                table: "SuppressionRules",
                columns: new[] { "SubscriptionId", "ResourceId" });

            migrationBuilder.CreateIndex(
                name: "IX_TaskComments_TaskId",
                table: "TaskComments",
                column: "TaskId");

            migrationBuilder.CreateIndex(
                name: "IX_TaskComments_TaskId_CreatedAt",
                table: "TaskComments",
                columns: new[] { "TaskId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_TaskHistoryEntries_TaskId",
                table: "TaskHistoryEntries",
                column: "TaskId");

            migrationBuilder.CreateIndex(
                name: "IX_TaskHistoryEntries_TaskId_Timestamp",
                table: "TaskHistoryEntries",
                columns: new[] { "TaskId", "Timestamp" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AlertIdCounters");

            migrationBuilder.DropTable(
                name: "AlertNotifications");

            migrationBuilder.DropTable(
                name: "AlertRules");

            migrationBuilder.DropTable(
                name: "AutoRemediationRules");

            migrationBuilder.DropTable(
                name: "CertificateRoleMappings");

            migrationBuilder.DropTable(
                name: "ComplianceBaselines");

            migrationBuilder.DropTable(
                name: "ComplianceSnapshots");

            migrationBuilder.DropTable(
                name: "EscalationPaths");

            migrationBuilder.DropTable(
                name: "JitRequests");

            migrationBuilder.DropTable(
                name: "MonitoringConfigurations");

            migrationBuilder.DropTable(
                name: "SuppressionRules");

            migrationBuilder.DropTable(
                name: "TaskComments");

            migrationBuilder.DropTable(
                name: "TaskHistoryEntries");

            migrationBuilder.DropTable(
                name: "ComplianceAlerts");

            migrationBuilder.DropTable(
                name: "CacSessions");

            migrationBuilder.DropTable(
                name: "RemediationTasks");

            migrationBuilder.DropTable(
                name: "RemediationBoards");

            migrationBuilder.DropColumn(
                name: "ControlDescription",
                table: "Findings");

            migrationBuilder.DropColumn(
                name: "ControlTitle",
                table: "Findings");

            migrationBuilder.DropColumn(
                name: "RemediatedAt",
                table: "Findings");

            migrationBuilder.DropColumn(
                name: "RemediatedBy",
                table: "Findings");

            migrationBuilder.DropColumn(
                name: "RemediationTrackingStatus",
                table: "Findings");

            migrationBuilder.DropColumn(
                name: "StigFinding",
                table: "Findings");

            migrationBuilder.DropColumn(
                name: "StigId",
                table: "Findings");

            migrationBuilder.DropColumn(
                name: "SubscriptionId",
                table: "Findings");

            migrationBuilder.DropColumn(
                name: "AssessmentDuration",
                table: "Assessments");

            migrationBuilder.DropColumn(
                name: "ControlFamilyResults",
                table: "Assessments");

            migrationBuilder.DropColumn(
                name: "EnvironmentName",
                table: "Assessments");

            migrationBuilder.DropColumn(
                name: "ExecutiveSummary",
                table: "Assessments");

            migrationBuilder.DropColumn(
                name: "ResourceGroupFilter",
                table: "Assessments");

            migrationBuilder.DropColumn(
                name: "RiskProfile",
                table: "Assessments");

            migrationBuilder.DropColumn(
                name: "ScanPillarResults",
                table: "Assessments");

            migrationBuilder.DropColumn(
                name: "SubscriptionIds",
                table: "Assessments");
        }
    }
}
