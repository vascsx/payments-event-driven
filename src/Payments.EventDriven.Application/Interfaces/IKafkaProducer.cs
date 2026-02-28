namespace Payments.EventDriven.Application.Interfaces;

public interface IEventPublisher
{
    Task PublishAsync(
        string topic,
        string key,
        string message,
        IReadOnlyDictionary<string, string>? headers = null,
        CancellationToken cancellationToken = default);
}