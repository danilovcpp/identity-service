using Identity.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Identity.Infrastructure.Persistence.Configurations;

internal sealed class OneTimeTokenConfiguration : IEntityTypeConfiguration<OneTimeToken>
{
    public void Configure(EntityTypeBuilder<OneTimeToken> builder)
    {
        builder.ToTable("one_time_tokens", table =>
        {
            table.HasCheckConstraint("ck_one_time_hash", "token_hash ~ '^[0-9a-f]{64}$'");
            table.HasCheckConstraint("ck_one_time_purpose", "purpose IN (0, 1, 2)");
            table.HasCheckConstraint("ck_one_time_times", "expires_at > created_at AND expires_at <= created_at + CASE purpose WHEN 0 THEN interval '24 hours' WHEN 2 THEN interval '72 hours' ELSE interval '30 minutes' END AND (consumed_at IS NULL OR (consumed_at >= created_at AND consumed_at < expires_at))");
        });
        builder.HasKey(token => token.Id);
        builder.Property(token => token.Id).ValueGeneratedNever();
        builder.Property(token => token.TokenHash).HasColumnType("character(64)").IsRequired();
        builder.Property(token => token.Purpose).HasConversion<short>();
        builder.Property<uint>("Version").IsRowVersion();
        builder.HasOne<User>().WithMany().HasForeignKey(token => token.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(token => token.TokenHash).IsUnique().HasDatabaseName("ux_one_time_tokens_hash");
        builder.HasIndex(token => token.ExpiresAt).HasDatabaseName("ix_one_time_tokens_expiry");
    }
}
