using System.Diagnostics;
using System.Text;
using Confluent.Kafka;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Payments.EventDriven.Application.Interfaces;
using Payments.EventDriven.Infrastructure.Settings;

namespace Payments.EventDriven.Infrastructure.Messaging;

/// <summary>
/// Kafka producer com circuit breaker integrado para prevenir cascata de falhas.
/// 
/// Circuit Breaker States:
/// - CLOSED: Normal operation, requests pass through
/// - OPEN: After 5 consecutive failures, rejects all requests for 1 minute (fail-fast)
/// - HALF-OPEN: After timeout, allows one request to test if Kafka recovered
/// 
/// Prevents:
/// - Thundering herd when Kafka is down
/// - Resource exhaustion from infinite retries
/// - Cascading failures to API layer
/// </summary>
public class ResilientKafkaProducer : IEventPublisher, IDisposable
{
    private readonly IProducer<string, string> _producer;
    private readonly ILogger<ResilientKafkaProducer> _logger;
    private readonly IMetricsService _metrics;
    private bool _disposed;
    
    // Circuit breaker state
    private int _consecutiveFailures = 0;
    private DateTime _circuitOpenedAt = DateTime.MinValue;
    private CircuitState _circuitState = CircuitState.Closed;
    
    private const int FailureThreshold = 5;
    private static readonly TimeSpan BreakDuration = TimeSpan.FromMinutes(1);
    private readonly object _circuitLock = new();

    private enum CircuitState { Closed, Open, HalfOpen }

    public ResilientKafkaProducer(
        IOptions<KafkaSettings> settings,
        ILogger<ResilientKafkaProducer> logger,
        IMetricsService metrics)
    {
        _logger = logger;
        _metrics = metrics;

        var config = new ProducerConfig
        {
            BootstrapServers = settings.Value.BootstrapServers,
            Acks = Acks.All,
            EnableIdempotence = true,           // Garante exatamente-uma-vez por partição
            MessageSendMaxRetries = int.MaxValue, // Retry infinito com backoff
            MaxInFlight = 5,                    // Permite paralelismo mantendo ordem com idempotência
            MessageTimeoutMs = 120000,
            LingerMs = 5,
            RetryBackoffMs = 100,
            RequestTimeoutMs = 30000
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
        // Check circuit breaker state
        lock (_circuitLock)
        {
            if (_circuitState == CircuitState.Open)
            {
                if (DateTime.UtcNow - _circuitOpenedAt >= BreakDuration)
                {
                    _circuitState = CircuitState.HalfOpen;
                    _logger.LogWarning(
                        "CIRCUIT BREAKER HALF-OPEN - Testing if Kafka recovered...");
                }
                else
                {
                    _logger.LogError(
                        "Kafka publish rejected by circuit breaker (fail-fast). Topic: {Topic}, Key: {Key}",
                        topic, key);
                    
                    throw new InvalidOperationException(
                        $"Kafka producer circuit breaker is OPEN. Topic: {topic}, Key: {key}. " +
                        $"Retry after {(BreakDuration - (DateTime.UtcNow - _circuitOpenedAt)).TotalSeconds:F0}s.");
                }
            }
        }

        var sw = Stopwatch.StartNew();
        
        try
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

            // CRITICAL: Verificar se Kafka realmente persistiu antes de retornar sucesso
            var deliveryReport = await _producer.ProduceAsync(topic, kafkaMessage, cancellationToken);
            
            if (deliveryReport.Status != PersistenceStatus.Persisted)
            {
                throw new InvalidOperationException(
                    $"Kafka did not persist message. Status: {deliveryReport.Status}, " +
                    $"Topic: {topic}, Partition: {deliveryReport.Partition}, Key: {key}");
            }
            
            // Success - reset circuit breaker
            lock (_circuitLock)
            {
                if (_circuitState == CircuitState.HalfOpen)
                {
                    _circuitState = CircuitState.Closed;
                    _logger.LogInformation(
                        "CIRCUIT BREAKER CLOSED - Kafka producer recovered. Normal operation resumed.");
                }
                _consecutiveFailures = 0;
            }
            
            // Record latency metrics on success
            sw.Stop();
            _metrics.RecordPublishDuration(topic, sw.Elapsed);
        }
        catch (InvalidOperationException) when (_circuitState == CircuitState.Open)
        {
            throw; // Already handled above
        }
        catch (Exception ex) when (IsCircuitBreakerException(ex))
        {
            lock (_circuitLock)
            {
                _consecutiveFailures++;
                
                if (_consecutiveFailures >= FailureThreshold && _circuitState != CircuitState.Open)
                {
                    _circuitState = CircuitState.Open;
                    _circuitOpenedAt = DateTime.UtcNow;
                    
                    _logger.LogError(ex,
                        "CIRCUIT BREAKER OPEN - Kafka producer failing after {Failures} consecutive failures. " +
                        "Requests will fail-fast for {BreakDuration} minute(s).",
                        _consecutiveFailures, BreakDuration.TotalMinutes);
                }
            }
            
            throw;
        }
    }

    private static bool IsCircuitBreakerException(Exception ex)
    {
        return ex is KafkaException
            || ex is TimeoutException
            || (ex is InvalidOperationException && ex.Message.Contains("did not persist"));
    }

    public void Dispose()
    {
        if (_disposed) return;
        _producer.Flush(TimeSpan.FromSeconds(10));
        _producer.Dispose();
        _disposed = true;
    }
}
