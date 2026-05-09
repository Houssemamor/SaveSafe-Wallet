using AuthService.API.Entities;
using AuthService.API.Persistence.Firestore.Documents;

namespace AuthService.API.Persistence.Firestore;

internal static class UserDocumentMapper
{
    public static UserDocument ToDocument(User user) =>
        new()
        {
            Id = user.Id.ToString(),
            Email = user.Email,
            Name = user.Name,
            PasswordHash = user.PasswordHash,
            GoogleId = user.GoogleId,
            MfaEnabled = user.MfaEnabled,
            AccountStatus = user.AccountStatus.ToString(),
            Role = user.Role.ToString(),
            CreatedAt = user.CreatedAt,
            UpdatedAt = user.UpdatedAt,
            LastLoginAt = user.LastLoginAt
        };

    public static User ToEntity(UserDocument doc)
    {
        return new User
        {
            Id = Guid.Parse(doc.Id),
            Email = doc.Email,
            Name = doc.Name,
            PasswordHash = doc.PasswordHash,
            GoogleId = doc.GoogleId,
            MfaEnabled = doc.MfaEnabled,
            AccountStatus = Enum.Parse<UserAccountStatus>(doc.AccountStatus),
            Role = Enum.Parse<UserRole>(doc.Role),
            CreatedAt = doc.CreatedAt,
            UpdatedAt = doc.UpdatedAt,
            LastLoginAt = doc.LastLoginAt
        };
    }
}
