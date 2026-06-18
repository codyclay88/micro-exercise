using MicroExercise.Core.Entities;
using MicroExercise.Core.Enums;
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

        // Optional per-burst resistance (spec §3). Enums persist as their string name (matching
        // the TrackingType convention); the default backfills existing rows to "Bodyweight".
        builder.Property(l => l.ResistanceType)
            .HasConversion<string>()
            .HasMaxLength(20)
            .HasDefaultValue(ResistanceType.Bodyweight);

        builder.Property(l => l.WeightUnit)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(l => l.ResistanceAmount)
            .HasPrecision(6, 2);

        builder.Property(l => l.BandLabel)
            .HasMaxLength(40);

        // Report aggregation filters by pool and a date range (spec §4.3 / §5.1).
        builder.HasIndex(l => new { l.ExercisePoolId, l.Timestamp });
    }
}
