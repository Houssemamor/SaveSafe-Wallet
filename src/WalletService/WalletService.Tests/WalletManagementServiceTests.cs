using Moq;
using WalletService.API.DTOs;
using WalletService.API.Entities;
using WalletService.API.Persistence;
using WalletService.API.Services;

namespace WalletService.API.Tests;

/// <summary>
/// Unit tests for WalletManagementService
/// Tests business logic for wallet CRUD operations
/// </summary>
public class WalletManagementServiceTests
{
    private readonly Mock<IAccountRepository> _mockAccountRepository;
    private readonly Mock<ILedgerRepository> _mockLedgerRepository;
    private readonly WalletManagementService _service;

    public WalletManagementServiceTests()
    {
        _mockAccountRepository = new Mock<IAccountRepository>();
        _mockLedgerRepository = new Mock<ILedgerRepository>();
        _service = new WalletManagementService(_mockAccountRepository.Object, _mockLedgerRepository.Object);
    }

    [Fact]
    /// <summary>
    /// Test that GetUserWalletsAsync returns empty collection when user has no wallets
    /// </summary>
    public async Task GetUserWalletsAsync_ReturnsEmptyCollection_WhenUserHasNoWallets()
    {
        // Arrange
        var userId = "user123";
        _mockAccountRepository
            .Setup(repo => repo.GetAccountsByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Enumerable.Empty<Account>());

        // Act
        var result = await _service.GetUserWalletsAsync(userId);

        // Assert
        Assert.Empty(result);
        _mockAccountRepository.Verify(repo => repo.GetAccountsByUserIdAsync(userId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    /// <summary>
    /// Test that GetUserWalletsAsync returns user wallets correctly
    /// </summary>
    public async Task GetUserWalletsAsync_ReturnsUserWallets_WhenUserHasWallets()
    {
        // Arrange
        var userId = "user123";
        var accounts = new List<Account>
        {
            new Account
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Name = "Main Wallet",
                Type = "checking",
                Currency = "USD",
                Balance = 1000.50m,
                IsActive = true,
                IsDefault = true,
                CreatedAt = DateTime.UtcNow
            },
            new Account
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Name = "Savings",
                Type = "savings",
                Currency = "USD",
                Balance = 5000.00m,
                IsActive = true,
                IsDefault = false,
                CreatedAt = DateTime.UtcNow
            }
        };

        _mockAccountRepository
            .Setup(repo => repo.GetAccountsByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(accounts);

        // Act
        var result = await _service.GetUserWalletsAsync(userId);

        // Assert
        Assert.Equal(2, result.Count());

        var firstWallet = result.First();
        Assert.Equal("Main Wallet", firstWallet.Name);
        Assert.Equal("checking", firstWallet.Type);
        Assert.Equal(1000.50m, firstWallet.Balance);
        Assert.True(firstWallet.IsDefault);

        var secondWallet = result.Last();
        Assert.Equal("Savings", secondWallet.Name);
        Assert.Equal("savings", secondWallet.Type);
        Assert.Equal(5000.00m, secondWallet.Balance);
        Assert.False(secondWallet.IsDefault);
    }

    [Fact]
    /// <summary>
    /// Test that CreateWalletAsync fails when name is empty
    /// </summary>
    public async Task CreateWalletAsync_Fails_WhenNameIsEmpty()
    {
        // Arrange
        var userId = "user123";
        var request = new CreateManagedWalletRequestDto
        {
            Name = "",
            Type = "checking",
            Currency = "USD"
        };

        // Act
        var result = await _service.CreateWalletAsync(userId, request);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("Wallet name is required.", result.ErrorMessage);
        Assert.Null(result.Wallet);
    }

    [Fact]
    /// <summary>
    /// Test that CreateWalletAsync fails when name is too short
    /// </summary>
    public async Task CreateWalletAsync_Fails_WhenNameIsTooShort()
    {
        // Arrange
        var userId = "user123";
        var request = new CreateManagedWalletRequestDto
        {
            Name = "A",
            Type = "checking",
            Currency = "USD"
        };

        // Act
        var result = await _service.CreateWalletAsync(userId, request);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("Wallet name must be between 2 and 50 characters.", result.ErrorMessage);
    }

    [Fact]
    /// <summary>
    /// Test that CreateWalletAsync fails when name is too long
    /// </summary>
    public async Task CreateWalletAsync_Fails_WhenNameIsTooLong()
    {
        // Arrange
        var userId = "user123";
        var request = new CreateManagedWalletRequestDto
        {
            Name = new string('A', 51),
            Type = "checking",
            Currency = "USD"
        };

        // Act
        var result = await _service.CreateWalletAsync(userId, request);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("Wallet name must be between 2 and 50 characters.", result.ErrorMessage);
    }

    [Fact]
    /// <summary>
    /// Test that CreateWalletAsync fails when wallet type is invalid
    /// </summary>
    public async Task CreateWalletAsync_Fails_WhenWalletTypeIsInvalid()
    {
        // Arrange
        var userId = "user123";
        var request = new CreateManagedWalletRequestDto
        {
            Name = "My Wallet",
            Type = "invalid_type",
            Currency = "USD"
        };

        // Act
        var result = await _service.CreateWalletAsync(userId, request);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("Invalid wallet type. Must be checking, savings, investment, or reserve.", result.ErrorMessage);
    }

    [Fact]
    /// <summary>
    /// Test that CreateWalletAsync fails when wallet name already exists
    /// </summary>
    public async Task CreateWalletAsync_Fails_WhenWalletNameAlreadyExists()
    {
        // Arrange
        var userId = "user123";
        var request = new CreateManagedWalletRequestDto
        {
            Name = "My Wallet",
            Type = "checking",
            Currency = "USD"
        };

        var existingAccounts = new List<Account>
        {
            new Account
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Name = "My Wallet",
                Type = "checking",
                Currency = "USD",
                Balance = 0,
                IsActive = true,
                IsDefault = true,
                CreatedAt = DateTime.UtcNow
            }
        };

        _mockAccountRepository
            .Setup(repo => repo.GetAccountsByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingAccounts);

        // Act
        var result = await _service.CreateWalletAsync(userId, request);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("A wallet with this name already exists.", result.ErrorMessage);
    }

    [Fact]
    /// <summary>
    /// Test that CreateWalletAsync creates first wallet as default
    /// </summary>
    public async Task CreateWalletAsync_CreatesFirstWalletAsDefault_WhenUserHasNoWallets()
    {
        // Arrange
        var userId = "user123";
        var request = new CreateManagedWalletRequestDto
        {
            Name = "My Wallet",
            Type = "checking",
            Currency = "USD",
            InitialBalance = 100
        };

        _mockAccountRepository
            .Setup(repo => repo.GetAccountsByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Enumerable.Empty<Account>());

        var createdAccount = new Account
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = "My Wallet",
            Type = "checking",
            Currency = "USD",
            Balance = 100,
            IsActive = true,
            IsDefault = true,
            CreatedAt = DateTime.UtcNow
        };

        _mockAccountRepository
            .Setup(repo => repo.CreateAccountAsync(It.IsAny<Account>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(createdAccount);

        // Act
        var result = await _service.CreateWalletAsync(userId, request);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Wallet);
        Assert.Equal("My Wallet", result.Wallet.Name);
        Assert.Equal("checking", result.Wallet.Type);
        Assert.Equal(100m, result.Wallet.Balance);
        Assert.True(result.Wallet.IsDefault);

        _mockAccountRepository.Verify(repo => repo.CreateAccountAsync(
            It.Is<Account>(a =>
                a.Name == "My Wallet" &&
                a.Type == "checking" &&
                a.IsDefault == true),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    /// <summary>
    /// Test that CreateWalletAsync creates subsequent wallet as non-default
    /// </summary>
    public async Task CreateWalletAsync_CreatesSubsequentWalletAsNonDefault_WhenUserHasExistingWallets()
    {
        // Arrange
        var userId = "user123";
        var request = new CreateManagedWalletRequestDto
        {
            Name = "Savings Wallet",
            Type = "savings",
            Currency = "USD"
        };

        var existingAccounts = new List<Account>
        {
            new Account
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Name = "Main Wallet",
                Type = "checking",
                Currency = "USD",
                Balance = 1000,
                IsActive = true,
                IsDefault = true,
                CreatedAt = DateTime.UtcNow
            }
        };

        _mockAccountRepository
            .Setup(repo => repo.GetAccountsByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingAccounts);

        var createdAccount = new Account
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = "Savings Wallet",
            Type = "savings",
            Currency = "USD",
            Balance = 0,
            IsActive = true,
            IsDefault = false,
            CreatedAt = DateTime.UtcNow
        };

        _mockAccountRepository
            .Setup(repo => repo.CreateAccountAsync(It.IsAny<Account>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(createdAccount);

        // Act
        var result = await _service.CreateWalletAsync(userId, request);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Wallet);
        Assert.Equal("Savings Wallet", result.Wallet.Name);
        Assert.Equal("savings", result.Wallet.Type);
        Assert.False(result.Wallet.IsDefault);

        _mockAccountRepository.Verify(repo => repo.CreateAccountAsync(
            It.Is<Account>(a =>
                a.Name == "Savings Wallet" &&
                a.Type == "savings" &&
                a.IsDefault == false),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    /// <summary>
    /// Test that CreateWalletAsync creates ledger entry for initial balance
    /// </summary>
    public async Task CreateWalletAsync_CreatesLedgerEntry_WhenInitialBalanceProvided()
    {
        // Arrange
        var userId = "user123";
        var request = new CreateManagedWalletRequestDto
        {
            Name = "My Wallet",
            Type = "checking",
            Currency = "USD",
            InitialBalance = 500
        };

        _mockAccountRepository
            .Setup(repo => repo.GetAccountsByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Enumerable.Empty<Account>());

        var createdAccount = new Account
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = "My Wallet",
            Type = "checking",
            Currency = "USD",
            Balance = 500,
            IsActive = true,
            IsDefault = true,
            CreatedAt = DateTime.UtcNow
        };

        _mockAccountRepository
            .Setup(repo => repo.CreateAccountAsync(It.IsAny<Account>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(createdAccount);

        // Act
        var result = await _service.CreateWalletAsync(userId, request);

        // Assert
        Assert.True(result.Success);

        _mockLedgerRepository.Verify(repo => repo.CreateAsync(
            It.Is<LedgerEntry>(e =>
                e.AccountId == createdAccount.Id &&
                e.Amount == 500 &&
                e.Type == LedgerEntryType.Credit &&
                e.Description == "Initial deposit"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    /// <summary>
    /// Test that DeleteWalletAsync throws when wallet not found
    /// </summary>
    public async Task DeleteWalletAsync_Throws_WhenWalletNotFound()
    {
        // Arrange
        var userId = "user123";
        var walletId = "nonexistent";

        _mockAccountRepository
            .Setup(repo => repo.GetAccountByIdAsync(walletId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Account?)null);

        // Act & Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _service.DeleteWalletAsync(userId, walletId));
    }

    [Fact]
    /// <summary>
    /// Test that DeleteWalletAsync throws when user doesn't own wallet
    /// </summary>
    public async Task DeleteWalletAsync_Throws_WhenUserDoesNotOwnWallet()
    {
        // Arrange
        var userId = "user123";
        var walletId = "wallet456";

        var account = new Account
        {
            Id = Guid.NewGuid(),
            UserId = "different_user",
            Name = "Other Wallet",
            Type = "checking",
            Currency = "USD",
            Balance = 0,
            IsActive = true,
            IsDefault = false,
            CreatedAt = DateTime.UtcNow
        };

        _mockAccountRepository
            .Setup(repo => repo.GetAccountByIdAsync(walletId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _service.DeleteWalletAsync(userId, walletId));
    }

    [Fact]
    /// <summary>
    /// Test that DeleteWalletAsync throws when trying to delete default wallet
    /// </summary>
    public async Task DeleteWalletAsync_Throws_WhenTryingToDeleteDefaultWallet()
    {
        // Arrange
        var userId = "user123";
        var walletId = "wallet456";

        var account = new Account
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = "Default Wallet",
            Type = "checking",
            Currency = "USD",
            Balance = 0,
            IsActive = true,
            IsDefault = true,
            CreatedAt = DateTime.UtcNow
        };

        _mockAccountRepository
            .Setup(repo => repo.GetAccountByIdAsync(walletId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.DeleteWalletAsync(userId, walletId));
        Assert.Equal("Cannot delete default wallet. Set another wallet as default first.", exception.Message);
    }

    [Fact]
    /// <summary>
    /// Test that DeleteWalletAsync throws when trying to delete wallet with balance
    /// </summary>
    public async Task DeleteWalletAsync_Throws_WhenTryingToDeleteWalletWithBalance()
    {
        // Arrange
        var userId = "user123";
        var walletId = "wallet456";

        var account = new Account
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = "Wallet with Balance",
            Type = "checking",
            Currency = "USD",
            Balance = 100,
            IsActive = true,
            IsDefault = false,
            CreatedAt = DateTime.UtcNow
        };

        _mockAccountRepository
            .Setup(repo => repo.GetAccountByIdAsync(walletId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.DeleteWalletAsync(userId, walletId));
        Assert.Equal("Cannot delete wallet with non-zero balance.", exception.Message);
    }

    [Fact]
    /// <summary>
    /// Test that DeleteWalletAsync throws when trying to delete only wallet
    /// </summary>
    public async Task DeleteWalletAsync_Throws_WhenTryingToDeleteOnlyWallet()
    {
        // Arrange
        var userId = "user123";
        var walletId = "wallet456";

        var account = new Account
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = "Only Wallet",
            Type = "checking",
            Currency = "USD",
            Balance = 0,
            IsActive = true,
            IsDefault = false,
            CreatedAt = DateTime.UtcNow
        };

        var allAccounts = new List<Account> { account };

        _mockAccountRepository
            .Setup(repo => repo.GetAccountByIdAsync(walletId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);

        _mockAccountRepository
            .Setup(repo => repo.GetAccountsByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(allAccounts);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.DeleteWalletAsync(userId, walletId));
        Assert.Equal("Cannot delete the only wallet. Create another wallet first.", exception.Message);
    }

    [Fact]
    /// <summary>
    /// Test that DeleteWalletAsync successfully deletes wallet
    /// </summary>
    public async Task DeleteWalletAsync_DeactivatesWallet_WhenValid()
    {
        // Arrange
        var userId = "user123";
        var walletId = "wallet456";

        var account = new Account
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = "Wallet to Delete",
            Type = "checking",
            Currency = "USD",
            Balance = 0,
            IsActive = true,
            IsDefault = false,
            CreatedAt = DateTime.UtcNow
        };

        var allAccounts = new List<Account>
        {
            account,
            new Account
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Name = "Other Wallet",
                Type = "savings",
                Currency = "USD",
                Balance = 1000,
                IsActive = true,
                IsDefault = true,
                CreatedAt = DateTime.UtcNow
            }
        };

        _mockAccountRepository
            .Setup(repo => repo.GetAccountByIdAsync(walletId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);

        _mockAccountRepository
            .Setup(repo => repo.GetAccountsByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(allAccounts);

        // Act
        await _service.DeleteWalletAsync(userId, walletId);

        // Assert
        _mockAccountRepository.Verify(repo =>
            repo.DeactivateAccountAsync(walletId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    /// <summary>
    /// Test that SetDefaultWalletAsync throws when wallet not found
    /// </summary>
    public async Task SetDefaultWalletAsync_Throws_WhenWalletNotFound()
    {
        // Arrange
        var userId = "user123";
        var walletId = "nonexistent";

        _mockAccountRepository
            .Setup(repo => repo.GetAccountByIdAsync(walletId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Account?)null);

        // Act & Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _service.SetDefaultWalletAsync(userId, walletId));
    }

    [Fact]
    /// <summary>
    /// Test that SetDefaultWalletAsync throws when user doesn't own wallet
    /// </summary>
    public async Task SetDefaultWalletAsync_Throws_WhenUserDoesNotOwnWallet()
    {
        // Arrange
        var userId = "user123";
        var walletId = "wallet456";

        var account = new Account
        {
            Id = Guid.NewGuid(),
            UserId = "different_user",
            Name = "Other Wallet",
            Type = "checking",
            Currency = "USD",
            Balance = 0,
            IsActive = true,
            IsDefault = false,
            CreatedAt = DateTime.UtcNow
        };

        _mockAccountRepository
            .Setup(repo => repo.GetAccountByIdAsync(walletId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _service.SetDefaultWalletAsync(userId, walletId));
    }

    [Fact]
    /// <summary>
    /// Test that SetDefaultWalletAsync throws when wallet is inactive
    /// </summary>
    public async Task SetDefaultWalletAsync_Throws_WhenWalletIsInactive()
    {
        // Arrange
        var userId = "user123";
        var walletId = "wallet456";

        var account = new Account
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = "Inactive Wallet",
            Type = "checking",
            Currency = "USD",
            Balance = 0,
            IsActive = false,
            IsDefault = false,
            CreatedAt = DateTime.UtcNow
        };

        _mockAccountRepository
            .Setup(repo => repo.GetAccountByIdAsync(walletId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.SetDefaultWalletAsync(userId, walletId));
        Assert.Equal("Cannot set inactive wallet as default.", exception.Message);
    }

    [Fact]
    /// <summary>
    /// Test that SetDefaultWalletAsync unsets previous default wallet
    /// </summary>
    public async Task SetDefaultWalletAsync_UnsetsPreviousDefault_WhenSettingNewDefault()
    {
        // Arrange
        var userId = "user123";
        var walletId = "wallet456";

        var newDefaultAccount = new Account
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = "New Default",
            Type = "checking",
            Currency = "USD",
            Balance = 100,
            IsActive = true,
            IsDefault = false,
            CreatedAt = DateTime.UtcNow
        };

        var currentDefaultAccount = new Account
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = "Current Default",
            Type = "checking",
            Currency = "USD",
            Balance = 500,
            IsActive = true,
            IsDefault = true,
            CreatedAt = DateTime.UtcNow
        };

        _mockAccountRepository
            .Setup(repo => repo.GetAccountByIdAsync(walletId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(newDefaultAccount);

        _mockAccountRepository
            .Setup(repo => repo.GetDefaultAccountAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(currentDefaultAccount);

        // Act
        await _service.SetDefaultWalletAsync(userId, walletId);

        // Assert
        _mockAccountRepository.Verify(repo =>
            repo.UnsetDefaultWalletAsync(currentDefaultAccount.Id.ToString(), It.IsAny<CancellationToken>()), Times.Once);
        _mockAccountRepository.Verify(repo =>
            repo.SetDefaultWalletAsync(walletId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    /// <summary>
    /// Test that SetDefaultWalletAsync does nothing when wallet is already default
    /// </summary>
    public async Task SetDefaultWalletAsync_DoesNothing_WhenWalletIsAlreadyDefault()
    {
        // Arrange
        var userId = "user123";
        var walletId = "wallet456";

        var account = new Account
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = "Default Wallet",
            Type = "checking",
            Currency = "USD",
            Balance = 100,
            IsActive = true,
            IsDefault = true,
            CreatedAt = DateTime.UtcNow
        };

        _mockAccountRepository
            .Setup(repo => repo.GetAccountByIdAsync(walletId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);

        // Act
        await _service.SetDefaultWalletAsync(userId, walletId);

        // Assert
        _mockAccountRepository.Verify(repo =>
            repo.SetDefaultWalletAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    /// <summary>
    /// Test that GetWalletBalanceAsync returns wallet balance
    /// </summary>
    public async Task GetWalletBalanceAsync_ReturnsBalance_WhenWalletExists()
    {
        // Arrange
        var userId = "user123";
        var walletId = "wallet456";

        var account = new Account
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = "My Wallet",
            Type = "checking",
            Currency = "EUR",
            Balance = 2500.75m,
            IsActive = true,
            IsDefault = true,
            CreatedAt = DateTime.UtcNow
        };

        _mockAccountRepository
            .Setup(repo => repo.GetAccountByIdAsync(walletId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);

        // Act
        var result = await _service.GetWalletBalanceAsync(userId, walletId);

        // Assert
        Assert.Equal(2500.75m, result.Balance);
        Assert.Equal("EUR", result.Currency);
    }

    [Fact]
    /// <summary>
    /// Test that GetWalletBalanceAsync throws when wallet not found
    /// </summary>
    public async Task GetWalletBalanceAsync_Throws_WhenWalletNotFound()
    {
        // Arrange
        var userId = "user123";
        var walletId = "nonexistent";

        _mockAccountRepository
            .Setup(repo => repo.GetAccountByIdAsync(walletId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Account?)null);

        // Act & Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _service.GetWalletBalanceAsync(userId, walletId));
    }

    [Fact]
    /// <summary>
    /// Test that GetWalletBalanceAsync throws when user doesn't own wallet
    /// </summary>
    public async Task GetWalletBalanceAsync_Throws_WhenUserDoesNotOwnWallet()
    {
        // Arrange
        var userId = "user123";
        var walletId = "wallet456";

        var account = new Account
        {
            Id = Guid.NewGuid(),
            UserId = "different_user",
            Name = "Other Wallet",
            Type = "checking",
            Currency = "USD",
            Balance = 1000,
            IsActive = true,
            IsDefault = false,
            CreatedAt = DateTime.UtcNow
        };

        _mockAccountRepository
            .Setup(repo => repo.GetAccountByIdAsync(walletId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _service.GetWalletBalanceAsync(userId, walletId));
    }

    [Fact]
    /// <summary>
    /// Test that TransferBetweenWalletsAsync fails when amount is zero or negative
    /// </summary>
    public async Task TransferBetweenWalletsAsync_Fails_WhenAmountIsZeroOrNegative()
    {
        // Arrange
        var userId = "user123";
        var sourceWalletId = "wallet1";
        var targetWalletId = "wallet2";
        var amount = 0m;

        // Act
        var result = await _service.TransferBetweenWalletsAsync(userId, sourceWalletId, targetWalletId, amount);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("Transfer amount must be greater than zero.", result.ErrorMessage);
    }

    [Fact]
    /// <summary>
    /// Test that TransferBetweenWalletsAsync fails when source and target are same
    /// </summary>
    public async Task TransferBetweenWalletsAsync_Fails_WhenSourceAndTargetAreSame()
    {
        // Arrange
        var userId = "user123";
        var walletId = "wallet1";
        var amount = 100m;

        // Act
        var result = await _service.TransferBetweenWalletsAsync(userId, walletId, walletId, amount);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("Source and target wallets cannot be the same.", result.ErrorMessage);
    }

    [Fact]
    /// <summary>
    /// Test that TransferBetweenWalletsAsync fails when source wallet not found
    /// </summary>
    public async Task TransferBetweenWalletsAsync_Fails_WhenSourceWalletNotFound()
    {
        // Arrange
        var userId = "user123";
        var sourceWalletId = Guid.NewGuid().ToString();
        var targetWalletId = Guid.NewGuid().ToString();
        var amount = 100m;

        _mockAccountRepository
            .Setup(repo => repo.GetAccountByIdAsync(sourceWalletId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Account?)null);

        // Act
        var result = await _service.TransferBetweenWalletsAsync(userId, sourceWalletId, targetWalletId, amount);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("Source wallet not found.", result.ErrorMessage);
    }

    [Fact]
    /// <summary>
    /// Test that TransferBetweenWalletsAsync fails when user doesn't own source wallet
    /// </summary>
    public async Task TransferBetweenWalletsAsync_Fails_WhenUserDoesNotOwnSourceWallet()
    {
        // Arrange
        var userId = "user123";
        var sourceWalletId = Guid.NewGuid().ToString();
        var targetWalletId = Guid.NewGuid().ToString();
        var amount = 100m;

        var sourceAccount = new Account
        {
            Id = Guid.Parse(sourceWalletId),
            UserId = "different_user",
            Name = "Other Wallet",
            Type = "checking",
            Currency = "USD",
            Balance = 1000,
            IsActive = true,
            IsDefault = false,
            CreatedAt = DateTime.UtcNow
        };

        _mockAccountRepository
            .Setup(repo => repo.GetAccountByIdAsync(sourceWalletId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sourceAccount);

        // Act
        var result = await _service.TransferBetweenWalletsAsync(userId, sourceWalletId, targetWalletId, amount);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("You do not have permission to access the source wallet.", result.ErrorMessage);
    }

    [Fact]
    /// <summary>
    /// Test that TransferBetweenWalletsAsync fails when target wallet not found
    /// </summary>
    public async Task TransferBetweenWalletsAsync_Fails_WhenTargetWalletNotFound()
    {
        // Arrange
        var userId = "user123";
        var sourceWalletId = Guid.NewGuid().ToString();
        var targetWalletId = Guid.NewGuid().ToString();
        var amount = 100m;

        var sourceAccount = new Account
        {
            Id = Guid.Parse(sourceWalletId),
            UserId = userId,
            Name = "Source Wallet",
            Type = "checking",
            Currency = "USD",
            Balance = 1000,
            IsActive = true,
            IsDefault = true,
            CreatedAt = DateTime.UtcNow
        };

        _mockAccountRepository
            .Setup(repo => repo.GetAccountByIdAsync(sourceWalletId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sourceAccount);

        _mockAccountRepository
            .Setup(repo => repo.GetAccountByIdAsync(targetWalletId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Account?)null);

        // Act
        var result = await _service.TransferBetweenWalletsAsync(userId, sourceWalletId, targetWalletId, amount);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("Target wallet not found.", result.ErrorMessage);
    }

    [Fact]
    /// <summary>
    /// Test that TransferBetweenWalletsAsync fails when user doesn't own target wallet
    /// </summary>
    public async Task TransferBetweenWalletsAsync_Fails_WhenUserDoesNotOwnTargetWallet()
    {
        // Arrange
        var userId = "user123";
        var sourceWalletId = Guid.NewGuid().ToString();
        var targetWalletId = Guid.NewGuid().ToString();
        var amount = 100m;

        var sourceAccount = new Account
        {
            Id = Guid.Parse(sourceWalletId),
            UserId = userId,
            Name = "Source Wallet",
            Type = "checking",
            Currency = "USD",
            Balance = 1000,
            IsActive = true,
            IsDefault = true,
            CreatedAt = DateTime.UtcNow
        };

        var targetAccount = new Account
        {
            Id = Guid.Parse(targetWalletId),
            UserId = "different_user",
            Name = "Other Wallet",
            Type = "checking",
            Currency = "USD",
            Balance = 0,
            IsActive = true,
            IsDefault = false,
            CreatedAt = DateTime.UtcNow
        };

        _mockAccountRepository
            .Setup(repo => repo.GetAccountByIdAsync(sourceWalletId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sourceAccount);

        _mockAccountRepository
            .Setup(repo => repo.GetAccountByIdAsync(targetWalletId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(targetAccount);

        // Act
        var result = await _service.TransferBetweenWalletsAsync(userId, sourceWalletId, targetWalletId, amount);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("You do not have permission to access the target wallet.", result.ErrorMessage);
    }

    [Fact]
    /// <summary>
    /// Test that TransferBetweenWalletsAsync fails when insufficient balance
    /// </summary>
    public async Task TransferBetweenWalletsAsync_Fails_WhenInsufficientBalance()
    {
        // Arrange
        var userId = "user123";
        var sourceWalletId = Guid.NewGuid().ToString();
        var targetWalletId = Guid.NewGuid().ToString();
        var amount = 1000m;

        var sourceAccount = new Account
        {
            Id = Guid.Parse(sourceWalletId),
            UserId = userId,
            Name = "Source Wallet",
            Type = "checking",
            Currency = "USD",
            Balance = 500,
            IsActive = true,
            IsDefault = true,
            CreatedAt = DateTime.UtcNow
        };

        var targetAccount = new Account
        {
            Id = Guid.Parse(targetWalletId),
            UserId = userId,
            Name = "Target Wallet",
            Type = "savings",
            Currency = "USD",
            Balance = 0,
            IsActive = true,
            IsDefault = false,
            CreatedAt = DateTime.UtcNow
        };

        _mockAccountRepository
            .Setup(repo => repo.GetAccountByIdAsync(sourceWalletId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sourceAccount);

        _mockAccountRepository
            .Setup(repo => repo.GetAccountByIdAsync(targetWalletId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(targetAccount);

        // Act
        var result = await _service.TransferBetweenWalletsAsync(userId, sourceWalletId, targetWalletId, amount);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("Insufficient balance in source wallet.", result.ErrorMessage);
    }

    [Fact]
    /// <summary>
    /// Test that TransferBetweenWalletsAsync successfully transfers funds
    /// </summary>
    public async Task TransferBetweenWalletsAsync_TransfersFunds_WhenValid()
    {
        // Arrange
        var userId = "user123";
        var sourceWalletId = Guid.NewGuid().ToString();
        var targetWalletId = Guid.NewGuid().ToString();
        var amount = 100m;

        var sourceAccount = new Account
        {
            Id = Guid.Parse(sourceWalletId),
            UserId = userId,
            Name = "Source Wallet",
            Type = "checking",
            Currency = "USD",
            Balance = 1000,
            IsActive = true,
            IsDefault = true,
            CreatedAt = DateTime.UtcNow
        };

        var targetAccount = new Account
        {
            Id = Guid.Parse(targetWalletId),
            UserId = userId,
            Name = "Target Wallet",
            Type = "savings",
            Currency = "USD",
            Balance = 0,
            IsActive = true,
            IsDefault = false,
            CreatedAt = DateTime.UtcNow
        };

        _mockAccountRepository
            .Setup(repo => repo.GetAccountByIdAsync(sourceWalletId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sourceAccount);

        _mockAccountRepository
            .Setup(repo => repo.GetAccountByIdAsync(targetWalletId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(targetAccount);

        // Act
        var result = await _service.TransferBetweenWalletsAsync(userId, sourceWalletId, targetWalletId, amount);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.TransactionId);

        // Verify ledger entries were created
        _mockLedgerRepository.Verify(repo => repo.CreateAsync(
            It.Is<LedgerEntry>(e =>
                e.AccountId == sourceAccount.Id &&
                e.Amount == -100 &&
                e.Type == LedgerEntryType.Debit &&
                e.Description != null &&
                e.Description.Contains("Target Wallet")),
            It.IsAny<CancellationToken>()), Times.Once);

        _mockLedgerRepository.Verify(repo => repo.CreateAsync(
            It.Is<LedgerEntry>(e =>
                e.AccountId == targetAccount.Id &&
                e.Amount == 100 &&
                e.Type == LedgerEntryType.Credit &&
                e.Description != null &&
                e.Description.Contains("Source Wallet")),
            It.IsAny<CancellationToken>()), Times.Once);

        // Verify account balances were updated
        _mockAccountRepository.Verify(repo => repo.UpdateAsync(
            It.Is<Account>(a => a.Id == sourceAccount.Id && a.Balance == 900),
            It.IsAny<CancellationToken>()), Times.Once);

        _mockAccountRepository.Verify(repo => repo.UpdateAsync(
            It.Is<Account>(a => a.Id == targetAccount.Id && a.Balance == 100),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}