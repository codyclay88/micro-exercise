using MicroExercise.Core.Entities;
using MicroExercise.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MicroExercise.Infrastructure.Data.Configurations;

public class GoalConfiguration : IEntityTypeConfiguration<Goal>
{
    public void Configure(EntityTypeBuilder<Goal> builder)
    {
        builder.ToTable("Goals");

        builder.HasKey(g => g.Id);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(g => g.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        // A goal targets a pool item; deleting a pool item is a soft-delete (IsActive), so the
        // FK never cascades — Restrict matches the rest of the schema.
        builder.HasOne(g => g.ExercisePool)
            .WithMany()
            .HasForeignKey(g => g.ExercisePoolId)
            .OnDelete(DeleteBehavior.Restrict);

        // Active-goal lookups by user, ordered by deadline.
        builder.HasIndex(g => new { g.UserId, g.Deadline });
    }
}
