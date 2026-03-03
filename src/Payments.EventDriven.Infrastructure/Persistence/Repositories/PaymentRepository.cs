using Microsoft.EntityFrameworkCore;
using Payments.EventDriven.Application.Interfaces;
using Payments.EventDriven.Domain.Entities;

namespace Payments.EventDriven.Infrastructure.Persistence.Repositories;

public class PaymentRepository : IPaymentRepository
{
    private readonly PaymentDbContext _context;

    public PaymentRepository(PaymentDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(Payment payment, CancellationToken cancellationToken)
    {
        // SaveChanges is handled by IUnitOfWork to allow atomic outbox writes
        await _context.Payments.AddAsync(payment, cancellationToken);
    }

    public async Task<Payment?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return await _context.Payments
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
    }

    public async Task<Payment?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken)
    {
        return await _context.Payments
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.IdempotencyKey == idempotencyKey, cancellationToken);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var payment = await _context.Payments
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

        if (payment is null)
            return false;

        _context.Payments.Remove(payment);
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }
}