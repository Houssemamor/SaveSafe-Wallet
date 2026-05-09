using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using System.Security.Claims;
using WalletService.API.Controllers;
using WalletService.API.DTOs;
using WalletService.API.Services;

namespace WalletService.API.Tests;

/// <summary>
/// Unit tests for WalletManagementController
/// Tests API endpoints for wallet management operations
/// </summary>
public class WalletManagementControllerTests
{
    private readonly Mock<IWalletManagementService> _mockService;
    private readonly Mock<ILogger<WalletManagementController>> _mockLogger;
    private readonly WalletManagementController _controller;

    public WalletManagementControllerTests()
    {
        _mockService = new Mock<IWalletManagementService>();
        _mockLogger = new Mock<ILogger<WalletManagementController>>();
        _controller = new WalletManagementController(_mockService.Object, _mockLogger.Object);

        // Setup default user context
        var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "test-user-123")
        }));

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = user }
        };
    }

    [Fact]
    /// <summary>
    /// Test that GetWallets returns user wallets successfully
    /// </summary>
    public async Task GetWallets_ReturnsOk_WhenUserHasWallets()
    {
        // Arrange
        var userId = "test-user-123";
        var wallets = new List<WalletResponseDto>
        {
            new WalletResponseDto
            {
                Id = "wallet1",
                Name = "Main Wallet",
                Type = "checking",
                Balance = 1000m,
                Currency = "USD",
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                IsDefault = true
            },
            new WalletResponseDto
            {
                Id = "wallet2",
                Name = "Savings",
                Type = "savings",
                Balance = 5000m,
                Currency = "USD",
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                IsDefault = false
            }
        };

        _mockService
            .Setup(service => service.GetUserWalletsAsync(userId))
            .ReturnsAsync(wallets);

        // Act
        var result = await _controller.GetWallets();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var returnedWallets = Assert.IsAssignableFrom<IEnumerable<WalletResponseDto>>(okResult.Value);
        Assert.Equal(2, returnedWallets.Count());

        _mockService.Verify(service => service.GetUserWalletsAsync(userId), Times.Once);
    }

    [Fact]
    /// <summary>
    /// Test that GetWallets returns 401 when user is not authenticated
    /// </summary>
    public async Task GetWallets_ReturnsUnauthorized_WhenUserNotAuthenticated()
    {
        // Arrange
        var unauthorizedUser = new ClaimsPrincipal(new ClaimsIdentity());
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = unauthorizedUser }
        };

        // Act
        var result = await _controller.GetWallets();

        // Assert
        var objectResult = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(500, objectResult.StatusCode);
        Assert.Equal("An error occurred while retrieving wallets.", objectResult.Value);
    }

    [Fact]
    /// <summary>
    /// Test that CreateWallet returns created wallet successfully
    /// </summary>
    public async Task CreateWallet_ReturnsCreatedWallet_WhenRequestIsValid()
    {
        // Arrange
        var userId = "test-user-123";
        var request = new CreateManagedWalletRequestDto
        {
            Name = "New Wallet",
            Type = "checking",
            Currency = "USD",
            InitialBalance = 500
        };

        var response = new CreateWalletResponseDto
        {
            Success = true,
            Wallet = new WalletResponseDto
            {
                Id = "new-wallet-123",
                Name = "New Wallet",
                Type = "checking",
                Balance = 500m,
                Currency = "USD",
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                IsDefault = false
            }
        };

        _mockService
            .Setup(service => service.CreateWalletAsync(userId, request))
            .ReturnsAsync(response);

        // Act
        var result = await _controller.CreateWallet(request);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var createdWallet = Assert.IsType<CreateWalletResponseDto>(okResult.Value);
        Assert.True(createdWallet.Success);
        Assert.NotNull(createdWallet.Wallet);
        Assert.Equal("New Wallet", createdWallet.Wallet.Name);

        _mockService.Verify(service => service.CreateWalletAsync(userId, request), Times.Once);
    }

    [Fact]
    /// <summary>
    /// Test that CreateWallet returns bad request when validation fails
    /// </summary>
    public async Task CreateWallet_ReturnsBadRequest_WhenValidationFails()
    {
        // Arrange
        var userId = "test-user-123";
        var request = new CreateManagedWalletRequestDto
        {
            Name = "", // Invalid: empty name
            Type = "checking",
            Currency = "USD"
        };

        var response = new CreateWalletResponseDto
        {
            Success = false,
            ErrorMessage = "Wallet name is required."
        };

        _mockService
            .Setup(service => service.CreateWalletAsync(userId, request))
            .ReturnsAsync(response);

        // Act
        var result = await _controller.CreateWallet(request);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result.Result);
        var errorResponse = Assert.IsType<CreateWalletResponseDto>(badRequestResult.Value);
        Assert.False(errorResponse.Success);
        Assert.Equal("Wallet name is required.", errorResponse.ErrorMessage);

        _mockService.Verify(service => service.CreateWalletAsync(userId, request), Times.Once);
    }

    [Fact]
    /// <summary>
    /// Test that DeleteWallet returns no content when successful
    /// </summary>
    public async Task DeleteWallet_ReturnsNoContent_WhenDeletionSuccessful()
    {
        // Arrange
        var userId = "test-user-123";
        var walletId = "wallet-to-delete";

        _mockService
            .Setup(service => service.DeleteWalletAsync(userId, walletId))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _controller.DeleteWallet(walletId);

        // Assert
        Assert.IsType<NoContentResult>(result);

        _mockService.Verify(service => service.DeleteWalletAsync(userId, walletId), Times.Once);
    }

    [Fact]
    /// <summary>
    /// Test that DeleteWallet returns not found when wallet doesn't exist
    /// </summary>
    public async Task DeleteWallet_ReturnsNotFound_WhenWalletNotFound()
    {
        // Arrange
        var userId = "test-user-123";
        var walletId = "nonexistent-wallet";

        _mockService
            .Setup(service => service.DeleteWalletAsync(userId, walletId))
            .ThrowsAsync(new KeyNotFoundException("Wallet not found."));

        // Act
        var result = await _controller.DeleteWallet(walletId);

        // Assert
        var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
        Assert.Equal("Wallet not found.", notFoundResult.Value);

        _mockService.Verify(service => service.DeleteWalletAsync(userId, walletId), Times.Once);
    }

    [Fact]
    /// <summary>
    /// Test that DeleteWallet returns forbidden when user doesn't have permission
    /// </summary>
    public async Task DeleteWallet_ReturnsForbidden_WhenUserDoesNotHavePermission()
    {
        // Arrange
        var userId = "test-user-123";
        var walletId = "restricted-wallet";

        _mockService
            .Setup(service => service.DeleteWalletAsync(userId, walletId))
            .ThrowsAsync(new UnauthorizedAccessException("You do not have permission to delete this wallet."));

        // Act
        var result = await _controller.DeleteWallet(walletId);

        // Assert
        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(403, objectResult.StatusCode);
        Assert.Equal("You do not have permission to delete this wallet.", objectResult.Value);

        _mockService.Verify(service => service.DeleteWalletAsync(userId, walletId), Times.Once);
    }

    [Fact]
    /// <summary>
    /// Test that DeleteWallet returns bad request when operation is invalid
    /// </summary>
    public async Task DeleteWallet_ReturnsBadRequest_WhenOperationIsInvalid()
    {
        // Arrange
        var userId = "test-user-123";
        var walletId = "default-wallet";

        _mockService
            .Setup(service => service.DeleteWalletAsync(userId, walletId))
            .ThrowsAsync(new InvalidOperationException("Cannot delete default wallet."));

        // Act
        var result = await _controller.DeleteWallet(walletId);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Cannot delete default wallet.", badRequestResult.Value);

        _mockService.Verify(service => service.DeleteWalletAsync(userId, walletId), Times.Once);
    }

    [Fact]
    /// <summary>
    /// Test that SetDefaultWallet returns no content when successful
    /// </summary>
    public async Task SetDefaultWallet_ReturnsNoContent_WhenSettingSuccessful()
    {
        // Arrange
        var userId = "test-user-123";
        var walletId = "wallet-to-set-default";

        _mockService
            .Setup(service => service.SetDefaultWalletAsync(userId, walletId))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _controller.SetDefaultWallet(walletId);

        // Assert
        Assert.IsType<NoContentResult>(result);

        _mockService.Verify(service => service.SetDefaultWalletAsync(userId, walletId), Times.Once);
    }

    [Fact]
    /// <summary>
    /// Test that SetDefaultWallet returns not found when wallet doesn't exist
    /// </summary>
    public async Task SetDefaultWallet_ReturnsNotFound_WhenWalletNotFound()
    {
        // Arrange
        var userId = "test-user-123";
        var walletId = "nonexistent-wallet";

        _mockService
            .Setup(service => service.SetDefaultWalletAsync(userId, walletId))
            .ThrowsAsync(new KeyNotFoundException("Wallet not found."));

        // Act
        var result = await _controller.SetDefaultWallet(walletId);

        // Assert
        var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
        Assert.Equal("Wallet not found.", notFoundResult.Value);

        _mockService.Verify(service => service.SetDefaultWalletAsync(userId, walletId), Times.Once);
    }

    [Fact]
    /// <summary>
    /// Test that SetDefaultWallet returns forbidden when user doesn't have permission
    /// </summary>
    public async Task SetDefaultWallet_ReturnsForbidden_WhenUserDoesNotHavePermission()
    {
        // Arrange
        var userId = "test-user-123";
        var walletId = "restricted-wallet";

        _mockService
            .Setup(service => service.SetDefaultWalletAsync(userId, walletId))
            .ThrowsAsync(new UnauthorizedAccessException("You do not have permission to modify this wallet."));

        // Act
        var result = await _controller.SetDefaultWallet(walletId);

        // Assert
        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(403, objectResult.StatusCode);
        Assert.Equal("You do not have permission to modify this wallet.", objectResult.Value);

        _mockService.Verify(service => service.SetDefaultWalletAsync(userId, walletId), Times.Once);
    }

    [Fact]
    /// <summary>
    /// Test that SetDefaultWallet returns bad request when operation is invalid
    /// </summary>
    public async Task SetDefaultWallet_ReturnsBadRequest_WhenOperationIsInvalid()
    {
        // Arrange
        var userId = "test-user-123";
        var walletId = "inactive-wallet";

        _mockService
            .Setup(service => service.SetDefaultWalletAsync(userId, walletId))
            .ThrowsAsync(new InvalidOperationException("Cannot set inactive wallet as default."));

        // Act
        var result = await _controller.SetDefaultWallet(walletId);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Cannot set inactive wallet as default.", badRequestResult.Value);

        _mockService.Verify(service => service.SetDefaultWalletAsync(userId, walletId), Times.Once);
    }

    [Fact]
    /// <summary>
    /// Test that GetWalletBalance returns wallet balance successfully
    /// </summary>
    public async Task GetWalletBalance_ReturnsBalance_WhenWalletExists()
    {
        // Arrange
        var userId = "test-user-123";
        var walletId = "wallet-with-balance";

        var balanceResponse = new WalletBalanceResponseDto(
            Guid.NewGuid(),
            "SSW-1234567890",
            "EUR",
            2500.75m,
            DateTime.UtcNow
        );

        _mockService
            .Setup(service => service.GetWalletBalanceAsync(userId, walletId))
            .ReturnsAsync(balanceResponse);

        // Act
        var result = await _controller.GetWalletBalance(walletId);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var balance = Assert.IsType<WalletBalanceResponseDto>(okResult.Value);
        Assert.Equal(2500.75m, balance.Balance);
        Assert.Equal("EUR", balance.Currency);

        _mockService.Verify(service => service.GetWalletBalanceAsync(userId, walletId), Times.Once);
    }

    [Fact]
    /// <summary>
    /// Test that GetWalletBalance returns not found when wallet doesn't exist
    /// </summary>
    public async Task GetWalletBalance_ReturnsNotFound_WhenWalletNotFound()
    {
        // Arrange
        var userId = "test-user-123";
        var walletId = "nonexistent-wallet";

        _mockService
            .Setup(service => service.GetWalletBalanceAsync(userId, walletId))
            .ThrowsAsync(new KeyNotFoundException("Wallet not found."));

        // Act
        var result = await _controller.GetWalletBalance(walletId);

        // Assert
        var notFoundResult = Assert.IsType<NotFoundObjectResult>(result.Result);
        Assert.Equal("Wallet not found.", notFoundResult.Value);

        _mockService.Verify(service => service.GetWalletBalanceAsync(userId, walletId), Times.Once);
    }

    [Fact]
    /// <summary>
    /// Test that GetWalletBalance returns forbidden when user doesn't have permission
    /// </summary>
    public async Task GetWalletBalance_ReturnsForbidden_WhenUserDoesNotHavePermission()
    {
        // Arrange
        var userId = "test-user-123";
        var walletId = "restricted-wallet";

        _mockService
            .Setup(service => service.GetWalletBalanceAsync(userId, walletId))
            .ThrowsAsync(new UnauthorizedAccessException("You do not have permission to access this wallet."));

        // Act
        var result = await _controller.GetWalletBalance(walletId);

        // Assert
        var objectResult = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(403, objectResult.StatusCode);
        Assert.Equal("You do not have permission to access this wallet.", objectResult.Value);

        _mockService.Verify(service => service.GetWalletBalanceAsync(userId, walletId), Times.Once);
    }

    [Fact]
    /// <summary>
    /// Test that TransferBetweenWallets returns success when transfer is valid
    /// </summary>
    public async Task TransferBetweenWallets_ReturnsSuccess_WhenTransferIsValid()
    {
        // Arrange
        var userId = "test-user-123";
        var request = new InternalTransferRequestDto
        {
            SourceWalletId = "source-wallet",
            TargetWalletId = "target-wallet",
            Amount = 100m
        };

        var response = new InternalTransferResponseDto
        {
            Success = true,
            TransactionId = "transaction-123"
        };

        _mockService
            .Setup(service => service.TransferBetweenWalletsAsync(userId, request.SourceWalletId, request.TargetWalletId, request.Amount))
            .ReturnsAsync(response);

        // Act
        var result = await _controller.TransferBetweenWallets(request);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var transferResponse = Assert.IsType<InternalTransferResponseDto>(okResult.Value);
        Assert.True(transferResponse.Success);
        Assert.Equal("transaction-123", transferResponse.TransactionId);

        _mockService.Verify(service => service.TransferBetweenWalletsAsync(userId, request.SourceWalletId, request.TargetWalletId, request.Amount), Times.Once);
    }

    [Fact]
    /// <summary>
    /// Test that TransferBetweenWallets returns bad request when transfer fails
    /// </summary>
    public async Task TransferBetweenWallets_ReturnsBadRequest_WhenTransferFails()
    {
        // Arrange
        var userId = "test-user-123";
        var request = new InternalTransferRequestDto
        {
            SourceWalletId = "source-wallet",
            TargetWalletId = "target-wallet",
            Amount = 1000m
        };

        var response = new InternalTransferResponseDto
        {
            Success = false,
            ErrorMessage = "Insufficient balance in source wallet."
        };

        _mockService
            .Setup(service => service.TransferBetweenWalletsAsync(userId, request.SourceWalletId, request.TargetWalletId, request.Amount))
            .ReturnsAsync(response);

        // Act
        var result = await _controller.TransferBetweenWallets(request);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result.Result);
        var errorResponse = Assert.IsType<InternalTransferResponseDto>(badRequestResult.Value);
        Assert.False(errorResponse.Success);
        Assert.Equal("Insufficient balance in source wallet.", errorResponse.ErrorMessage);

        _mockService.Verify(service => service.TransferBetweenWalletsAsync(userId, request.SourceWalletId, request.TargetWalletId, request.Amount), Times.Once);
    }

    [Fact]
    /// <summary>
    /// Test that TransferBetweenWallets returns bad request when amount is invalid
    /// </summary>
    public async Task TransferBetweenWallets_ReturnsBadRequest_WhenAmountIsInvalid()
    {
        // Arrange
        var userId = "test-user-123";
        var request = new InternalTransferRequestDto
        {
            SourceWalletId = "source-wallet",
            TargetWalletId = "target-wallet",
            Amount = 0m // Invalid: zero amount
        };

        var response = new InternalTransferResponseDto
        {
            Success = false,
            ErrorMessage = "Transfer amount must be greater than zero."
        };

        _mockService
            .Setup(service => service.TransferBetweenWalletsAsync(userId, request.SourceWalletId, request.TargetWalletId, request.Amount))
            .ReturnsAsync(response);

        // Act
        var result = await _controller.TransferBetweenWallets(request);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result.Result);
        var errorResponse = Assert.IsType<InternalTransferResponseDto>(badRequestResult.Value);
        Assert.False(errorResponse.Success);
        Assert.Equal("Transfer amount must be greater than zero.", errorResponse.ErrorMessage);

        _mockService.Verify(service => service.TransferBetweenWalletsAsync(userId, request.SourceWalletId, request.TargetWalletId, request.Amount), Times.Once);
    }

    [Fact]
    /// <summary>
    /// Test that TransferBetweenWallets returns bad request when source and target are same
    /// </summary>
    public async Task TransferBetweenWallets_ReturnsBadRequest_WhenSourceAndTargetAreSame()
    {
        // Arrange
        var userId = "test-user-123";
        var request = new InternalTransferRequestDto
        {
            SourceWalletId = "same-wallet",
            TargetWalletId = "same-wallet", // Same as source
            Amount = 100m
        };

        var response = new InternalTransferResponseDto
        {
            Success = false,
            ErrorMessage = "Source and target wallets cannot be the same."
        };

        _mockService
            .Setup(service => service.TransferBetweenWalletsAsync(userId, request.SourceWalletId, request.TargetWalletId, request.Amount))
            .ReturnsAsync(response);

        // Act
        var result = await _controller.TransferBetweenWallets(request);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result.Result);
        var errorResponse = Assert.IsType<InternalTransferResponseDto>(badRequestResult.Value);
        Assert.False(errorResponse.Success);
        Assert.Equal("Source and target wallets cannot be the same.", errorResponse.ErrorMessage);

        _mockService.Verify(service => service.TransferBetweenWalletsAsync(userId, request.SourceWalletId, request.TargetWalletId, request.Amount), Times.Once);
    }
}