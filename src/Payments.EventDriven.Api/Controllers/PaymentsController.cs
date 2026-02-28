using Microsoft.AspNetCore.Mvc;
using Payments.EventDriven.Application.DTOs;
using Payments.EventDriven.Application.Interfaces;

namespace Payments.EventDriven.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PaymentsController : ControllerBase
{
    private readonly ICreatePaymentUseCase _useCase;

    public PaymentsController(ICreatePaymentUseCase useCase)
    {
        _useCase = useCase;
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreatePaymentRequest request,
        CancellationToken cancellationToken)
    {
        var correlationId = Request.Headers.TryGetValue("X-Correlation-Id", out var cid) && !string.IsNullOrWhiteSpace(cid)
            ? cid.ToString()
            : Guid.NewGuid().ToString();

        Response.Headers["X-Correlation-Id"] = correlationId;

        var id = await _useCase.ExecuteAsync(request, cancellationToken, correlationId);

        return CreatedAtAction(nameof(Create), new { id }, new { id });
    }
}