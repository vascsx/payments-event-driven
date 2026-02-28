using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Payments.EventDriven.Application.Interfaces;

namespace Payments.EventDriven.Infrastructure.Messaging;

public class OutboxPublisherService : BackgroundService
{
    private readonly ILogger<OutboxPublisherService> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly IKafkaProducer _kafkaProducer;
    private static readonly TimeSpan PollingInterval = TimeSpan.FromSeconds(5);

    public OutboxPublisherService(
        ILogger<OutboxPublisherService> logger,
        IServiceProvider serviceProvider,
        IKafkaProducer kafkaProducer)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        _kafkaProducer = kafkaProducer;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("OutboxPublisherService started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PublishPendingMessagesAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while publishing outbox messages");
            }

            await Task.Delay(PollingInterval, stoppingToken);
        }
    }

    private async Task PublishPendingMessagesAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var outboxRepository = scope.ServiceProvider.GetRequiredService<IOutboxRepository>();

        var messages = await outboxRepository.GetPendingAsync(50, cancellationToken);

        if (messages.Count == 0) return;

        _logger.LogInformation("Publishing {Count} outbox messages", messages.Count);

        foreach (var message in messages)
        {
            try
            {
                var headers = new Dictionary<string, string>();
                if (message.CorrelationId is not null)
                    headers["X-Correlation-Id"] = message.CorrelationId;

                await _kafkaProducer.PublishAsync(
                    message.Topic,
                    message.MessageKey,
                    message.Payload,
                    cancellationToken,
                    headers.Count > 0 ? headers : null);

                message.MarkAsProcessed();

                _logger.LogInformation(
                    "Outbox message {Id} published to topic {Topic} with key {Key}",
                    message.Id, message.Topic, message.MessageKey);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to publish outbox message {Id}", message.Id);
                // Stop here to preserve ordering; next poll will retry
                break;
            }
        }

        await outboxRepository.SaveChangesAsync(cancellationToken);
    }
}
