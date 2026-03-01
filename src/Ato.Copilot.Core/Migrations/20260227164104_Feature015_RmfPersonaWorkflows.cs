using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ato.Copilot.Core.Migrations
{
    /// <inheritdoc />
    public partial class Feature015_RmfPersonaWorkflows : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_NistControls_NistControls_ParentControlId",
                table: "NistControls");

            migrationBuilder.AddColumn<string>(
                name: "RemediationScriptType",
                table: "RemediationTasks",
                type: "TEXT",
                maxLength: 20,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "RegisteredSystems",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", maxLength: 36, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Acronym = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    SystemType = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    MissionCriticality = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    IsNationalSecuritySystem = table.Column<bool>(type: "INTEGER", nullable: false),
                    ClassifiedDesignation = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    HostingEnvironment = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    CurrentRmfStep = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    RmfStepUpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    Azure_CloudEnvironment = table.Column<string>(type: "TEXT", maxLength: 40, nullable: true),
                    Azure_ArmEndpoint = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    Azure_AuthenticationEndpoint = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    Azure_DefenderEndpoint = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    Azure_PolicyEndpoint = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    Azure_ProxyUrl = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    Azure_SubscriptionIds = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RegisteredSystems", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AuthorizationBoundaries",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", maxLength: 36, nullable: false),
                    RegisteredSystemId = table.Column<string>(type: "TEXT", maxLength: 36, nullable: false),
                    ResourceId = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    ResourceType = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    ResourceName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    IsInBoundary = table.Column<bool>(type: "INTEGER", nullable: false),
                    ExclusionRationale = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    InheritanceProvider = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    AddedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    AddedBy = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuthorizationBoundaries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AuthorizationBoundaries_RegisteredSystems_RegisteredSystemId",
                        column: x => x.RegisteredSystemId,
                        principalTable: "RegisteredSystems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ControlBaselines",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", maxLength: 36, nullable: false),
                    RegisteredSystemId = table.Column<string>(type: "TEXT", maxLength: 36, nullable: false),
                    BaselineLevel = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    OverlayApplied = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    TotalControls = table.Column<int>(type: "INTEGER", nullable: false),
                    CustomerControls = table.Column<int>(type: "INTEGER", nullable: false),
                    InheritedControls = table.Column<int>(type: "INTEGER", nullable: false),
                    SharedControls = table.Column<int>(type: "INTEGER", nullable: false),
                    TailoredOutControls = table.Column<int>(type: "INTEGER", nullable: false),
                    TailoredInControls = table.Column<int>(type: "INTEGER", nullable: false),
                    ControlIds = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ControlBaselines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ControlBaselines_RegisteredSystems_RegisteredSystemId",
                        column: x => x.RegisteredSystemId,
                        principalTable: "RegisteredSystems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RmfRoleAssignments",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", maxLength: 36, nullable: false),
                    RegisteredSystemId = table.Column<string>(type: "TEXT", maxLength: 36, nullable: false),
                    RmfRole = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    UserId = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    UserDisplayName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    AssignedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    AssignedBy = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RmfRoleAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RmfRoleAssignments_RegisteredSystems_RegisteredSystemId",
                        column: x => x.RegisteredSystemId,
                        principalTable: "RegisteredSystems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SecurityCategorizations",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", maxLength: 36, nullable: false),
                    RegisteredSystemId = table.Column<string>(type: "TEXT", maxLength: 36, nullable: false),
                    IsNationalSecuritySystem = table.Column<bool>(type: "INTEGER", nullable: false),
                    Justification = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: true),
                    CategorizedBy = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    CategorizedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SecurityCategorizations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SecurityCategorizations_RegisteredSystems_RegisteredSystemId",
                        column: x => x.RegisteredSystemId,
                        principalTable: "RegisteredSystems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ControlInheritances",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", maxLength: 36, nullable: false),
                    ControlBaselineId = table.Column<string>(type: "TEXT", maxLength: 36, nullable: false),
                    ControlId = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    InheritanceType = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Provider = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    CustomerResponsibility = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    SetBy = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    SetAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ControlInheritances", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ControlInheritances_ControlBaselines_ControlBaselineId",
                        column: x => x.ControlBaselineId,
                        principalTable: "ControlBaselines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ControlTailorings",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", maxLength: 36, nullable: false),
                    ControlBaselineId = table.Column<string>(type: "TEXT", maxLength: 36, nullable: false),
                    ControlId = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Action = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    Rationale = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    IsOverlayRequired = table.Column<bool>(type: "INTEGER", nullable: false),
                    TailoredBy = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    TailoredAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ControlTailorings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ControlTailorings_ControlBaselines_ControlBaselineId",
                        column: x => x.ControlBaselineId,
                        principalTable: "ControlBaselines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "InformationTypes",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", maxLength: 36, nullable: false),
                    SecurityCategorizationId = table.Column<string>(type: "TEXT", maxLength: 36, nullable: false),
                    Sp80060Id = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Category = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    ConfidentialityImpact = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    IntegrityImpact = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    AvailabilityImpact = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    UsesProvisionalImpactLevels = table.Column<bool>(type: "INTEGER", nullable: false),
                    AdjustmentJustification = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InformationTypes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InformationTypes_SecurityCategorizations_SecurityCategorizationId",
                        column: x => x.SecurityCategorizationId,
                        principalTable: "SecurityCategorizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AuthorizationBoundary_System_Resource",
                table: "AuthorizationBoundaries",
                columns: new[] { "RegisteredSystemId", "ResourceId" });

            migrationBuilder.CreateIndex(
                name: "IX_AuthorizationBoundary_SystemId",
                table: "AuthorizationBoundaries",
                column: "RegisteredSystemId");

            migrationBuilder.CreateIndex(
                name: "IX_ControlBaseline_SystemId",
                table: "ControlBaselines",
                column: "RegisteredSystemId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ControlInheritance_Baseline_Control",
                table: "ControlInheritances",
                columns: new[] { "ControlBaselineId", "ControlId" });

            migrationBuilder.CreateIndex(
                name: "IX_ControlInheritance_BaselineId",
                table: "ControlInheritances",
                column: "ControlBaselineId");

            migrationBuilder.CreateIndex(
                name: "IX_ControlInheritance_Type",
                table: "ControlInheritances",
                column: "InheritanceType");

            migrationBuilder.CreateIndex(
                name: "IX_ControlTailoring_Baseline_Control",
                table: "ControlTailorings",
                columns: new[] { "ControlBaselineId", "ControlId" });

            migrationBuilder.CreateIndex(
                name: "IX_ControlTailoring_BaselineId",
                table: "ControlTailorings",
                column: "ControlBaselineId");

            migrationBuilder.CreateIndex(
                name: "IX_InformationType_CategorizationId",
                table: "InformationTypes",
                column: "SecurityCategorizationId");

            migrationBuilder.CreateIndex(
                name: "IX_InformationType_Sp80060Id",
                table: "InformationTypes",
                column: "Sp80060Id");

            migrationBuilder.CreateIndex(
                name: "IX_RegisteredSystem_Acronym",
                table: "RegisteredSystems",
                column: "Acronym");

            migrationBuilder.CreateIndex(
                name: "IX_RegisteredSystem_Active_Step",
                table: "RegisteredSystems",
                columns: new[] { "IsActive", "CurrentRmfStep" });

            migrationBuilder.CreateIndex(
                name: "IX_RegisteredSystem_CreatedBy",
                table: "RegisteredSystems",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_RegisteredSystem_Name",
                table: "RegisteredSystems",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_RmfRoleAssignment_System_Role",
                table: "RmfRoleAssignments",
                columns: new[] { "RegisteredSystemId", "RmfRole" });

            migrationBuilder.CreateIndex(
                name: "IX_RmfRoleAssignment_SystemId",
                table: "RmfRoleAssignments",
                column: "RegisteredSystemId");

            migrationBuilder.CreateIndex(
                name: "IX_RmfRoleAssignment_UserId",
                table: "RmfRoleAssignments",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_SecurityCategorization_SystemId",
                table: "SecurityCategorizations",
                column: "RegisteredSystemId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_NistControls_NistControls_ParentControlId",
                table: "NistControls",
                column: "ParentControlId",
                principalTable: "NistControls",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_NistControls_NistControls_ParentControlId",
                table: "NistControls");

            migrationBuilder.DropTable(
                name: "AuthorizationBoundaries");

            migrationBuilder.DropTable(
                name: "ControlInheritances");

            migrationBuilder.DropTable(
                name: "ControlTailorings");

            migrationBuilder.DropTable(
                name: "InformationTypes");

            migrationBuilder.DropTable(
                name: "RmfRoleAssignments");

            migrationBuilder.DropTable(
                name: "ControlBaselines");

            migrationBuilder.DropTable(
                name: "SecurityCategorizations");

            migrationBuilder.DropTable(
                name: "RegisteredSystems");

            migrationBuilder.DropColumn(
                name: "RemediationScriptType",
                table: "RemediationTasks");

            migrationBuilder.AddForeignKey(
                name: "FK_NistControls_NistControls_ParentControlId",
                table: "NistControls",
                column: "ParentControlId",
                principalTable: "NistControls",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
