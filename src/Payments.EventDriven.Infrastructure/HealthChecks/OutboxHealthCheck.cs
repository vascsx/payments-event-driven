using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Payments.EventDriven.Domain.Entities;
using Payments.EventDriven.Infrastructure.Persistence;

namespace Payments.EventDriven.Infrastructure.HealthChecks;

/// <summary>
/// Health check que verifica o estado da fila de outbox.
/// Retorna Unhealthy se houver muitas mensagens pendentes acumuladas.
/// </summary>
public class OutboxHealthCheck : IHealthCheck
{
    private readonly PaymentDbContext _context;
    private const int WarningThreshold = 100;
    private const int CriticalThreshold = 500;

    public OutboxHealthCheck(PaymentDbContext context)
    {
        _context = context;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var pendingCount = await _context.OutboxMessages
                .CountAsync(m => m.Status == OutboxMessageStatus.Pending, cancellationToken);

            var failedCount = await _context.OutboxMessages
                .CountAsync(m => m.Status == OutboxMessageStatus.Failed, cancellationToken);

            var data = new Dictionary<string, object>
            {
                ["PendingMessages"] = pendingCount,
                ["FailedMessages"] = failedCount
            };

            if (pendingCount >= CriticalThreshold)
            {
                return HealthCheckResult.Unhealthy(
                    $"Outbox has {pendingCount} pending messages (critical threshold: {CriticalThreshold})",
                    data: data);
            }

            if (pendingCount >= WarningThreshold)
            {
                return HealthCheckResult.Degraded(
                    $"Outbox has {pendingCount} pending messages (warning threshold: {WarningThreshold})",
                    data: data);
            }

            return HealthCheckResult.Healthy(
                $"Outbox is healthy. Pending: {pendingCount}, Failed: {failedCount}",
                data: data);
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy(
                "Failed to check outbox health",
                exception: ex);
        }
    }
}
