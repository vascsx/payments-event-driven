using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Payments.EventDriven.Application.Interfaces;

namespace Payments.EventDriven.Infrastructure.Observability;

/// <summary>
/// Implementação de métricas baseada em logs estruturados.
/// Em produção, substituir por Prometheus, OpenTelemetry ou similar.
/// 
/// Métricas são emitidas como logs estruturados que podem ser:
/// - Coletados por ferramentas como Loki, Elasticsearch
/// - Convertidos em métricas via log-based metrics (Datadog, CloudWatch)
/// </summary>
public class LogBasedMetricsService : IMetricsService
{
    private readonly ILogger<LogBasedMetricsService> _logger;
    
    // Counters em memória para agregação
    private readonly ConcurrentDictionary<string, long> _counters = new();
    private readonly ConcurrentDictionary<string, long> _gauges = new();
    
    // Intervalo de flush de métricas agregadas
    private readonly Timer _flushTimer;
    private const int FlushIntervalSeconds = 60;

    public LogBasedMetricsService(ILogger<LogBasedMetricsService> logger)
    {
        _logger = logger;
        _flushTimer = new Timer(FlushMetrics, null, 
            TimeSpan.FromSeconds(FlushIntervalSeconds), 
            TimeSpan.FromSeconds(FlushIntervalSeconds));
    }

    // === COUNTERS ===
    
    public void IncrementMessagesProcessed(string topic, string eventType)
    {
        var key = $"messages.processed.{topic}.{eventType}";
        IncrementCounter(key);
        
        _logger.LogInformation(
            "[METRIC] MessagesProcessed Topic={Topic} EventType={EventType} Count=1",
            topic, eventType);
    }

    public void IncrementMessagesFailed(string topic, string eventType, string errorType)
    {
        var key = $"messages.failed.{topic}.{eventType}.{errorType}";
        IncrementCounter(key);
        
        _logger.LogWarning(
            "[METRIC] MessagesFailed Topic={Topic} EventType={EventType} ErrorType={ErrorType} Count=1",
            topic, eventType, errorType);
    }

    public void IncrementMessagesSentToDlq(string topic, string reason)
    {
        var key = $"messages.dlq.{topic}";
        IncrementCounter(key);
        
        _logger.LogWarning(
            "[METRIC] MessagesSentToDlq Topic={Topic} Reason={Reason} Count=1",
            topic, reason);
    }

    public void IncrementRetries(string topic, string eventType)
    {
        var key = $"messages.retries.{topic}.{eventType}";
        IncrementCounter(key);
        
        _logger.LogInformation(
            "[METRIC] MessageRetries Topic={Topic} EventType={EventType} Count=1",
            topic, eventType);
    }

    public void IncrementPaymentsCreated(string paymentType)
    {
        var key = $"payments.created.{paymentType}";
        IncrementCounter(key);
        
        _logger.LogInformation(
            "[METRIC] PaymentsCreated PaymentType={PaymentType} Count=1",
            paymentType);
    }

    public void IncrementPaymentsProcessed(string paymentType, string result)
    {
        var key = $"payments.processed.{paymentType}.{result}";
        IncrementCounter(key);
        
        _logger.LogInformation(
            "[METRIC] PaymentsProcessed PaymentType={PaymentType} Result={Result} Count=1",
            paymentType, result);
    }

    public void IncrementOutboxPublished()
    {
        IncrementCounter("outbox.published");
        
        _logger.LogDebug("[METRIC] OutboxPublished Count=1");
    }

    public void IncrementOutboxFailed()
    {
        IncrementCounter("outbox.failed");
        
        _logger.LogWarning("[METRIC] OutboxFailed Count=1");
    }

    // === GAUGES ===
    
    public void SetOutboxPendingCount(long count)
    {
        _gauges["outbox.pending"] = count;
        
        if (count > 100) // Alerta se backlog alto
        {
            _logger.LogWarning(
                "[METRIC][ALERT] OutboxPendingCount={Count} - HIGH BACKLOG",
                count);
        }
        else
        {
            _logger.LogInformation(
                "[METRIC] OutboxPendingCount={Count}",
                count);
        }
    }

    public void SetOutboxFailedCount(long count)
    {
        _gauges["outbox.failed_total"] = count;
        
        if (count > 0)
        {
            _logger.LogWarning(
                "[METRIC][ALERT] OutboxFailedCount={Count} - FAILED MESSAGES EXIST",
                count);
        }
    }

    // === HISTOGRAMAS ===
    
    public void RecordProcessingDuration(string topic, string eventType, TimeSpan duration)
    {
        _logger.LogInformation(
            "[METRIC] ProcessingDuration Topic={Topic} EventType={EventType} DurationMs={DurationMs}",
            topic, eventType, duration.TotalMilliseconds);

        // Alerta se processamento muito lento
        if (duration.TotalSeconds > 5)
        {
            _logger.LogWarning(
                "[METRIC][ALERT] SlowProcessing Topic={Topic} EventType={EventType} DurationMs={DurationMs}",
                topic, eventType, duration.TotalMilliseconds);
        }
    }

    public void RecordPublishDuration(string topic, TimeSpan duration)
    {
        _logger.LogInformation(
            "[METRIC] PublishDuration Topic={Topic} DurationMs={DurationMs}",
            topic, duration.TotalMilliseconds);
    }

    // === HELPERS ===
    
    private void IncrementCounter(string key)
    {
        _counters.AddOrUpdate(key, 1, (_, current) => current + 1);
    }

    private void FlushMetrics(object? state)
    {
        // Emite métricas agregadas periodicamente
        foreach (var (key, value) in _counters)
        {
            _logger.LogInformation(
                "[METRIC][AGGREGATE] Counter={Counter} Value={Value} Period={Period}s",
                key, value, FlushIntervalSeconds);
        }

        foreach (var (key, value) in _gauges)
        {
            _logger.LogInformation(
                "[METRIC][AGGREGATE] Gauge={Gauge} Value={Value}",
                key, value);
        }
    }
}
