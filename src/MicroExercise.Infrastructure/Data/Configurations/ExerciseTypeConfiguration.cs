using MicroExercise.Core.Entities;
using MicroExercise.Core.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MicroExercise.Infrastructure.Data.Configurations;

public class ExerciseTypeConfiguration : IEntityTypeConfiguration<ExerciseType>
{
    public void Configure(EntityTypeBuilder<ExerciseType> builder)
    {
        builder.ToTable("ExerciseTypes");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Name)
            .IsRequired()
            .HasMaxLength(50);

        // Persist the enum as its string name (spec §3: DefaultTrackingType VARCHAR(20)).
        builder.Property(t => t.DefaultTrackingType)
            .HasConversion<string>()
            .HasMaxLength(20);

        // Global lookup catalog available to every user's pool.
        builder.HasData(
            new ExerciseType { Id = 1, Name = "Push-ups", DefaultTrackingType = TrackingType.Reps },
            new ExerciseType { Id = 2, Name = "Pull-ups", DefaultTrackingType = TrackingType.Reps },
            new ExerciseType { Id = 3, Name = "Bodyweight Squats", DefaultTrackingType = TrackingType.Reps },
            new ExerciseType { Id = 4, Name = "KB Swings", DefaultTrackingType = TrackingType.Reps },
            new ExerciseType { Id = 5, Name = "Lunges", DefaultTrackingType = TrackingType.Reps },
            new ExerciseType { Id = 6, Name = "Dead Hang", DefaultTrackingType = TrackingType.Seconds },
            new ExerciseType { Id = 7, Name = "Plank", DefaultTrackingType = TrackingType.Seconds },
            new ExerciseType { Id = 8, Name = "Wall Sit", DefaultTrackingType = TrackingType.Seconds }
        );
    }
}
