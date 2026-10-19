using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Trail.Domain.Planning;

namespace Trail.Infrastructure.Persistence.Configurations.Planning;

public sealed class SubjectPlacementConfiguration : IEntityTypeConfiguration<SubjectPlacement>
{
    public void Configure(EntityTypeBuilder<SubjectPlacement> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("SubjectPlacements");
        builder.HasKey(x => new { x.StudyPlanId, x.SubjectId });
        builder.Property(x => x.SectionId);
        builder.Property(x => x.Order).IsRequired();
    }
}
