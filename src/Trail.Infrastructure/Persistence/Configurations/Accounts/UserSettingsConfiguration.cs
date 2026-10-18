using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Trail.Domain.Accounts;

namespace Trail.Infrastructure.Persistence.Configurations.Accounts;

public sealed class UserSettingsConfiguration : IEntityTypeConfiguration<UserSettings>
{
    public void Configure(EntityTypeBuilder<UserSettings> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.HasKey(x => x.Id);
        builder.Property(x => x.TimeZoneId)
            .HasMaxLength(100)
            .IsRequired();
        builder.Property(x => x.PomodoroFocusMinutes)
            .IsRequired();
        builder.Property(x => x.PomodoroBreakMinutes)
            .IsRequired();
        builder.Property(x => x.OwnerId)
            .IsRequired();
    }
}
