using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ato.Copilot.Core.Migrations
{
    /// <inheritdoc />
    public partial class Feature022_SspOscal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PoamItemId",
                table: "RemediationTasks",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DisposalDate",
                table: "RegisteredSystems",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DitprId",
                table: "RegisteredSystems",
                type: "TEXT",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EmassId",
                table: "RegisteredSystems",
                type: "TEXT",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "HasNoExternalInterconnections",
                table: "RegisteredSystems",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "OperationalDate",
                table: "RegisteredSystems",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OperationalStatus",
                table: "RegisteredSystems",
                type: "TEXT",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CatSeverity",
                table: "Findings",
                type: "TEXT",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ImportRecordId",
                table: "Findings",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CollectionMethod",
                table: "Evidence",
                type: "TEXT",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CollectorIdentity",
                table: "Evidence",
                type: "TEXT",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "IntegrityVerifiedAt",
                table: "Evidence",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IntegrityHash",
                table: "ComplianceSnapshots",
                type: "TEXT",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "IsImmutable",
                table: "ComplianceSnapshots",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "RegisteredSystemId",
                table: "ComplianceAlerts",
                type: "TEXT",
                maxLength: 36,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RegisteredSystemId",
                table: "Assessments",
                type: "TEXT",
                maxLength: 36,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AssessmentRecords",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", maxLength: 36, nullable: false),
                    RegisteredSystemId = table.Column<string>(type: "TEXT", maxLength: 36, nullable: false),
                    ComplianceAssessmentId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    ControlsAssessed = table.Column<int>(type: "INTEGER", nullable: false),
                    ControlsSatisfied = table.Column<int>(type: "INTEGER", nullable: false),
                    ControlsOtherThanSatisfied = table.Column<int>(type: "INTEGER", nullable: false),
                    ControlsNotApplicable = table.Column<int>(type: "INTEGER", nullable: false),
                    ComplianceScore = table.Column<double>(type: "REAL", nullable: false),
                    OverallDetermination = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    AssessorId = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    AssessorName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    AssessedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssessmentRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AssessmentRecords_Assessments_ComplianceAssessmentId",
                        column: x => x.ComplianceAssessmentId,
                        principalTable: "Assessments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AssessmentRecords_RegisteredSystems_RegisteredSystemId",
                        column: x => x.RegisteredSystemId,
                        principalTable: "RegisteredSystems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AuthorizationDecisions",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", maxLength: 36, nullable: false),
                    RegisteredSystemId = table.Column<string>(type: "TEXT", maxLength: 36, nullable: false),
                    DecisionType = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    DecisionDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ExpirationDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    TermsAndConditions = table.Column<string>(type: "TEXT", maxLength: 8000, nullable: true),
                    ResidualRiskLevel = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    ResidualRiskJustification = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: true),
                    ComplianceScoreAtDecision = table.Column<double>(type: "REAL", nullable: false),
                    FindingsAtDecision = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    IssuedBy = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    IssuedByName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    SupersededById = table.Column<string>(type: "TEXT", maxLength: 36, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuthorizationDecisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AuthorizationDecisions_AuthorizationDecisions_SupersededById",
                        column: x => x.SupersededById,
                        principalTable: "AuthorizationDecisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AuthorizationDecisions_RegisteredSystems_RegisteredSystemId",
                        column: x => x.RegisteredSystemId,
                        principalTable: "RegisteredSystems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ConMonPlans",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", maxLength: 36, nullable: false),
                    RegisteredSystemId = table.Column<string>(type: "TEXT", maxLength: 36, nullable: false),
                    AssessmentFrequency = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    AnnualReviewDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ReportDistribution = table.Column<string>(type: "TEXT", nullable: false),
                    SignificantChangeTriggers = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConMonPlans", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ConMonPlans_RegisteredSystems_RegisteredSystemId",
                        column: x => x.RegisteredSystemId,
                        principalTable: "RegisteredSystems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ContingencyPlanReferences",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", maxLength: 36, nullable: false),
                    RegisteredSystemId = table.Column<string>(type: "TEXT", maxLength: 36, nullable: false),
                    DocumentTitle = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    DocumentLocation = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    DocumentVersion = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    LastTestedDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    TestType = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    RecoveryTimeObjective = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    RecoveryPointObjective = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    AlternateProcessingSite = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    BackupProceduresSummary = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: true),
                    CreatedBy = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContingencyPlanReferences", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ContingencyPlanReferences_RegisteredSystems_RegisteredSystemId",
                        column: x => x.RegisteredSystemId,
                        principalTable: "RegisteredSystems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ControlEffectivenessRecords",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", maxLength: 36, nullable: false),
                    AssessmentId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    RegisteredSystemId = table.Column<string>(type: "TEXT", maxLength: 36, nullable: false),
                    ControlId = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Determination = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    AssessmentMethod = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    EvidenceIds = table.Column<string>(type: "TEXT", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: true),
                    CatSeverity = table.Column<string>(type: "TEXT", maxLength: 10, nullable: true),
                    AssessorId = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    AssessedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ControlEffectivenessRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ControlEffectivenessRecords_Assessments_AssessmentId",
                        column: x => x.AssessmentId,
                        principalTable: "Assessments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ControlEffectivenessRecords_RegisteredSystems_RegisteredSystemId",
                        column: x => x.RegisteredSystemId,
                        principalTable: "RegisteredSystems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ControlImplementations",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", maxLength: 36, nullable: false),
                    RegisteredSystemId = table.Column<string>(type: "TEXT", maxLength: 36, nullable: false),
                    ControlId = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    ImplementationStatus = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    Narrative = table.Column<string>(type: "TEXT", maxLength: 8000, nullable: true),
                    IsAutoPopulated = table.Column<bool>(type: "INTEGER", nullable: false),
                    AiSuggested = table.Column<bool>(type: "INTEGER", nullable: false),
                    ReviewedBy = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    ReviewedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    AuthoredBy = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    AuthoredAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ControlImplementations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ControlImplementations_RegisteredSystems_RegisteredSystemId",
                        column: x => x.RegisteredSystemId,
                        principalTable: "RegisteredSystems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PoamItems",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", maxLength: 36, nullable: false),
                    RegisteredSystemId = table.Column<string>(type: "TEXT", maxLength: 36, nullable: false),
                    FindingId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    RemediationTaskId = table.Column<string>(type: "TEXT", maxLength: 36, nullable: true),
                    Weakness = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    WeaknessSource = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    SecurityControlNumber = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    CatSeverity = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    PointOfContact = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    PocEmail = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    ResourcesRequired = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    CostEstimate = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    ScheduledCompletionDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ActualCompletionDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Status = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Comments = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PoamItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PoamItems_Findings_FindingId",
                        column: x => x.FindingId,
                        principalTable: "Findings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PoamItems_RegisteredSystems_RegisteredSystemId",
                        column: x => x.RegisteredSystemId,
                        principalTable: "RegisteredSystems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PrivacyThresholdAnalyses",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", maxLength: 36, nullable: false),
                    RegisteredSystemId = table.Column<string>(type: "TEXT", maxLength: 36, nullable: false),
                    Determination = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    CollectsPii = table.Column<bool>(type: "INTEGER", nullable: false),
                    MaintainsPii = table.Column<bool>(type: "INTEGER", nullable: false),
                    DisseminatesPii = table.Column<bool>(type: "INTEGER", nullable: false),
                    PiiCategories = table.Column<string>(type: "TEXT", nullable: false),
                    AffectedIndividuals = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    EstimatedRecordCount = table.Column<int>(type: "INTEGER", nullable: true),
                    PiiSourceInfoTypes = table.Column<string>(type: "TEXT", nullable: false),
                    ExemptionRationale = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    Rationale = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: true),
                    AnalyzedBy = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    AnalyzedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PrivacyThresholdAnalyses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PrivacyThresholdAnalyses_RegisteredSystems_RegisteredSystemId",
                        column: x => x.RegisteredSystemId,
                        principalTable: "RegisteredSystems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ScanImportRecords",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    RegisteredSystemId = table.Column<string>(type: "TEXT", maxLength: 36, nullable: false),
                    AssessmentId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    ImportType = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    FileName = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    FileHash = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    FileSizeBytes = table.Column<long>(type: "INTEGER", nullable: false),
                    BenchmarkId = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    BenchmarkVersion = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    BenchmarkTitle = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    TargetHostName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    TargetIpAddress = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    ScanTimestamp = table.Column<DateTime>(type: "TEXT", nullable: true),
                    TotalEntries = table.Column<int>(type: "INTEGER", nullable: false),
                    OpenCount = table.Column<int>(type: "INTEGER", nullable: false),
                    PassCount = table.Column<int>(type: "INTEGER", nullable: false),
                    NotApplicableCount = table.Column<int>(type: "INTEGER", nullable: false),
                    NotReviewedCount = table.Column<int>(type: "INTEGER", nullable: false),
                    ErrorCount = table.Column<int>(type: "INTEGER", nullable: false),
                    SkippedCount = table.Column<int>(type: "INTEGER", nullable: false),
                    UnmatchedCount = table.Column<int>(type: "INTEGER", nullable: false),
                    FindingsCreated = table.Column<int>(type: "INTEGER", nullable: false),
                    FindingsUpdated = table.Column<int>(type: "INTEGER", nullable: false),
                    EffectivenessRecordsCreated = table.Column<int>(type: "INTEGER", nullable: false),
                    EffectivenessRecordsUpdated = table.Column<int>(type: "INTEGER", nullable: false),
                    NistControlsAffected = table.Column<int>(type: "INTEGER", nullable: false),
                    ConflictResolution = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    IsDryRun = table.Column<bool>(type: "INTEGER", nullable: false),
                    XccdfScore = table.Column<decimal>(type: "TEXT", nullable: true),
                    ImportStatus = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    ErrorMessage = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: true),
                    Warnings = table.Column<string>(type: "TEXT", nullable: false),
                    ImportedBy = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    ImportedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScanImportRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ScanImportRecords_Assessments_AssessmentId",
                        column: x => x.AssessmentId,
                        principalTable: "Assessments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ScanImportRecords_RegisteredSystems_RegisteredSystemId",
                        column: x => x.RegisteredSystemId,
                        principalTable: "RegisteredSystems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SecurityAssessmentPlans",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", maxLength: 36, nullable: false),
                    RegisteredSystemId = table.Column<string>(type: "TEXT", maxLength: 36, nullable: false),
                    AssessmentId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Status = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    BaselineLevel = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    ScopeNotes = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: true),
                    RulesOfEngagement = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: true),
                    ScheduleStart = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ScheduleEnd = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Content = table.Column<string>(type: "TEXT", nullable: false),
                    ContentHash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    TotalControls = table.Column<int>(type: "INTEGER", nullable: false),
                    CustomerControls = table.Column<int>(type: "INTEGER", nullable: false),
                    InheritedControls = table.Column<int>(type: "INTEGER", nullable: false),
                    SharedControls = table.Column<int>(type: "INTEGER", nullable: false),
                    StigBenchmarkCount = table.Column<int>(type: "INTEGER", nullable: false),
                    GeneratedBy = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    GeneratedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    FinalizedBy = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    FinalizedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Format = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false, defaultValue: "markdown")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SecurityAssessmentPlans", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SecurityAssessmentPlans_Assessments_AssessmentId",
                        column: x => x.AssessmentId,
                        principalTable: "Assessments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_SecurityAssessmentPlans_RegisteredSystems_RegisteredSystemId",
                        column: x => x.RegisteredSystemId,
                        principalTable: "RegisteredSystems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SspSections",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", maxLength: 36, nullable: false),
                    RegisteredSystemId = table.Column<string>(type: "TEXT", maxLength: 36, nullable: false),
                    SectionNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    SectionTitle = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Content = table.Column<string>(type: "TEXT", maxLength: 32000, nullable: true),
                    Status = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    IsAutoGenerated = table.Column<bool>(type: "INTEGER", nullable: false),
                    HasManualOverride = table.Column<bool>(type: "INTEGER", nullable: false),
                    AuthoredBy = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    AuthoredAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ReviewedBy = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    ReviewedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ReviewerComments = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: true),
                    Version = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SspSections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SspSections_RegisteredSystems_RegisteredSystemId",
                        column: x => x.RegisteredSystemId,
                        principalTable: "RegisteredSystems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SystemInterconnections",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", maxLength: 36, nullable: false),
                    RegisteredSystemId = table.Column<string>(type: "TEXT", maxLength: 36, nullable: false),
                    TargetSystemName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    TargetSystemOwner = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    TargetSystemAcronym = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    InterconnectionType = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    DataFlowDirection = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    DataClassification = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    DataDescription = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    ProtocolsUsed = table.Column<string>(type: "TEXT", nullable: false),
                    PortsUsed = table.Column<string>(type: "TEXT", nullable: false),
                    SecurityMeasures = table.Column<string>(type: "TEXT", nullable: false),
                    AuthenticationMethod = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    Status = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    StatusReason = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    AuthorizationToConnect = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SystemInterconnections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SystemInterconnections_RegisteredSystems_RegisteredSystemId",
                        column: x => x.RegisteredSystemId,
                        principalTable: "RegisteredSystems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RiskAcceptances",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", maxLength: 36, nullable: false),
                    AuthorizationDecisionId = table.Column<string>(type: "TEXT", maxLength: 36, nullable: false),
                    FindingId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    ControlId = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    CatSeverity = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    Justification = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: false),
                    CompensatingControl = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    ExpirationDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    AcceptedBy = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    AcceptedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    RevokedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    RevokedBy = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    RevocationReason = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RiskAcceptances", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RiskAcceptances_AuthorizationDecisions_AuthorizationDecisionId",
                        column: x => x.AuthorizationDecisionId,
                        principalTable: "AuthorizationDecisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RiskAcceptances_Findings_FindingId",
                        column: x => x.FindingId,
                        principalTable: "Findings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ConMonReports",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", maxLength: 36, nullable: false),
                    ConMonPlanId = table.Column<string>(type: "TEXT", maxLength: 36, nullable: false),
                    RegisteredSystemId = table.Column<string>(type: "TEXT", maxLength: 36, nullable: false),
                    ReportPeriod = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    ReportType = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    ComplianceScore = table.Column<double>(type: "REAL", nullable: false),
                    AuthorizedBaselineScore = table.Column<double>(type: "REAL", nullable: true),
                    NewFindings = table.Column<int>(type: "INTEGER", nullable: false),
                    ResolvedFindings = table.Column<int>(type: "INTEGER", nullable: false),
                    OpenPoamItems = table.Column<int>(type: "INTEGER", nullable: false),
                    OverduePoamItems = table.Column<int>(type: "INTEGER", nullable: false),
                    ReportContent = table.Column<string>(type: "TEXT", maxLength: 50000, nullable: false),
                    GeneratedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    GeneratedBy = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    MonitoringEnabled = table.Column<bool>(type: "INTEGER", nullable: true),
                    DriftAlertCount = table.Column<int>(type: "INTEGER", nullable: true),
                    AutoRemediationRuleCount = table.Column<int>(type: "INTEGER", nullable: true),
                    LastMonitoringCheck = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConMonReports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ConMonReports_ConMonPlans_ConMonPlanId",
                        column: x => x.ConMonPlanId,
                        principalTable: "ConMonPlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ConMonReports_RegisteredSystems_RegisteredSystemId",
                        column: x => x.RegisteredSystemId,
                        principalTable: "RegisteredSystems",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "SignificantChanges",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", maxLength: 36, nullable: false),
                    RegisteredSystemId = table.Column<string>(type: "TEXT", maxLength: 36, nullable: false),
                    ChangeType = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: false),
                    DetectedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    DetectedBy = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    RequiresReauthorization = table.Column<bool>(type: "INTEGER", nullable: false),
                    ReauthorizationTriggered = table.Column<bool>(type: "INTEGER", nullable: false),
                    ReviewedBy = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    ReviewedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Disposition = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    ConMonPlanId = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SignificantChanges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SignificantChanges_ConMonPlans_ConMonPlanId",
                        column: x => x.ConMonPlanId,
                        principalTable: "ConMonPlans",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_SignificantChanges_RegisteredSystems_RegisteredSystemId",
                        column: x => x.RegisteredSystemId,
                        principalTable: "RegisteredSystems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PoamMilestones",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", maxLength: 36, nullable: false),
                    PoamItemId = table.Column<string>(type: "TEXT", maxLength: 36, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    TargetDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CompletedDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Sequence = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PoamMilestones", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PoamMilestones_PoamItems_PoamItemId",
                        column: x => x.PoamItemId,
                        principalTable: "PoamItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PrivacyImpactAssessments",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", maxLength: 36, nullable: false),
                    RegisteredSystemId = table.Column<string>(type: "TEXT", maxLength: 36, nullable: false),
                    PtaId = table.Column<string>(type: "TEXT", maxLength: 36, nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Version = table.Column<int>(type: "INTEGER", nullable: false),
                    SystemDescription = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: true),
                    PurposeOfCollection = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: true),
                    IntendedUse = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: true),
                    SharingPartners = table.Column<string>(type: "TEXT", nullable: false),
                    NoticeAndConsent = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: true),
                    IndividualAccess = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: true),
                    Safeguards = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: true),
                    RetentionPeriod = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    DisposalMethod = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    SornRequired = table.Column<bool>(type: "INTEGER", nullable: true),
                    SornReference = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    NarrativeDocument = table.Column<string>(type: "TEXT", maxLength: 16000, nullable: true),
                    ReviewerComments = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: true),
                    ReviewDeficiencies = table.Column<string>(type: "TEXT", nullable: false),
                    ApprovedBy = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ExpirationDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedBy = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Sections = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PrivacyImpactAssessments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PrivacyImpactAssessments_PrivacyThresholdAnalyses_PtaId",
                        column: x => x.PtaId,
                        principalTable: "PrivacyThresholdAnalyses",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PrivacyImpactAssessments_RegisteredSystems_RegisteredSystemId",
                        column: x => x.RegisteredSystemId,
                        principalTable: "RegisteredSystems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ScanImportFindings",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    ScanImportRecordId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    VulnId = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    RuleId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    StigVersion = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    RawStatus = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    RawSeverity = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    MappedSeverity = table.Column<string>(type: "TEXT", maxLength: 10, nullable: true),
                    FindingDetails = table.Column<string>(type: "TEXT", maxLength: 8000, nullable: true),
                    Comments = table.Column<string>(type: "TEXT", maxLength: 8000, nullable: true),
                    SeverityOverride = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    SeverityJustification = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: true),
                    ResolvedStigControlId = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    ResolvedNistControlIds = table.Column<string>(type: "TEXT", nullable: false),
                    ResolvedCciRefs = table.Column<string>(type: "TEXT", nullable: false),
                    PrismaAlertId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    PrismaPolicyId = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    PrismaPolicyName = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    CloudResourceId = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    CloudResourceType = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    CloudRegion = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    CloudAccountId = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    ImportAction = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    ComplianceFindingId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScanImportFindings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ScanImportFindings_Findings_ComplianceFindingId",
                        column: x => x.ComplianceFindingId,
                        principalTable: "Findings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ScanImportFindings_ScanImportRecords_ScanImportRecordId",
                        column: x => x.ScanImportRecordId,
                        principalTable: "ScanImportRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SapControlEntries",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", maxLength: 36, nullable: false),
                    SecurityAssessmentPlanId = table.Column<string>(type: "TEXT", maxLength: 36, nullable: false),
                    ControlId = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    ControlTitle = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    ControlFamily = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    InheritanceType = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Provider = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    AssessmentMethods = table.Column<string>(type: "TEXT", nullable: false),
                    AssessmentObjectives = table.Column<string>(type: "TEXT", nullable: false),
                    EvidenceRequirements = table.Column<string>(type: "TEXT", nullable: false),
                    StigBenchmarks = table.Column<string>(type: "TEXT", nullable: false),
                    EvidenceExpected = table.Column<int>(type: "INTEGER", nullable: false),
                    EvidenceCollected = table.Column<int>(type: "INTEGER", nullable: false),
                    IsMethodOverridden = table.Column<bool>(type: "INTEGER", nullable: false),
                    OverrideRationale = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SapControlEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SapControlEntries_SecurityAssessmentPlans_SecurityAssessmentPlanId",
                        column: x => x.SecurityAssessmentPlanId,
                        principalTable: "SecurityAssessmentPlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SapTeamMembers",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", maxLength: 36, nullable: false),
                    SecurityAssessmentPlanId = table.Column<string>(type: "TEXT", maxLength: 36, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Organization = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Role = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    ContactInfo = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SapTeamMembers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SapTeamMembers_SecurityAssessmentPlans_SecurityAssessmentPlanId",
                        column: x => x.SecurityAssessmentPlanId,
                        principalTable: "SecurityAssessmentPlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "InterconnectionAgreements",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", maxLength: 36, nullable: false),
                    SystemInterconnectionId = table.Column<string>(type: "TEXT", maxLength: 36, nullable: false),
                    AgreementType = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    DocumentReference = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    Status = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    EffectiveDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ExpirationDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    SignedByLocal = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    SignedByLocalDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    SignedByRemote = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    SignedByRemoteDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ReviewNotes = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: true),
                    NarrativeDocument = table.Column<string>(type: "TEXT", maxLength: 16000, nullable: true),
                    CreatedBy = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InterconnectionAgreements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InterconnectionAgreements_SystemInterconnections_SystemInterconnectionId",
                        column: x => x.SystemInterconnectionId,
                        principalTable: "SystemInterconnections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RemediationTask_PoamItemId",
                table: "RemediationTasks",
                column: "PoamItemId");

            migrationBuilder.CreateIndex(
                name: "IX_ComplianceFinding_ImportRecordId",
                table: "Findings",
                column: "ImportRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_ComplianceAlert_RegisteredSystemId",
                table: "ComplianceAlerts",
                column: "RegisteredSystemId");

            migrationBuilder.CreateIndex(
                name: "IX_Assessments_RegisteredSystemId",
                table: "Assessments",
                column: "RegisteredSystemId");

            migrationBuilder.CreateIndex(
                name: "IX_AssessmentRecord_AssessedAt",
                table: "AssessmentRecords",
                column: "AssessedAt");

            migrationBuilder.CreateIndex(
                name: "IX_AssessmentRecord_System_Assessment",
                table: "AssessmentRecords",
                columns: new[] { "RegisteredSystemId", "ComplianceAssessmentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AssessmentRecord_SystemId",
                table: "AssessmentRecords",
                column: "RegisteredSystemId");

            migrationBuilder.CreateIndex(
                name: "IX_AssessmentRecords_ComplianceAssessmentId",
                table: "AssessmentRecords",
                column: "ComplianceAssessmentId");

            migrationBuilder.CreateIndex(
                name: "IX_AuthorizationDecision_DecisionDate",
                table: "AuthorizationDecisions",
                column: "DecisionDate");

            migrationBuilder.CreateIndex(
                name: "IX_AuthorizationDecision_IsActive",
                table: "AuthorizationDecisions",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_AuthorizationDecision_SystemId",
                table: "AuthorizationDecisions",
                column: "RegisteredSystemId");

            migrationBuilder.CreateIndex(
                name: "IX_AuthorizationDecisions_SupersededById",
                table: "AuthorizationDecisions",
                column: "SupersededById");

            migrationBuilder.CreateIndex(
                name: "IX_ConMonPlan_RegisteredSystemId",
                table: "ConMonPlans",
                column: "RegisteredSystemId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ConMonReport_ConMonPlanId",
                table: "ConMonReports",
                column: "ConMonPlanId");

            migrationBuilder.CreateIndex(
                name: "IX_ConMonReport_RegisteredSystemId",
                table: "ConMonReports",
                column: "RegisteredSystemId");

            migrationBuilder.CreateIndex(
                name: "IX_ContingencyPlan_SystemId",
                table: "ContingencyPlanReferences",
                column: "RegisteredSystemId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ControlEffectiveness_Assessment_Control",
                table: "ControlEffectivenessRecords",
                columns: new[] { "AssessmentId", "ControlId" });

            migrationBuilder.CreateIndex(
                name: "IX_ControlEffectiveness_AssessmentId",
                table: "ControlEffectivenessRecords",
                column: "AssessmentId");

            migrationBuilder.CreateIndex(
                name: "IX_ControlEffectiveness_Determination",
                table: "ControlEffectivenessRecords",
                column: "Determination");

            migrationBuilder.CreateIndex(
                name: "IX_ControlEffectiveness_SystemId",
                table: "ControlEffectivenessRecords",
                column: "RegisteredSystemId");

            migrationBuilder.CreateIndex(
                name: "IX_ControlImplementation_Status",
                table: "ControlImplementations",
                column: "ImplementationStatus");

            migrationBuilder.CreateIndex(
                name: "IX_ControlImplementation_System_Control",
                table: "ControlImplementations",
                columns: new[] { "RegisteredSystemId", "ControlId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ControlImplementation_SystemId",
                table: "ControlImplementations",
                column: "RegisteredSystemId");

            migrationBuilder.CreateIndex(
                name: "IX_Agreement_Expiration",
                table: "InterconnectionAgreements",
                column: "ExpirationDate");

            migrationBuilder.CreateIndex(
                name: "IX_Agreement_Status",
                table: "InterconnectionAgreements",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_InterconnectionAgreements_SystemInterconnectionId",
                table: "InterconnectionAgreements",
                column: "SystemInterconnectionId");

            migrationBuilder.CreateIndex(
                name: "IX_PoamItem_CatSeverity",
                table: "PoamItems",
                column: "CatSeverity");

            migrationBuilder.CreateIndex(
                name: "IX_PoamItem_ScheduledDate",
                table: "PoamItems",
                column: "ScheduledCompletionDate");

            migrationBuilder.CreateIndex(
                name: "IX_PoamItem_Status",
                table: "PoamItems",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_PoamItem_SystemId",
                table: "PoamItems",
                column: "RegisteredSystemId");

            migrationBuilder.CreateIndex(
                name: "IX_PoamItems_FindingId",
                table: "PoamItems",
                column: "FindingId");

            migrationBuilder.CreateIndex(
                name: "IX_PoamMilestone_PoamItemId",
                table: "PoamMilestones",
                column: "PoamItemId");

            migrationBuilder.CreateIndex(
                name: "IX_PIA_Status",
                table: "PrivacyImpactAssessments",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_PIA_SystemId",
                table: "PrivacyImpactAssessments",
                column: "RegisteredSystemId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PrivacyImpactAssessments_PtaId",
                table: "PrivacyImpactAssessments",
                column: "PtaId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PTA_SystemId",
                table: "PrivacyThresholdAnalyses",
                column: "RegisteredSystemId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RiskAcceptance_DecisionId",
                table: "RiskAcceptances",
                column: "AuthorizationDecisionId");

            migrationBuilder.CreateIndex(
                name: "IX_RiskAcceptance_ExpirationDate",
                table: "RiskAcceptances",
                column: "ExpirationDate");

            migrationBuilder.CreateIndex(
                name: "IX_RiskAcceptance_FindingId",
                table: "RiskAcceptances",
                column: "FindingId");

            migrationBuilder.CreateIndex(
                name: "IX_RiskAcceptance_IsActive",
                table: "RiskAcceptances",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_SapControlEntry_Plan_Control",
                table: "SapControlEntries",
                columns: new[] { "SecurityAssessmentPlanId", "ControlId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SapTeamMembers_SecurityAssessmentPlanId",
                table: "SapTeamMembers",
                column: "SecurityAssessmentPlanId");

            migrationBuilder.CreateIndex(
                name: "IX_ScanImportFinding_Import_VulnId",
                table: "ScanImportFindings",
                columns: new[] { "ScanImportRecordId", "VulnId" });

            migrationBuilder.CreateIndex(
                name: "IX_ScanImportFinding_ImportRecordId",
                table: "ScanImportFindings",
                column: "ScanImportRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_ScanImportFindings_ComplianceFindingId",
                table: "ScanImportFindings",
                column: "ComplianceFindingId");

            migrationBuilder.CreateIndex(
                name: "IX_ScanImportRecord_RegisteredSystemId",
                table: "ScanImportRecords",
                column: "RegisteredSystemId");

            migrationBuilder.CreateIndex(
                name: "IX_ScanImportRecord_System_Benchmark",
                table: "ScanImportRecords",
                columns: new[] { "RegisteredSystemId", "BenchmarkId" });

            migrationBuilder.CreateIndex(
                name: "IX_ScanImportRecord_System_FileHash",
                table: "ScanImportRecords",
                columns: new[] { "RegisteredSystemId", "FileHash" });

            migrationBuilder.CreateIndex(
                name: "IX_ScanImportRecords_AssessmentId",
                table: "ScanImportRecords",
                column: "AssessmentId");

            migrationBuilder.CreateIndex(
                name: "IX_SecurityAssessmentPlan_System_Status",
                table: "SecurityAssessmentPlans",
                columns: new[] { "RegisteredSystemId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_SecurityAssessmentPlans_AssessmentId",
                table: "SecurityAssessmentPlans",
                column: "AssessmentId");

            migrationBuilder.CreateIndex(
                name: "IX_SignificantChange_RegisteredSystemId",
                table: "SignificantChanges",
                column: "RegisteredSystemId");

            migrationBuilder.CreateIndex(
                name: "IX_SignificantChanges_ConMonPlanId",
                table: "SignificantChanges",
                column: "ConMonPlanId");

            migrationBuilder.CreateIndex(
                name: "IX_SspSection_Status",
                table: "SspSections",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_SspSection_System_Number",
                table: "SspSections",
                columns: new[] { "RegisteredSystemId", "SectionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Interconnection_System_Status",
                table: "SystemInterconnections",
                columns: new[] { "RegisteredSystemId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Interconnection_TargetSystem",
                table: "SystemInterconnections",
                column: "TargetSystemName");

            migrationBuilder.AddForeignKey(
                name: "FK_Assessments_RegisteredSystems_RegisteredSystemId",
                table: "Assessments",
                column: "RegisteredSystemId",
                principalTable: "RegisteredSystems",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ComplianceAlerts_RegisteredSystems_RegisteredSystemId",
                table: "ComplianceAlerts",
                column: "RegisteredSystemId",
                principalTable: "RegisteredSystems",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Findings_ScanImportRecords_ImportRecordId",
                table: "Findings",
                column: "ImportRecordId",
                principalTable: "ScanImportRecords",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Assessments_RegisteredSystems_RegisteredSystemId",
                table: "Assessments");

            migrationBuilder.DropForeignKey(
                name: "FK_ComplianceAlerts_RegisteredSystems_RegisteredSystemId",
                table: "ComplianceAlerts");

            migrationBuilder.DropForeignKey(
                name: "FK_Findings_ScanImportRecords_ImportRecordId",
                table: "Findings");

            migrationBuilder.DropTable(
                name: "AssessmentRecords");

            migrationBuilder.DropTable(
                name: "ConMonReports");

            migrationBuilder.DropTable(
                name: "ContingencyPlanReferences");

            migrationBuilder.DropTable(
                name: "ControlEffectivenessRecords");

            migrationBuilder.DropTable(
                name: "ControlImplementations");

            migrationBuilder.DropTable(
                name: "InterconnectionAgreements");

            migrationBuilder.DropTable(
                name: "PoamMilestones");

            migrationBuilder.DropTable(
                name: "PrivacyImpactAssessments");

            migrationBuilder.DropTable(
                name: "RiskAcceptances");

            migrationBuilder.DropTable(
                name: "SapControlEntries");

            migrationBuilder.DropTable(
                name: "SapTeamMembers");

            migrationBuilder.DropTable(
                name: "ScanImportFindings");

            migrationBuilder.DropTable(
                name: "SignificantChanges");

            migrationBuilder.DropTable(
                name: "SspSections");

            migrationBuilder.DropTable(
                name: "SystemInterconnections");

            migrationBuilder.DropTable(
                name: "PoamItems");

            migrationBuilder.DropTable(
                name: "PrivacyThresholdAnalyses");

            migrationBuilder.DropTable(
                name: "AuthorizationDecisions");

            migrationBuilder.DropTable(
                name: "SecurityAssessmentPlans");

            migrationBuilder.DropTable(
                name: "ScanImportRecords");

            migrationBuilder.DropTable(
                name: "ConMonPlans");

            migrationBuilder.DropIndex(
                name: "IX_RemediationTask_PoamItemId",
                table: "RemediationTasks");

            migrationBuilder.DropIndex(
                name: "IX_ComplianceFinding_ImportRecordId",
                table: "Findings");

            migrationBuilder.DropIndex(
                name: "IX_ComplianceAlert_RegisteredSystemId",
                table: "ComplianceAlerts");

            migrationBuilder.DropIndex(
                name: "IX_Assessments_RegisteredSystemId",
                table: "Assessments");

            migrationBuilder.DropColumn(
                name: "PoamItemId",
                table: "RemediationTasks");

            migrationBuilder.DropColumn(
                name: "DisposalDate",
                table: "RegisteredSystems");

            migrationBuilder.DropColumn(
                name: "DitprId",
                table: "RegisteredSystems");

            migrationBuilder.DropColumn(
                name: "EmassId",
                table: "RegisteredSystems");

            migrationBuilder.DropColumn(
                name: "HasNoExternalInterconnections",
                table: "RegisteredSystems");

            migrationBuilder.DropColumn(
                name: "OperationalDate",
                table: "RegisteredSystems");

            migrationBuilder.DropColumn(
                name: "OperationalStatus",
                table: "RegisteredSystems");

            migrationBuilder.DropColumn(
                name: "CatSeverity",
                table: "Findings");

            migrationBuilder.DropColumn(
                name: "ImportRecordId",
                table: "Findings");

            migrationBuilder.DropColumn(
                name: "CollectionMethod",
                table: "Evidence");

            migrationBuilder.DropColumn(
                name: "CollectorIdentity",
                table: "Evidence");

            migrationBuilder.DropColumn(
                name: "IntegrityVerifiedAt",
                table: "Evidence");

            migrationBuilder.DropColumn(
                name: "IntegrityHash",
                table: "ComplianceSnapshots");

            migrationBuilder.DropColumn(
                name: "IsImmutable",
                table: "ComplianceSnapshots");

            migrationBuilder.DropColumn(
                name: "RegisteredSystemId",
                table: "ComplianceAlerts");

            migrationBuilder.DropColumn(
                name: "RegisteredSystemId",
                table: "Assessments");
        }
    }
}
