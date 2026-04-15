namespace AuthService.API.DTOs;

public record AdminSecuritySummaryDto(
    int TotalUsers,
    int ActiveUsers,
    int SuspendedUsers,
    int DeletedUsers,
    int TotalLoginEventsLast24Hours,
    int FailedLoginEventsLast24Hours,
    int FlaggedEventsLast24Hours,
    int DistinctSourceIpsLast24Hours
);
