using Microsoft.AspNetCore.Mvc;
using WalletService.API.DTOs;
using WalletService.API.Services;

namespace WalletService.API.Controllers;

/// <summary>
/// Internal-only endpoints called by other microservices.
/// Protected by shared API key (X-Internal-Api-Key header), NOT by JWT.
/// Only reachable within the Docker network - never exposed directly to frontend.
/// </summary>
[ApiController]
[Route("api/internal")]
public class InternalController : ControllerBase
{
    private readonly IWalletService _walletService;
    private readonly IConfiguration _config;
    private readonly ILogger<InternalController> _logger;

    public InternalController(
        IWalletService walletService,
        IConfiguration config,
        ILogger<InternalController> logger)
    {
        _walletService = walletService;
        _config = config;
        _logger = logger;
    }

    /// <summary>Provisions a new wallet for a newly registered user.</summary>
    [HttpPost("wallet/provision")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ProvisionWallet([FromBody] CreateWalletRequestDto request)
    {
        // Validate shared API key - guards against external callers
        var apiKey = Request.Headers["X-Internal-Api-Key"].FirstOrDefault();
        var expectedKey = _config["InternalApi:ApiKey"];

        if (string.IsNullOrEmpty(apiKey) || apiKey != expectedKey)
        {
            _logger.LogWarning(
                "Unauthorized internal provisioning attempt for user {UserId}", request.UserId);
            return Unauthorized(new { message = "Invalid internal API key." });
        }

        var accountId = await _walletService.CreateAccountAsync(
            request.UserId, request.Currency);

        return CreatedAtAction(nameof(ProvisionWallet), new { accountId });
    }

    /// <summary>Credits a wallet after Stripe payment confirmation.</summary>
    [HttpPost("wallet/topup")]
    [ProducesResponseType(typeof(InternalTopUpResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreditTopUp([FromBody] InternalTopUpRequestDto request)
    {
        var apiKey = Request.Headers["X-Internal-Api-Key"].FirstOrDefault();
        var expectedKey = _config["InternalApi:ApiKey"];

        if (string.IsNullOrEmpty(apiKey) || apiKey != expectedKey)
        {
            _logger.LogWarning(
                "Unauthorized top-up credit attempt for wallet {WalletId} and user {UserId}",
                request.WalletId, request.UserId);
            return Unauthorized(new { message = "Invalid internal API key." });
        }

        var response = await _walletService.CreditTopUpAsync(request);

        if (!response.Success && !response.Duplicate)
        {
            return BadRequest(response);
        }

        return Ok(response);
    }
}
