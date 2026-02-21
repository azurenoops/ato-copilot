using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Ato.Copilot.Core.Models.Compliance;

namespace Ato.Copilot.Core.Data.Context;

/// <summary>
/// Database context for ATO Copilot compliance data.
/// Supports SQLite (development) and SQL Server (production) providers.
/// </summary>
public class AtoCopilotContext : DbContext
{
    /// <summary>
    /// Initializes a new instance of <see cref="AtoCopilotContext"/>.
    /// </summary>
    /// <param name="options">Database context options with provider configuration.</param>
    public AtoCopilotContext(DbContextOptions<AtoCopilotContext> options) : base(options) { }

    /// <summary>Compliance assessments with scan results and statistics.</summary>
    public DbSet<ComplianceAssessment> Assessments => Set<ComplianceAssessment>();

    /// <summary>Individual compliance findings linked to assessments.</summary>
    public DbSet<ComplianceFinding> Findings => Set<ComplianceFinding>();

    /// <summary>Evidence artifacts collected for compliance controls.</summary>
    public DbSet<ComplianceEvidence> Evidence => Set<ComplianceEvidence>();

    /// <summary>Generated compliance documents (SSP, SAR, POA&M).</summary>
    public DbSet<ComplianceDocument> Documents => Set<ComplianceDocument>();

    /// <summary>NIST 800-53 Rev 5 controls loaded from OSCAL catalog.</summary>
    public DbSet<NistControl> NistControls => Set<NistControl>();

    /// <summary>Audit log entries for all compliance actions (730-day retention).</summary>
    public DbSet<AuditLogEntry> AuditLogs => Set<AuditLogEntry>();

    /// <summary>Remediation plans with ordered steps.</summary>
    public DbSet<RemediationPlan> RemediationPlans => Set<RemediationPlan>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // ─── Value Converters ────────────────────────────────────────────────────
        var stringListConverter = new ValueConverter<List<string>, string>(
            v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
            v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null) ?? new List<string>()
        );

        // ─── ComplianceAssessment ────────────────────────────────────────────────
        modelBuilder.Entity<ComplianceAssessment>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.SubscriptionId).HasMaxLength(100);
            entity.Property(e => e.Framework).HasMaxLength(50);
            entity.Property(e => e.Baseline).HasMaxLength(20);
            entity.Property(e => e.ScanType).HasMaxLength(20);
            entity.Property(e => e.InitiatedBy).HasMaxLength(200);
            entity.Property(e => e.ProgressMessage).HasMaxLength(500);

            // Owned types: ScanSummary stored as columns in Assessments table
            entity.OwnsOne(e => e.ResourceScanSummary, summary =>
            {
                summary.Property(s => s.ResourcesScanned).HasColumnName("ResourceScan_ResourcesScanned");
                summary.Property(s => s.PoliciesEvaluated).HasColumnName("ResourceScan_PoliciesEvaluated");
                summary.Property(s => s.Compliant).HasColumnName("ResourceScan_Compliant");
                summary.Property(s => s.NonCompliant).HasColumnName("ResourceScan_NonCompliant");
                summary.Property(s => s.CompliancePercentage).HasColumnName("ResourceScan_CompliancePercentage");
            });
            entity.OwnsOne(e => e.PolicyScanSummary, summary =>
            {
                summary.Property(s => s.ResourcesScanned).HasColumnName("PolicyScan_ResourcesScanned");
                summary.Property(s => s.PoliciesEvaluated).HasColumnName("PolicyScan_PoliciesEvaluated");
                summary.Property(s => s.Compliant).HasColumnName("PolicyScan_Compliant");
                summary.Property(s => s.NonCompliant).HasColumnName("PolicyScan_NonCompliant");
                summary.Property(s => s.CompliancePercentage).HasColumnName("PolicyScan_CompliancePercentage");
            });

            // Relationship: Assessment 1:N Findings (cascade delete)
            entity.HasMany(e => e.Findings)
                .WithOne()
                .HasForeignKey(f => f.AssessmentId)
                .OnDelete(DeleteBehavior.Cascade);

            // Indexes
            entity.HasIndex(e => e.SubscriptionId);
            entity.HasIndex(e => e.AssessedAt);
            entity.HasIndex(e => new { e.SubscriptionId, e.Framework });
        });

        // ─── ComplianceFinding ───────────────────────────────────────────────────
        modelBuilder.Entity<ComplianceFinding>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ControlId).HasMaxLength(20);
            entity.Property(e => e.ControlFamily).HasMaxLength(5);
            entity.Property(e => e.ResourceType).HasMaxLength(200);
            entity.Property(e => e.Source).HasMaxLength(50);
            entity.Property(e => e.PolicyDefinitionId).HasMaxLength(500);
            entity.Property(e => e.PolicyAssignmentId).HasMaxLength(500);
            entity.Property(e => e.DefenderRecommendationId).HasMaxLength(200);

            // Indexes
            entity.HasIndex(e => e.ControlId);
            entity.HasIndex(e => e.AssessmentId);
            entity.HasIndex(e => e.ControlFamily);
            entity.HasIndex(e => new { e.AssessmentId, e.Severity });
        });

        // ─── NistControl ────────────────────────────────────────────────────────
        modelBuilder.Entity<NistControl>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Family).HasMaxLength(5);
            entity.Property(e => e.ImpactLevel).HasMaxLength(20);
            entity.Property(e => e.ParentControlId).HasMaxLength(20);

            // Value conversions for List<string> properties
            entity.Property(e => e.Enhancements).HasConversion(stringListConverter);
            entity.Property(e => e.Baselines).HasConversion(stringListConverter);
            entity.Property(e => e.AzurePolicyDefinitionIds).HasConversion(stringListConverter);

            // Self-referential: NistControl 1:N ControlEnhancements (cascade)
            entity.HasMany(e => e.ControlEnhancements)
                .WithOne()
                .HasForeignKey(e => e.ParentControlId)
                .OnDelete(DeleteBehavior.Cascade);

            // NistControl 1:N ComplianceFinding (restrict — findings must not be orphaned)
            entity.HasMany<ComplianceFinding>()
                .WithOne()
                .HasForeignKey(f => f.ControlId)
                .HasPrincipalKey(c => c.Id)
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired(false);

            // Indexes
            entity.HasIndex(e => e.Family);
            entity.HasIndex(e => e.ParentControlId);
        });

        // ─── ComplianceEvidence ──────────────────────────────────────────────────
        modelBuilder.Entity<ComplianceEvidence>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ControlId).HasMaxLength(20);
            entity.Property(e => e.SubscriptionId).HasMaxLength(100);
            entity.Property(e => e.EvidenceType).HasMaxLength(50);
            entity.Property(e => e.CollectedBy).HasMaxLength(200);
            entity.Property(e => e.ContentHash).HasMaxLength(64);
            entity.Property(e => e.ResourceId).HasMaxLength(500);

            // Indexes
            entity.HasIndex(e => e.ControlId);
            entity.HasIndex(e => e.AssessmentId);
        });

        // ─── ComplianceDocument ──────────────────────────────────────────────────
        modelBuilder.Entity<ComplianceDocument>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.DocumentType).HasMaxLength(10);
            entity.Property(e => e.Framework).HasMaxLength(50);
            entity.Property(e => e.SystemName).HasMaxLength(200);
            entity.Property(e => e.Owner).HasMaxLength(200);
            entity.Property(e => e.GeneratedBy).HasMaxLength(200);

            // Owned type: DocumentMetadata stored as columns in Documents table
            entity.OwnsOne(e => e.Metadata, meta =>
            {
                meta.Property(m => m.PreparedBy).HasMaxLength(200);
                meta.Property(m => m.ApprovedBy).HasMaxLength(200);
            });
        });

        // ─── RemediationPlan ─────────────────────────────────────────────────────
        modelBuilder.Entity<RemediationPlan>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.SubscriptionId).HasMaxLength(100);
            entity.Property(e => e.ApprovedBy).HasMaxLength(200);
            entity.Property(e => e.FailedStepId).HasMaxLength(50);

            // Owned collection: RemediationSteps in separate table with implicit FK
            entity.OwnsMany(e => e.Steps, step =>
            {
                step.WithOwner().HasForeignKey("RemediationPlanId");
                step.HasKey(s => s.Id);
                step.Property(s => s.ControlId).HasMaxLength(20);
                step.Property(s => s.FindingId).HasMaxLength(50);
                step.Property(s => s.Effort).HasMaxLength(20);
                step.Property(s => s.ResourceId).HasMaxLength(500);
            });
        });

        // ─── AuditLogEntry ──────────────────────────────────────────────────────
        modelBuilder.Entity<AuditLogEntry>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.UserId).HasMaxLength(200);
            entity.Property(e => e.UserRole).HasMaxLength(50);
            entity.Property(e => e.Action).HasMaxLength(50);
            entity.Property(e => e.ScanType).HasMaxLength(20);
            entity.Property(e => e.SubscriptionId).HasMaxLength(100);

            // Value conversions for List<string> properties
            entity.Property(e => e.AffectedResources).HasConversion(stringListConverter);
            entity.Property(e => e.AffectedControls).HasConversion(stringListConverter);

            // Indexes
            entity.HasIndex(e => e.Timestamp);
            entity.HasIndex(e => e.SubscriptionId);
            entity.HasIndex(e => new { e.UserId, e.Timestamp });
        });
    }
}
