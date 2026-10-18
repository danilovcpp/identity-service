using Identity.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Identity.Infrastructure.Persistence.Configurations;

internal sealed class SessionConfiguration : IEntityTypeConfiguration<Session>
{
    public void Configure(EntityTypeBuilder<Session> builder)
    {
        builder.ToTable("sessions", table =>
        {
            table.HasCheckConstraint("ck_sessions_client", "length(btrim(client_id)) > 0");
            table.HasCheckConstraint("ck_sessions_audience", "audience IS NULL OR (length(btrim(audience)) > 0 AND audience !~ '[[:cntrl:]]')");
            table.HasCheckConstraint("ck_sessions_times", "absolute_expires_at > created_at AND absolute_expires_at <= created_at + interval '720 hours' AND last_used_at >= created_at AND last_used_at < absolute_expires_at AND (revoked_at IS NULL OR revoked_at >= last_used_at)");
            table.HasCheckConstraint("ck_sessions_revocation", "(revoked_at IS NULL) = (revoke_reason IS NULL) AND (revoke_reason IS NULL OR revoke_reason BETWEEN 0 AND 6)");
        });
        builder.HasKey(session => session.Id);
        builder.Property(session => session.Id).ValueGeneratedNever();
        builder.Property(session => session.ClientId).HasMaxLength(100).IsRequired();
        builder.Property(session => session.Audience).HasMaxLength(300);
        builder.Property(session => session.UserAgent).HasMaxLength(512);
        builder.Property(session => session.RevokeReason).HasConversion<short>();
        builder.Property<uint>("Version").IsRowVersion();
        builder.HasOne<User>().WithMany().HasForeignKey(session => session.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(session => new { session.UserId, session.RevokedAt }).HasDatabaseName("ix_sessions_user_revocation");
        builder.HasIndex(session => session.AbsoluteExpiresAt).HasDatabaseName("ix_sessions_expiry");
    }
}
