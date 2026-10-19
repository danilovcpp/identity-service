using Identity.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Identity.Infrastructure.Persistence.Configurations;

internal sealed class IdentityAdministratorConfiguration : IEntityTypeConfiguration<IdentityAdministrator>
{
    public void Configure(EntityTypeBuilder<IdentityAdministrator> builder)
    {
        builder.ToTable("identity_administrators", table => table.HasCheckConstraint("ck_identity_administrators_role", "role IN (0, 1)"));
        builder.HasKey(role => role.UserId);
        builder.Property(role => role.UserId).ValueGeneratedNever();
        builder.Property(role => role.Role).HasConversion<short>();
        builder.HasOne<User>().WithMany().HasForeignKey(role => role.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(role => role.GrantedBy).OnDelete(DeleteBehavior.SetNull);
        builder.HasIndex(role => role.Role).HasDatabaseName("ix_identity_administrators_role");
    }
}
