using System.ComponentModel.DataAnnotations;

namespace Payments.EventDriven.Application.DTOs;

public class CreatePaymentRequest
{
    [Required]
    [Range(0.01, double.MaxValue, ErrorMessage = "Amount must be greater than 0.")]
    public decimal Amount { get; set; }

    [Required]
    [StringLength(10, MinimumLength = 3, ErrorMessage = "Currency must be between 3 and 10 characters.")]
    public string Currency { get; set; } = string.Empty;
}