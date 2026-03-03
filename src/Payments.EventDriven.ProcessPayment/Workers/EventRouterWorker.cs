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

    private const int MaxRetries = 3;
    private static readonly TimeSpan InitialRetryDelay = TimeSpan.FromSeconds(1);

    // Metrics
    private long _messagesProcessed = 0;
    private long _messagesFailed = 0;
    private long _messagesSentToDlq = 0;
    private long _messagesRetried = 0;

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

                    var processResult = await ProcessMessageWithRetryAsync(consumeResult, stoppingToken);

                    // CRITICAL: Só commita se o processamento foi bem-sucedido
                    if (processResult == ProcessingResult.Success)
                    {
                        _consumer.Commit(consumeResult);
                        _consumer.StoreOffset(consumeResult);
                    }
                    else if (processResult == ProcessingResult.SentToDlq)
                    {
                        // Mensagem foi enviada para DLQ, pode commitar o offset
                        _consumer.Commit(consumeResult);
                        _consumer.StoreOffset(consumeResult);
                    }
                    else // ProcessingResult.Failed - não commita, será reprocessado
                    {
                        _logger.LogWarning(
                            "Message processing failed, offset will NOT be committed. Partition {Partition}, Offset {Offset}",
                            consumeResult.Partition.Value, consumeResult.Offset.Value);
                        
                        // Pausa para evitar loop infinito rápido
                        await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                    }
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
            
            _logger.LogInformation(
                "EventRouterWorker stopped. Final metrics - Processed: {Processed}, Failed: {Failed}, Retried: {Retried}, SentToDlq: {Dlq}",
                _messagesProcessed, _messagesFailed, _messagesRetried, _messagesSentToDlq);
        }
    }

    /// <summary>
    /// Processa mensagem com retry exponencial para erros transitórios
    /// </summary>
    private async Task<ProcessingResult> ProcessMessageWithRetryAsync(
        ConsumeResult<string, string> consumeResult,
        CancellationToken cancellationToken)
    {
        var retryCount = 0;

        while (retryCount <= MaxRetries)
        {
            try
            {
                return await ProcessMessageAsync(consumeResult, cancellationToken);
            }
            catch (Exception ex) when (IsTransientError(ex) && retryCount < MaxRetries)
            {
                retryCount++;
                Interlocked.Increment(ref _messagesRetried);
                var delay = TimeSpan.FromSeconds(Math.Pow(2, retryCount - 1) * InitialRetryDelay.TotalSeconds);

                _logger.LogWarning(ex,
                    "Transient error processing message. Retry {Retry}/{MaxRetries} after {Delay}s. Key: {Key}",
                    retryCount, MaxRetries, delay.TotalSeconds, consumeResult.Message.Key);

                await Task.Delay(delay, cancellationToken);
            }
            catch (Exception ex)
            {
                // Erro permanente ou último retry falhou
                Interlocked.Increment(ref _messagesFailed);
                Interlocked.Increment(ref _messagesSentToDlq);

                _logger.LogError(ex,
                    "Permanent error or max retries exceeded. Sending to DLQ. Key: {Key}",
                    consumeResult.Message.Key);

                await SendToDlqAsync(consumeResult, $"Processing failed: {ex.Message}", cancellationToken);
                return ProcessingResult.SentToDlq;
            }
        }

        // Não deve chegar aqui, mas por segurança
        Interlocked.Increment(ref _messagesSentToDlq);
        await SendToDlqAsync(consumeResult, "Max retries exceeded", cancellationToken);
        return ProcessingResult.SentToDlq;
    }

    private static bool IsTransientError(Exception ex)
    {
        return ex is TimeoutException
            || ex is OperationCanceledException
            || ex.Message.Contains("connection", StringComparison.OrdinalIgnoreCase)
            || ex.Message.Contains("timeout", StringComparison.OrdinalIgnoreCase)
            || ex.Message.Contains("temporarily unavailable", StringComparison.OrdinalIgnoreCase)
            || ex.InnerException is TimeoutException;
    }

    private async Task<ProcessingResult> ProcessMessageAsync(ConsumeResult<string, string> consumeResult, CancellationToken cancellationToken)
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

            // CRITICAL-FIX: Unknown event types vão para DLQ ao invés de serem skipped
            if (!handlerFactory.HasHandler(eventType))
            {
                _logger.LogError(
                    "No handler registered for event type {EventType}. Key: {Key}, CorrelationId: {CorrelationId}",
                    eventType, message.Key, correlationId);
                
                throw new NotSupportedException($"No handler for event type: {eventType}");
            }

            var handler = handlerFactory.GetHandler(eventType);
            await handler.HandleAsync(message.Value, correlationId, cancellationToken);

            Interlocked.Increment(ref _messagesProcessed);

            _logger.LogInformation(
                "Event {EventType} processed successfully. Key: {Key}, CorrelationId: {CorrelationId}",
                eventType, message.Key, correlationId);
            
            if (_messagesProcessed % 100 == 0)
            {
                _logger.LogInformation(
                    "[METRICS] Processed: {Processed}, Failed: {Failed}, Retried: {Retried}, SentToDlq: {Dlq}",
                    _messagesProcessed, _messagesFailed, _messagesRetried, _messagesSentToDlq);
            }

            return ProcessingResult.Success;
        }
        catch (NotSupportedException)
        {
            // Erro permanente - re-lançar para enviar diretamente para DLQ sem retry
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to process event type {EventType} with key {Key}. CorrelationId: {CorrelationId}.",
                eventType, message.Key, correlationId);
            
            // Re-lançar para permitir retry ou DLQ
            throw;
        }
    }

    private async Task SendToDlqAsync(ConsumeResult<string, string> originalMessage, string errorReason, CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = _serviceProvider.CreateAsyncScope();
            var eventPublisher = scope.ServiceProvider.GetRequiredService<IEventPublisher>();

            var dlqTopic = $"{originalMessage.Topic}-dlq";
            
            // Preserva headers originais e adiciona informações de erro
            var headers = new Dictionary<string, string>();
            
            if (originalMessage.Message.Headers != null)
            {
                foreach (var header in originalMessage.Message.Headers)
                {
                    headers[header.Key] = System.Text.Encoding.UTF8.GetString(header.GetValueBytes());
                }
            }
            
            headers["dlq-reason"] = errorReason;
            headers["dlq-original-topic"] = originalMessage.Topic;
            headers["dlq-original-partition"] = originalMessage.Partition.Value.ToString();
            headers["dlq-original-offset"] = originalMessage.Offset.Value.ToString();
            headers["dlq-timestamp"] = DateTime.UtcNow.ToString("O");

            await eventPublisher.PublishAsync(
                dlqTopic,
                originalMessage.Message.Key,
                originalMessage.Message.Value,
                headers,
                cancellationToken);

            _logger.LogWarning(
                "Message sent to DLQ topic {DlqTopic}. Reason: {Reason}. Original Key: {Key}",
                dlqTopic, errorReason, originalMessage.Message.Key);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "CRITICAL: Failed to send message to DLQ. Original message will be LOST if offset is committed. Key: {Key}",
                originalMessage.Message.Key);
            
            // Se falhar ao enviar para DLQ, não podemos commitar o offset
            throw;
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
