using System.Text;
using System.Text.Json;
using Confluent.Kafka;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Payments.EventDriven.Application.Constants;
using Payments.EventDriven.Application.Events;
using Payments.EventDriven.Application.Interfaces;
using Payments.EventDriven.Domain.Exceptions;
using Payments.EventDriven.Infrastructure.Settings;

namespace Payments.EventDriven.Processor.Workers;

public class PaymentConsumerWorker : BackgroundService
{
    private readonly ILogger<PaymentConsumerWorker> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly KafkaSettings _settings;
    private readonly IEventPublisher _eventPublisher;
    private const int MaxRetries = 3;

    public PaymentConsumerWorker(
        ILogger<PaymentConsumerWorker> logger,
        IServiceProvider serviceProvider,
        KafkaSettings settings,
        IEventPublisher eventPublisher)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        _settings = settings;
        _eventPublisher = eventPublisher;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Use LongRunning to avoid blocking a ThreadPool thread with the Kafka consume loop
        return Task.Factory.StartNew(
            () => ConsumeLoop(stoppingToken),
            stoppingToken,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default).Unwrap();
    }

    private async Task ConsumeLoop(CancellationToken stoppingToken)
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
                    consumer.Commit(result);
                    continue;
                }

                using (_logger.BeginScope(new Dictionary<string, object?>
                {
                    ["PaymentId"] = @event.PaymentId,
                    ["CorrelationId"] = correlationId
                }))
                {
                    await ProcessWithRetryAsync(@event.PaymentId, stoppingToken);
                }

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

                        await _eventPublisher.PublishAsync(
                            KafkaTopics.PaymentCreatedDlq,
                            result.Message.Key,
                            result.Message.Value,
                            dlqHeaders,
                            stoppingToken);

                        _logger.LogWarning("Message routed to DLQ topic {Dlq}", KafkaTopics.PaymentCreatedDlq);
                    }
                    catch (Exception dlqEx)
                    {
                        _logger.LogError(dlqEx, "Failed to publish to DLQ; committing offset anyway to prevent poison pill");
                    }

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

    private async Task ProcessWithRetryAsync(Guid paymentId, CancellationToken cancellationToken)
    {
        var attempts = 0;
        while (true)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var useCase = scope.ServiceProvider.GetRequiredService<IProcessPaymentUseCase>();
                var result = await useCase.ProcessAsync(paymentId, cancellationToken);

                if (result == ProcessPaymentResult.AlreadyProcessed)
                    _logger.LogInformation("Payment {PaymentId} already processed, skipping (idempotent)", paymentId);
                else
                    _logger.LogInformation("Payment {PaymentId} successfully marked as Processed", paymentId);

                return;
            }
            catch (Exception ex) when (IsTransient(ex) && ++attempts < MaxRetries)
            {
                var delay = TimeSpan.FromSeconds(Math.Pow(2, attempts));
                _logger.LogWarning(ex,
                    "Failed to process payment {PaymentId}, attempt {Attempt}/{MaxRetries}. Retrying in {Delay}s",
                    paymentId, attempts, MaxRetries, delay.TotalSeconds);
                await Task.Delay(delay, cancellationToken);
            }
            // Permanent exceptions (JsonException, ArgumentException, etc.) propagate directly → DLQ
        }
    }

    /// <summary>
    /// Whitelist approach: only known transient exceptions are retried.
    /// Unknown exceptions go straight to DLQ.
    /// </summary>
    private static bool IsTransient(Exception ex) =>
        ex is PaymentNotYetVisibleException
            or TimeoutException
            or System.Net.Sockets.SocketException;
}