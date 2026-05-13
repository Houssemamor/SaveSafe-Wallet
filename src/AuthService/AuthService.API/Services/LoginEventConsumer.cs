using System.Text.Json;
using AuthService.API.Persistence;
using KafkaInfrastructure;

namespace AuthService.API.Services;

public sealed class LoginEventConsumer : KafkaConsumerBase
{
    private const string ConsumerGroupId = "auth-service-login-consumer";

    private readonly ILoginEventRepository _loginEventRepository;
    private readonly ILogger<LoginEventConsumer> _logger;

    public LoginEventConsumer(
        IConfiguration configuration,
        IKafkaProducer kafkaProducer,
        ILoginEventRepository loginEventRepository,
        ILogger<LoginEventConsumer> logger)
        : base(
            configuration,
            kafkaProducer,
            logger,
            KafkaTopics.LoginEvents,
            ConsumerGroupId)
    {
        _loginEventRepository = loginEventRepository;
        _logger = logger;
    }

    protected override async Task HandleMessageAsync(string message, string? key, CancellationToken cancellationToken)
    {
        var loginEvent = JsonSerializer.Deserialize<LoginEventMessage>(message);
        if (loginEvent is null)
        {
            throw new InvalidOperationException("Unable to deserialize LoginEventMessage.");
        }

        var userId = Guid.TryParse(loginEvent.UserId, out var parsedUserId)
            ? parsedUserId
            : Guid.Empty;

        var record = new LoginEventRecord(
            EventId: loginEvent.EventId,
            UserId: userId,
            UserEmail: loginEvent.Email,
            UserName: loginEvent.Email,
            IpAddress: loginEvent.IpAddress,
            Country: loginEvent.CountryCode,
            Success: loginEvent.Success,
            FailureReason: loginEvent.FailureReason,
            IsFlagged: false,
            Timestamp: loginEvent.TimestampUtc,
            UserAgent: loginEvent.UserAgent);

        await _loginEventRepository.AddAsync(record, cancellationToken);

        _logger.LogInformation(
            "Persisted login event from Kafka. EventId={EventId} Key={Key} Success={Success}",
            loginEvent.EventId,
            key,
            loginEvent.Success);
    }
}
