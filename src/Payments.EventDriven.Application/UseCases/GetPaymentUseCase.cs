using Payments.EventDriven.Application.DTOs;
using Payments.EventDriven.Application.Interfaces;

namespace Payments.EventDriven.Application.UseCases;

public class GetPaymentUseCase : IGetPaymentUseCase
{
    private readonly IPaymentRepository _repository;

    public GetPaymentUseCase(IPaymentRepository repository)
    {
        _repository = repository;
    }

    public async Task<GetPaymentResponse?> ExecuteAsync(Guid paymentId, CancellationToken cancellationToken)
    {
        var payment = await _repository.GetByIdAsync(paymentId, cancellationToken);

        if (payment is null)
            return null;

        return new GetPaymentResponse(
            payment.Id,
            payment.Amount,
            payment.Currency,
            payment.Status.ToString(),
            payment.CreatedAt,
            payment.FailureReason);
    }
}
