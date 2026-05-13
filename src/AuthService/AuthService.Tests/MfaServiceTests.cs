using AuthService.API.DTOs;
using AuthService.API.Entities;
using AuthService.API.Persistence;
using AuthService.API.Services;
using Microsoft.Extensions.Configuration;

namespace AuthService.Tests;

public class MfaServiceTests
{
    private readonly InMemoryUserRepository _users = new();
    private readonly InMemoryMfaQuestionRepository _questions = new();
    private readonly ISecurityQuestionCipher _cipher;
    private readonly IMfaService _service;
    private readonly Guid _userId = Guid.NewGuid();

    public MfaServiceTests()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Key"] = "test-secret-key-for-testing-purposes-only-32chars"
            })
            .Build();

        _cipher = new SecurityQuestionCipher(configuration);
        _service = new MfaService(_users, _questions, _cipher, configuration);

        _users.Seed(new User
        {
            Id = _userId,
            Email = "test@example.com",
            Name = "Test User",
            PasswordHash = "password-hash",
            AccountStatus = UserAccountStatus.Active,
            Role = UserRole.User,
            MfaEnabled = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
    }

    [Fact]
    public async Task EnableAsync_PersistsEncryptedQuestions_AndSetsMfaEnabled()
    {
        await _service.EnableAsync(_userId, new[]
        {
            new MfaEnrollQuestionDto("q1", "Blue School"),
            new MfaEnrollQuestionDto("q2", "River Road")
        });

        var storedQuestions = await _questions.GetByUserIdAsync(_userId);

        Assert.True(_users.StoredUser!.MfaEnabled);
        Assert.Equal(2, storedQuestions.Count);
        Assert.All(storedQuestions, question => Assert.False(string.Equals(question.EncryptedAnswer, "Blue School", StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public async Task CreateChallengeAndVerifyChallengeAsync_ReturnsUserId_ForMatchingAnswer()
    {
        await _service.EnableAsync(_userId, new[]
        {
            new MfaEnrollQuestionDto("q1", "Blue School"),
            new MfaEnrollQuestionDto("q2", "River Road")
        });

        var challenge = await _service.CreateChallengeAsync(_userId);
        var answer = challenge.QuestionId == "q1" ? "Blue School" : "River Road";

        var verifiedUserId = await _service.VerifyChallengeAsync(challenge.ChallengeToken, answer);

        Assert.Equal(_userId, verifiedUserId);
    }

    [Fact]
    public async Task VerifyChallengeAsync_RejectsWrongAnswer()
    {
        await _service.EnableAsync(_userId, new[]
        {
            new MfaEnrollQuestionDto("q1", "Blue School"),
            new MfaEnrollQuestionDto("q2", "River Road")
        });

        var challenge = await _service.CreateChallengeAsync(_userId);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _service.VerifyChallengeAsync(challenge.ChallengeToken, "Wrong Answer"));
    }

    private sealed class InMemoryUserRepository : IUserRepository
    {
        public User? StoredUser { get; private set; }

        public void Seed(User user) => StoredUser = user;

        public Task<bool> EmailExistsAsync(string normalizedEmail, CancellationToken ct = default) => Task.FromResult(false);
        public Task<User?> GetByIdAsync(Guid userId, CancellationToken ct = default) => Task.FromResult(StoredUser?.Id == userId ? StoredUser : null);
        public Task<User?> GetByEmailAsync(string normalizedEmail, CancellationToken ct = default) => Task.FromResult(StoredUser?.Email == normalizedEmail ? StoredUser : null);
        public Task CreateAsync(User user, string normalizedEmail, CancellationToken ct = default) => throw new NotImplementedException();
        public Task UpdateAsync(User user, CancellationToken ct = default)
        {
            StoredUser = user;
            return Task.CompletedTask;
        }
        public Task UpdateLastLoginAsync(Guid userId, DateTime lastLoginAt, CancellationToken ct = default) => Task.CompletedTask;
        public Task<IReadOnlyList<User>> GetRecentUsersAsync(int limit, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<User>>(Array.Empty<User>());
        public Task<UserCounts> GetUserCountsAsync(CancellationToken ct = default) => Task.FromResult(new UserCounts(0, 0, 0, 0));
        public Task<bool> AnyWithRoleAsync(UserRole role, CancellationToken ct = default) => Task.FromResult(false);
    }

    private sealed class InMemoryMfaQuestionRepository : IMfaQuestionRepository
    {
        private readonly Dictionary<string, UserMfaQuestionRecord> _questions = new();

        public Task<IReadOnlyList<UserMfaQuestionRecord>> GetByUserIdAsync(Guid userId, CancellationToken ct = default)
        {
            IReadOnlyList<UserMfaQuestionRecord> result = _questions.Values
                .Where(question => question.UserId == userId && question.IsActive)
                .ToList();
            return Task.FromResult(result);
        }

        public Task<UserMfaQuestionRecord?> GetAsync(Guid userId, string questionId, CancellationToken ct = default)
        {
            _questions.TryGetValue(BuildKey(userId, questionId), out var record);
            return Task.FromResult(record);
        }

        public Task ReplaceAsync(Guid userId, IReadOnlyList<UserMfaQuestionRecord> questions, CancellationToken ct = default)
        {
            foreach (var key in _questions.Keys.Where(key => key.StartsWith($"{userId:N}_", StringComparison.Ordinal)).ToList())
            {
                _questions.Remove(key);
            }

            foreach (var question in questions)
            {
                _questions[BuildKey(userId, question.QuestionId)] = question;
            }

            return Task.CompletedTask;
        }

        public Task DeleteByUserIdAsync(Guid userId, CancellationToken ct = default)
        {
            foreach (var key in _questions.Keys.Where(key => key.StartsWith($"{userId:N}_", StringComparison.Ordinal)).ToList())
            {
                _questions.Remove(key);
            }

            return Task.CompletedTask;
        }

        public Task UpdateLastVerifiedAsync(Guid userId, string questionId, DateTime lastVerifiedAt, CancellationToken ct = default)
        {
            var key = BuildKey(userId, questionId);
            if (_questions.TryGetValue(key, out var record))
            {
                _questions[key] = record with { LastVerifiedAt = lastVerifiedAt, UpdatedAt = lastVerifiedAt };
            }

            return Task.CompletedTask;
        }

        private static string BuildKey(Guid userId, string questionId) => $"{userId:N}_{questionId}";
    }
}