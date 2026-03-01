using Microsoft.Extensions.DependencyInjection;
using Payments.EventDriven.Application.Interfaces;

namespace Payments.EventDriven.Application.EventHandlers;

/// <summary>
/// Factory que resolve handlers de eventos dinamicamente
/// </summary>
public class EventHandlerFactory : IEventHandlerFactory
{
    private readonly IServiceProvider _serviceProvider;
    private readonly Dictionary<string, Type> _handlerTypes;

    public EventHandlerFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        _handlerTypes = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);
        
        // Registra handlers disponíveis
        RegisterHandlers();
    }

    private void RegisterHandlers()
    {
        // Busca todos os IEventHandler registrados no DI
        var handlers = _serviceProvider.GetServices<IEventHandler>();
        
        foreach (var handler in handlers)
        {
            _handlerTypes[handler.EventType] = handler.GetType();
        }
    }

    public IEventHandler GetHandler(string eventType)
    {
        if (!_handlerTypes.TryGetValue(eventType, out var handlerType))
        {
            throw new NotSupportedException($"No handler registered for event type: {eventType}");
        }

        return (IEventHandler)_serviceProvider.GetRequiredService(handlerType);
    }

    public bool HasHandler(string eventType)
    {
        return _handlerTypes.ContainsKey(eventType);
    }
}
