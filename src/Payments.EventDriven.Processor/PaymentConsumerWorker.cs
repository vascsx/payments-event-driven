using System.Text;
using System.Text.Json;
using Confluent.Kafka;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Payments.EventDriven.Application.Constants;
using Payments.EventDriven.Application.Events;
using Payments.EventDriven.Application.Interfaces;
using Payments.EventDriven.Domain.Enums;
using Payments.EventDriven.Infrastructure.Persistence;
using Payments.EventDriven.Infrastructure.Settings;

namespace Payments.EventDriven.Processor.Workers;

public class PaymentConsumerWorker : BackgroundService
{
    private readonly ILogger<PaymentConsumerWorker> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly KafkaSettings _settings;
    private readonly IKafkaProducer _kafkaProducer;
    private const int MaxRetries = 3;

    public PaymentConsumerWorker(
        ILogger<PaymentConsumerWorker> logger,
        IServiceProvider serviceProvider,
        KafkaSettings settings,
        IKafkaProducer kafkaProducer)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        _settings = settings;
        _kafkaProducer = kafkaProducer;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var config = new ConsumerConfig
        {
            BootstrapServers = _settings.BootstrapServers,
            GroupId = _settings.GroupId,
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false,
            EnableAutoOffsetStore = false,
            AllowAutoCreateTopics = true
        };

        using var consumer = new ConsumerBuilder<string, string>(config).Build();
        consumer.Subscribe(_settings.Topic);

        _logger.LogInformation("Subscribed to topic {Topic}", _settings.Topic);

        while (!stoppingToken.IsCancellationRequested)
        {
            ConsumeResult<string, string>? result = null;
            string? correlationId = null;
            try
            {
                result = consumer.Consume(stoppingToken);

                if (result?.Message?.Value is null) continue;

                correlationId = ExtractHeader(result.Message.Headers, "X-Correlation-Id");

                var @event = JsonSerializer.Deserialize<PaymentCreatedEvent>(result.Message.Value);
                if (@event is null)
                {
                    _logger.LogWarning("Failed to deserialize message at offset {Offset}, skipping", result.Offset);
                    consumer.StoreOffset(result);
                    consumer.Commit(result);
                    continue;
                }

                using (_logger.BeginScope(new Dictionary<string, object?>
                {
                    ["PaymentId"] = @event.PaymentId,
                    ["CorrelationId"] = correlationId
                }))
                {
                    await ProcessWithRetryAsync(@event, stoppingToken);
                }

                consumer.StoreOffset(result);
                consumer.Commit(result);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (ConsumeException ex)
            {
                _logger.LogError(ex, "Kafka consume error: {Reason}", ex.Error.Reason);
                await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "All retries exhausted processing message. Publishing to DLQ");
                if (result is not null)
                {
                    try
                    {
                        var dlqHeaders = new Dictionary<string, string>
                        {
                            ["X-Error-Message"] = ex.Message,
                            ["X-Original-Topic"] = _settings.Topic
                        };
                        if (correlationId is not null) dlqHeaders["X-Correlation-Id"] = correlationId;

                        await _kafkaProducer.PublishAsync(
                            KafkaTopics.PaymentCreatedDlq,
                            result.Message.Key,
                            result.Message.Value,
                            stoppingToken,
                            dlqHeaders);

                        _logger.LogWarning("Message routed to DLQ topic {Dlq}", KafkaTopics.PaymentCreatedDlq);
                    }
                    catch (Exception dlqEx)
                    {
                        _logger.LogError(dlqEx, "Failed to publish to DLQ; committing offset anyway to prevent poison pill");
                    }

                    consumer.StoreOffset(result);
                    consumer.Commit(result);
                }

                await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);
            }
        }

        consumer.Close();
    }

    private static string? ExtractHeader(Headers? headers, string key)
    {
        if (headers is null) return null;
        return headers.TryGetLastBytes(key, out var bytes)
            ? Encoding.UTF8.GetString(bytes)
            : null;
    }

    private async Task ProcessWithRetryAsync(PaymentCreatedEvent @event, CancellationToken cancellationToken)
    {
        var attempts = 0;
        while (true)
        {
            try
            {
                await ProcessPaymentAsync(@event, cancellationToken);
                return;
            }
            catch (Exception ex) when (++attempts < MaxRetries)
            {
                var delay = TimeSpan.FromSeconds(Math.Pow(2, attempts));
                _logger.LogWarning(ex,
                    "Failed to process payment {PaymentId}, attempt {Attempt}/{MaxRetries}. Retrying in {Delay}s",
                    @event.PaymentId, attempts, MaxRetries, delay.TotalSeconds);
                await Task.Delay(delay, cancellationToken);
            }
        }
    }

    private async Task ProcessPaymentAsync(PaymentCreatedEvent @event, CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<PaymentDbContext>();

        await using var tx = await context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var payment = await context.Payments
                .FirstOrDefaultAsync(p => p.Id == @event.PaymentId, cancellationToken);

            if (payment is null)
            {
                _logger.LogWarning("Payment {PaymentId} not found in database, skipping", @event.PaymentId);
                await tx.RollbackAsync(cancellationToken);
                return;
            }

            // Idempotency: skip if already processed
            if (payment.Status != PaymentStatus.Pending)
            {
                _logger.LogInformation(
                    "Payment {PaymentId} already in status {Status}, skipping (idempotent)",
                    @event.PaymentId, payment.Status);
                await tx.RollbackAsync(cancellationToken);
                return;
            }

            payment.MarkAsProcessed();
            await context.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);

            _logger.LogInformation("Payment {PaymentId} successfully marked as Processed", @event.PaymentId);
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken);
            throw;
        }
    }
}