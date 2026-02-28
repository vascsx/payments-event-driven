using Microsoft.EntityFrameworkCore;
using Payments.EventDriven.Application.Interfaces;
using Payments.EventDriven.Domain.Entities;

namespace Payments.EventDriven.Infrastructure.Persistence.Repositories;

public class OutboxRepository : IOutboxRepository
{
    private readonly PaymentDbContext _context;

    public OutboxRepository(PaymentDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(OutboxMessage message, CancellationToken cancellationToken)
    {
        await _context.OutboxMessages.AddAsync(message, cancellationToken);
    }

    public async Task<IReadOnlyList<OutboxMessage>> GetPendingAsync(int limit, CancellationToken cancellationToken)
    {
        return await _context.OutboxMessages
            .Where(m => m.ProcessedAt == null)
            .OrderBy(m => m.Topic)
            .ThenBy(m => m.MessageKey)
            .ThenBy(m => m.CreatedAt)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }
}
