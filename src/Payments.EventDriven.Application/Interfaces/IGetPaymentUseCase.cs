using Payments.EventDriven.Application.DTOs;

namespace Payments.EventDriven.Application.Interfaces;

public interface IGetPaymentUseCase
{
    Task<GetPaymentResponse?> ExecuteAsync(Guid paymentId, CancellationToken cancellationToken);
}
