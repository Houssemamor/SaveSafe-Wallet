namespace KafkaInfrastructure;

public static class KafkaTopics
{
    public const string LoginEvents = "login-events";
    public const string PaymentCompleted = "payment.completed";
    public const string PaymentFailed = "payment.failed";
    public const string SecurityEventCreated = "security.event.created";
    public const string RiskScoreComputed = "risk.score.computed";
    public const string DlqSuffix = ".dlq";

    public static string ToDlq(string topic) => $"{topic}{DlqSuffix}";
}
