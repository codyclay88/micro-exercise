using MicroExercise.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MicroExercise.Infrastructure.Data.Configurations;

public class WorkoutLogConfiguration : IEntityTypeConfiguration<WorkoutLog>
{
    public void Configure(EntityTypeBuilder<WorkoutLog> builder)
    {
        builder.ToTable("WorkoutLogs");

        builder.HasKey(l => l.Id);

        builder.HasOne(l => l.ExercisePool)
            .WithMany(p => p.Logs)
            .HasForeignKey(l => l.ExercisePoolId)
            .OnDelete(DeleteBehavior.Restrict);

        // Report aggregation filters by pool and a date range (spec §4.3 / §5.1).
        builder.HasIndex(l => new { l.ExercisePoolId, l.Timestamp });
    }
}
