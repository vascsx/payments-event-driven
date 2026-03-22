using System.Text.Json;
using Microsoft.Extensions.Logging;
using Payments.EventDriven.Application.Events;
using Payments.EventDriven.Application.Interfaces;

namespace Payments.EventDriven.Application.EventHandlers;

/// <summary>
/// Handler para eventos de deleção de pagamento.
/// Processa o evento e permite que lógica downstream reaja à deleção.
/// </summary>
public class PaymentDeletedHandler : IEventHandler
{
    private readonly ILogger<PaymentDeletedHandler> _logger;

    public string EventType => "payment-deleted";

    public PaymentDeletedHandler(ILogger<PaymentDeletedHandler> logger)
    {
        _logger = logger;
    }

    public Task HandleAsync(string eventPayload, string? correlationId, CancellationToken cancellationToken)
    {
        var deletedEvent = JsonSerializer.Deserialize<PaymentDeletedEvent>(eventPayload);

        if (deletedEvent is null)
        {
            _logger.LogWarning(
                "Failed to deserialize PaymentDeletedEvent. CorrelationId: {CorrelationId}",
                correlationId);
            return Task.CompletedTask;
        }

        _logger.LogInformation(
            "Payment {PaymentId} deletion event processed. Amount: {Amount} {Currency}, DeletedAt: {DeletedAt}. CorrelationId: {CorrelationId}",
            deletedEvent.PaymentId, deletedEvent.Amount, deletedEvent.Currency, deletedEvent.DeletedAt, correlationId);

        return Task.CompletedTask;
    }
}
