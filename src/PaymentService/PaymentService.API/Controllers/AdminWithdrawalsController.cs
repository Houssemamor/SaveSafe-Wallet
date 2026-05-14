using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PaymentService.API.Attributes;
using PaymentService.API.DTOs;
using PaymentService.API.Services;
using System.Security.Claims;

namespace PaymentService.API.Controllers;

[ApiController]
[Route("api/admin/withdrawals")]
[Authorize]
[RequireAdmin]
public sealed class AdminWithdrawalsController : ControllerBase
{
    private readonly IPaymentService _paymentService;

    public AdminWithdrawalsController(IPaymentService paymentService)
    {
        _paymentService = paymentService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<WithdrawalRequestDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<WithdrawalRequestDto>>> GetAll(
        [FromQuery] string? status,
        CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(status)
            && !string.Equals(status, "Pending", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(status, "Approved", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(status, "Rejected", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { message = "Status filter must be Pending, Approved, or Rejected." });
        }

        var response = await _paymentService.GetAllWithdrawRequestsAsync(status, ct);
        return Ok(response);
    }

    [HttpPatch("{id:guid}/approve")]
    [ProducesResponseType(typeof(WithdrawalRequestDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<WithdrawalRequestDto>> Approve(
        [FromRoute] Guid id,
        CancellationToken ct)
    {
        if (!TryGetAdminId(out var adminId))
        {
            return Unauthorized(new { message = "Admin is not authenticated." });
        }

        try
        {
            var updated = await _paymentService.ApproveWithdrawalAsync(id, adminId, ct);
            if (updated is null)
            {
                return NotFound(new { message = "Withdrawal request not found." });
            }

            return Ok(updated);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPatch("{id:guid}/reject")]
    [ProducesResponseType(typeof(WithdrawalRequestDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<WithdrawalRequestDto>> Reject(
        [FromRoute] Guid id,
        [FromBody] RejectWithdrawalRequestDto request,
        CancellationToken ct)
    {
        if (!TryGetAdminId(out var adminId))
        {
            return Unauthorized(new { message = "Admin is not authenticated." });
        }

        try
        {
            var updated = await _paymentService.RejectWithdrawalAsync(id, adminId, request.RejectionReason, ct);
            if (updated is null)
            {
                return NotFound(new { message = "Withdrawal request not found." });
            }

            return Ok(updated);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    private bool TryGetAdminId(out Guid adminId)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(userIdClaim, out adminId);
    }
}
