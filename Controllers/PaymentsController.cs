using Microsoft.AspNetCore.Mvc;
using Payments.EventDriven.Application.DTOs;
using Payments.EventDriven.Application.UseCases;

namespace Payments.EventDriven.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PaymentsController : ControllerBase
{
    private readonly CreatePaymentUseCase _useCase;

    public PaymentsController(CreatePaymentUseCase useCase)
    {
        _useCase = useCase;
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreatePaymentRequest request,
        CancellationToken cancellationToken)
    {
        var id = await _useCase.ExecuteAsync(request, cancellationToken);

        return CreatedAtAction(nameof(Create), new { id }, new { id });
    }
}