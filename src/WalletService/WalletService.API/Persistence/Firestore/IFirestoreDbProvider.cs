using Google.Cloud.Firestore;

namespace WalletService.API.Persistence.Firestore;

public interface IFirestoreDbProvider
{
    FirestoreDb GetDb();
}
