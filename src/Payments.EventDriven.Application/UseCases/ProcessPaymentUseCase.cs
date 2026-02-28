using Payments.EventDriven.Application.Interfaces;
using Payments.EventDriven.Domain.Enums;
using Payments.EventDriven.Domain.Exceptions;

namespace Payments.EventDriven.Application.UseCases;

public class ProcessPaymentUseCase : IProcessPaymentUseCase
{
    private readonly IPaymentRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public ProcessPaymentUseCase(
        IPaymentRepository repository,
        IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ProcessPaymentResult> ProcessAsync(Guid paymentId, CancellationToken cancellationToken)
    {
        var payment = await _repository.GetByIdAsync(paymentId, cancellationToken);

        if (payment is null)
            throw new PaymentNotYetVisibleException(paymentId);

        if (payment.Status != PaymentStatus.Pending)
            return ProcessPaymentResult.AlreadyProcessed;

        payment.MarkAsProcessed();
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ProcessPaymentResult.Processed;
    }
}
