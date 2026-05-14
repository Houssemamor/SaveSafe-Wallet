namespace AuthService.API.DTOs;

public sealed record AdminLokiQueryRequestDto(
    string Query,
    int Hours = 1,
    int Limit = 20
);

public sealed record AdminLokiQueryResponseDto(
    string Query,
    DateTime From,
    DateTime To,
    IReadOnlyList<AdminLokiSeriesDto> Series
);

public sealed record AdminLokiSeriesDto(
    string Name,
    IReadOnlyDictionary<string, string> Labels,
    IReadOnlyList<AdminLokiPointDto> Points,
    double Total
);

public sealed record AdminLokiPointDto(
    DateTime Timestamp,
    double Value
);
