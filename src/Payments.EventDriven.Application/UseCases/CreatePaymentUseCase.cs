using System.Text.Json;
using Microsoft.Extensions.Logging;
using Payments.EventDriven.Application.Constants;
using Payments.EventDriven.Application.DTOs;
using Payments.EventDriven.Application.Events;
using Payments.EventDriven.Application.Interfaces;
using Payments.EventDriven.Domain.Entities;

namespace Payments.EventDriven.Application.UseCases;

public class CreatePaymentUseCase : ICreatePaymentUseCase
{
    private readonly IPaymentRepository _repository;
    private readonly IOutboxRepository _outboxRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<CreatePaymentUseCase> _logger;

    public CreatePaymentUseCase(
        IPaymentRepository repository,
        IOutboxRepository outboxRepository,
        IUnitOfWork unitOfWork,
        ILogger<CreatePaymentUseCase> logger)
    {
        _repository = repository;
        _outboxRepository = outboxRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Guid> ExecuteAsync(
        CreatePaymentRequest request,
        CancellationToken cancellationToken,
        string? correlationId = null)
    {
        var payment = new Payment(request.Amount, request.Currency.ToUpperInvariant());

        var @event = new PaymentCreatedEvent
        {
            PaymentId = payment.Id,
            Amount = payment.Amount,
            Currency = payment.Currency,
            CreatedAt = payment.CreatedAt,
            Version = 1
        };

        var payload = JsonSerializer.Serialize(@event);
        var topic = KafkaTopics.PaymentCreated;
        var messageKey = payment.Id.ToString();

        // Persiste pagamento e evento no outbox na mesma transação (Outbox Pattern)
        await _repository.AddAsync(payment, cancellationToken);
        
        var outboxMessage = new OutboxMessage(topic, messageKey, payload, correlationId);
        await _outboxRepository.AddAsync(outboxMessage, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Payment {PaymentId} created and event persisted to outbox with id {OutboxId}",
            payment.Id, outboxMessage.Id);

        return payment.Id;
    }
}