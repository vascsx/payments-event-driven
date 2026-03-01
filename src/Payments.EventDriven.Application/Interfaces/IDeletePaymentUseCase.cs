namespace Payments.EventDriven.Application.Interfaces;

public interface IDeletePaymentUseCase
{
    Task<bool> ExecuteAsync(Guid paymentId, CancellationToken cancellationToken);
}
