using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Moq;
using WalletService.API.Entities;
using WalletService.API.Persistence;
using WalletService.API.Services;

namespace WalletService.API.Tests;

public class WalletManagementQrTests
{
    [Fact]
    public async Task CreateAndResolveReceiveQr_IncludesOwnerName()
    {
        // Arrange
        var userId = Guid.NewGuid().ToString();
        var accountId = Guid.NewGuid();

        var account = new Account
        {
            Id = accountId,
            UserId = userId,
            Name = "Test Wallet",
            Currency = "USD",
            IsActive = true
        };

        var mockAccountRepo = new Mock<IAccountRepository>();
        mockAccountRepo
            .Setup(r => r.GetDefaultAccountAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);
        mockAccountRepo
            .Setup(r => r.GetAccountByIdAsync(accountId.ToString(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);
        mockAccountRepo
            .Setup(r => r.GetAccountsByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Account> { account });

        var mockLedgerRepo = new Mock<ILedgerRepository>();

        var mockUserLookup = new Mock<IUserLookupService>();
        mockUserLookup
            .Setup(s => s.GetUserNameAsync(Guid.Parse(userId)))
            .ReturnsAsync("Alice Example");

        var inMemory = new Dictionary<string, string?> { ["Jwt:Key"] = "test-signing-key" };
        var config = new ConfigurationBuilder().AddInMemoryCollection(inMemory).Build();

        var svc = new WalletManagementService(mockAccountRepo.Object, mockLedgerRepo.Object, mockUserLookup.Object, config);

        // Act - create token
        var createResp = await svc.CreateReceiveQrAsync(userId);

        // Assert - token created and owner name present
        Assert.True(createResp.Success);
        Assert.False(string.IsNullOrWhiteSpace(createResp.Token));
        Assert.Equal("Alice Example", createResp.OwnerName);

        // Act - resolve token
        var resolveResp = await svc.ResolveReceiveQrAsync(createResp.Token!);

        // Assert - resolved owner name
        Assert.True(resolveResp.Success);
        Assert.Equal("Alice Example", resolveResp.OwnerName);
    }
}
