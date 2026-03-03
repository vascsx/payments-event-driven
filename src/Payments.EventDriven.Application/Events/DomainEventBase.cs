using System.Text.Json.Serialization;

namespace Payments.EventDriven.Application.Events;

/// <summary>
/// Classe base para todos os eventos do domínio.
/// Implementa governança de schema com versionamento explícito.
/// 
/// Regras de compatibilidade:
/// - Novos campos opcionais: OK (backward compatible)
/// - Remover campos: BREAKING (exige nova versão major)
/// - Alterar tipo de campo: BREAKING (exige nova versão major)
/// - Renomear campo: BREAKING (exige nova versão major)
/// </summary>
public abstract class DomainEventBase
{
    /// <summary>
    /// ID único do evento (para deduplicação e idempotência)
    /// </summary>
    [JsonPropertyName("eventId")]
    public Guid EventId { get; init; } = Guid.CreateVersion7();

    /// <summary>
    /// Tipo do evento (usado para roteamento)
    /// Ex: "payment-created", "darf-payment-created"
    /// </summary>
    [JsonPropertyName("eventType")]
    public abstract string EventType { get; }

    /// <summary>
    /// Versão do schema do evento (para compatibilidade)
    /// Incrementar ao fazer mudanças no schema
    /// </summary>
    [JsonPropertyName("schemaVersion")]
    public abstract int SchemaVersion { get; }

    /// <summary>
    /// Timestamp UTC de quando o evento foi criado
    /// </summary>
    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// ID de correlação para rastreamento distribuído
    /// </summary>
    [JsonPropertyName("correlationId")]
    public string? CorrelationId { get; init; }

    /// <summary>
    /// Namespace/domínio do evento (para segregação)
    /// Ex: "payments", "accounts", "transfers"
    /// </summary>
    [JsonPropertyName("domain")]
    public virtual string Domain => "payments";
}

/// <summary>
/// Envelope que wrappa um evento para transporte.
/// Permite metadata adicional sem poluir o evento de domínio.
/// </summary>
/// <typeparam name="T">Tipo do evento</typeparam>
public class EventEnvelope<T> where T : DomainEventBase
{
    /// <summary>
    /// O evento em si
    /// </summary>
    [JsonPropertyName("data")]
    public required T Data { get; init; }

    /// <summary>
    /// Metadata de transporte
    /// </summary>
    [JsonPropertyName("metadata")]
    public EventMetadata Metadata { get; init; } = new();
}

/// <summary>
/// Metadata de transporte do evento
/// </summary>
public class EventMetadata
{
    /// <summary>
    /// Origem do evento (nome do serviço)
    /// </summary>
    [JsonPropertyName("source")]
    public string Source { get; init; } = "payments-api";

    /// <summary>
    /// Ambiente (dev, staging, prod)
    /// </summary>
    [JsonPropertyName("environment")]
    public string? Environment { get; init; }

    /// <summary>
    /// ID da instância que gerou o evento
    /// </summary>
    [JsonPropertyName("instanceId")]
    public string? InstanceId { get; init; }
    
    /// <summary>
    /// Trace ID para distributed tracing
    /// </summary>
    [JsonPropertyName("traceId")]
    public string? TraceId { get; init; }

    /// <summary>
    /// Span ID para distributed tracing
    /// </summary>
    [JsonPropertyName("spanId")]
    public string? SpanId { get; init; }
}
