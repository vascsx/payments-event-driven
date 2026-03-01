using System.Text.Json;
using Confluent.Kafka;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Payments.EventDriven.Application.Events;
using Payments.EventDriven.Application.Interfaces;
using Payments.EventDriven.Infrastructure.Settings;

namespace Payments.EventDriven.ProcessPayment.Workers;

/// <summary>
/// Background service que consome mensagens do Kafka topic "payment-created"
/// e processa os pagamentos chamando ProcessPaymentUseCase
/// </summary>
public class ProcessPaymentConsumerWorker : BackgroundService
{
    private readonly ILogger<ProcessPaymentConsumerWorker> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly KafkaSettings _kafkaSettings;
    private IConsumer<string, string>? _consumer;

    public ProcessPaymentConsumerWorker(
        ILogger<ProcessPaymentConsumerWorker> logger,
        IServiceProvider serviceProvider,
        IOptions<KafkaSettings> kafkaSettings)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        _kafkaSettings = kafkaSettings.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var config = new ConsumerConfig
        {
            BootstrapServers = _kafkaSettings.BootstrapServers,
            GroupId = _kafkaSettings.GroupId,
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false, // Commit manual para garantir processamento
            EnableAutoOffsetStore = false
        };

        _consumer = new ConsumerBuilder<string, string>(config).Build();
        _consumer.Subscribe(_kafkaSettings.Topic);

        _logger.LogInformation(
            "PaymentConsumerWorker started - consuming from topic {Topic} with group {GroupId}",
            _kafkaSettings.Topic, _kafkaSettings.GroupId);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var consumeResult = _consumer.Consume(stoppingToken);

                    if (consumeResult?.Message == null)
                        continue;

                    await ProcessMessageAsync(consumeResult, stoppingToken);

                    // Commit manual após processamento bem-sucedido
                    _consumer.Commit(consumeResult);
                    _consumer.StoreOffset(consumeResult);
                }
                catch (ConsumeException ex)
                {
                    _logger.LogError(ex, "Error consuming message from Kafka");
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unexpected error while consuming messages");
                    await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken); // Backoff
                }
            }
        }
        finally
        {
            _consumer.Close();
            _consumer.Dispose();
            _logger.LogInformation("PaymentConsumerWorker stopped");
        }
    }

    private async Task ProcessMessageAsync(ConsumeResult<string, string> consumeResult, CancellationToken cancellationToken)
    {
        var message = consumeResult.Message;
        var correlationId = message.Headers
            ?.FirstOrDefault(h => h.Key == "X-Correlation-Id")
            ?.GetValueBytes() is byte[] bytes
            ? System.Text.Encoding.UTF8.GetString(bytes)
            : null;

        _logger.LogInformation(
            "Received message from topic {Topic}, partition {Partition}, offset {Offset}. Key: {Key}, CorrelationId: {CorrelationId}",
            consumeResult.Topic,
            consumeResult.Partition.Value,
            consumeResult.Offset.Value,
            message.Key,
            correlationId);

        try
        {
            // Deserializa o evento
            var paymentEvent = JsonSerializer.Deserialize<PaymentCreatedEvent>(message.Value);

            if (paymentEvent is null)
            {
                _logger.LogWarning(
                    "Failed to deserialize PaymentCreatedEvent from message. Key: {Key}, Payload: {Payload}",
                    message.Key, message.Value);
                return;
            }

            // Cria um novo scope para resolver dependências scoped
            await using var scope = _serviceProvider.CreateAsyncScope();
            var processPaymentUseCase = scope.ServiceProvider.GetRequiredService<IProcessPaymentUseCase>();

            // Processa o pagamento
            var result = await processPaymentUseCase.ProcessAsync(paymentEvent.PaymentId, cancellationToken);

            _logger.LogInformation(
                "Payment {PaymentId} processing result: {Result}. CorrelationId: {CorrelationId}",
                paymentEvent.PaymentId, result, correlationId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to process message with key {Key}. CorrelationId: {CorrelationId}. Message will NOT be retried to avoid infinite loop.",
                message.Key, correlationId);

            // Não re-throw para não bloquear o consumer
            // Em produção, considere enviar para DLQ (Dead Letter Queue)
        }
    }

    public override void Dispose()
    {
        _consumer?.Dispose();
        base.Dispose();
    }
}
