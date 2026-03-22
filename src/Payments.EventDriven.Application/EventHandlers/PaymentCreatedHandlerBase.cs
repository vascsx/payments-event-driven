using System.Text.Json;
using Microsoft.Extensions.Logging;
using Payments.EventDriven.Application.Events;
using Payments.EventDriven.Application.Interfaces;

namespace Payments.EventDriven.Application.EventHandlers;

/// <summary>
/// Base handler para eventos de criação de pagamento.
/// Centraliza a lógica comum de deserialização, logging e processamento,
/// permitindo que handlers específicos adicionem validações customizadas.
/// </summary>
public abstract class PaymentCreatedHandlerBase<TEvent> : IEventHandler
    where TEvent : PaymentCreatedEvent
{
    private readonly IProcessPaymentUseCase _processPaymentUseCase;
    private readonly ILogger _logger;

    public abstract string EventType { get; }

    protected PaymentCreatedHandlerBase(
        IProcessPaymentUseCase processPaymentUseCase,
        ILogger logger)
    {
        _processPaymentUseCase = processPaymentUseCase;
        _logger = logger;
    }

    public async Task HandleAsync(string eventPayload, string? correlationId, CancellationToken cancellationToken)
    {
        var paymentEvent = JsonSerializer.Deserialize<TEvent>(eventPayload);

        if (paymentEvent is null)
        {
            _logger.LogWarning(
                "Failed to deserialize {EventType}. CorrelationId: {CorrelationId}",
                EventType, correlationId);
            return;
        }

        _logger.LogInformation(
            "Processing {EventType} payment {PaymentId}. CorrelationId: {CorrelationId}",
            EventType, paymentEvent.PaymentId, correlationId);

        await ValidateAsync(paymentEvent, cancellationToken);

        var result = await _processPaymentUseCase.ProcessAsync(paymentEvent.PaymentId, cancellationToken);

        _logger.LogInformation(
            "{EventType} payment {PaymentId} processed with result: {Result}. CorrelationId: {CorrelationId}",
            EventType, paymentEvent.PaymentId, result, correlationId);
    }

    /// <summary>
    /// Hook para validações específicas do tipo de pagamento.
    /// Chamado após deserialização e antes do processamento.
    /// </summary>
    protected virtual Task ValidateAsync(TEvent paymentEvent, CancellationToken cancellationToken) => Task.CompletedTask;
}
