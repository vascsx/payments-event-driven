using Payments.EventDriven.Application.Interfaces;

namespace Payments.EventDriven.Application.UseCases;

public class DeletePaymentUseCase : IDeletePaymentUseCase
{
    private readonly IPaymentRepository _repository;

    public DeletePaymentUseCase(IPaymentRepository repository)
    {
        _repository = repository;
    }

    public async Task<bool> ExecuteAsync(Guid paymentId, CancellationToken cancellationToken)
    {
        return await _repository.DeleteAsync(paymentId, cancellationToken);
    }
}
