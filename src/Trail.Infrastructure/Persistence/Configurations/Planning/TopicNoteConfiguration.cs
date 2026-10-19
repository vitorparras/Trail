using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Trail.Domain.Planning;

namespace Trail.Infrastructure.Persistence.Configurations.Planning;

public sealed class TopicNoteConfiguration : IEntityTypeConfiguration<TopicNote>
{
    public void Configure(EntityTypeBuilder<TopicNote> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("TopicNotes");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.OwnerId).IsRequired();
        builder.HasIndex(x => x.OwnerId);
        builder.Property(x => x.TopicId).IsRequired();
        builder.HasIndex(x => x.TopicId).IsUnique();
        builder.HasOne<Topic>()
            .WithOne()
            .HasForeignKey<TopicNote>(x => x.TopicId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Property(x => x.Markdown)
            .HasMaxLength(TopicNote.MaxMarkdownLength)
            .IsRequired();
    }
}
