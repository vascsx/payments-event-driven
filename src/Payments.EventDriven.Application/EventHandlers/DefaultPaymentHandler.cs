using Microsoft.Extensions.Logging;
using Payments.EventDriven.Application.Events;
using Payments.EventDriven.Application.Interfaces;

namespace Payments.EventDriven.Application.EventHandlers;

/// <summary>
/// Handler padrão para pagamentos genéricos
/// Processa PaymentCreatedEvent sem tipo específico
/// </summary>
public class DefaultPaymentHandler : PaymentCreatedHandlerBase<PaymentCreatedEvent>
{
    public override string EventType => "payment-created";

    public DefaultPaymentHandler(
        IProcessPaymentUseCase processPaymentUseCase,
        ILogger<DefaultPaymentHandler> logger)
        : base(processPaymentUseCase, logger) { }
}
