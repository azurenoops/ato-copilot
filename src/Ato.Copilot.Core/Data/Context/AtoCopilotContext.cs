using Microsoft.EntityFrameworkCore;
using Ato.Copilot.Core.Models.Compliance;

namespace Ato.Copilot.Core.Data.Context;

/// <summary>
/// Database context for ATO Copilot compliance data
/// </summary>
public class AtoCopilotContext : DbContext
{
    public AtoCopilotContext(DbContextOptions<AtoCopilotContext> options) : base(options) { }

    public DbSet<ComplianceAssessmentEntity> Assessments { get; set; } = null!;
    public DbSet<ComplianceFindingEntity> Findings { get; set; } = null!;
    public DbSet<ComplianceEvidenceEntity> Evidence { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ComplianceAssessmentEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.SubscriptionId).HasMaxLength(100);
            entity.Property(e => e.Framework).HasMaxLength(50);
            entity.HasMany(e => e.Findings)
                .WithOne()
                .HasForeignKey(f => f.AssessmentId);
        });

        modelBuilder.Entity<ComplianceFindingEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ControlId).HasMaxLength(20);
            entity.Property(e => e.Severity).HasMaxLength(20);
        });

        modelBuilder.Entity<ComplianceEvidenceEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ControlId).HasMaxLength(20);
        });
    }
}

public class ComplianceAssessmentEntity
{
    public string Id { get; set; } = string.Empty;
    public string SubscriptionId { get; set; } = string.Empty;
    public string Framework { get; set; } = string.Empty;
    public string ScanType { get; set; } = string.Empty;
    public DateTime AssessedAt { get; set; }
    public double ComplianceScore { get; set; }
    public int TotalControls { get; set; }
    public int PassedControls { get; set; }
    public int FailedControls { get; set; }
    public List<ComplianceFindingEntity> Findings { get; set; } = new();
}

public class ComplianceFindingEntity
{
    public string Id { get; set; } = string.Empty;
    public string AssessmentId { get; set; } = string.Empty;
    public string ControlId { get; set; } = string.Empty;
    public string ControlFamily { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string ResourceId { get; set; } = string.Empty;
    public DateTime DiscoveredAt { get; set; }
}

public class ComplianceEvidenceEntity
{
    public string Id { get; set; } = string.Empty;
    public string ControlId { get; set; } = string.Empty;
    public string SubscriptionId { get; set; } = string.Empty;
    public string EvidenceType { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime CollectedAt { get; set; }
}
