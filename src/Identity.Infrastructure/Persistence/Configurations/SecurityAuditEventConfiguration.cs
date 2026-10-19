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
            table.HasCheckConstraint("ck_audit_event_type", "event_type IN ('register', 'verify_email', 'login_success', 'login_failed', 'refresh_rotated', 'refresh_replay_detected', 'logout', 'logout_all', 'password_changed', 'password_reset', 'account_deactivated', 'admin_login', 'admin_login_failed', 'admin_logout', 'admin_role_granted', 'admin_role_revoked', 'admin_invited_user', 'admin_resent_invitation', 'admin_requested_password_reset', 'admin_disabled_user', 'admin_enabled_user', 'admin_revoked_session')");
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
        builder.HasOne<User>().WithMany().HasForeignKey(audit => audit.ActorUserId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne<User>().WithMany().HasForeignKey(audit => audit.TargetUserId).OnDelete(DeleteBehavior.SetNull);
        builder.HasIndex(audit => new { audit.ActorUserId, audit.OccurredAt }).HasDatabaseName("ix_audit_actor_time");
        builder.HasIndex(audit => new { audit.TargetUserId, audit.OccurredAt }).HasDatabaseName("ix_audit_target_time");
        builder.HasIndex(audit => new { audit.OccurredAt, audit.Id }).HasDatabaseName("ix_audit_time_id");
        builder.HasIndex(audit => new { audit.UserId, audit.OccurredAt, audit.Id }).HasDatabaseName("ix_audit_user_time_id");
        builder.HasIndex(audit => new { audit.EventType, audit.OccurredAt, audit.Id }).HasDatabaseName("ix_audit_event_time_id");
    }
}
