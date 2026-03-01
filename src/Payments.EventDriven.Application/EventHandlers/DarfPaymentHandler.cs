using System.Text.Json;
using Microsoft.Extensions.Logging;
using Payments.EventDriven.Application.Events;
using Payments.EventDriven.Application.Interfaces;

namespace Payments.EventDriven.Application.EventHandlers;

/// <summary>
/// Handler especializado para pagamentos DARF
/// Documento de Arrecadação de Receitas Federais
/// </summary>
public class DarfPaymentHandler : IEventHandler
{
    private readonly IProcessPaymentUseCase _processPaymentUseCase;
    private readonly ILogger<DarfPaymentHandler> _logger;

    public string EventType => "darf-payment-created";

    public DarfPaymentHandler(
        IProcessPaymentUseCase processPaymentUseCase,
        ILogger<DarfPaymentHandler> logger)
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
                "Failed to deserialize DARF PaymentCreatedEvent. CorrelationId: {CorrelationId}",
                correlationId);
            return;
        }

        _logger.LogInformation(
            "Processing DARF payment {PaymentId}. CorrelationId: {CorrelationId}",
            paymentEvent.PaymentId, correlationId);

        // TODO: Adicionar validações específicas do DARF
        // - Validar código da receita
        // - Validar período de apuração
        // - Validar valores e multas

        var result = await _processPaymentUseCase.ProcessAsync(paymentEvent.PaymentId, cancellationToken);

        _logger.LogInformation(
            "DARF payment {PaymentId} processed with result: {Result}. CorrelationId: {CorrelationId}",
            paymentEvent.PaymentId, result, correlationId);
    }
}
