using Microsoft.Extensions.Logging;
using Payments.EventDriven.Application.Events;
using Payments.EventDriven.Application.Interfaces;

namespace Payments.EventDriven.Application.EventHandlers;

/// <summary>
/// Handler especializado para pagamentos DARJ
/// Documento de Arrecadação do Estado do Rio de Janeiro
/// </summary>
public class DarjPaymentHandler : PaymentCreatedHandlerBase<DarjPaymentCreatedEvent>
{
    public override string EventType => "darj-payment-created";

    public DarjPaymentHandler(
        IProcessPaymentUseCase processPaymentUseCase,
        ILogger<DarjPaymentHandler> logger)
        : base(processPaymentUseCase, logger) { }

    protected override Task ValidateAsync(DarjPaymentCreatedEvent paymentEvent, CancellationToken cancellationToken)
    {
        // Validações específicas do DARJ podem ser adicionadas aqui
        // - Validar código da receita estadual
        // - Validar inscrição estadual
        // - Validar valores e acréscimos
        return Task.CompletedTask;
    }
}
