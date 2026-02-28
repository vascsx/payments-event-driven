using Payments.EventDriven.Domain.Entities;

namespace Payments.EventDriven.Application.Interfaces;

public interface IOutboxRepository
{
    Task AddAsync(OutboxMessage message, CancellationToken cancellationToken);
}
