using System.Security.Cryptography;
using System.Text;

namespace AuthService.API.Services;

public sealed class SecurityQuestionCipher : ISecurityQuestionCipher
{
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private const int KeySize = 32;
    private const int CipherVersion = 1;

    private readonly byte[] _key;

    public SecurityQuestionCipher(IConfiguration configuration)
    {
        var keyMaterial = configuration["Mfa:EncryptionKey"]
            ?? configuration["Jwt:Key"]
            ?? throw new InvalidOperationException("MFA encryption key is not configured.");

        _key = SHA256.HashData(Encoding.UTF8.GetBytes(keyMaterial)).Take(KeySize).ToArray();
    }

    public string NormalizeAnswer(string answer)
    {
        var trimmed = answer.Trim().ToLowerInvariant();
        return string.Join(' ', trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    public EncryptedSecurityAnswer Encrypt(string normalizedAnswer)
    {
        var plainBytes = Encoding.UTF8.GetBytes(normalizedAnswer);
        var cipherBytes = new byte[plainBytes.Length];
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var tag = new byte[TagSize];

        using var aes = new AesGcm(_key, TagSize);
        aes.Encrypt(nonce, plainBytes, cipherBytes, tag);

        return new EncryptedSecurityAnswer(
            CipherText: Convert.ToBase64String(cipherBytes),
            Nonce: Convert.ToBase64String(nonce),
            Tag: Convert.ToBase64String(tag),
            Version: CipherVersion);
    }

    public string Decrypt(EncryptedSecurityAnswer encrypted)
    {
        var cipherBytes = Convert.FromBase64String(encrypted.CipherText);
        var nonce = Convert.FromBase64String(encrypted.Nonce);
        var tag = Convert.FromBase64String(encrypted.Tag);
        var plainBytes = new byte[cipherBytes.Length];

        using var aes = new AesGcm(_key, TagSize);
        aes.Decrypt(nonce, cipherBytes, tag, plainBytes);

        return Encoding.UTF8.GetString(plainBytes);
    }
}