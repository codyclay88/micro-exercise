using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace MicroExercise.Infrastructure.Data;

/// <summary>
/// Lets EF Core design-time tooling (migrations, scaffolding) construct the context
/// without booting the Web host. The connection string here is only used at design
/// time; the running app supplies its own via <c>AddInfrastructure</c>.
/// </summary>
public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Port=55432;Database=microburst;Username=postgres;Password=postgres")
            .Options;

        return new AppDbContext(options);
    }
}
