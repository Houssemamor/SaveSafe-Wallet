using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using WalletService.API.DTOs;
using WalletService.API.Services;

namespace WalletService.API.Controllers;

[ApiController]
[Route("api/wallet")]
[Authorize]
public class WalletController : ControllerBase
{
    private readonly IWalletService _walletService;

    public WalletController(IWalletService walletService) =>
        _walletService = walletService;

    /// <summary>Returns the current wallet balance for the authenticated user.</summary>
    [HttpGet("balance")]
    [ProducesResponseType(typeof(WalletBalanceResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetBalance()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub);
        if (userId is null) return Unauthorized();

        var balance = await _walletService.GetBalanceAsync(Guid.Parse(userId));
        if (balance is null)
            return NotFound(new { message = "Wallet not found for this user." });

        return Ok(balance);
    }

    /// <summary>Returns paginated wallet transaction history (ledger entries).</summary>
    [HttpGet("history")]
    [ProducesResponseType(typeof(WalletHistoryResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetHistory([FromQuery] string? pageToken = null, [FromQuery] int pageSize = 10)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null) return Unauthorized();

        // Validate pagination parameters
        if (pageSize < 1 || pageSize > 100) pageSize = 10;

        var history = await _walletService.GetWalletHistoryAsync(
            Guid.Parse(userId), pageToken, pageSize);
        if (history is null)
            return NotFound(new { message = "Wallet not found for this user." });

        return Ok(history);
    }

    /// <summary>Transfer funds to another user's wallet.</summary>
    [HttpPost("transfer")]
    [ProducesResponseType(typeof(WalletTransferResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> TransferFunds([FromBody] WalletTransferRequestDto request)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null) return Unauthorized();

        var result = await _walletService.TransferFundsAsync(
            Guid.Parse(userId),
            request.RecipientEmail,
            request.Amount,
            request.Description);

        if (!result.Success)
            return BadRequest(new { message = result.ErrorMessage });

        return Ok(result);
    }

    /// <summary>Export wallet transaction history as CSV.</summary>
    [HttpGet("export")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ExportHistory([FromQuery] string? startDate = null, [FromQuery] string? endDate = null)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null) return Unauthorized();

        var csvData = await _walletService.ExportTransactionHistoryAsync(
            Guid.Parse(userId),
            startDate,
            endDate);

        if (string.IsNullOrEmpty(csvData))
            return NotFound(new { message = "No transactions found for export." });

        var fileName = $"wallet-history-{DateTime.UtcNow:yyyyMMdd-HHmmss}.csv";
        return File(
            System.Text.Encoding.UTF8.GetBytes(csvData),
            "text/csv",
            fileName);
    }
}
