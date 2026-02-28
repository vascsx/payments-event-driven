namespace Payments.EventDriven.Infrastructure.Settings;

public class KafkaSettings
{
    public string BootstrapServers { get; set; } = string.Empty;
    public string Topic { get; set; } = "payment-created";
    public string GroupId { get; set; } = "payment-processor-group";
}