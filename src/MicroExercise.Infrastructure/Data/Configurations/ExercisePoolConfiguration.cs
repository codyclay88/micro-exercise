using MicroExercise.Core.Entities;
using MicroExercise.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MicroExercise.Infrastructure.Data.Configurations;

public class ExercisePoolConfiguration : IEntityTypeConfiguration<ExercisePool>
{
    public void Configure(EntityTypeBuilder<ExercisePool> builder)
    {
        builder.ToTable("ExercisePool");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.CustomName)
            .HasMaxLength(100);

        builder.Property(p => p.IsActive)
            .HasDefaultValue(true);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(p => p.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(p => p.ExerciseType)
            .WithMany(t => t.PoolEntries)
            .HasForeignKey(p => p.ExerciseTypeId)
            .OnDelete(DeleteBehavior.Restrict);

        // Dashboard grids list a user's active entries in display order.
        builder.HasIndex(p => new { p.UserId, p.IsActive, p.SortOrder });
    }
}
