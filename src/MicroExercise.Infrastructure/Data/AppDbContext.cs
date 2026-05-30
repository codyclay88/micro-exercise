using MicroExercise.Core.Entities;
using MicroExercise.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace MicroExercise.Infrastructure.Data;

/// <summary>
/// EF Core context for the Micro-Burst Exercise Tracker, backed by ASP.NET Core Identity
/// (integer keys). Domain entity shapes are defined in <c>Data/Configurations</c> via
/// <see cref="IEntityTypeConfiguration{TEntity}"/>.
/// </summary>
public class AppDbContext(DbContextOptions<AppDbContext> options)
    : IdentityDbContext<ApplicationUser, IdentityRole<int>, int>(options)
{
    public DbSet<ExerciseType> ExerciseTypes => Set<ExerciseType>();
    public DbSet<ExercisePool> ExercisePool => Set<ExercisePool>();
    public DbSet<WorkoutLog> WorkoutLogs => Set<WorkoutLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Apply every IEntityTypeConfiguration in this assembly.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        base.ConfigureConventions(configurationBuilder);

        // SQLite has no native DateTimeOffset type, so range comparisons (used by the
        // date-range reports, spec §5.1) don't translate. Storing as an order-preserving
        // long retains the offset and makes comparisons work. Native providers
        // (SQL Server / PostgreSQL) support DateTimeOffset directly and wouldn't need this.
        configurationBuilder.Properties<DateTimeOffset>()
            .HaveConversion<DateTimeOffsetToBinaryConverter>();
    }
}
