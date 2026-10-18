using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Trail.Domain.Planning;

namespace Trail.Infrastructure.Persistence.Configurations.Planning;

public sealed class StudyPlanConfiguration : IEntityTypeConfiguration<StudyPlan>
{
    public void Configure(EntityTypeBuilder<StudyPlan> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("StudyPlans");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.OwnerId).IsRequired();
        builder.HasIndex(x => x.OwnerId);
        builder.Property(x => x.Name)
            .HasMaxLength(StudyPlan.MaxNameLength)
            .IsRequired();
        builder.Property(x => x.Description)
            .HasMaxLength(StudyPlan.MaxDescriptionLength)
            .IsRequired();
        builder.Property(x => x.ScoringRule)
            .IsRequired();
        builder.Property(x => x.ExamDate);
        builder.Property(x => x.DefaultMockExamQuestions)
            .IsRequired();
        builder.Property(x => x.FitnessTestEnabled)
            .IsRequired();

        builder.OwnsOne(x => x.DefaultPerformancePeriod, p =>
        {
            p.Property(x => x.Kind).IsRequired();
            p.Property(x => x.Days);
            p.Property(x => x.StartDate);
            p.Property(x => x.EndDate);
        });

        builder.OwnsOne(x => x.PerformanceBands, pb =>
        {
            pb.Property(x => x.LowThreshold).IsRequired();
            pb.Property(x => x.HighThreshold).IsRequired();
        });

        builder.HasMany(x => x.Sections)
            .WithOne()
            .HasForeignKey(x => x.StudyPlanId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(x => x.Sections)
            .HasField("_sections")
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany(x => x.Outline)
            .WithOne()
            .HasForeignKey(x => x.StudyPlanId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(x => x.Outline)
            .HasField("_outline")
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
