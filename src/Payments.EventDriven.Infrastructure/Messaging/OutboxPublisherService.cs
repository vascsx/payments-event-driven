using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Payments.EventDriven.Application.Interfaces;
using Payments.EventDriven.Infrastructure.Persistence;

namespace Payments.EventDriven.Infrastructure.Messaging;

public class OutboxPublisherService : BackgroundService
{
    private readonly ILogger<OutboxPublisherService> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly IEventPublisher _eventPublisher;
    private static readonly TimeSpan PollingInterval = TimeSpan.FromMilliseconds(500);
    private const int MaxOutboxRetries = 5;

    public OutboxPublisherService(
        ILogger<OutboxPublisherService> logger,
        IServiceProvider serviceProvider,
        IEventPublisher eventPublisher)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        _eventPublisher = eventPublisher;
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
        var context = scope.ServiceProvider.GetRequiredService<PaymentDbContext>();

        // FOR UPDATE SKIP LOCKED: garante que múltiplas instâncias não processem
        // as mesmas mensagens simultaneamente (safety em multi-réplica)
        await using var tx = await context.Database.BeginTransactionAsync(cancellationToken);

        int maxRetries = MaxOutboxRetries;
        var messages = await context.OutboxMessages
            .FromSql($@"
                SELECT * FROM outbox_messages
                WHERE processed_at IS NULL
                  AND retry_count < {maxRetries}
                ORDER BY created_at
                LIMIT 50
                FOR UPDATE SKIP LOCKED")
            .ToListAsync(cancellationToken);

        if (messages.Count == 0)
        {
            await tx.RollbackAsync(cancellationToken);
            return;
        }

        _logger.LogInformation("Publishing {Count} outbox messages", messages.Count);

        foreach (var message in messages)
        {
            try
            {
                var headers = new Dictionary<string, string>();
                if (message.CorrelationId is not null)
                    headers["X-Correlation-Id"] = message.CorrelationId;

                await _eventPublisher.PublishAsync(
                    message.Topic,
                    message.MessageKey,
                    message.Payload,
                    headers.Count > 0 ? headers : null,
                    cancellationToken);

                message.MarkAsProcessed();

                _logger.LogInformation(
                    "Outbox message {Id} published to topic {Topic} with key {Key}",
                    message.Id, message.Topic, message.MessageKey);
            }
            catch (Exception ex)
            {
                message.IncrementRetry();

                if (message.RetryCount >= MaxOutboxRetries)
                    _logger.LogError(ex,
                        "Outbox message {Id} exceeded {Max} retries; will be permanently skipped",
                        message.Id, MaxOutboxRetries);
                else
                    _logger.LogError(ex,
                        "Failed to publish outbox message {Id} (attempt {Retry}/{Max}); preserving order",
                        message.Id, message.RetryCount, MaxOutboxRetries);

                // Preserva ordem: para no primeiro erro
                break;
            }
        }

        // Salva todos os MarkAsProcessed() e IncrementRetry() de uma vez dentro da transação
        await context.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);
    }
}
