using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Ato.Copilot.Chat.Models;

namespace Ato.Copilot.Chat.Data;

/// <summary>
/// EF Core database context for the Chat application.
/// Supports dual-provider registration (SQLite for development, SQL Server for production).
/// Uses JSON ValueConverters for cross-provider compatibility.
/// </summary>
public class ChatDbContext : DbContext
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ChatDbContext"/> class.
    /// </summary>
    public ChatDbContext(DbContextOptions<ChatDbContext> options) : base(options) { }

    // ─── DbSets ──────────────────────────────────────────────────────

    /// <summary>Chat conversations.</summary>
    public DbSet<Conversation> Conversations => Set<Conversation>();

    /// <summary>Chat messages within conversations.</summary>
    public DbSet<ChatMessage> Messages => Set<ChatMessage>();

    /// <summary>Contextual metadata for conversations.</summary>
    public DbSet<ConversationContext> ConversationContexts => Set<ConversationContext>();

    /// <summary>File attachments for messages.</summary>
    public DbSet<MessageAttachment> Attachments => Set<MessageAttachment>();

    /// <summary>
    /// Configures entity relationships, constraints, indexes, and JSON value converters
    /// per data-model.md specification.
    /// </summary>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // ─── Value Converters (cross-provider: SQLite + SQL Server) ───

        var stringListConverter = new ValueConverter<List<string>, string>(
            v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
            v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null) ?? new List<string>()
        );

        var dictConverter = new ValueConverter<Dictionary<string, object>, string>(
            v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
            v => JsonSerializer.Deserialize<Dictionary<string, object>>(v, (JsonSerializerOptions?)null) ?? new Dictionary<string, object>()
        );

        var toolResultConverter = new ValueConverter<ToolExecutionResult?, string?>(
            v => v == null ? null : JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
            v => string.IsNullOrEmpty(v) ? null : JsonSerializer.Deserialize<ToolExecutionResult>(v, (JsonSerializerOptions?)null)
        );

        // ─── Conversation ────────────────────────────────────────────

        modelBuilder.Entity<Conversation>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasMaxLength(450);
            entity.Property(e => e.Title).HasMaxLength(200);
            entity.Property(e => e.UserId).HasMaxLength(100);
            entity.Property(e => e.Metadata).HasConversion(dictConverter);

            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.CreatedAt);
            entity.HasIndex(e => e.UpdatedAt);

            entity.HasMany(e => e.Messages)
                .WithOne(m => m.Conversation)
                .HasForeignKey(m => m.ConversationId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Context)
                .WithOne(c => c.Conversation)
                .HasForeignKey<ConversationContext>(c => c.ConversationId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ─── ChatMessage ─────────────────────────────────────────────

        modelBuilder.Entity<ChatMessage>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasMaxLength(450);
            entity.Property(e => e.ConversationId).HasMaxLength(450);
            entity.Property(e => e.Content).IsRequired();
            entity.Property(e => e.Role).HasConversion<string>();
            entity.Property(e => e.Status).HasConversion<string>();
            entity.Property(e => e.Metadata).HasConversion(dictConverter);
            entity.Property(e => e.ParentMessageId).HasMaxLength(450);
            entity.Property(e => e.Tools).HasConversion(stringListConverter);
            entity.Property(e => e.ToolResult).HasConversion(toolResultConverter);

            entity.HasIndex(e => e.ConversationId);
            entity.HasIndex(e => e.Timestamp);
            entity.HasIndex(e => e.Role);

            entity.HasMany(e => e.Attachments)
                .WithOne(a => a.Message)
                .HasForeignKey(a => a.MessageId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ─── ConversationContext ─────────────────────────────────────

        modelBuilder.Entity<ConversationContext>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasMaxLength(450);
            entity.Property(e => e.ConversationId).HasMaxLength(450);
            entity.Property(e => e.Type).HasMaxLength(50);
            entity.Property(e => e.Title).HasMaxLength(200);
            entity.Property(e => e.Data).HasConversion(dictConverter);
            entity.Property(e => e.Tags).HasConversion(stringListConverter);

            entity.HasIndex(e => e.ConversationId);
            entity.HasIndex(e => e.Type);
            entity.HasIndex(e => e.CreatedAt);
            entity.HasIndex(e => e.LastAccessedAt);
        });

        // ─── MessageAttachment ───────────────────────────────────────

        modelBuilder.Entity<MessageAttachment>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasMaxLength(450);
            entity.Property(e => e.MessageId).HasMaxLength(450);
            entity.Property(e => e.FileName).HasMaxLength(255);
            entity.Property(e => e.ContentType).HasMaxLength(100);
            entity.Property(e => e.StoragePath).HasMaxLength(500);
            entity.Property(e => e.Type).HasConversion<string>();
            entity.Property(e => e.Metadata).HasConversion(dictConverter);

            entity.HasIndex(e => e.MessageId);
            entity.HasIndex(e => e.UploadedAt);
            entity.HasIndex(e => e.Type);
        });
    }
}
