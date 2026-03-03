using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Payments.EventDriven.Application.Interfaces;
using Payments.EventDriven.Infrastructure.HealthChecks;
using Payments.EventDriven.Infrastructure.Messaging;
using Payments.EventDriven.Infrastructure.Observability;
using Payments.EventDriven.Infrastructure.Persistence;
using Payments.EventDriven.Infrastructure.Persistence.Repositories;
using Payments.EventDriven.Infrastructure.Settings;

namespace Payments.EventDriven.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // PostgreSQL para persistência transacional
        services.AddDbContext<PaymentDbContext>(options =>
            options.UseNpgsql(
                configuration.GetConnectionString("DefaultConnection"),
                npgsql => npgsql.EnableRetryOnFailure(
                    maxRetryCount: 3,
                    maxRetryDelay: TimeSpan.FromSeconds(5),
                    errorCodesToAdd: null)));

        // Kafka
        services.Configure<KafkaSettings>(options =>
            configuration.GetSection("Kafka").Bind(options));

        // Repositories
        services.AddScoped<IPaymentRepository, PaymentRepository>();
        services.AddScoped<IOutboxRepository, OutboxRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        
        // Messaging
        services.AddSingleton<IEventPublisher, KafkaProducer>();

        // Health checks
        services.AddTransient<OutboxHealthCheck>();

        // Observability - Metrics
        services.AddSingleton<IMetricsService, LogBasedMetricsService>();

        return services;
    }
}