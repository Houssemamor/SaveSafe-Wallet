namespace AuthService.API.DTOs;

public sealed record AdminAiReviewQueueResponseDto(
    IReadOnlyList<AdminAiReviewItemDto> Items
);

public sealed record AdminAiReviewItemDto(
    string EventId,
    string? UserId,
    string Email,
    string? IpAddress,
    string? CountryCode,
    string? UserAgent,
    bool Success,
    string? FailureReason,
    DateTime TimestampUtc,
    double RiskScore,
    string Label,
    IReadOnlyList<string> Reasons,
    string RecommendedAction,
    string ReviewStatus,
    DateTime AnalyzedAt
);
