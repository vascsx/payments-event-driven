namespace Payments.EventDriven.Application.Interfaces;

public interface IKafkaProducer
{
    Task PublishAsync(string topic, string key, string message, CancellationToken cancellationToken);
}