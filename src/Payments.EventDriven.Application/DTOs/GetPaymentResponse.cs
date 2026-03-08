using Payments.EventDriven.Domain.Enums;

namespace Payments.EventDriven.Application.DTOs;

public record GetPaymentResponse(
    Guid Id,
    decimal Amount,
    string Currency,
    PaymentType Type,
    string Status,
    DateTime CreatedAt,
    string? FailureReason);
