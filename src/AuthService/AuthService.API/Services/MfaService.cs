using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AuthService.API.DTOs;
using AuthService.API.Persistence;

namespace AuthService.API.Services;

public sealed class MfaService : IMfaService
{
    private static readonly IReadOnlyList<SecurityQuestionCatalogDto> QuestionCatalog = new[]
    {
        new SecurityQuestionCatalogDto("q1", "What was the name of your first school?"),
        new SecurityQuestionCatalogDto("q2", "What is the name of the street you grew up on?"),
        new SecurityQuestionCatalogDto("q3", "What was your first pet's name?"),
        new SecurityQuestionCatalogDto("q4", "What is your mother's middle name?"),
        new SecurityQuestionCatalogDto("q5", "What city were you born in?")
    };

    private readonly IUserRepository _users;
    private readonly IMfaQuestionRepository _questions;
    private readonly ISecurityQuestionCipher _cipher;
    private readonly IConfiguration _configuration;

    public MfaService(
        IUserRepository users,
        IMfaQuestionRepository questions,
        ISecurityQuestionCipher cipher,
        IConfiguration configuration)
    {
        _users = users;
        _questions = questions;
        _cipher = cipher;
        _configuration = configuration;
    }

    public IReadOnlyList<SecurityQuestionCatalogDto> GetQuestionCatalog() => QuestionCatalog;

    public async Task<MfaChallengeResult> CreateChallengeAsync(Guid userId, CancellationToken ct = default)
    {
        var activeQuestions = await _questions.GetByUserIdAsync(userId, ct);
        if (activeQuestions.Count == 0)
        {
            throw new UnauthorizedAccessException("MFA is not configured for this account.");
        }

        var selected = activeQuestions[RandomNumberGenerator.GetInt32(activeQuestions.Count)];
        var catalogItem = QuestionCatalog.FirstOrDefault(q => q.QuestionId == selected.QuestionId)
            ?? throw new InvalidOperationException("MFA question catalog is misconfigured.");

        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(5);
        var payload = new MfaChallengeTokenPayload(
            UserId: userId,
            QuestionId: selected.QuestionId,
            ExpiresAtUnixSeconds: expiresAt.ToUnixTimeSeconds(),
            Nonce: Guid.NewGuid().ToString("N"));

        return new MfaChallengeResult(
            ChallengeToken: MfaChallengeTokenCodec.Create(payload, GetSigningKey()),
            QuestionId: selected.QuestionId,
            QuestionText: catalogItem.QuestionText,
            ExpiresAt: expiresAt.UtcDateTime);
    }

    public async Task<Guid> VerifyChallengeAsync(string challengeToken, string answer, CancellationToken ct = default)
    {
        if (!MfaChallengeTokenCodec.TryValidate(challengeToken, GetSigningKey(), out var payload, out var errorMessage))
        {
            throw new UnauthorizedAccessException(errorMessage);
        }

        var question = await _questions.GetAsync(payload.UserId, payload.QuestionId, ct);
        if (question is null || !question.IsActive)
        {
            throw new UnauthorizedAccessException("MFA question is no longer available.");
        }

        var decrypted = _cipher.Decrypt(new EncryptedSecurityAnswer(
            CipherText: question.EncryptedAnswer,
            Nonce: question.Nonce,
            Tag: question.Tag,
            Version: question.CipherVersion));

        var normalizedAnswer = _cipher.NormalizeAnswer(answer);
        if (!string.Equals(decrypted, normalizedAnswer, StringComparison.Ordinal))
        {
            throw new UnauthorizedAccessException("Invalid MFA answer.");
        }

        await _questions.UpdateLastVerifiedAsync(payload.UserId, payload.QuestionId, DateTime.UtcNow, ct);
        return payload.UserId;
    }

    public async Task EnableAsync(Guid userId, IReadOnlyList<MfaEnrollQuestionDto> questions, CancellationToken ct = default)
    {
        var user = await _users.GetByIdAsync(userId, ct);
        if (user is null)
        {
            throw new KeyNotFoundException($"User {userId} not found.");
        }

        ValidateEnrollmentQuestions(questions);

        var now = DateTime.UtcNow;
        var records = questions.Select(question =>
        {
            var normalizedAnswer = _cipher.NormalizeAnswer(question.Answer);
            var encrypted = _cipher.Encrypt(normalizedAnswer);

            return new UserMfaQuestionRecord(
                UserId: userId,
                QuestionId: question.QuestionId,
                EncryptedAnswer: encrypted.CipherText,
                Nonce: encrypted.Nonce,
                Tag: encrypted.Tag,
                CipherVersion: encrypted.Version,
                IsActive: true,
                CreatedAt: now,
                UpdatedAt: now,
                LastVerifiedAt: null);
        }).ToList();

        await _questions.ReplaceAsync(userId, records, ct);

        user.MfaEnabled = true;
        user.UpdatedAt = now;
        await _users.UpdateAsync(user, ct);
    }

    public async Task DisableAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await _users.GetByIdAsync(userId, ct);
        if (user is null)
        {
            throw new KeyNotFoundException($"User {userId} not found.");
        }

        await _questions.DeleteByUserIdAsync(userId, ct);

        user.MfaEnabled = false;
        user.UpdatedAt = DateTime.UtcNow;
        await _users.UpdateAsync(user, ct);
    }

    private static void ValidateEnrollmentQuestions(IReadOnlyList<MfaEnrollQuestionDto> questions)
    {
        if (questions.Count < 2 || questions.Count > 3)
        {
            throw new InvalidOperationException("Choose between 2 and 3 security questions.");
        }

        var questionIds = questions.Select(question => question.QuestionId).ToList();
        if (questionIds.Distinct(StringComparer.OrdinalIgnoreCase).Count() != questionIds.Count)
        {
            throw new InvalidOperationException("Security questions must be unique.");
        }

        foreach (var question in questions)
        {
            if (!QuestionCatalog.Any(item => string.Equals(item.QuestionId, question.QuestionId, StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException($"Security question '{question.QuestionId}' is not supported.");
            }

            if (string.IsNullOrWhiteSpace(question.Answer))
            {
                throw new InvalidOperationException("All security question answers are required.");
            }
        }
    }

    private string GetSigningKey() => _configuration["Jwt:Key"] ?? throw new InvalidOperationException("JWT signing key is not configured.");

    private sealed record MfaChallengeTokenPayload(
        Guid UserId,
        string QuestionId,
        long ExpiresAtUnixSeconds,
        string Nonce);

    private static class MfaChallengeTokenCodec
    {
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

        public static string Create(MfaChallengeTokenPayload payload, string signingKey)
        {
            var payloadJson = JsonSerializer.Serialize(payload, JsonOptions);
            var encodedPayload = Base64UrlEncode(Encoding.UTF8.GetBytes(payloadJson));
            var signature = CreateSignature(encodedPayload, signingKey);
            return string.Join('.', "sswmfa", encodedPayload, signature);
        }

        public static bool TryValidate(
            string token,
            string signingKey,
            out MfaChallengeTokenPayload payload,
            out string errorMessage)
        {
            payload = default!;
            errorMessage = string.Empty;

            if (string.IsNullOrWhiteSpace(token))
            {
                errorMessage = "MFA challenge token is required.";
                return false;
            }

            var segments = token.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (segments.Length != 3 || !string.Equals(segments[0], "sswmfa", StringComparison.Ordinal))
            {
                errorMessage = "Invalid MFA challenge format.";
                return false;
            }

            var expectedSignature = CreateSignature(segments[1], signingKey);
            if (!FixedTimeEquals(expectedSignature, segments[2]))
            {
                errorMessage = "MFA challenge signature is invalid.";
                return false;
            }

            if (!TryBase64UrlDecode(segments[1], out var payloadBytes))
            {
                errorMessage = "MFA challenge payload is invalid.";
                return false;
            }

            try
            {
                payload = JsonSerializer.Deserialize<MfaChallengeTokenPayload>(payloadBytes, JsonOptions)
                    ?? throw new JsonException("Missing payload.");
            }
            catch
            {
                errorMessage = "MFA challenge payload could not be read.";
                return false;
            }

            if (payload.ExpiresAtUnixSeconds < DateTimeOffset.UtcNow.ToUnixTimeSeconds())
            {
                errorMessage = "MFA challenge has expired.";
                return false;
            }

            return true;
        }

        private static string CreateSignature(string payload, string signingKey)
        {
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(signingKey));
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
            return Base64UrlEncode(hash);
        }

        private static bool TryBase64UrlDecode(string value, out byte[] bytes)
        {
            bytes = Array.Empty<byte>();

            var normalized = value.Replace('-', '+').Replace('_', '/');
            normalized = normalized.PadRight(normalized.Length + (4 - normalized.Length % 4) % 4, '=');

            try
            {
                bytes = Convert.FromBase64String(normalized);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static string Base64UrlEncode(byte[] bytes)
        {
            return Convert.ToBase64String(bytes)
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');
        }

        private static bool FixedTimeEquals(string left, string right)
        {
            var leftBytes = Encoding.UTF8.GetBytes(left);
            var rightBytes = Encoding.UTF8.GetBytes(right);
            return leftBytes.Length == rightBytes.Length && CryptographicOperations.FixedTimeEquals(leftBytes, rightBytes);
        }
    }
}