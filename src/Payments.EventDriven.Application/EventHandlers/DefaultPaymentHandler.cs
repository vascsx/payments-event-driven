using System.Text.Json;
using Microsoft.Extensions.Logging;
using Payments.EventDriven.Application.Events;
using Payments.EventDriven.Application.Interfaces;

namespace Payments.EventDriven.Application.EventHandlers;

/// <summary>
/// Handler padrão para pagamentos genéricos
/// Processa PaymentCreatedEvent sem tipo específico
/// </summary>
public class DefaultPaymentHandler : IEventHandler
{
    private readonly IProcessPaymentUseCase _processPaymentUseCase;
    private readonly ILogger<DefaultPaymentHandler> _logger;

    public string EventType => "payment-created";

    public DefaultPaymentHandler(
        IProcessPaymentUseCase processPaymentUseCase,
        ILogger<DefaultPaymentHandler> logger)
    {
        _processPaymentUseCase = processPaymentUseCase;
        _logger = logger;
    }

    public async Task HandleAsync(string eventPayload, string? correlationId, CancellationToken cancellationToken)
    {
        var paymentEvent = JsonSerializer.Deserialize<PaymentCreatedEvent>(eventPayload);

        if (paymentEvent is null)
        {
            _logger.LogWarning(
                "Failed to deserialize PaymentCreatedEvent. CorrelationId: {CorrelationId}, Payload: {Payload}",
                correlationId, eventPayload);
            return;
        }

        _logger.LogInformation(
            "Processing default payment {PaymentId}. CorrelationId: {CorrelationId}",
            paymentEvent.PaymentId, correlationId);

        var result = await _processPaymentUseCase.ProcessAsync(paymentEvent.PaymentId, cancellationToken);

        _logger.LogInformation(
            "Default payment {PaymentId} processed with result: {Result}. CorrelationId: {CorrelationId}",
            paymentEvent.PaymentId, result, correlationId);
    }
}
