using Identity.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Identity.Infrastructure.Persistence.Configurations;

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users", table =>
        {
            table.HasCheckConstraint("ck_users_email", "length(btrim(email)) > 0 AND email !~ '[[:cntrl:]]' AND length(btrim(normalized_email)) > 0 AND normalized_email !~ '[[:cntrl:]]'");
            table.HasCheckConstraint("ck_users_status", "status IN (0, 1, 2, 3, 4)");
            table.HasCheckConstraint("ck_users_verification", "(status IN (0, 4) AND email_verified_at IS NULL) OR (status = 1 AND email_verified_at IS NOT NULL) OR status IN (2, 3)");
            table.HasCheckConstraint("ck_users_disable_metadata", "(disabled_at IS NULL OR disabled_at BETWEEN created_at AND updated_at) AND (disable_reason IS NULL OR (length(btrim(disable_reason)) > 0 AND disable_reason !~ '[[:cntrl:]]'))");
            table.HasCheckConstraint("ck_users_previous_status", "status_before_disable IS NULL OR (status = 2 AND status_before_disable IN (0, 1, 4) AND ((status_before_disable = 1) = (email_verified_at IS NOT NULL)))");
            table.HasCheckConstraint("ck_users_deactivation", "(status = 3) = (deactivated_at IS NOT NULL)");
            table.HasCheckConstraint("ck_users_times", "updated_at >= created_at AND (email_verified_at IS NULL OR email_verified_at BETWEEN created_at AND updated_at) AND (deactivated_at IS NULL OR deactivated_at BETWEEN created_at AND updated_at)");
            table.HasCheckConstraint("ck_users_login_failures", "failed_login_count >= 0 AND (locked_until IS NULL OR locked_until > created_at)");
        });
        builder.HasKey(user => user.Id);
        builder.Property(user => user.Id).ValueGeneratedNever();
        builder.Property(user => user.Email).HasMaxLength(320).IsRequired();
        builder.Property(user => user.NormalizedEmail).HasMaxLength(320).IsRequired();
        builder.Property(user => user.DisplayName).HasMaxLength(120);
        builder.Property(user => user.DisableReason).HasMaxLength(500);
        builder.HasOne<User>().WithMany().HasForeignKey(user => user.DisabledBy).OnDelete(DeleteBehavior.SetNull);
        builder.Property(user => user.Status).HasConversion<short>();
        builder.Property(user => user.StatusBeforeDisable).HasConversion<short>();
        builder.Property<uint>("Version").IsRowVersion();
        builder.HasIndex(user => user.NormalizedEmail).IsUnique().HasDatabaseName("ux_users_normalized_email");
        builder.HasIndex(user => new { user.CreatedAt, user.Id }).HasDatabaseName("ix_users_created_id");
        builder.HasIndex(user => new { user.Email, user.Id }).HasDatabaseName("ix_users_email_id");
        builder.HasIndex(user => new { user.Status, user.Id }).HasDatabaseName("ix_users_status_id");
    }
}
