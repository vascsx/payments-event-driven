using Microsoft.Extensions.DependencyInjection;
using Payments.EventDriven.Application.Interfaces;

namespace Payments.EventDriven.Application.EventHandlers;

/// <summary>
/// Factory que resolve handlers de eventos dinamicamente.
/// Registrada como Scoped para garantir que handlers (e suas dependências como DbContext)
/// sejam resolvidos no escopo correto, evitando problemas de thread-safety.
/// </summary>
public class EventHandlerFactory : IEventHandlerFactory
{
    private readonly IServiceProvider _serviceProvider;

    private static readonly object _lock = new();
    private static Dictionary<string, Type>? _handlerTypes;

    public EventHandlerFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        EnsureHandlerTypesDiscovered();
    }

    private void EnsureHandlerTypesDiscovered()
    {
        if (_handlerTypes != null) return;

        lock (_lock)
        {
            if (_handlerTypes != null) return;

            var types = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);
            var handlers = _serviceProvider.GetServices<IEventHandler>();
            foreach (var handler in handlers)
            {
                types[handler.EventType] = handler.GetType();
            }
            _handlerTypes = types;
        }
    }

    public IEventHandler GetHandler(string eventType)
    {
        if (!_handlerTypes!.TryGetValue(eventType, out var handlerType))
        {
            throw new NotSupportedException($"No handler registered for event type: {eventType}");
        }

        return (IEventHandler)_serviceProvider.GetRequiredService(handlerType);
    }

    public bool HasHandler(string eventType)
    {
        return _handlerTypes!.ContainsKey(eventType);
    }
}
