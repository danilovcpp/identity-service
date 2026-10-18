using System.Text.Json;
using Identity.Domain.Entities;
using Identity.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Identity.Infrastructure.Persistence.Configurations;

internal sealed class SecurityAuditEventConfiguration : IEntityTypeConfiguration<SecurityAuditEvent>
{
    public void Configure(EntityTypeBuilder<SecurityAuditEvent> builder)
    {
        builder.ToTable("security_audit_events", table =>
        {
            table.HasCheckConstraint("ck_audit_event_type", "event_type IN ('register', 'verify_email', 'login_success', 'login_failed', 'refresh_rotated', 'refresh_replay_detected', 'logout', 'logout_all', 'password_changed', 'password_reset', 'account_deactivated')");
            table.HasCheckConstraint("ck_audit_metadata", "jsonb_typeof(metadata) = 'object' AND metadata - 'attemptCount' = '{}'::jsonb AND CASE WHEN NOT (metadata ? 'attemptCount') THEN true WHEN jsonb_typeof(metadata->'attemptCount') = 'number' THEN (metadata->>'attemptCount')::numeric BETWEEN 0 AND 2147483647 AND mod((metadata->>'attemptCount')::numeric, 1) = 0 ELSE false END");
        });
        builder.HasKey(audit => audit.Id);
        builder.Property(audit => audit.Id).ValueGeneratedNever();
        builder.Property(audit => audit.EventType).HasMaxLength(80).IsRequired();
        builder.Property(audit => audit.ClientId).HasMaxLength(100);
        builder.Property(audit => audit.CorrelationId).HasMaxLength(128).IsRequired();
        builder.Property(audit => audit.Metadata).HasColumnType("jsonb").IsRequired()
            .HasConversion(metadata => JsonSerializer.Serialize(metadata, JsonConfiguration.Options),
                json => JsonSerializer.Deserialize<AuditMetadata>(json, JsonConfiguration.Options)!);
        builder.HasOne<User>().WithMany().HasForeignKey(audit => audit.UserId).OnDelete(DeleteBehavior.SetNull);
        builder.HasIndex(audit => audit.OccurredAt).HasDatabaseName("ix_audit_occurred_at");
    }
}
