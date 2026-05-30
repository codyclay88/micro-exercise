using MicroExercise.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace MicroExercise.Infrastructure.Data;

/// <summary>
/// EF Core context for the Micro-Burst Exercise Tracker. Entity shapes are defined
/// in <c>Data/Configurations</c> via <see cref="IEntityTypeConfiguration{TEntity}"/>.
/// </summary>
public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<ExerciseType> ExerciseTypes => Set<ExerciseType>();
    public DbSet<ExercisePool> ExercisePool => Set<ExercisePool>();
    public DbSet<WorkoutLog> WorkoutLogs => Set<WorkoutLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Apply every IEntityTypeConfiguration in this assembly.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
