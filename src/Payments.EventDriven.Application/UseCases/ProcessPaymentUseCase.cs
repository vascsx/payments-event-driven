using Microsoft.Extensions.Logging;
using Payments.EventDriven.Application.Interfaces;
using Payments.EventDriven.Domain.Enums;
using Payments.EventDriven.Domain.Exceptions;

namespace Payments.EventDriven.Application.UseCases;

public class ProcessPaymentUseCase : IProcessPaymentUseCase
{
    private readonly IPaymentRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ProcessPaymentUseCase> _logger;

    public ProcessPaymentUseCase(
        IPaymentRepository repository,
        IUnitOfWork unitOfWork,
        ILogger<ProcessPaymentUseCase> logger)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<ProcessPaymentResult> ProcessAsync(Guid paymentId, CancellationToken cancellationToken)
    {
        var payment = await _repository.GetByIdAsync(paymentId, cancellationToken);

        if (payment is null)
        {
            _logger.LogWarning("Payment {PaymentId} not yet visible in database, likely replication lag", paymentId);
            throw new PaymentNotYetVisibleException(paymentId);
        }

        if (payment.Status != PaymentStatus.Pending)
        {
            _logger.LogInformation(
                "Payment {PaymentId} already in status {Status}, skipping processing (idempotent)",
                paymentId, payment.Status);
            return ProcessPaymentResult.AlreadyProcessed;
        }

        payment.MarkAsProcessed();
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Payment {PaymentId} marked as Processed. Amount: {Amount} {Currency}",
            paymentId, payment.Amount, payment.Currency);

        return ProcessPaymentResult.Processed;
    }
}
