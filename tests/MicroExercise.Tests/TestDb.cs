using MicroExercise.Infrastructure.Data;
using MicroExercise.Infrastructure.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace MicroExercise.Tests;

/// <summary>
/// An isolated, in-memory SQLite database for a single test. The connection is held
/// open for the lifetime of the instance (closing it would drop the schema). Schema is
/// created from the model via EnsureCreated, which also applies HasData seeds (the global
/// exercise catalog and the demo user).
/// </summary>
public sealed class TestDb : IDisposable
{
    private readonly SqliteConnection _connection;

    public TestDb()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;

        Context = new AppDbContext(options);
        Context.Database.EnsureCreated();

        // Seed the primary test user (id 1). The global exercise catalog is seeded via
        // HasData; the demo user used to be too, but Identity now owns the users table.
        Context.Users.Add(new ApplicationUser
        {
            Id = PrimaryUserId,
            UserName = "primary@test.local",
            Email = "primary@test.local",
            DisplayName = "Primary"
        });
        Context.SaveChanges();
    }

    /// <summary>The user id seeded by this fixture; tests act as this user.</summary>
    public const int PrimaryUserId = 1;

    public AppDbContext Context { get; }

    /// <summary>A fresh context over the same database — useful to verify persistence across instances.</summary>
    public AppDbContext NewContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;
        return new AppDbContext(options);
    }

    public void Dispose()
    {
        Context.Dispose();
        _connection.Dispose();
    }
}
