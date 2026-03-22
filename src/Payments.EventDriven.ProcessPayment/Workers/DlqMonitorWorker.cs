using Confluent.Kafka;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Payments.EventDriven.Application.Constants;
using Payments.EventDriven.Infrastructure.Settings;

namespace Payments.EventDriven.ProcessPayment.Workers;

/// <summary>
/// DLQ Monitor Worker - Monitora mensagens que falharam e foram enviadas para DLQ
/// 
/// Responsabilidades:
/// - Consome mensagens de payment-created-dlq
/// - Gera logs e métricas de falhas
/// - Permite análise e eventual replay manual
/// - Gera alertas quando DLQ acumula mensagens
/// </summary>
public class DlqMonitorWorker : BackgroundService
{
    private readonly ILogger<DlqMonitorWorker> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly KafkaSettings _kafkaSettings;
    private IConsumer<string, string>? _consumer;

    private long _totalDlqMessages = 0;
    private readonly Dictionary<string, long> _dlqReasonCounts = new();

    public DlqMonitorWorker(
        ILogger<DlqMonitorWorker> logger,
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
            GroupId = $"{_kafkaSettings.GroupId}-dlq-monitor",
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = true,
            EnableAutoOffsetStore = true
        };

        _consumer = new ConsumerBuilder<string, string>(config).Build();
        _consumer.Subscribe(KafkaTopics.AllDlqTopics);

        _logger.LogInformation(
            "DLQ Monitor started - consuming from topics {DlqTopics} with group {GroupId}",
            string.Join(", ", KafkaTopics.AllDlqTopics), config.GroupId);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var consumeResult = _consumer.Consume(TimeSpan.FromSeconds(5));

                    if (consumeResult?.Message == null)
                        continue;

                    await ProcessDlqMessageAsync(consumeResult);
                }
                catch (ConsumeException ex)
                {
                    _logger.LogError(ex, "Error consuming from DLQ");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unexpected error in DLQ monitor");
                    await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                }
            }
        }
        finally
        {
            _consumer.Close();
            _consumer.Dispose();
            
            _logger.LogWarning(
                "DLQ Monitor stopped. Total DLQ messages: {Total}. Breakdown by reason: {@Reasons}",
                _totalDlqMessages, _dlqReasonCounts);
        }
    }

    private Task ProcessDlqMessageAsync(ConsumeResult<string, string> consumeResult)
    {
        var message = consumeResult.Message;
        _totalDlqMessages++;

        // Extrai metadata dos headers
        var dlqReason = GetHeaderValue(message.Headers, "dlq-reason") ?? "Unknown";
        var originalTopic = GetHeaderValue(message.Headers, "dlq-original-topic");
        var originalPartition = GetHeaderValue(message.Headers, "dlq-original-partition");
        var originalOffset = GetHeaderValue(message.Headers, "dlq-original-offset");
        var dlqTimestamp = GetHeaderValue(message.Headers, "dlq-timestamp");
        var correlationId = GetHeaderValue(message.Headers, "X-Correlation-Id");
        var eventType = GetHeaderValue(message.Headers, "event-type");

        // Conta falhas por razão
        if (!_dlqReasonCounts.ContainsKey(dlqReason))
            _dlqReasonCounts[dlqReason] = 0;
        _dlqReasonCounts[dlqReason]++;

        // Log detalhado para análise
        _logger.LogWarning(
            "[DLQ] Message failed permanently. " +
            "Key: {Key}, EventType: {EventType}, Reason: {Reason}, " +
            "OriginalTopic: {OriginalTopic}, Partition: {Partition}, Offset: {Offset}, " +
            "DLQ Timestamp: {DlqTimestamp}, CorrelationId: {CorrelationId}, " +
            "Total DLQ Count: {TotalDlq}",
            message.Key, eventType, dlqReason,
            originalTopic, originalPartition, originalOffset,
            dlqTimestamp, correlationId, _totalDlqMessages);

        // Gera alerta se DLQ crescer muito
        if (_totalDlqMessages % 10 == 0)
        {
            _logger.LogError(
                "[ALERT] DLQ has accumulated {Count} messages! Top reasons: {@TopReasons}",
                _totalDlqMessages,
                _dlqReasonCounts.OrderByDescending(x => x.Value).Take(3));
        }

        return Task.CompletedTask;
    }

    private static string? GetHeaderValue(Headers? headers, string key)
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
