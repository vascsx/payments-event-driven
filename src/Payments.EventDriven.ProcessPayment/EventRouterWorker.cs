using Confluent.Kafka;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Payments.EventDriven.Application.Interfaces;
using Payments.EventDriven.Infrastructure.Settings;

namespace Payments.EventDriven.ProcessPayment.Workers;

/// <summary>
/// Event Router Worker - Consome eventos do Kafka e roteia para handlers específicos
/// 
/// Arquitetura:
/// - 1 worker genérico consume TODOS os tipos de eventos
/// - Usa event-type header para rotear para handler correto
/// - Adicionar novo tipo = só criar handler (sem novo worker/deployment)
/// - Escala horizontalmente com consumer groups
/// 
/// Suporta:
/// - payment-created (default)
/// - pix-payment-created
/// - p2p-payment-created
/// - boleto-payment-created
/// - ... (extensível sem código)
/// </summary>
public class EventRouterWorker : BackgroundService
{
    private readonly ILogger<EventRouterWorker> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly KafkaSettings _kafkaSettings;
    private IConsumer<string, string>? _consumer;

    public EventRouterWorker(
        ILogger<EventRouterWorker> logger,
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
            EnableAutoCommit = false,
            EnableAutoOffsetStore = false
        };

        _consumer = new ConsumerBuilder<string, string>(config).Build();
        _consumer.Subscribe(_kafkaSettings.Topic);

        _logger.LogInformation(
            "EventRouterWorker started - consuming from topic {Topic} with group {GroupId}",
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
                    await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                }
            }
        }
        finally
        {
            _consumer.Close();
            _consumer.Dispose();
            _logger.LogInformation("EventRouterWorker stopped");
        }
    }

    private async Task ProcessMessageAsync(ConsumeResult<string, string> consumeResult, CancellationToken cancellationToken)
    {
        var message = consumeResult.Message;
        
        // Extrai metadata dos headers
        var correlationId = GetHeaderValue(message.Headers, "X-Correlation-Id");
        var eventType = GetHeaderValue(message.Headers, "event-type") ?? "payment-created"; // fallback para eventos antigos

        _logger.LogInformation(
            "Received event type {EventType} from topic {Topic}, partition {Partition}, offset {Offset}. Key: {Key}, CorrelationId: {CorrelationId}",
            eventType,
            consumeResult.Topic,
            consumeResult.Partition.Value,
            consumeResult.Offset.Value,
            message.Key,
            correlationId);

        try
        {
            // Resolve handler usando factory
            await using var scope = _serviceProvider.CreateAsyncScope();
            var handlerFactory = scope.ServiceProvider.GetRequiredService<IEventHandlerFactory>();

            if (!handlerFactory.HasHandler(eventType))
            {
                _logger.LogWarning(
                    "No handler registered for event type {EventType}. Message will be skipped. Key: {Key}, CorrelationId: {CorrelationId}",
                    eventType, message.Key, correlationId);
                return;
            }

            var handler = handlerFactory.GetHandler(eventType);
            await handler.HandleAsync(message.Value, correlationId, cancellationToken);

            _logger.LogInformation(
                "Event {EventType} processed successfully. Key: {Key}, CorrelationId: {CorrelationId}",
                eventType, message.Key, correlationId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to process event type {EventType} with key {Key}. CorrelationId: {CorrelationId}",
                eventType, message.Key, correlationId);
            
            // Não re-throw para não bloquear o consumer
            // TODO: Considere enviar para DLQ baseado no event-type
        }
    }

    private static string? GetHeaderValue(Headers headers, string key)
    {
        var header = headers?.FirstOrDefault(h => h.Key == key);
        if (header?.GetValueBytes() is byte[] bytes)
        {
            return System.Text.Encoding.UTF8.GetString(bytes);
        }
        return null;
    }

    public override void Dispose()
    {
        _consumer?.Dispose();
        base.Dispose();
    }
}
