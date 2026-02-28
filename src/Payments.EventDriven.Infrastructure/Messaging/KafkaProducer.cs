using System.Text;
using Confluent.Kafka;
using Payments.EventDriven.Application.Interfaces;
using Payments.EventDriven.Infrastructure.Settings;

namespace Payments.EventDriven.Infrastructure.Messaging;

public class KafkaProducer : IEventPublisher, IDisposable
{
    private readonly IProducer<string, string> _producer;
    private bool _disposed;

    public KafkaProducer(KafkaSettings settings)
    {
        var config = new ProducerConfig
        {
            BootstrapServers = settings.BootstrapServers,
            Acks = Acks.All,
            EnableIdempotence = true,
            MessageSendMaxRetries = 3,
            MaxInFlight = 1,  
            MessageTimeoutMs = 120000,
            LingerMs = 5
        };

        _producer = new ProducerBuilder<string, string>(config).Build();
    }

    public async Task PublishAsync(
        string topic,
        string key,
        string message,
        IReadOnlyDictionary<string, string>? headers = null,
        CancellationToken cancellationToken = default)
    {
        var kafkaMessage = new Message<string, string>
        {
            Key = key,
            Value = message
        };

        if (headers is not null && headers.Count > 0)
        {
            kafkaMessage.Headers = new Headers();
            foreach (var (k, v) in headers)
                kafkaMessage.Headers.Add(k, Encoding.UTF8.GetBytes(v));
        }

        await _producer.ProduceAsync(topic, kafkaMessage, cancellationToken);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _producer.Flush(TimeSpan.FromSeconds(10));
        _producer.Dispose();
        _disposed = true;
    }
}