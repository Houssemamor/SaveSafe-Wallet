using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using WalletService.API.DTOs;
using WalletService.API.Services;

namespace WalletService.API.Controllers;

/// <summary>
/// Controller for wallet management operations
/// Provides endpoints for creating, listing, and managing user wallets
/// </summary>
[ApiController]
[Route("api/wallet")]
[Authorize]
public class WalletManagementController : ControllerBase
{
    private readonly IWalletManagementService _walletManagementService;
    private readonly ILogger<WalletManagementController> _logger;

    public WalletManagementController(
        IWalletManagementService walletManagementService,
        ILogger<WalletManagementController> logger)
    {
        _walletManagementService = walletManagementService;
        _logger = logger;
    }

    /// <summary>
    /// Get all wallets for the authenticated user
    /// </summary>
    [HttpGet("wallets")]
    [ProducesResponseType(typeof(IEnumerable<WalletResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IEnumerable<WalletResponseDto>>> GetWallets()
    {
        try
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? throw new UnauthorizedAccessException("User not authenticated.");

            var wallets = await _walletManagementService.GetUserWalletsAsync(userId);
            return Ok(wallets);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving wallets for user");
            return StatusCode(500, "An error occurred while retrieving wallets.");
        }
    }

    /// <summary>
    /// Create a new wallet for the authenticated user
    /// </summary>
    [HttpPost("wallets")]
    [ProducesResponseType(typeof(CreateWalletResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<CreateWalletResponseDto>> CreateWallet([FromBody] CreateManagedWalletRequestDto request)
    {
        try
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? throw new UnauthorizedAccessException("User not authenticated.");

            var response = await _walletManagementService.CreateWalletAsync(userId, request);

            if (!response.Success)
            {
                return BadRequest(response);
            }

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating wallet for user");
            return StatusCode(500, "An error occurred while creating the wallet.");
        }
    }

    /// <summary>
    /// Delete a wallet by ID
    /// </summary>
    [HttpDelete("wallets/{walletId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteWallet(string walletId)
    {
        try
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? throw new UnauthorizedAccessException("User not authenticated.");

            await _walletManagementService.DeleteWalletAsync(userId, walletId);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Wallet not found: {WalletId}", walletId);
            return NotFound("Wallet not found.");
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized access attempt to wallet: {WalletId}", walletId);
            return StatusCode(403, "You do not have permission to delete this wallet.");
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Invalid operation for wallet: {WalletId}", walletId);
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting wallet: {WalletId}", walletId);
            return StatusCode(500, "An error occurred while deleting the wallet.");
        }
    }

    /// <summary>
    /// Set a wallet as the default wallet for the user
    /// </summary>
    [HttpPost("wallets/{walletId}/set-default")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetDefaultWallet(string walletId)
    {
        try
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? throw new UnauthorizedAccessException("User not authenticated.");

            await _walletManagementService.SetDefaultWalletAsync(userId, walletId);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Wallet not found: {WalletId}", walletId);
            return NotFound("Wallet not found.");
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized access attempt to wallet: {WalletId}", walletId);
            return StatusCode(403, "You do not have permission to modify this wallet.");
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Invalid operation for wallet: {WalletId}", walletId);
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting default wallet: {WalletId}", walletId);
            return StatusCode(500, "An error occurred while setting the default wallet.");
        }
    }

    /// <summary>
    /// Get balance for a specific wallet
    /// </summary>
    [HttpGet("wallets/{walletId}/balance")]
    [ProducesResponseType(typeof(WalletBalanceResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<WalletBalanceResponseDto>> GetWalletBalance(string walletId)
    {
        try
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? throw new UnauthorizedAccessException("User not authenticated.");

            var balance = await _walletManagementService.GetWalletBalanceAsync(userId, walletId);
            return Ok(balance);
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Wallet not found: {WalletId}", walletId);
            return NotFound("Wallet not found.");
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized access attempt to wallet: {WalletId}", walletId);
            return StatusCode(403, "You do not have permission to access this wallet.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving wallet balance: {WalletId}", walletId);
            return StatusCode(500, "An error occurred while retrieving the wallet balance.");
        }
    }

    /// <summary>
    /// Transfer funds between user's own wallets
    /// </summary>
    [HttpPost("internal-transfer")]
    [ProducesResponseType(typeof(InternalTransferResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<InternalTransferResponseDto>> TransferBetweenWallets(
        [FromBody] InternalTransferRequestDto request)
    {
        try
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? throw new UnauthorizedAccessException("User not authenticated.");

            var response = await _walletManagementService.TransferBetweenWalletsAsync(
                userId,
                request.SourceWalletId,
                request.TargetWalletId,
                request.Amount);

            if (!response.Success)
            {
                return BadRequest(response);
            }

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error transferring between wallets");
            return StatusCode(500, "An error occurred while processing the transfer.");
        }
    }
}