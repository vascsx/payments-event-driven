using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Payments.EventDriven.Application.Interfaces;
using Payments.EventDriven.Domain.Entities;
using Payments.EventDriven.Infrastructure.Persistence;

namespace Payments.EventDriven.ProcessPayment.Workers;

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

        // Usa a estratégia de execução para suportar retry com transações
        var strategy = context.Database.CreateExecutionStrategy();

        await strategy.ExecuteAsync(async () =>
        {
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

            var processedCount = 0;
            var skippedCount = 0;

                foreach (var message in messages)
                {
                    try
                    {
                        if (message.RetryCount > 0)
                        {
                            var baseDelay = Math.Pow(2, message.RetryCount);
                            var jitter = Random.Shared.NextDouble() * 0.3 * baseDelay; 
                            var delaySeconds = Math.Min(baseDelay + jitter, 3600); 
                            var timeSinceLastRetry = DateTime.UtcNow - (message.LastRetryAt ?? message.CreatedAt);

                            if (timeSinceLastRetry.TotalSeconds < delaySeconds)
                            {
                                _logger.LogDebug(
                                    "Outbox message {Id} not ready for retry yet (waiting {Delay:F1}s with jitter)",
                                    message.Id, delaySeconds);
                                skippedCount++;
                                continue;
                            }
                        }

                        // Marca como Processing otimisticamente
                        message.MarkAsProcessing();

                        // Publica no Kafka com headers (correlation-id + event-type para roteamento)
                        var headers = new Dictionary<string, string>
                        {
                            ["event-type"] = message.EventType
                        };
                        
                        if (message.CorrelationId is not null)
                        {
                            headers["X-Correlation-Id"] = message.CorrelationId;
                        }

                        await eventPublisher.PublishAsync(
                            message.Topic,
                            message.MessageKey,
                            message.Payload,
                            headers,
                            cancellationToken);

                        processedCount++;
                        message.MarkAsProcessed();
                        context.Update(message); 

                        _logger.LogInformation(
                            "Outbox message {OutboxId} successfully published to {Topic} after {Retries} retries. CorrelationId: {CorrelationId}",
                            message.Id, message.Topic, message.RetryCount, message.CorrelationId);
                    }
                    catch (Exception ex)
                    {
                        if (IsInfrastructureFailure(ex))
                        {
                            _logger.LogWarning(ex,
                                "Infrastructure failure for outbox message {OutboxId}. Will retry without incrementing counter. Error: {Error}",
                                message.Id, ex.Message);
                            
                            skippedCount++;
                            continue;
                        }

                        message.IncrementRetry(ex.Message);

                        if (message.RetryCount >= MaxRetries)
                        {
                            message.MarkAsFailed(ex.Message);

                            try
                            {
                                if (Guid.TryParse(message.MessageKey, out var paymentId))
                                {
                                    var payment = await context.Payments
                                        .FirstOrDefaultAsync(p => p.Id == paymentId, cancellationToken);

                                    if (payment != null && payment.Status == Domain.Enums.PaymentStatus.Pending)
                                    {
                                        payment.MarkAsFailed($"Failed to publish event after {MaxRetries} retries: {ex.Message}");
                                        context.Payments.Update(payment);
                                        
                                        _logger.LogWarning(
                                            "Payment {PaymentId} marked as Failed due to outbox message failure. CorrelationId: {CorrelationId}",
                                            paymentId, message.CorrelationId);
                                    }
                                }
                            }
                            catch (Exception paymentEx)
                            {
                                _logger.LogError(paymentEx,
                                    "Failed to mark Payment {MessageKey} as Failed. Outbox message will be marked as Failed anyway.",
                                    message.MessageKey);
                            }

                            _logger.LogError(ex,
                                "Outbox message {OutboxId} exceeded {MaxRetries} retries and marked as Failed. " +
                                "Topic: {Topic}, Key: {Key}, CorrelationId: {CorrelationId}. Sending to DLQ.",
                                message.Id, MaxRetries, message.Topic, message.MessageKey, message.CorrelationId);

                            try
                            {
                                await SendFailedMessageToDlqAsync(message, eventPublisher, cancellationToken);
                            }
                            catch (Exception dlqEx)
                            {
                                _logger.LogCritical(dlqEx,
                                    "CRITICAL: Failed to send outbox message {OutboxId} to DLQ after {MaxRetries} retries!",
                                    message.Id, MaxRetries);
                            }
                        }
                        else
                        {
                            _logger.LogWarning(ex,
                                "Failed to publish Outbox message {OutboxId} (attempt {Retry}/{MaxRetries}). Will retry later. CorrelationId: {CorrelationId}",
                                message.Id, message.RetryCount, MaxRetries, message.CorrelationId);
                        }

                        // Garante que o EF rastreie as mudanças de retry/failed
                        context.Update(message);
                    }
                }

                if (processedCount > 0 || skippedCount < messages.Count)
                {
                    await context.SaveChangesAsync(cancellationToken);
                    await tx.CommitAsync(cancellationToken);

                    _logger.LogInformation(
                        "Batch completed: {Processed} processed, {Skipped} skipped, {Failed} failed",
                        processedCount, skippedCount, messages.Count - processedCount - skippedCount);
                }
                else
                {
                    await tx.RollbackAsync(cancellationToken);
                    _logger.LogDebug("All messages in backoff, skipping batch");
                    
                    await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
                }
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync(cancellationToken);
                _logger.LogError(ex, "Failed to process outbox batch, transaction rolled back");
                throw; // Re-throw para a estratégia de execução lidar com retry se necessário
            }
        });
    }

    /// <summary>
    /// Envia mensagens do outbox que falharam permanentemente para DLQ
    /// </summary>
    private async Task SendFailedMessageToDlqAsync(
        OutboxMessage message,
        IEventPublisher eventPublisher,
        CancellationToken cancellationToken)
    {
        var dlqTopic = $"{message.Topic}-outbox-dlq";

        var headers = new Dictionary<string, string>
        {
            ["dlq-reason"] = message.LastError ?? "Max retries exceeded",
            ["dlq-original-topic"] = message.Topic,
            ["dlq-outbox-id"] = message.Id.ToString(),
            ["dlq-retry-count"] = message.RetryCount.ToString(),
            ["dlq-timestamp"] = DateTime.UtcNow.ToString("O"),
            ["event-type"] = message.EventType
        };

        if (message.CorrelationId is not null)
        {
            headers["X-Correlation-Id"] = message.CorrelationId;
        }

        await eventPublisher.PublishAsync(
            dlqTopic,
            message.MessageKey,
            message.Payload,
            headers,
            cancellationToken);

        _logger.LogWarning(
            "Outbox message {OutboxId} sent to DLQ topic {DlqTopic}. Original topic: {Topic}",
            message.Id, dlqTopic, message.Topic);
    }

    /// <summary>
    /// Determines if an exception is due to infrastructure failure (not business logic).
    /// Infrastructure failures should not count against retry limits.
    /// </summary>
    private static bool IsInfrastructureFailure(Exception ex)
    {
        return ex.Message.Contains("circuit breaker is OPEN", StringComparison.OrdinalIgnoreCase)
            || ex.Message.Contains("did not persist", StringComparison.OrdinalIgnoreCase)
            || ex is Confluent.Kafka.KafkaException
            || ex is TimeoutException
            || ex.Message.Contains("Kafka", StringComparison.OrdinalIgnoreCase)
            || ex.Message.Contains("broker", StringComparison.OrdinalIgnoreCase);
    }
}
