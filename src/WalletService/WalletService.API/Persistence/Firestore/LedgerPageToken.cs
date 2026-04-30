using System.Text;

namespace WalletService.API.Persistence.Firestore;

internal readonly record struct LedgerPageToken(DateTime CreatedAt, string Id);

internal static class LedgerPageTokenCodec
{
    public static string Encode(LedgerPageToken token)
    {
        var raw = $"{token.CreatedAt.Ticks}|{token.Id}";
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(raw));
    }

    public static bool TryDecode(string? value, out LedgerPageToken token)
    {
        token = default;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        try
        {
            var raw = Encoding.UTF8.GetString(Convert.FromBase64String(value));
            var parts = raw.Split('|', 2);
            if (parts.Length != 2)
            {
                return false;
            }

            if (!long.TryParse(parts[0], out var ticks))
            {
                return false;
            }

            token = new LedgerPageToken(new DateTime(ticks, DateTimeKind.Utc), parts[1]);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
