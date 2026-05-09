namespace WalletService.API.Persistence.Firestore;

// Centralized Firestore configuration loaded from appsettings/environment.
public sealed class FirestoreOptions
{
    public string ProjectId { get; set; } = string.Empty;
    public string? CredentialsPath { get; set; }
}
