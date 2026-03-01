using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Payments.EventDriven.Application.Interfaces;
using Payments.EventDriven.Infrastructure.HealthChecks;
using Payments.EventDriven.Infrastructure.Messaging;
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
        var kafkaSettings = new KafkaSettings();
        configuration.GetSection("Kafka").Bind(kafkaSettings);
        services.AddSingleton(kafkaSettings);

        // Repositories
        services.AddScoped<IPaymentRepository, PaymentRepository>();
        services.AddScoped<IOutboxRepository, OutboxRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        
        // Messaging
        services.AddSingleton<IEventPublisher, KafkaProducer>();

        // Health checks
        services.AddTransient<OutboxHealthCheck>();

        return services;
    }
}