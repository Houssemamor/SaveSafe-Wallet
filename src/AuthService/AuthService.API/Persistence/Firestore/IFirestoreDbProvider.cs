using Google.Cloud.Firestore;

namespace AuthService.API.Persistence.Firestore;

public interface IFirestoreDbProvider
{
    FirestoreDb GetDb();
}
