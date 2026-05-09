using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PaymentService.API.DTOs;
using PaymentService.API.Services;
using System.Security.Claims;
using System.Text;

namespace PaymentService.API.Controllers;

[ApiController]
[Route("api/payment")]
public sealed class PaymentController : ControllerBase
{
    private readonly IPaymentService _paymentService;
    private readonly ILogger<PaymentController> _logger;

    public PaymentController(IPaymentService paymentService, ILogger<PaymentController> logger)
    {
        _paymentService = paymentService;
        _logger = logger;
    }

    [HttpPost("create-checkout-session")]
    [Authorize]
    [ProducesResponseType(typeof(CreateCheckoutSessionResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<CreateCheckoutSessionResponseDto>> CreateCheckoutSession(
        [FromBody] CreateCheckoutSessionRequestDto request,
        CancellationToken ct)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized(new { message = "User is not authenticated." });
        }

        var authorizationHeader = Request.Headers.Authorization.FirstOrDefault();
        var bearerToken = authorizationHeader?.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) == true
            ? authorizationHeader["Bearer ".Length..]
            : string.Empty;

        var response = await _paymentService.CreateCheckoutSessionAsync(bearerToken, userId, request.Amount, ct);
        if (!response.Success)
        {
            return BadRequest(response);
        }

        return Ok(response);
    }

    [HttpPost("webhook")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(StripeWebhookResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<StripeWebhookResponseDto>> Webhook(CancellationToken ct)
    {
        _logger.LogInformation("=== STRIPE WEBHOOK RECEIVED ===");
        _logger.LogInformation("Request Path: {Path}", Request.Path);
        _logger.LogInformation("Request Method: {Method}", Request.Method);
        
        Request.EnableBuffering();

        using var reader = new StreamReader(Request.Body, Encoding.UTF8, leaveOpen: true);
        var payload = await reader.ReadToEndAsync(ct);
        Request.Body.Position = 0;

        _logger.LogInformation("Webhook Payload Length: {Length} bytes", payload.Length);
        _logger.LogDebug("Webhook Payload: {Payload}", payload[..Math.Min(500, payload.Length)]);

        var signatureHeader = Request.Headers["Stripe-Signature"].FirstOrDefault() ?? string.Empty;
        _logger.LogInformation("Stripe-Signature Header: {Signature}", string.IsNullOrWhiteSpace(signatureHeader) ? "MISSING" : "Present");
        
        if (string.IsNullOrWhiteSpace(signatureHeader))
        {
            _logger.LogWarning("Webhook rejected: Missing Stripe-Signature header");
            return BadRequest(new StripeWebhookResponseDto(false, "Missing Stripe-Signature header."));
        }

        var response = await _paymentService.HandleStripeWebhookAsync(payload, signatureHeader, ct);
        
        if (!response.Success)
        {
            _logger.LogWarning("Stripe webhook rejected: {ErrorMessage}", response.ErrorMessage);
            return BadRequest(response);
        }

        _logger.LogInformation("Stripe webhook processed successfully");
        return Ok(response);
    }
}
