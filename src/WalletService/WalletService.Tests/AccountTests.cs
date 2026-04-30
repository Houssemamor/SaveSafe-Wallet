using WalletService.API.Entities;
using Xunit;

namespace WalletService.Tests;

public class AccountTests
{
    [Fact]
    public void Account_Constructor_ShouldCreateValidAccount()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var accountNumber = "SSW-0000000001";

        // Act
        var account = new Account
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            AccountNumber = accountNumber,
            Currency = "USD",
            Balance = 0.00m,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        // Assert
        Assert.NotNull(account);
        Assert.Equal(userId, account.UserId);
        Assert.Equal(accountNumber, account.AccountNumber);
        Assert.Equal("USD", account.Currency);
        Assert.Equal(0.00m, account.Balance);
    }

    [Fact]
    public void Account_Balance_ShouldAllowDecimalValues()
    {
        // Arrange
        var account = new Account
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            AccountNumber = "SSW-0000000002",
            Currency = "USD",
            Balance = 100.50m,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        // Act & Assert
        Assert.Equal(100.50m, account.Balance);
    }

    [Fact]
    public void Account_ShouldSupportNegativeBalance()
    {
        // Arrange
        var account = new Account
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            AccountNumber = "SSW-0000000003",
            Currency = "USD",
            Balance = -50.25m,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        // Act & Assert
        Assert.Equal(-50.25m, account.Balance);
    }

    [Fact]
    public void Account_ShouldTrackUpdatedAtTimestamp()
    {
        // Arrange
        var initialTime = DateTime.UtcNow;
        var account = new Account
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            AccountNumber = "SSW-0000000004",
            Currency = "USD",
            Balance = 0.00m,
            CreatedAt = initialTime,
            UpdatedAt = initialTime
        };

        // Act
        Thread.Sleep(10); // Small delay to ensure timestamp difference
        account.UpdatedAt = DateTime.UtcNow;

        // Assert
        Assert.True(account.UpdatedAt > account.CreatedAt);
    }

    [Fact]
    public void Account_ShouldHaveUniqueIds()
    {
        // Arrange
        var account1 = new Account
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            AccountNumber = "SSW-0000000005",
            Currency = "USD",
            Balance = 0.00m,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var account2 = new Account
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            AccountNumber = "SSW-0000000006",
            Currency = "USD",
            Balance = 0.00m,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        // Act & Assert
        Assert.NotEqual(account1.Id, account2.Id);
    }
}