namespace Payments.EventDriven.Application.Interfaces;

public enum ProcessPaymentResult
{
    Processed,
    AlreadyProcessed,
}

public interface IProcessPaymentUseCase
{
    Task<ProcessPaymentResult> ProcessAsync(Guid paymentId, CancellationToken cancellationToken);
}
