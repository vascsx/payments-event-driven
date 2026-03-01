namespace Payments.EventDriven.Application.Interfaces;

/// <summary>
/// Factory para resolver handlers de eventos por tipo
/// </summary>
public interface IEventHandlerFactory
{
    /// <summary>
    /// Obtém o handler apropriado para o tipo de evento
    /// </summary>
    IEventHandler GetHandler(string eventType);

    /// <summary>
    /// Verifica se existe handler para o tipo de evento
    /// </summary>
    bool HasHandler(string eventType);
}
