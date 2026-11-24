using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Transfers.Infrastructure.Persistence;

namespace Transfers.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        string connectionString)
    {
        services.AddDbContext<TransfersDbContext>(options =>
            options.UseNpgsql(connectionString));

        return services;
    }
}