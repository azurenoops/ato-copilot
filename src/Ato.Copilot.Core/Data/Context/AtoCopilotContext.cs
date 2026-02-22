using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Ato.Copilot.Core.Models.Auth;
using Ato.Copilot.Core.Models.Compliance;
using Ato.Copilot.Core.Models.Kanban;

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

    /// <summary>Kanban remediation boards grouping tasks by subscription.</summary>
    public DbSet<RemediationBoard> RemediationBoards => Set<RemediationBoard>();

    /// <summary>Individual remediation tasks (Kanban cards).</summary>
    public DbSet<RemediationTask> RemediationTasks => Set<RemediationTask>();

    /// <summary>Threaded comments on remediation tasks.</summary>
    public DbSet<TaskComment> TaskComments => Set<TaskComment>();

    /// <summary>Immutable history entries for remediation task changes.</summary>
    public DbSet<TaskHistoryEntry> TaskHistoryEntries => Set<TaskHistoryEntry>();

    /// <summary>CAC/PIV authentication sessions with configurable timeout.</summary>
    public DbSet<CacSession> CacSessions => Set<CacSession>();

    /// <summary>JIT access requests for PIM role activations and VM network access.</summary>
    public DbSet<JitRequestEntity> JitRequests => Set<JitRequestEntity>();

    /// <summary>Certificate-to-role mappings for automatic role resolution.</summary>
    public DbSet<CertificateRoleMapping> CertificateRoleMappings => Set<CertificateRoleMapping>();

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

        // ─── RemediationBoard ────────────────────────────────────────────────────
        modelBuilder.Entity<RemediationBoard>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).HasMaxLength(200).IsRequired();
            entity.Property(e => e.SubscriptionId).HasMaxLength(100).IsRequired();
            entity.Property(e => e.AssessmentId).HasMaxLength(100);
            entity.Property(e => e.Owner).HasMaxLength(200).IsRequired();
            entity.Property(e => e.RowVersion).IsConcurrencyToken();

            // Relationship: Board → Assessment (optional, restrict delete)
            entity.HasOne<ComplianceAssessment>()
                .WithMany()
                .HasForeignKey(e => e.AssessmentId)
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired(false);

            // Relationship: Board 1:N Tasks (cascade delete)
            entity.HasMany(e => e.Tasks)
                .WithOne(t => t.Board)
                .HasForeignKey(t => t.BoardId)
                .OnDelete(DeleteBehavior.Cascade);

            // Indexes
            entity.HasIndex(e => e.SubscriptionId);
            entity.HasIndex(e => new { e.SubscriptionId, e.IsArchived });
        });

        // ─── RemediationTask ─────────────────────────────────────────────────────
        modelBuilder.Entity<RemediationTask>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.TaskNumber).HasMaxLength(10).IsRequired();
            entity.Property(e => e.BoardId).IsRequired();
            entity.Property(e => e.Title).HasMaxLength(500).IsRequired();
            entity.Property(e => e.Description).HasMaxLength(4000);
            entity.Property(e => e.ControlId).HasMaxLength(20).IsRequired();
            entity.Property(e => e.ControlFamily).HasMaxLength(5);
            entity.Property(e => e.AssigneeId).HasMaxLength(200);
            entity.Property(e => e.AssigneeName).HasMaxLength(200);
            entity.Property(e => e.RemediationScript).HasMaxLength(8000);
            entity.Property(e => e.ValidationCriteria).HasMaxLength(2000);
            entity.Property(e => e.FindingId).HasMaxLength(100);
            entity.Property(e => e.CreatedBy).HasMaxLength(200).IsRequired();
            entity.Property(e => e.RowVersion).IsConcurrencyToken();

            // Value conversion for List<string> AffectedResources
            entity.Property(e => e.AffectedResources).HasConversion(stringListConverter);

            // Relationship: Task 1:N Comments (cascade delete)
            entity.HasMany(e => e.Comments)
                .WithOne(c => c.Task)
                .HasForeignKey(c => c.TaskId)
                .OnDelete(DeleteBehavior.Cascade);

            // Relationship: Task 1:N History (cascade delete)
            entity.HasMany(e => e.History)
                .WithOne(h => h.Task)
                .HasForeignKey(h => h.TaskId)
                .OnDelete(DeleteBehavior.Cascade);

            // Indexes
            entity.HasIndex(e => e.BoardId);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.AssigneeId);
            entity.HasIndex(e => e.ControlId);
            entity.HasIndex(e => e.DueDate);
            entity.HasIndex(e => new { e.BoardId, e.Status });
            entity.HasIndex(e => new { e.BoardId, e.ControlFamily });
        });

        // ─── TaskComment ─────────────────────────────────────────────────────────
        modelBuilder.Entity<TaskComment>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.TaskId).IsRequired();
            entity.Property(e => e.AuthorId).HasMaxLength(200).IsRequired();
            entity.Property(e => e.AuthorName).HasMaxLength(200).IsRequired();
            entity.Property(e => e.Content).HasMaxLength(4000).IsRequired();
            entity.Property(e => e.ParentCommentId).HasMaxLength(100);

            // Value conversion for List<string> Mentions
            entity.Property(e => e.Mentions).HasConversion(stringListConverter);

            // Indexes
            entity.HasIndex(e => e.TaskId);
            entity.HasIndex(e => new { e.TaskId, e.CreatedAt });
        });

        // ─── TaskHistoryEntry ────────────────────────────────────────────────────
        modelBuilder.Entity<TaskHistoryEntry>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.TaskId).IsRequired();
            entity.Property(e => e.OldValue).HasMaxLength(500);
            entity.Property(e => e.NewValue).HasMaxLength(500);
            entity.Property(e => e.ActingUserId).HasMaxLength(200).IsRequired();
            entity.Property(e => e.ActingUserName).HasMaxLength(200).IsRequired();
            entity.Property(e => e.Details).HasMaxLength(4000);

            // Indexes
            entity.HasIndex(e => e.TaskId);
            entity.HasIndex(e => new { e.TaskId, e.Timestamp });
        });

        // ─── CacSession ─────────────────────────────────────────────────────────
        modelBuilder.Entity<CacSession>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.UserId).HasMaxLength(200).IsRequired();
            entity.Property(e => e.DisplayName).HasMaxLength(200);
            entity.Property(e => e.Email).HasMaxLength(200);
            entity.Property(e => e.TokenHash).HasMaxLength(64).IsRequired();
            entity.Property(e => e.IpAddress).HasMaxLength(45);
            entity.Property(e => e.Status).HasConversion<string>().HasMaxLength(20);
            entity.Property(e => e.ClientType).HasConversion<string>().HasMaxLength(20);

            entity.HasIndex(e => new { e.UserId, e.Status }).HasDatabaseName("IX_CacSession_UserId_Status");
            entity.HasIndex(e => e.ExpiresAt).HasDatabaseName("IX_CacSession_ExpiresAt");
        });

        // ─── JitRequestEntity ────────────────────────────────────────────────────
        modelBuilder.Entity<JitRequestEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.UserId).HasMaxLength(200).IsRequired();
            entity.Property(e => e.UserDisplayName).HasMaxLength(200);
            entity.Property(e => e.ConversationId).HasMaxLength(100);
            entity.Property(e => e.RoleName).HasMaxLength(200);
            entity.Property(e => e.Scope).HasMaxLength(500);
            entity.Property(e => e.ScopeDisplayName).HasMaxLength(500);
            entity.Property(e => e.Justification).HasMaxLength(500).IsRequired();
            entity.Property(e => e.TicketNumber).HasMaxLength(50);
            entity.Property(e => e.TicketSystem).HasMaxLength(50);
            entity.Property(e => e.PimRequestId).HasMaxLength(100);
            entity.Property(e => e.ApproverId).HasMaxLength(200);
            entity.Property(e => e.ApproverDisplayName).HasMaxLength(200);
            entity.Property(e => e.ApproverComments).HasMaxLength(500);
            entity.Property(e => e.VmName).HasMaxLength(200);
            entity.Property(e => e.ResourceGroup).HasMaxLength(200);
            entity.Property(e => e.SubscriptionId).HasMaxLength(100);
            entity.Property(e => e.Protocol).HasMaxLength(10);
            entity.Property(e => e.SourceIp).HasMaxLength(45);
            entity.Property(e => e.RequestType).HasConversion<string>().HasMaxLength(30);
            entity.Property(e => e.Status).HasConversion<string>().HasMaxLength(30);
            entity.Property(e => e.RowVersion).IsConcurrencyToken();

            // Relationship: JitRequest → CacSession (optional, restrict delete)
            entity.HasOne(e => e.Session)
                .WithMany()
                .HasForeignKey(e => e.SessionId)
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired(false);

            entity.HasIndex(e => new { e.UserId, e.Status }).HasDatabaseName("IX_JitRequest_UserId_Status");
            entity.HasIndex(e => e.SessionId).HasDatabaseName("IX_JitRequest_SessionId");
            entity.HasIndex(e => e.RequestedAt).HasDatabaseName("IX_JitRequest_RequestedAt");
            entity.HasIndex(e => new { e.RoleName, e.Scope }).HasDatabaseName("IX_JitRequest_RoleName_Scope");
            entity.HasIndex(e => new { e.Status, e.ExpiresAt }).HasDatabaseName("IX_JitRequest_Status_ExpiresAt");
        });

        // ─── CertificateRoleMapping ──────────────────────────────────────────────
        modelBuilder.Entity<CertificateRoleMapping>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.CertificateThumbprint).HasMaxLength(64).IsRequired();
            entity.Property(e => e.CertificateSubject).HasMaxLength(500).IsRequired();
            entity.Property(e => e.MappedRole).HasMaxLength(100).IsRequired();
            entity.Property(e => e.CreatedBy).HasMaxLength(200);

            entity.HasIndex(e => e.CertificateThumbprint).IsUnique().HasDatabaseName("IX_CertMapping_Thumbprint");
            entity.HasIndex(e => e.CertificateSubject).IsUnique().HasDatabaseName("IX_CertMapping_Subject");
        });
    }

    /// <inheritdoc />
    /// <remarks>
    /// Auto-regenerates RowVersion for all modified ConcurrentEntity entries
    /// to support optimistic concurrency with Guid-based tokens (per research R-001).
    /// </remarks>
    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        foreach (var entry in ChangeTracker.Entries<ConcurrentEntity>())
        {
            if (entry.State == EntityState.Modified || entry.State == EntityState.Added)
            {
                entry.Entity.RowVersion = Guid.NewGuid();
            }
        }

        return await base.SaveChangesAsync(cancellationToken);
    }
}
