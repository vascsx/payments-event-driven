using Payments.EventDriven.Domain.Entities;

namespace Payments.EventDriven.Application.Interfaces;

/// <summary>
/// Repositório para mensagens do Outbox Pattern.
/// Permite persistência transacional de eventos junto com mudanças de estado do domínio.
/// </summary>
public interface IOutboxRepository
{
    /// <summary>
    /// Adiciona uma mensagem ao outbox (dentro da transação corrente)
    /// </summary>
    Task AddAsync(OutboxMessage message, CancellationToken cancellationToken = default);

    /// <summary>
    /// Busca mensagens pendentes do outbox para processamento
    /// </summary>
    Task<IEnumerable<OutboxMessage>> GetPendingMessagesAsync(int maxRetries, int limit, CancellationToken cancellationToken = default);

    /// <summary>
    /// Busca mensagens pendentes com lock pessimista (FOR UPDATE SKIP LOCKED) para processamento em batch.
    /// Garante que múltiplas instâncias não processem as mesmas mensagens.
    /// </summary>
    Task<IList<OutboxMessage>> GetPendingMessagesForProcessingAsync(int maxRetries, int batchSize, CancellationToken cancellationToken = default);

    /// <summary>
    /// Busca mensagens pendentes do outbox para um pagamento específico (usado ao deletar pagamento)
    /// </summary>
    Task<IEnumerable<OutboxMessage>> GetPendingMessagesByPaymentIdAsync(Guid paymentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Atualiza uma mensagem após tentativa de processamento
    /// </summary>
    Task UpdateAsync(OutboxMessage message, CancellationToken cancellationToken = default);
}
