using Microsoft.Extensions.Logging;
using Payments.EventDriven.Application.Events;
using Payments.EventDriven.Application.Interfaces;

namespace Payments.EventDriven.Application.EventHandlers;

/// <summary>
/// Handler especializado para pagamentos DARF
/// Documento de Arrecadação de Receitas Federais
/// </summary>
public class DarfPaymentHandler : PaymentCreatedHandlerBase<DarfPaymentCreatedEvent>
{
    public override string EventType => "darf-payment-created";

    public DarfPaymentHandler(
        IProcessPaymentUseCase processPaymentUseCase,
        ILogger<DarfPaymentHandler> logger)
        : base(processPaymentUseCase, logger) { }

    protected override Task ValidateAsync(DarfPaymentCreatedEvent paymentEvent, CancellationToken cancellationToken)
    {
        // Validações específicas do DARF podem ser adicionadas aqui
        // - Validar código da receita
        // - Validar período de apuração
        // - Validar valores e multas
        return Task.CompletedTask;
    }
}
