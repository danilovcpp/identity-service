using Identity.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Identity.Infrastructure.Persistence.Configurations;

internal sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("refresh_tokens", table =>
        {
            table.HasCheckConstraint("ck_refresh_hash", "token_hash ~ '^[0-9a-f]{64}$'");
            table.HasCheckConstraint("ck_refresh_times", "expires_at > created_at AND expires_at <= created_at + interval '168 hours' AND (consumed_at IS NULL OR (consumed_at >= created_at AND consumed_at < expires_at)) AND (revoked_at IS NULL OR revoked_at >= COALESCE(consumed_at, created_at))");
            table.HasCheckConstraint("ck_refresh_replacement", "(consumed_at IS NULL) = (replaced_by_token_id IS NULL) AND (replaced_by_token_id IS NULL OR replaced_by_token_id <> id)");
        });
        builder.HasKey(token => token.Id);
        builder.Property(token => token.Id).ValueGeneratedNever();
        builder.Property(token => token.TokenHash).HasColumnType("character(64)").IsRequired();
        builder.Property<uint>("Version").IsRowVersion();
        builder.HasOne<Session>().WithMany().HasForeignKey(token => token.SessionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasAlternateKey(token => new { token.Id, token.SessionId }).HasName("ak_refresh_tokens_id_session");
        builder.HasOne<RefreshToken>().WithMany()
            .HasForeignKey(token => new { token.ReplacedByTokenId, token.SessionId })
            .HasPrincipalKey(token => new { token.Id, token.SessionId }).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(token => token.TokenHash).IsUnique().HasDatabaseName("ux_refresh_tokens_hash");
        builder.HasIndex(token => token.SessionId).HasDatabaseName("ix_refresh_tokens_session");
        builder.HasIndex(token => token.ExpiresAt).HasDatabaseName("ix_refresh_tokens_expiry");
    }
}
