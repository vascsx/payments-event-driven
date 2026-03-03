namespace Payments.EventDriven.ProcessPayment.Workers;

/// <summary>
/// Resultado do processamento de uma mensagem Kafka.
/// Determina se o offset deve ser commitado ou não.
/// </summary>
public enum ProcessingResult
{
    /// <summary>
    /// Processado com sucesso, pode commitar offset
    /// </summary>
    Success,
    
    /// <summary>
    /// Falhou mas foi enviado para DLQ, pode commitar offset
    /// </summary>
    SentToDlq,
    
    /// <summary>
    /// Falhou e não foi para DLQ, NÃO commitar (será reprocessado)
    /// </summary>
    Failed,
    
    /// <summary>
    /// Erro transitório, deve fazer retry imediato
    /// </summary>
    RetryableError
}
