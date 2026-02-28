namespace Payments.EventDriven.Domain.Entities;

public class OutboxMessage
{
    public Guid Id { get; private set; }
    public string Topic { get; private set; } = string.Empty;
    public string MessageKey { get; private set; } = string.Empty;
    public string Payload { get; private set; } = string.Empty;
    public string? CorrelationId { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? ProcessedAt { get; private set; }
    public bool IsProcessed => ProcessedAt.HasValue;

    private OutboxMessage() { } // EF Core

    public OutboxMessage(string topic, string messageKey, string payload, string? correlationId = null)
    {
        Id = Guid.NewGuid();
        Topic = topic;
        MessageKey = messageKey;
        Payload = payload;
        CorrelationId = correlationId;
        CreatedAt = DateTime.UtcNow;
    }

    public void MarkAsProcessed()
    {
        ProcessedAt = DateTime.UtcNow;
    }
}
