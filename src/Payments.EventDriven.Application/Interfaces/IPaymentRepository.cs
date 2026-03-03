using Payments.EventDriven.Domain.Entities;

namespace Payments.EventDriven.Application.Interfaces;

public interface IPaymentRepository
{
    Task AddAsync(Payment payment, CancellationToken cancellationToken);
    Task<Payment?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<Payment?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken);
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken);
}