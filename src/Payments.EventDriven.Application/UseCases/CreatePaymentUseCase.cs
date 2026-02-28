using System.Text.Json;
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

    public CreatePaymentUseCase(
        IPaymentRepository repository,
        IOutboxRepository outboxRepository,
        IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _outboxRepository = outboxRepository;
        _unitOfWork = unitOfWork;
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
        var outboxMessage = new OutboxMessage(KafkaTopics.PaymentCreated, payment.Id.ToString(), payload, correlationId);

        // Atomic: persist payment + outbox in a single transaction
        await _repository.AddAsync(payment, cancellationToken);
        await _outboxRepository.AddAsync(outboxMessage, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return payment.Id;
    }
}