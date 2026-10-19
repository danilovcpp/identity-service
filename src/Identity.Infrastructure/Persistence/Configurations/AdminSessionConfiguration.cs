using Identity.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Identity.Infrastructure.Persistence.Configurations;

internal sealed class AdminSessionConfiguration : IEntityTypeConfiguration<AdminSession>
{
    public void Configure(EntityTypeBuilder<AdminSession> builder)
    {
        builder.ToTable("admin_sessions", table => table.HasCheckConstraint("ck_admin_sessions_times",
            "last_seen_at >= created_at AND last_seen_at < expires_at AND expires_at > created_at AND expires_at <= created_at + interval '8 hours' AND (revoked_at IS NULL OR revoked_at >= created_at)"));
        builder.HasKey(session => session.Id);
        builder.Property(session => session.Id).ValueGeneratedNever();
        builder.Property<uint>("Version").IsRowVersion();
        builder.HasOne<User>().WithMany().HasForeignKey(session => session.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(session => new { session.UserId, session.RevokedAt }).HasDatabaseName("ix_admin_sessions_user_revocation");
        builder.HasIndex(session => session.ExpiresAt).HasDatabaseName("ix_admin_sessions_expiry");
    }
}
