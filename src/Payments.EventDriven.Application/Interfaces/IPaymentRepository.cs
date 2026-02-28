using Payments.EventDriven.Domain.Entities;

namespace Payments.EventDriven.Application.Interfaces;

public interface IPaymentRepository
{
    Task AddAsync(Payment payment, CancellationToken cancellationToken);
}