using AuthService.API.Services;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace AuthService.Tests;

public class TokenServiceTests
{
    private readonly ITokenService _tokenService;
    private readonly IConfiguration _configuration;

    public TokenServiceTests()
    {
        // Create a test configuration
        var inMemorySettings = new Dictionary<string, string?> {
            {"Jwt:Key", "test-secret-key-for-testing-purposes-only-32chars"},
            {"Jwt:Issuer", "test-issuer"},
            {"Jwt:Audience", "test-audience"}
        };

        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();

        _tokenService = new TokenService(_configuration);
    }

    [Fact]
    public void GenerateRefreshToken_ShouldReturnNonEmptyString()
    {
        // Act
        var token = _tokenService.GenerateRefreshToken();

        // Assert
        Assert.NotNull(token);
        Assert.NotEmpty(token);
        Assert.True(token.Length > 20);
    }

    [Fact]
    public void GenerateRefreshToken_ShouldReturnUniqueTokens()
    {
        // Act
        var token1 = _tokenService.GenerateRefreshToken();
        var token2 = _tokenService.GenerateRefreshToken();

        // Assert
        Assert.NotEqual(token1, token2);
    }

    [Fact]
    public void HashToken_ShouldReturnConsistentHash()
    {
        // Arrange
        var token = "test-token-value";

        // Act
        var hash1 = _tokenService.HashToken(token);
        var hash2 = _tokenService.HashToken(token);

        // Assert
        Assert.Equal(hash1, hash2);
        Assert.NotEqual(token, hash);
    }

    [Fact]
    public void HashToken_ShouldReturnDifferentHashForDifferentTokens()
    {
        // Arrange
        var token1 = "test-token-value-1";
        var token2 = "test-token-value-2";

        // Act
        var hash1 = _tokenService.HashToken(token1);
        var hash2 = _tokenService.HashToken(token2);

        // Assert
        Assert.NotEqual(hash1, hash2);
    }

    [Fact]
    public void GenerateAccessToken_ShouldReturnValidToken()
    {
        // Arrange
        var user = new AuthService.API.Entities.User
        {
            Id = Guid.NewGuid(),
            Email = "test@example.com",
            Name = "Test User",
            Role = AuthService.API.Entities.UserRole.User
        };

        // Act
        var token = _tokenService.GenerateAccessToken(user);

        // Assert
        Assert.NotNull(token);
        Assert.NotEmpty(token);
        Assert.True(token.Length > 50);
    }

    [Fact]
    public void GenerateAccessToken_ShouldContainUserClaims()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new AuthService.API.Entities.User
        {
            Id = userId,
            Email = "test@example.com",
            Name = "Test User",
            Role = AuthService.API.Entities.UserRole.User
        };

        // Act
        var token = _tokenService.GenerateAccessToken(user);

        // Assert
        Assert.Contains(userId.ToString(), token);
        Assert.Contains("test@example.com", token);
    }
}