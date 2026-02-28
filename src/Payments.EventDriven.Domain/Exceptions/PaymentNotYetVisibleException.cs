namespace Payments.EventDriven.Domain.Exceptions;

public class PaymentNotYetVisibleException : Exception
{
    public Guid PaymentId { get; }

    public PaymentNotYetVisibleException(Guid paymentId)
        : base($"Payment {paymentId} not yet visible in database.")
    {
        PaymentId = paymentId;
    }
}
