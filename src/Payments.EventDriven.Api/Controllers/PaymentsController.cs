using Microsoft.AspNetCore.Mvc;
using Payments.EventDriven.Application.DTOs;
using Payments.EventDriven.Application.Interfaces;

namespace Payments.EventDriven.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PaymentsController : ControllerBase
{
    private readonly ICreatePaymentUseCase _createPaymentUseCase;
    private readonly IGetPaymentUseCase _getPaymentUseCase;
    private readonly IDeletePaymentUseCase _deletePaymentUseCase;

    public PaymentsController(
        ICreatePaymentUseCase createPaymentUseCase,
        IGetPaymentUseCase getPaymentUseCase,
        IDeletePaymentUseCase deletePaymentUseCase)
    {
        _createPaymentUseCase = createPaymentUseCase;
        _getPaymentUseCase = getPaymentUseCase;
        _deletePaymentUseCase = deletePaymentUseCase;
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

        var id = await _createPaymentUseCase.ExecuteAsync(request, cancellationToken, correlationId);

        return CreatedAtAction(nameof(GetById), new { id }, new { id });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var payment = await _getPaymentUseCase.ExecuteAsync(id, cancellationToken);

        if (payment is null)
            return NotFound();

        return Ok(payment);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var deleted = await _deletePaymentUseCase.ExecuteAsync(id, cancellationToken);

        if (!deleted)
            return NotFound();

        return NoContent();
    }
}