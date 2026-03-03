using System.Text.Json.Serialization;

namespace Payments.EventDriven.Application.Events;

/// <summary>
/// Evento emitido quando um pagamento é criado.
/// 
/// Schema Version History:
/// - v1: Versão inicial (Amount, Currency, PaymentId, CreatedAt)
/// - v2: Adicionado EventId, SchemaVersion, Timestamp para governança
/// 
/// Compatibilidade:
/// - Leitura: aceita v1 e v2 (backward compatible)
/// - Escrita: sempre emite v2
/// </summary>
public class PaymentCreatedEvent : DomainEventBase
{
    /// <inheritdoc />
    [JsonPropertyName("eventType")]
    public override string EventType => "payment-created";

    /// <inheritdoc />
    [JsonPropertyName("schemaVersion")]
    public override int SchemaVersion => 2;

    /// <summary>
    /// ID do pagamento criado
    /// </summary>
    [JsonPropertyName("paymentId")]
    public Guid PaymentId { get; init; }

    /// <summary>
    /// Valor do pagamento
    /// </summary>
    [JsonNumberHandling(JsonNumberHandling.WriteAsString | JsonNumberHandling.AllowReadingFromString)]
    [JsonPropertyName("amount")]
    public decimal Amount { get; init; }

    /// <summary>
    /// Moeda do pagamento (ISO 4217)
    /// </summary>
    [JsonPropertyName("currency")]
    public string Currency { get; init; } = string.Empty;

    /// <summary>
    /// Timestamp de criação do pagamento
    /// </summary>
    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; init; }

    /// <summary>
    /// Versão legada - mantida para backward compatibility
    /// Use SchemaVersion ao invés desta propriedade
    /// </summary>
    [JsonPropertyName("version")]
    [Obsolete("Use SchemaVersion instead. Kept for backward compatibility with v1 events.")]
    public int Version { get; init; } = 1;
}

/// <summary>
/// Evento emitido quando um pagamento DARF é criado.
/// Herda de PaymentCreatedEvent com campos específicos do DARF.
/// </summary>
public class DarfPaymentCreatedEvent : PaymentCreatedEvent
{
    /// <inheritdoc />
    [JsonPropertyName("eventType")]
    public override string EventType => "darf-payment-created";

    /// <summary>
    /// Código da receita federal
    /// </summary>
    [JsonPropertyName("codigoReceita")]
    public string? CodigoReceita { get; init; }

    /// <summary>
    /// Período de apuração (MMAAAA)
    /// </summary>
    [JsonPropertyName("periodoApuracao")]
    public string? PeriodoApuracao { get; init; }

    /// <summary>
    /// Número de referência
    /// </summary>
    [JsonPropertyName("numeroReferencia")]
    public string? NumeroReferencia { get; init; }
}

/// <summary>
/// Evento emitido quando um pagamento DARJ é criado.
/// Herda de PaymentCreatedEvent com campos específicos do DARJ.
/// </summary>
public class DarjPaymentCreatedEvent : PaymentCreatedEvent
{
    /// <inheritdoc />
    [JsonPropertyName("eventType")]
    public override string EventType => "darj-payment-created";

    /// <summary>
    /// Código da receita estadual
    /// </summary>
    [JsonPropertyName("codigoReceitaEstadual")]
    public string? CodigoReceitaEstadual { get; init; }

    /// <summary>
    /// Inscrição estadual
    /// </summary>
    [JsonPropertyName("inscricaoEstadual")]
    public string? InscricaoEstadual { get; init; }
}
