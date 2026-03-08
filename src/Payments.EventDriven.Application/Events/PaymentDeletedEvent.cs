using System.Text.Json.Serialization;

namespace Payments.EventDriven.Application.Events;

/// <summary>
/// Evento emitido quando um pagamento é deletado.
/// Permite que consumidores downstream removam dados relacionados.
/// </summary>
public class PaymentDeletedEvent : DomainEventBase
{
    /// <inheritdoc />
    [JsonPropertyName("eventType")]
    public override string EventType => "payment-deleted";

    /// <inheritdoc />
    [JsonPropertyName("schemaVersion")]
    public override int SchemaVersion => 1;

    /// <summary>
    /// ID do pagamento deletado
    /// </summary>
    [JsonPropertyName("paymentId")]
    public Guid PaymentId { get; init; }

    /// <summary>
    /// Valor do pagamento (para auditoria)
    /// </summary>
    [JsonPropertyName("amount")]
    public decimal Amount { get; init; }

    /// <summary>
    /// Moeda do pagamento (para auditoria)
    /// </summary>
    [JsonPropertyName("currency")]
    public string Currency { get; init; } = string.Empty;

    /// <summary>
    /// Timestamp da deleção
    /// </summary>
    [JsonPropertyName("deletedAt")]
    public DateTime DeletedAt { get; init; }
}
