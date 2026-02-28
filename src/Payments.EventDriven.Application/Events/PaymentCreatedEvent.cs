using System.Text.Json.Serialization;

namespace Payments.EventDriven.Application.Events;

public class PaymentCreatedEvent
{
    public Guid PaymentId { get; init; }

    [JsonNumberHandling(JsonNumberHandling.WriteAsString | JsonNumberHandling.AllowReadingFromString)]
    public decimal Amount { get; init; }

    public string Currency { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
    public int Version { get; init; } = 1;
}
