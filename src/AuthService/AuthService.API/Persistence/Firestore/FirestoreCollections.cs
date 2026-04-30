namespace AuthService.API.Persistence.Firestore;

public static class FirestoreCollections
{
    public const string Users = "users";
    public const string UsersByEmail = "usersByEmail";
    public const string RefreshTokens = "refreshTokens";
    public const string LoginEvents = "loginEvents";
    public const string FailedLoginsByIp = "failedLoginsByIp";
    public const string AdminStats = "adminStats";
}
