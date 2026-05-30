using MicroExercise.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MicroExercise.Infrastructure;

/// <summary>
/// Composition root for the Infrastructure layer. The Web host calls
/// <see cref="AddInfrastructure"/> to wire up data access (and, in later milestones,
/// the service implementations) behind Core's abstractions.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        string connectionString)
    {
        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlite(connectionString));

        return services;
    }
}
