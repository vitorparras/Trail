using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Trail.Domain.Planning;

namespace Trail.Infrastructure.Persistence.Configurations.Planning;

public sealed class SectionConfiguration : IEntityTypeConfiguration<Section>
{
    public void Configure(EntityTypeBuilder<Section> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("Sections");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.StudyPlanId).IsRequired();
        builder.Property(x => x.Name)
            .HasMaxLength(StudyPlan.MaxSectionNameLength)
            .IsRequired();
        builder.Property(x => x.Order)
            .IsRequired();
    }
}
