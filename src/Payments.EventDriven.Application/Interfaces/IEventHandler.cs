namespace Payments.EventDriven.Application.Interfaces;

/// <summary>
/// Interface para handlers de eventos específicos
/// Permite adicionar novos tipos de pagamento sem criar novos workers
/// </summary>
public interface IEventHandler
{
    /// <summary>
    /// Tipo de evento que este handler processa (pix, p2p, boleto, etc.)
    /// </summary>
    string EventType { get; }

    /// <summary>
    /// Processa o evento
    /// </summary>
    Task HandleAsync(string eventPayload, string? correlationId, CancellationToken cancellationToken);
}
