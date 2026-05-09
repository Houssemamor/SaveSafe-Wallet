using Google.Apis.Auth.OAuth2;
using Google.Cloud.Firestore;
using Microsoft.Extensions.Options;

namespace WalletService.API.Persistence.Firestore;

// Adapter to create a singleton FirestoreDb with explicit credentials or ADC fallback.
public sealed class FirestoreDbProvider : IFirestoreDbProvider
{
    private readonly FirestoreOptions _options;
    private readonly object _lock = new();
    private FirestoreDb? _db;

    public FirestoreDbProvider(IOptions<FirestoreOptions> options)
    {
        _options = options.Value;
    }

    public FirestoreDb GetDb()
    {
        if (_db is not null)
        {
            return _db;
        }

        lock (_lock)
        {
            if (_db is not null)
            {
                return _db;
            }

            if (string.IsNullOrWhiteSpace(_options.ProjectId))
            {
                throw new InvalidOperationException("Firestore ProjectId is not configured.");
            }

            var credential = ResolveCredential();
            var builder = new FirestoreDbBuilder
            {
                ProjectId = _options.ProjectId,
                Credential = credential
            };

            _db = builder.Build();
            return _db;
        }
    }

    private GoogleCredential ResolveCredential()
    {
        if (string.IsNullOrWhiteSpace(_options.CredentialsPath))
        {
            return GoogleCredential.GetApplicationDefault();
        }

        if (!File.Exists(_options.CredentialsPath))
        {
            throw new InvalidOperationException(
                $"Firestore credentials file not found: {_options.CredentialsPath}");
        }

        return GoogleCredential.FromFile(_options.CredentialsPath);
    }
}
