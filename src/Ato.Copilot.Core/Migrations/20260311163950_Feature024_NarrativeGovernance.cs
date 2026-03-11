using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ato.Copilot.Core.Migrations
{
    /// <inheritdoc />
    public partial class Feature024_NarrativeGovernance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ApprovalStatus",
                table: "ControlImplementations",
                type: "TEXT",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ApprovedVersionId",
                table: "ControlImplementations",
                type: "TEXT",
                maxLength: 36,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CurrentVersion",
                table: "ControlImplementations",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "NarrativeVersions",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", maxLength: 36, nullable: false),
                    ControlImplementationId = table.Column<string>(type: "TEXT", maxLength: 36, nullable: false),
                    VersionNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    Content = table.Column<string>(type: "TEXT", maxLength: 8000, nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    AuthoredBy = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    AuthoredAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ChangeReason = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    SubmittedBy = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    SubmittedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NarrativeVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NarrativeVersions_ControlImplementations_ControlImplementationId",
                        column: x => x.ControlImplementationId,
                        principalTable: "ControlImplementations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "NarrativeReviews",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", maxLength: 36, nullable: false),
                    NarrativeVersionId = table.Column<string>(type: "TEXT", maxLength: 36, nullable: false),
                    ReviewedBy = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Decision = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    ReviewerComments = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    ReviewedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NarrativeReviews", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NarrativeReviews_NarrativeVersions_NarrativeVersionId",
                        column: x => x.NarrativeVersionId,
                        principalTable: "NarrativeVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ControlImplementation_ApprovalStatus",
                table: "ControlImplementations",
                column: "ApprovalStatus");

            migrationBuilder.CreateIndex(
                name: "IX_ControlImplementations_ApprovedVersionId",
                table: "ControlImplementations",
                column: "ApprovedVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_NarrativeReview_VersionId",
                table: "NarrativeReviews",
                column: "NarrativeVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_NarrativeVersion_Impl_Version",
                table: "NarrativeVersions",
                columns: new[] { "ControlImplementationId", "VersionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NarrativeVersion_Status",
                table: "NarrativeVersions",
                column: "Status");

            migrationBuilder.AddForeignKey(
                name: "FK_ControlImplementations_NarrativeVersions_ApprovedVersionId",
                table: "ControlImplementations",
                column: "ApprovedVersionId",
                principalTable: "NarrativeVersions",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            // ── Data seeding: bootstrap NarrativeVersion v1 for existing rows with narrative text ──
            migrationBuilder.Sql("""
                INSERT INTO NarrativeVersions (Id, ControlImplementationId, VersionNumber, Content, Status, AuthoredBy, AuthoredAt, ChangeReason)
                SELECT
                    lower(hex(randomblob(4)) || '-' || hex(randomblob(2)) || '-4' || substr(hex(randomblob(2)),2) || '-' || substr('89ab', abs(random()) % 4 + 1, 1) || substr(hex(randomblob(2)),2) || '-' || hex(randomblob(6))),
                    Id,
                    1,
                    Narrative,
                    'Draft',
                    AuthoredBy,
                    COALESCE(ModifiedAt, AuthoredAt),
                    'Bootstrapped from pre-governance narrative'
                FROM ControlImplementations
                WHERE Narrative IS NOT NULL AND LENGTH(TRIM(Narrative)) > 0;
                """);

            // Set CurrentVersion = 1 for all seeded rows
            migrationBuilder.Sql("""
                UPDATE ControlImplementations SET CurrentVersion = 1
                WHERE Narrative IS NOT NULL AND LENGTH(TRIM(Narrative)) > 0;
                """);

            // Set default ApprovalStatus for all existing rows
            migrationBuilder.Sql("""
                UPDATE ControlImplementations SET ApprovalStatus = 'Draft'
                WHERE ApprovalStatus = '' OR ApprovalStatus IS NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ControlImplementations_NarrativeVersions_ApprovedVersionId",
                table: "ControlImplementations");

            migrationBuilder.DropTable(
                name: "NarrativeReviews");

            migrationBuilder.DropTable(
                name: "NarrativeVersions");

            migrationBuilder.DropIndex(
                name: "IX_ControlImplementation_ApprovalStatus",
                table: "ControlImplementations");

            migrationBuilder.DropIndex(
                name: "IX_ControlImplementations_ApprovedVersionId",
                table: "ControlImplementations");

            migrationBuilder.DropColumn(
                name: "ApprovalStatus",
                table: "ControlImplementations");

            migrationBuilder.DropColumn(
                name: "ApprovedVersionId",
                table: "ControlImplementations");

            migrationBuilder.DropColumn(
                name: "CurrentVersion",
                table: "ControlImplementations");
        }
    }
}
