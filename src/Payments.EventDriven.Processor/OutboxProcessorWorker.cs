using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Payments.EventDriven.Application.Interfaces;
using Payments.EventDriven.Domain.Entities;
using Payments.EventDriven.Infrastructure.Persistence;

namespace Payments.EventDriven.Processor.Workers;

/// <summary>
/// Background service que processa mensagens do Outbox Pattern
/// - Roda continuamente em background
/// - Consome mensagens persistidas transacionalmente no banco
/// - Usa FOR UPDATE SKIP LOCKED para evitar duplicação em multi-instância
/// - Publica no Kafka com retry e idempotência garantida
/// - Após MaxRetries, marca mensagens como Failed
/// </summary>
public class OutboxProcessorWorker : BackgroundService
{
    private readonly ILogger<OutboxProcessorWorker> _logger;
    private readonly IServiceProvider _serviceProvider;
    private static readonly TimeSpan PollingInterval = TimeSpan.FromMilliseconds(500);
    private const int MaxRetries = 10;
    private const int BatchSize = 50;

    public OutboxProcessorWorker(
        ILogger<OutboxProcessorWorker> logger,
        IServiceProvider serviceProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("OutboxProcessorWorker started - monitoring Outbox table with {PollingInterval}ms polling", 
            PollingInterval.TotalMilliseconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessOutboxMessagesAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while processing Outbox messages");
            }

            await Task.Delay(PollingInterval, stoppingToken);
        }

        _logger.LogInformation("OutboxProcessorWorker stopped");
    }

    private async Task ProcessOutboxMessagesAsync(CancellationToken cancellationToken)
    {
        await using var scope = _serviceProvider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<PaymentDbContext>();
        var eventPublisher = scope.ServiceProvider.GetRequiredService<IEventPublisher>();

        // FOR UPDATE SKIP LOCKED garante que múltiplas instâncias não processem as mesmas mensagens
        await using var tx = await context.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            var messages = await context.OutboxMessages
                .FromSql($@"
                    SELECT * FROM outbox_messages
                    WHERE status = {OutboxMessageStatus.Pending} 
                      AND retry_count < {MaxRetries}
                    ORDER BY created_at
                    LIMIT {BatchSize}
                    FOR UPDATE SKIP LOCKED")
                .ToListAsync(cancellationToken);

            if (messages.Count == 0)
            {
                await tx.RollbackAsync(cancellationToken);
                return;
            }

            _logger.LogInformation("Processing {Count} messages from Outbox", messages.Count);

            foreach (var message in messages)
            {
                try
                {
                    // Verifica backoff exponencial antes de tentar retry
                    if (message.RetryCount > 0)
                    {
                        var delaySeconds = Math.Min(Math.Pow(2, message.RetryCount), 3600); // Max 1 hora
                        var timeSinceLastRetry = DateTime.UtcNow - (message.LastRetryAt ?? message.CreatedAt);

                        if (timeSinceLastRetry.TotalSeconds < delaySeconds)
                        {
                            _logger.LogDebug(
                                "Outbox message {Id} not ready for retry yet (waiting {Delay}s)",
                                message.Id, delaySeconds);
                            continue;
                        }
                    }

                    // Marca como Processing otimisticamente
                    message.MarkAsProcessing();

                    // Publica no Kafka
                    var headers = message.CorrelationId is not null
                        ? new Dictionary<string, string> { ["X-Correlation-Id"] = message.CorrelationId }
                        : null;

                    await eventPublisher.PublishAsync(
                        message.Topic,
                        message.MessageKey,
                        message.Payload,
                        headers,
                        cancellationToken);

                    // Sucesso! Marca como processada
                    message.MarkAsProcessed();

                    _logger.LogInformation(
                        "Outbox message {OutboxId} successfully published to {Topic} after {Retries} retries. CorrelationId: {CorrelationId}",
                        message.Id, message.Topic, message.RetryCount, message.CorrelationId);
                }
                catch (Exception ex)
                {
                    message.IncrementRetry(ex.Message);

                    if (message.RetryCount >= MaxRetries)
                    {
                        message.MarkAsFailed(ex.Message);

                        _logger.LogError(ex,
                            "Outbox message {OutboxId} exceeded {MaxRetries} retries and marked as Failed. " +
                            "Topic: {Topic}, Key: {Key}, CorrelationId: {CorrelationId}",
                            message.Id, MaxRetries, message.Topic, message.MessageKey, message.CorrelationId);
                    }
                    else
                    {
                        _logger.LogWarning(ex,
                            "Failed to publish Outbox message {OutboxId} (attempt {Retry}/{MaxRetries}). Will retry later. CorrelationId: {CorrelationId}",
                            message.Id, message.RetryCount, MaxRetries, message.CorrelationId);
                    }

                    // Volta status para Pending para permitir retry
                    if (message.Status == OutboxMessageStatus.Processing && message.RetryCount < MaxRetries)
                    {
                        typeof(OutboxMessage)
                            .GetProperty(nameof(OutboxMessage.Status))!
                            .SetValue(message, OutboxMessageStatus.Pending);
                    }
                }
            }

            // Salva todas as mudanças dentro da transação
            await context.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync(cancellationToken);
            _logger.LogError(ex, "Failed to process outbox batch, transaction rolled back");
        }
    }
}
