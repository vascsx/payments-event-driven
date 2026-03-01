using System.Text.Json;
using Microsoft.Extensions.Logging;
using Payments.EventDriven.Application.Events;
using Payments.EventDriven.Application.Interfaces;

namespace Payments.EventDriven.Application.EventHandlers;

/// <summary>
/// Handler especializado para pagamentos DARJ
/// Documento de Arrecadação do Estado do Rio de Janeiro
/// </summary>
public class DarjPaymentHandler : IEventHandler
{
    private readonly IProcessPaymentUseCase _processPaymentUseCase;
    private readonly ILogger<DarjPaymentHandler> _logger;

    public string EventType => "darj-payment-created";

    public DarjPaymentHandler(
        IProcessPaymentUseCase processPaymentUseCase,
        ILogger<DarjPaymentHandler> logger)
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
                "Failed to deserialize DARJ PaymentCreatedEvent. CorrelationId: {CorrelationId}",
                correlationId);
            return;
        }

        _logger.LogInformation(
            "Processing DARJ payment {PaymentId}. CorrelationId: {CorrelationId}",
            paymentEvent.PaymentId, correlationId);

        // TODO: Adicionar validações específicas do DARJ
        // - Validar código da receita estadual
        // - Validar inscrição estadual
        // - Validar valores e acréscimos

        var result = await _processPaymentUseCase.ProcessAsync(paymentEvent.PaymentId, cancellationToken);

        _logger.LogInformation(
            "DARJ payment {PaymentId} processed with result: {Result}. CorrelationId: {CorrelationId}",
            paymentEvent.PaymentId, result, correlationId);
    }
}
