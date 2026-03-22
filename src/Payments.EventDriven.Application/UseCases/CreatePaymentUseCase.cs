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
        // Fast idempotency check outside transaction (optimization)
        if (!string.IsNullOrWhiteSpace(request.IdempotencyKey))
        {
            var existingPayment = await _repository.GetByIdempotencyKeyAsync(
                request.IdempotencyKey, 
                cancellationToken);

            if (existingPayment is not null)
            {
                _logger.LogInformation(
                    "Payment with idempotency key {IdempotencyKey} already exists with id {PaymentId}. Returning existing payment (idempotent).",
                    request.IdempotencyKey, existingPayment.Id);
                
                return existingPayment.Id;
            }
        }

        try
        {
            return await _unitOfWork.ExecuteInTransactionAsync(async ct =>
            {
                var payment = new Payment(
                    request.Amount, 
                    request.Currency.ToUpperInvariant(), 
                    request.Type,
                    request.IdempotencyKey);

                DomainEventBase @event = payment.Type switch
                {
                    Domain.Enums.PaymentType.Darf => new DarfPaymentCreatedEvent
                    {
                        PaymentId = payment.Id,
                        Amount = payment.Amount,
                        Currency = payment.Currency,
                        CreatedAt = payment.CreatedAt,
                        CorrelationId = correlationId,
                        CodigoReceita = request.CodigoReceita,
                        PeriodoApuracao = request.PeriodoApuracao,
                        NumeroReferencia = request.NumeroReferencia
                    },
                    Domain.Enums.PaymentType.Darj => new DarjPaymentCreatedEvent
                    {
                        PaymentId = payment.Id,
                        Amount = payment.Amount,
                        Currency = payment.Currency,
                        CreatedAt = payment.CreatedAt,
                        CorrelationId = correlationId,
                        CodigoReceitaEstadual = request.CodigoReceitaEstadual,
                        InscricaoEstadual = request.InscricaoEstadual
                    },
                    _ => new PaymentCreatedEvent
                    {
                        PaymentId = payment.Id,
                        Amount = payment.Amount,
                        Currency = payment.Currency,
                        CreatedAt = payment.CreatedAt,
                        CorrelationId = correlationId
                    }
                };

                var payload = JsonSerializer.Serialize(@event, @event.GetType());
                var topic = KafkaTopics.PaymentCreated;
                var messageKey = payment.Id.ToString();
                var eventType = @event.EventType;

                await _repository.AddAsync(payment, ct);
                
                var outboxMessage = new OutboxMessage(topic, messageKey, payload, correlationId, eventType);
                await _outboxRepository.AddAsync(outboxMessage, ct);

                _logger.LogInformation(
                    "Payment {PaymentId} of type {PaymentType} created and event {EventType} persisted to outbox with id {OutboxId}. IdempotencyKey: {IdempotencyKey}",
                    payment.Id, payment.Type, eventType, outboxMessage.Id, payment.IdempotencyKey ?? "<none>");

                return payment.Id;
            }, cancellationToken);
        }
        catch (Exception ex) when (
            !string.IsNullOrWhiteSpace(request.IdempotencyKey) &&
            IsUniqueConstraintViolation(ex))
        {
            _logger.LogInformation(
                "Concurrent request detected for idempotency key {IdempotencyKey}. Returning existing payment.",
                request.IdempotencyKey);

            var existingPayment = await _repository.GetByIdempotencyKeyAsync(
                request.IdempotencyKey!, cancellationToken);

            if (existingPayment is not null)
                return existingPayment.Id;

            throw;
        }
    }

    private static bool IsUniqueConstraintViolation(Exception ex)
    {
        var current = ex;
        while (current != null)
        {
            if (current.Message.Contains("23505", StringComparison.Ordinal) ||
                current.Message.Contains("duplicate key", StringComparison.OrdinalIgnoreCase))
                return true;
            current = current.InnerException;
        }
        return false;
    }
}