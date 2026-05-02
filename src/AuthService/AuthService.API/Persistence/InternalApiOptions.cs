namespace AuthService.API.Persistence;

public class InternalApiOptions
{
    public const string SectionName = "InternalApi";
    public string ApiKey { get; set; } = string.Empty;
}