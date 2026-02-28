using Payments.EventDriven.Application.DTOs;

namespace Payments.EventDriven.Application.Interfaces;

public interface ICreatePaymentUseCase
{
    Task<Guid> ExecuteAsync(
        CreatePaymentRequest request,
        CancellationToken cancellationToken,
        string? correlationId = null);
}
