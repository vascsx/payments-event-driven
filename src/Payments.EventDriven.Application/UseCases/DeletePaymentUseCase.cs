using System.Text.Json;
using Microsoft.Extensions.Logging;
using Payments.EventDriven.Application.Constants;
using Payments.EventDriven.Application.Events;
using Payments.EventDriven.Application.Interfaces;
using Payments.EventDriven.Domain.Entities;

namespace Payments.EventDriven.Application.UseCases;

public class DeletePaymentUseCase : IDeletePaymentUseCase
{
    private readonly IPaymentRepository _repository;
    private readonly IOutboxRepository _outboxRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<DeletePaymentUseCase> _logger;

    public DeletePaymentUseCase(
        IPaymentRepository repository,
        IOutboxRepository outboxRepository,
        IUnitOfWork unitOfWork,
        ILogger<DeletePaymentUseCase> logger)
    {
        _repository = repository;
        _outboxRepository = outboxRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<bool> ExecuteAsync(Guid paymentId, CancellationToken cancellationToken)
    {
        return await _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            var payment = await _repository.GetByIdAsync(paymentId, ct);
            
            if (payment is null)
            {
                _logger.LogWarning("Payment {PaymentId} not found for deletion", paymentId);
                return false;
            }

            var pendingOutboxMessages = await _outboxRepository.GetPendingMessagesByPaymentIdAsync(paymentId, ct);
            
            var messagesList = pendingOutboxMessages.ToList();
            if (messagesList.Count > 0)
            {
                _logger.LogInformation(
                    "Cancelling {Count} pending outbox messages for payment {PaymentId}",
                    messagesList.Count, paymentId);
                
                foreach (var msg in messagesList)
                {
                    msg.MarkAsFailed("Payment was deleted before event could be published");
                }
            }

            var deletionEvent = new PaymentDeletedEvent
            {
                PaymentId = paymentId,
                Amount = payment.Amount,
                Currency = payment.Currency,
                DeletedAt = DateTime.UtcNow
            };

            var payload = JsonSerializer.Serialize(deletionEvent);
            var outboxMessage = new OutboxMessage(
                KafkaTopics.PaymentDeleted,
                paymentId.ToString(),
                payload,
                eventType: "payment-deleted");

            await _outboxRepository.AddAsync(outboxMessage, ct);
            await _repository.DeleteAsync(paymentId, ct);

            _logger.LogInformation(
                "Payment {PaymentId} deleted and event persisted to outbox with id {OutboxId}",
                paymentId, outboxMessage.Id);

            return true;
        }, cancellationToken);
    }
}
