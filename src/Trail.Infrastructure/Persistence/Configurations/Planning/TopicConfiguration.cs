using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Trail.Domain.Planning;

namespace Trail.Infrastructure.Persistence.Configurations.Planning;

public sealed class TopicConfiguration : IEntityTypeConfiguration<Topic>
{
    public void Configure(EntityTypeBuilder<Topic> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("Topics");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.SubjectId).IsRequired();
        builder.HasIndex(x => x.SubjectId);
        builder.Property(x => x.ParentId);
        builder.HasIndex(x => x.ParentId);

        var topicCodeConverter = new ValueConverter<TopicCode?, string?>(
            c => c == null ? null : c.Value,
            v => string.IsNullOrEmpty(v) ? null : TopicCode.Create(v).Data);

        builder.Property(x => x.Code)
            .HasConversion(topicCodeConverter)
            .HasMaxLength(100);

        builder.HasIndex(x => new { x.SubjectId, x.Code }).IsUnique();

        builder.Property(x => x.Title)
            .HasMaxLength(Subject.MaxTopicTitleLength)
            .IsRequired();
        builder.Property(x => x.Order)
            .IsRequired();
        builder.Property(x => x.Status)
            .IsRequired();
        builder.Property(x => x.SearchText)
            .HasMaxLength(650)
            .IsRequired();
        builder.HasIndex(x => x.SearchText);
    }
}
