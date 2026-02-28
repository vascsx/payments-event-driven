using System.Text.Json;
using Confluent.Kafka;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Payments.EventDriven.Application.Events;
using Payments.EventDriven.Domain.Enums;
using Payments.EventDriven.Infrastructure.Persistence;
using Payments.EventDriven.Infrastructure.Settings;

namespace Payments.EventDriven.Processor.Workers;

public class PaymentConsumerWorker : BackgroundService
{
    private readonly ILogger<PaymentConsumerWorker> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly KafkaSettings _settings;
    private const int MaxRetries = 3;

    public PaymentConsumerWorker(
        ILogger<PaymentConsumerWorker> logger,
        IServiceProvider serviceProvider,
        KafkaSettings settings)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        _settings = settings;
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
            try
            {
                result = consumer.Consume(stoppingToken);

                if (result?.Message?.Value is null) continue;

                var @event = JsonSerializer.Deserialize<PaymentCreatedEvent>(result.Message.Value);
                if (@event is null)
                {
                    _logger.LogWarning("Failed to deserialize message at offset {Offset}, skipping", result.Offset);
                    consumer.StoreOffset(result);
                    consumer.Commit(result);
                    continue;
                }

                using (_logger.BeginScope(new Dictionary<string, object> { ["PaymentId"] = @event.PaymentId }))
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
                _logger.LogError(ex, "Unhandled error processing message, committing offset to avoid poison pill");
                if (result is not null)
                {
                    // Publish to DLQ here if implemented
                    consumer.StoreOffset(result);
                    consumer.Commit(result);
                }
                await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);
            }
        }

        consumer.Close();
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

        var payment = await context.Payments
            .FirstOrDefaultAsync(p => p.Id == @event.PaymentId, cancellationToken);

        if (payment is null)
        {
            _logger.LogWarning("Payment {PaymentId} not found in database, skipping", @event.PaymentId);
            return;
        }

        // Idempotency: skip if already processed
        if (payment.Status != PaymentStatus.Pending)
        {
            _logger.LogInformation(
                "Payment {PaymentId} already in status {Status}, skipping (idempotent)",
                @event.PaymentId, payment.Status);
            return;
        }

        payment.MarkAsProcessed();
        await context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Payment {PaymentId} successfully marked as Processed", @event.PaymentId);
    }
}