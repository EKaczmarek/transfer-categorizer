using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Transfers.Application;
using Transfers.Infrastructure.Messaging;
using Transfers.Infrastructure.Persistence;

namespace Transfers.Infrastructure;

public static class DependencyInjection
{
    // Api: baza + outbox (Api tylko zapisuje, niczego nie publikuje)
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        string connectionString)
    {
        services.AddDbContext<TransfersDbContext>(options =>
            options.UseNpgsql(connectionString));

        services.AddScoped<IOutbox, EfOutbox>();

        return services;
    }

    public static IServiceCollection AddMessaging(
    this IServiceCollection services,
    IConfiguration configuration)
    {
        services.Configure<RabbitMqOptions>(configuration.GetSection("RabbitMq"));

        services.AddSingleton<RabbitMqConnection>();
        services.AddSingleton<IMessagePublisher, RabbitMqPublisher>();

        return services;
    }

}