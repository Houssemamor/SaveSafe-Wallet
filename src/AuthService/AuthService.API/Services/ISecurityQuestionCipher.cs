namespace AuthService.API.Services;

public sealed record EncryptedSecurityAnswer(
    string CipherText,
    string Nonce,
    string Tag,
    int Version);

public interface ISecurityQuestionCipher
{
    string NormalizeAnswer(string answer);
    EncryptedSecurityAnswer Encrypt(string normalizedAnswer);
    string Decrypt(EncryptedSecurityAnswer encrypted);
}