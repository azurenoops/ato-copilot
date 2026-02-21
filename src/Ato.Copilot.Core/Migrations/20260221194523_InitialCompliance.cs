using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ato.Copilot.Core.Migrations
{
    /// <inheritdoc />
    public partial class InitialCompliance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Assessments",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    SubscriptionId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Framework = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Baseline = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    ScanType = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    InitiatedBy = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    AssessedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ProgressMessage = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    ComplianceScore = table.Column<double>(type: "REAL", nullable: false),
                    TotalControls = table.Column<int>(type: "INTEGER", nullable: false),
                    PassedControls = table.Column<int>(type: "INTEGER", nullable: false),
                    FailedControls = table.Column<int>(type: "INTEGER", nullable: false),
                    NotAssessedControls = table.Column<int>(type: "INTEGER", nullable: false),
                    ResourceScan_ResourcesScanned = table.Column<int>(type: "INTEGER", nullable: true),
                    ResourceScan_PoliciesEvaluated = table.Column<int>(type: "INTEGER", nullable: true),
                    ResourceScan_Compliant = table.Column<int>(type: "INTEGER", nullable: true),
                    ResourceScan_NonCompliant = table.Column<int>(type: "INTEGER", nullable: true),
                    ResourceScan_CompliancePercentage = table.Column<double>(type: "REAL", nullable: true),
                    PolicyScan_ResourcesScanned = table.Column<int>(type: "INTEGER", nullable: true),
                    PolicyScan_PoliciesEvaluated = table.Column<int>(type: "INTEGER", nullable: true),
                    PolicyScan_Compliant = table.Column<int>(type: "INTEGER", nullable: true),
                    PolicyScan_NonCompliant = table.Column<int>(type: "INTEGER", nullable: true),
                    PolicyScan_CompliancePercentage = table.Column<double>(type: "REAL", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Assessments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AuditLogs",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    UserId = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    UserRole = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Action = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    ScanType = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    Timestamp = table.Column<DateTime>(type: "TEXT", nullable: false),
                    SubscriptionId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    AffectedResources = table.Column<string>(type: "TEXT", nullable: false),
                    AffectedControls = table.Column<string>(type: "TEXT", nullable: false),
                    Outcome = table.Column<int>(type: "INTEGER", nullable: false),
                    Details = table.Column<string>(type: "TEXT", nullable: false),
                    Duration = table.Column<TimeSpan>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Documents",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    DocumentType = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    SystemName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Framework = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Content = table.Column<string>(type: "TEXT", nullable: false),
                    GeneratedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    AssessmentId = table.Column<string>(type: "TEXT", nullable: true),
                    Owner = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    GeneratedBy = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Metadata_SystemDescription = table.Column<string>(type: "TEXT", nullable: false),
                    Metadata_AuthorizationBoundary = table.Column<string>(type: "TEXT", nullable: false),
                    Metadata_DateRange = table.Column<string>(type: "TEXT", nullable: false),
                    Metadata_PreparedBy = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Metadata_ApprovedBy = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Documents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Evidence",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    ControlId = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    SubscriptionId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    EvidenceType = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: false),
                    Content = table.Column<string>(type: "TEXT", nullable: false),
                    CollectedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CollectedBy = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    AssessmentId = table.Column<string>(type: "TEXT", nullable: true),
                    EvidenceCategory = table.Column<int>(type: "INTEGER", nullable: false),
                    ResourceId = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    ContentHash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Evidence", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "NistControls",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    Family = table.Column<string>(type: "TEXT", maxLength: 5, nullable: false),
                    Title = table.Column<string>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: false),
                    ImpactLevel = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Enhancements = table.Column<string>(type: "TEXT", nullable: false),
                    AzureImplementation = table.Column<string>(type: "TEXT", nullable: false),
                    Baselines = table.Column<string>(type: "TEXT", nullable: false),
                    FedRampParameters = table.Column<string>(type: "TEXT", nullable: true),
                    AzurePolicyDefinitionIds = table.Column<string>(type: "TEXT", nullable: false),
                    ParentControlId = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    IsEnhancement = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NistControls", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NistControls_NistControls_ParentControlId",
                        column: x => x.ParentControlId,
                        principalTable: "NistControls",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RemediationPlans",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    SubscriptionId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    TotalFindings = table.Column<int>(type: "INTEGER", nullable: false),
                    AutoRemediableCount = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    DryRun = table.Column<bool>(type: "INTEGER", nullable: false),
                    ApprovedBy = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    FailedStepId = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    FailureReason = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RemediationPlans", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Findings",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    ControlId = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    ControlFamily = table.Column<string>(type: "TEXT", maxLength: 5, nullable: false),
                    Title = table.Column<string>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: false),
                    Severity = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    ResourceId = table.Column<string>(type: "TEXT", nullable: false),
                    ResourceType = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    RemediationGuidance = table.Column<string>(type: "TEXT", nullable: false),
                    DiscoveredAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    RemediationScript = table.Column<string>(type: "TEXT", nullable: true),
                    AutoRemediable = table.Column<bool>(type: "INTEGER", nullable: false),
                    Source = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    ScanSource = table.Column<int>(type: "INTEGER", nullable: false),
                    PolicyDefinitionId = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    PolicyAssignmentId = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    DefenderRecommendationId = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    RemediationType = table.Column<int>(type: "INTEGER", nullable: false),
                    RiskLevel = table.Column<int>(type: "INTEGER", nullable: false),
                    AssessmentId = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Findings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Findings_Assessments_AssessmentId",
                        column: x => x.AssessmentId,
                        principalTable: "Assessments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Findings_NistControls_ControlId",
                        column: x => x.ControlId,
                        principalTable: "NistControls",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RemediationStep",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    FindingId = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    ControlId = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Priority = table.Column<int>(type: "INTEGER", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: false),
                    Script = table.Column<string>(type: "TEXT", nullable: false),
                    Effort = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    AutoRemediable = table.Column<bool>(type: "INTEGER", nullable: false),
                    RemediationType = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    BeforeState = table.Column<string>(type: "TEXT", nullable: true),
                    AfterState = table.Column<string>(type: "TEXT", nullable: true),
                    ExecutedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ResourceId = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    RiskLevel = table.Column<int>(type: "INTEGER", nullable: false),
                    RemediationPlanId = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RemediationStep", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RemediationStep_RemediationPlans_RemediationPlanId",
                        column: x => x.RemediationPlanId,
                        principalTable: "RemediationPlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Assessments_AssessedAt",
                table: "Assessments",
                column: "AssessedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Assessments_SubscriptionId",
                table: "Assessments",
                column: "SubscriptionId");

            migrationBuilder.CreateIndex(
                name: "IX_Assessments_SubscriptionId_Framework",
                table: "Assessments",
                columns: new[] { "SubscriptionId", "Framework" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_SubscriptionId",
                table: "AuditLogs",
                column: "SubscriptionId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_Timestamp",
                table: "AuditLogs",
                column: "Timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_UserId_Timestamp",
                table: "AuditLogs",
                columns: new[] { "UserId", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_Evidence_AssessmentId",
                table: "Evidence",
                column: "AssessmentId");

            migrationBuilder.CreateIndex(
                name: "IX_Evidence_ControlId",
                table: "Evidence",
                column: "ControlId");

            migrationBuilder.CreateIndex(
                name: "IX_Findings_AssessmentId",
                table: "Findings",
                column: "AssessmentId");

            migrationBuilder.CreateIndex(
                name: "IX_Findings_AssessmentId_Severity",
                table: "Findings",
                columns: new[] { "AssessmentId", "Severity" });

            migrationBuilder.CreateIndex(
                name: "IX_Findings_ControlFamily",
                table: "Findings",
                column: "ControlFamily");

            migrationBuilder.CreateIndex(
                name: "IX_Findings_ControlId",
                table: "Findings",
                column: "ControlId");

            migrationBuilder.CreateIndex(
                name: "IX_NistControls_Family",
                table: "NistControls",
                column: "Family");

            migrationBuilder.CreateIndex(
                name: "IX_NistControls_ParentControlId",
                table: "NistControls",
                column: "ParentControlId");

            migrationBuilder.CreateIndex(
                name: "IX_RemediationStep_RemediationPlanId",
                table: "RemediationStep",
                column: "RemediationPlanId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditLogs");

            migrationBuilder.DropTable(
                name: "Documents");

            migrationBuilder.DropTable(
                name: "Evidence");

            migrationBuilder.DropTable(
                name: "Findings");

            migrationBuilder.DropTable(
                name: "RemediationStep");

            migrationBuilder.DropTable(
                name: "Assessments");

            migrationBuilder.DropTable(
                name: "NistControls");

            migrationBuilder.DropTable(
                name: "RemediationPlans");
        }
    }
}
