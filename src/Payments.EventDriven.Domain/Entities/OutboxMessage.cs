namespace Payments.EventDriven.Domain.Entities;

/// <summary>
/// Representa uma mensagem de evento a ser publicada, persistida transacionalmente no banco.
/// Implementa o Outbox Pattern para garantir consistência eventual entre estado do domínio e eventos.
/// </summary>
public class OutboxMessage
{
    public Guid Id { get; private set; }
    public string Topic { get; private set; } = string.Empty;
    public string MessageKey { get; private set; } = string.Empty;
    public string Payload { get; private set; } = string.Empty;
    public string? CorrelationId { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? ProcessedAt { get; private set; }
    public int RetryCount { get; private set; }
    public DateTime? LastRetryAt { get; private set; }
    public string? LastError { get; private set; }
    public OutboxMessageStatus Status { get; private set; }

    private OutboxMessage() { } // EF Core

    public OutboxMessage(string topic, string messageKey, string payload, string? correlationId = null)
    {
        Id = Guid.CreateVersion7();
        Topic = topic;
        MessageKey = messageKey;
        Payload = payload;
        CorrelationId = correlationId;
        CreatedAt = DateTime.UtcNow;
        Status = OutboxMessageStatus.Pending;
        RetryCount = 0;
    }

    public void MarkAsProcessed()
    {
        Status = OutboxMessageStatus.Processed;
        ProcessedAt = DateTime.UtcNow;
    }

    public void MarkAsProcessing()
    {
        if (Status != OutboxMessageStatus.Pending)
            throw new InvalidOperationException($"Cannot mark message as Processing from status {Status}");
        
        Status = OutboxMessageStatus.Processing;
    }

    public void IncrementRetry(string? errorMessage = null)
    {
        RetryCount++;
        LastRetryAt = DateTime.UtcNow;
        LastError = errorMessage;
    }

    public void MarkAsFailed(string? errorMessage = null)
    {
        Status = OutboxMessageStatus.Failed;
        LastError = errorMessage;
    }
}

public enum OutboxMessageStatus
{
    Pending = 1,
    Processing = 2,
    Processed = 3,
    Failed = 4
}
