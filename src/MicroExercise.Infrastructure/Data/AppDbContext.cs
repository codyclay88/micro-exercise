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

        // DateTimeOffset handling is provider-specific:
        if (Database.IsSqlite())
        {
            // SQLite (used by the in-memory test DB) has no native DateTimeOffset type, so
            // range comparisons (the date-range reports, spec §5.1) don't translate. Storing
            // as an order-preserving long makes comparisons work by UTC instant.
            configurationBuilder.Properties<DateTimeOffset>()
                .HaveConversion<DateTimeOffsetToBinaryConverter>();
        }
        else
        {
            // PostgreSQL maps DateTimeOffset to `timestamptz` and rejects any non-UTC offset
            // (the app writes DateTimeOffset.Now, and history edits accept a client timestamp).
            // Normalizing to UTC on write keeps every write site safe and preserves
            // instant-ordered range comparisons.
            configurationBuilder.Properties<DateTimeOffset>()
                .HaveConversion<DateTimeOffsetUtcConverter>();
        }
    }

    /// <summary>
    /// Normalizes a <see cref="DateTimeOffset"/> to UTC on the way to the database. Required
    /// for Npgsql's <c>timestamptz</c> mapping, which only accepts a zero (UTC) offset.
    /// </summary>
    private sealed class DateTimeOffsetUtcConverter()
        : ValueConverter<DateTimeOffset, DateTimeOffset>(v => v.ToUniversalTime(), v => v);
}
