namespace Payments.EventDriven.Application.Interfaces;

/// <summary>
/// Interface para métricas e observabilidade do sistema.
/// Permite rastrear counters, gauges e histogramas para monitoramento.
/// Em produção, implementar com Prometheus, OpenTelemetry ou similar.
/// </summary>
public interface IMetricsService
{
    // === COUNTERS (incrementais) ===
    
    /// <summary>Incrementa contador de mensagens processadas com sucesso</summary>
    void IncrementMessagesProcessed(string topic, string eventType);
    
    /// <summary>Incrementa contador de mensagens com falha</summary>
    void IncrementMessagesFailed(string topic, string eventType, string errorType);
    
    /// <summary>Incrementa contador de mensagens enviadas para DLQ</summary>
    void IncrementMessagesSentToDlq(string topic, string reason);
    
    /// <summary>Incrementa contador de retries</summary>
    void IncrementRetries(string topic, string eventType);
    
    /// <summary>Incrementa contador de pagamentos criados</summary>
    void IncrementPaymentsCreated(string paymentType);
    
    /// <summary>Incrementa contador de pagamentos processados</summary>
    void IncrementPaymentsProcessed(string paymentType, string result);
    
    /// <summary>Incrementa contador de mensagens outbox publicadas</summary>
    void IncrementOutboxPublished();
    
    /// <summary>Incrementa contador de mensagens outbox com falha</summary>
    void IncrementOutboxFailed();

    // === GAUGES (valores instantâneos) ===
    
    /// <summary>Atualiza gauge de mensagens pendentes no outbox</summary>
    void SetOutboxPendingCount(long count);
    
    /// <summary>Atualiza gauge de mensagens com falha no outbox</summary>
    void SetOutboxFailedCount(long count);

    // === HISTOGRAMAS (latência) ===
    
    /// <summary>Registra duração do processamento de uma mensagem</summary>
    void RecordProcessingDuration(string topic, string eventType, TimeSpan duration);
    
    /// <summary>Registra duração da publicação no Kafka</summary>
    void RecordPublishDuration(string topic, TimeSpan duration);
}
