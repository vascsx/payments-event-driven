namespace Payments.EventDriven.Domain.Entities;

public class OutboxMessage
{
    public Guid Id { get; private set; }
    public string Topic { get; private set; } = string.Empty;
    public string MessageKey { get; private set; } = string.Empty;
    public string Payload { get; private set; } = string.Empty;
    public DateTime CreatedAt { get; private set; }
    public DateTime? ProcessedAt { get; private set; }
    public bool IsProcessed => ProcessedAt.HasValue;

    private OutboxMessage() { } // EF Core

    public OutboxMessage(string topic, string messageKey, string payload)
    {
        Id = Guid.NewGuid();
        Topic = topic;
        MessageKey = messageKey;
        Payload = payload;
        CreatedAt = DateTime.UtcNow;
    }

    public void MarkAsProcessed()
    {
        ProcessedAt = DateTime.UtcNow;
    }
}
