namespace KafkaInfrastructure;

public record LoginEventMessage(
    Guid EventId,
    string? UserId,
    string Email,
    string IpAddress,
    string? CountryCode,
    string? Asn,
    string UserAgent,
    string? DeviceFingerprint,
    bool Success,
    string? FailureReason,
    DateTime TimestampUtc
);
