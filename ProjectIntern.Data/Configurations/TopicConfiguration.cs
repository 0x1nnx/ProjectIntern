using ProjectIntern.Data.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ProjectIntern.Data.Configurations;

public class TopicConfiguration : IEntityTypeConfiguration<Topic>
{
    public void Configure(EntityTypeBuilder<Topic> builder)
    {
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Id)
            .HasColumnName("Id");

        builder.Property(t => t.Name)
            .IsRequired()
            .HasMaxLength(150);

        builder.Property(t => t.Description)
            .IsRequired();

        builder.Property(t => t.Order)
            .IsRequired();

        builder.HasQueryFilter(t => !t.IsDeleted);

        builder.HasOne(t => t.InternshipSpeciality)
            .WithMany(s => s.Topics)
            .HasForeignKey(t => t.InternshipSpecialityId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(t => new { t.InternshipSpecialityId, t.Order })
            .HasDatabaseName("IX_Topic_Speciality_Order");
    }
}
