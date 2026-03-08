using Microsoft.Extensions.Logging;
using Payments.EventDriven.Application.Interfaces;
using Payments.EventDriven.Domain.Entities;
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
        const int maxAttempts = 5;
        var delay = TimeSpan.FromMilliseconds(100);
        Payment? payment = null;

        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            payment = await _repository.GetByIdWithoutLockAsync(paymentId, cancellationToken);
            
            if (payment != null)
            {
                break;
            }
            
            if (attempt < maxAttempts)
            {
                _logger.LogWarning(
                    "Payment {PaymentId} not yet visible (attempt {Attempt}/{Max}). Retrying after {Delay}ms due to possible replication lag...",
                    paymentId, attempt, maxAttempts, delay.TotalMilliseconds);
                
                await Task.Delay(delay, cancellationToken);
                delay = TimeSpan.FromMilliseconds(delay.TotalMilliseconds * 2);
            }
        }

        if (payment is null)
        {
            _logger.LogWarning(
                "Payment {PaymentId} not visible after {MaxAttempts} attempts. Likely replication lag.",
                paymentId, maxAttempts);
            throw new PaymentNotYetVisibleException(paymentId);
        }

        return await _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            var lockedPayment = await _repository.GetByIdAsync(paymentId, ct);
            
            if (lockedPayment is null)
            {
                _logger.LogError("Payment {PaymentId} disappeared between checks", paymentId);
                throw new PaymentNotYetVisibleException(paymentId);
            }

            if (lockedPayment.Status != PaymentStatus.Pending)
            {
                _logger.LogInformation(
                    "Payment {PaymentId} already in status {Status}, skipping processing (idempotent)",
                    paymentId, lockedPayment.Status);
                return ProcessPaymentResult.AlreadyProcessed;
            }

            lockedPayment.MarkAsProcessed();

            _logger.LogInformation(
                "Payment {PaymentId} marked as Processed successfully",
                paymentId);

            return ProcessPaymentResult.Processed;
        }, cancellationToken);
    }
}
