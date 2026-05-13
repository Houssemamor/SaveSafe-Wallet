using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace AuthService.API.Services;

internal static class PasswordResetTokenCodec
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private sealed record Payload(Guid UserId, long ExpiresAtUnixSeconds, string Nonce);

    public static string Create(Guid userId, string signingKey, TimeSpan? ttl = null)
    {
        var expires = DateTimeOffset.UtcNow.Add(ttl ?? TimeSpan.FromMinutes(15));
        var payload = new Payload(userId, expires.ToUnixTimeSeconds(), Guid.NewGuid().ToString("N"));
        var json = JsonSerializer.Serialize(payload, JsonOptions);
        var encoded = Base64UrlEncode(Encoding.UTF8.GetBytes(json));
        var signature = CreateSignature(encoded, signingKey);
        return string.Join('.', "sswrst", encoded, signature);
    }

    public static bool TryValidate(string token, string signingKey, out Guid userId, out string errorMessage)
    {
        userId = Guid.Empty;
        errorMessage = string.Empty;

        if (string.IsNullOrWhiteSpace(token))
        {
            errorMessage = "Password reset token is required.";
            return false;
        }

        var parts = token.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 3 || parts[0] != "sswrst")
        {
            errorMessage = "Invalid password reset token format.";
            return false;
        }

        var expectedSig = CreateSignature(parts[1], signingKey);
        if (!FixedTimeEquals(expectedSig, parts[2]))
        {
            errorMessage = "Password reset token signature is invalid.";
            return false;
        }

        if (!TryBase64UrlDecode(parts[1], out var bytes))
        {
            errorMessage = "Password reset token payload is invalid.";
            return false;
        }

        try
        {
            var payload = JsonSerializer.Deserialize<Payload>(bytes, JsonOptions) ?? throw new JsonException("Missing payload");
            if (payload.ExpiresAtUnixSeconds < DateTimeOffset.UtcNow.ToUnixTimeSeconds())
            {
                errorMessage = "Password reset token has expired.";
                return false;
            }

            userId = payload.UserId;
            return true;
        }
        catch
        {
            errorMessage = "Password reset token payload could not be read.";
            return false;
        }
    }

    private static string CreateSignature(string payload, string signingKey)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(signingKey));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        return Base64UrlEncode(hash);
    }

    private static bool FixedTimeEquals(string a, string b)
    {
        var ab = Encoding.UTF8.GetBytes(a);
        var bb = Encoding.UTF8.GetBytes(b);
        return ab.Length == bb.Length && CryptographicOperations.FixedTimeEquals(ab, bb);
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
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }
}
