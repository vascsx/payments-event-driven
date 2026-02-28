using System.ComponentModel.DataAnnotations;

namespace Payments.EventDriven.Application.DTOs;

public class CreatePaymentRequest
{
    [Required]
    [Range(0.01, double.MaxValue, ErrorMessage = "Amount must be greater than 0.")]
    public decimal Amount { get; set; }

    [Required]
    [RegularExpression(@"^[A-Z]{3}$", ErrorMessage = "Currency must be a valid ISO 4217 code (e.g. USD, EUR, BRL).")]
    public string Currency { get; set; } = string.Empty;
}