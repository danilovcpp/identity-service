using Identity.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Identity.Infrastructure.Persistence.Configurations;

internal sealed class CredentialConfiguration : IEntityTypeConfiguration<Credential>
{
    public void Configure(EntityTypeBuilder<Credential> builder)
    {
        builder.ToTable("credentials", table =>
        {
            table.HasCheckConstraint("ck_credentials_hash", "length(password_hash) BETWEEN 1 AND 4096");
            table.HasCheckConstraint("ck_credentials_times", "password_changed_at >= created_at");
        });
        builder.HasKey(credential => credential.UserId);
        builder.Property(credential => credential.UserId).ValueGeneratedNever();
        builder.Property(credential => credential.PasswordHash).HasColumnType("text").IsRequired();
        builder.Property<uint>("Version").IsRowVersion();
        builder.HasOne<User>().WithOne().HasForeignKey<Credential>(credential => credential.UserId).OnDelete(DeleteBehavior.Restrict);
    }
}
