using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Trail.Domain.Planning;

namespace Trail.Infrastructure.Persistence.Configurations.Planning;

public sealed class SubjectConfiguration : IEntityTypeConfiguration<Subject>
{
    public void Configure(EntityTypeBuilder<Subject> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("Subjects");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.OwnerId).IsRequired();
        builder.HasIndex(x => x.OwnerId);
        builder.Property(x => x.StudyPlanId).IsRequired();
        builder.HasIndex(x => x.StudyPlanId);
        builder.HasOne<StudyPlan>()
            .WithMany()
            .HasForeignKey(x => x.StudyPlanId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Property(x => x.Name)
            .HasMaxLength(Subject.MaxNameLength)
            .IsRequired();

        builder.HasMany(x => x.Topics)
            .WithOne()
            .HasForeignKey(x => x.SubjectId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(x => x.Topics)
            .HasField("_topics")
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
