using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Trail.Infrastructure.Identity;

namespace Trail.Infrastructure.Persistence.Configurations.Accounts;

public sealed class AccountInvitationConfiguration : IEntityTypeConfiguration<AccountInvitation>
{
    public void Configure(EntityTypeBuilder<AccountInvitation> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Email)
            .HasMaxLength(256)
            .IsRequired();
        builder.Property(x => x.NormalizedEmail)
            .HasMaxLength(256)
            .IsRequired();
        builder.Property(x => x.Token)
            .HasMaxLength(256)
            .IsRequired();
        builder.Property(x => x.InvitedByUserId)
            .IsRequired();
        builder.Property(x => x.CreatedAt)
            .IsRequired();
        builder.Property(x => x.ExpiresAt)
            .IsRequired();

        builder.HasIndex(x => x.Token).IsUnique();
        builder.HasIndex(x => x.NormalizedEmail);
    }
}
