namespace Payments.EventDriven.Application.DTOs;

public record GetPaymentResponse(
    Guid Id,
    decimal Amount,
    string Currency,
    string Status,
    DateTime CreatedAt,
    string? FailureReason);
