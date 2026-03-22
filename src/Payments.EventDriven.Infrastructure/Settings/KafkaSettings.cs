namespace Payments.EventDriven.Infrastructure.Settings;

public class KafkaSettings
{
    public string BootstrapServers { get; set; } = string.Empty;
    public string Topic { get; set; } = "payment-created";
    public string[] Topics { get; set; } = [];
    public string GroupId { get; set; } = "payment-processor-group";

    /// <summary>
    /// Retorna todos os tópicos configurados. Se Topics não estiver definido, usa Topic como fallback.
    /// </summary>
    public IReadOnlyList<string> AllTopics => Topics.Length > 0 ? Topics : [Topic];
}