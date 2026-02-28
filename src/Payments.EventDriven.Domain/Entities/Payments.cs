using Payments.EventDriven.Domain.Abstractions;
using Payments.EventDriven.Domain.Enums;


namespace Payments.EventDriven.Domain.Entities;

public class Payment : IEntity
{
    public Guid Id { get; private set; }
    public decimal Amount { get; private set; }
    public string Currency { get; private set; } = string.Empty;
    public PaymentStatus Status { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private Payment() { } // EF Core

    public Payment(decimal amount, string currency)
    {
        Id = Guid.CreateVersion7();
        Amount = amount;
        Currency = currency;
        Status = PaymentStatus.Pending;
        CreatedAt = DateTime.UtcNow;

        Validate();
    }

    public void MarkAsProcessed()
    {
        if (Status != PaymentStatus.Pending)
            throw new InvalidOperationException(
                $"Cannot mark payment as Processed from status {Status}.");

        Status = PaymentStatus.Processed;
    }

    public void MarkAsFailed()
    {
        if (Status != PaymentStatus.Pending)
            throw new InvalidOperationException(
                $"Cannot mark payment as Failed from status {Status}.");

        Status = PaymentStatus.Failed;
    }

    private void Validate()
    {
        if (Amount <= 0)
            throw new ArgumentException("Amount must be greater than zero.");

        if (string.IsNullOrWhiteSpace(Currency))
            throw new ArgumentException("Currency is required.");
    }
}