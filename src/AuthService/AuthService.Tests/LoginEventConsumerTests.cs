using System.Reflection;
using System.Text.Json;
using AuthService.API.Persistence;
using AuthService.API.Services;
using KafkaInfrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AuthService.Tests;

public class LoginEventConsumerTests
{
    [Fact]
    public async Task HandleMessageAsync_PersistsLoginEventRecord()
    {
        var repositoryMock = new Mock<ILoginEventRepository>();
        LoginEventRecord? savedRecord = null;

        repositoryMock
            .Setup(x => x.AddAsync(It.IsAny<LoginEventRecord>(), It.IsAny<CancellationToken>()))
            .Callback<LoginEventRecord, CancellationToken>((record, _) => savedRecord = record)
            .Returns(Task.CompletedTask);

        var consumer = CreateConsumer(repositoryMock.Object);
        var message = new LoginEventMessage(
            EventId: Guid.Parse("22222222-2222-2222-2222-222222222222"),
            UserId: Guid.Parse("33333333-3333-3333-3333-333333333333").ToString(),
            Email: "user@example.com",
            IpAddress: "10.0.0.1",
            CountryCode: "FR",
            Asn: "AS1234",
            UserAgent: "Mozilla/5.0",
            DeviceFingerprint: "device-1",
            Success: true,
            FailureReason: null,
            TimestampUtc: DateTime.UtcNow);

        await InvokeProtectedAsync(consumer, "HandleMessageAsync", JsonSerializer.Serialize(message), "33333333-3333-3333-3333-333333333333", CancellationToken.None);

        Assert.NotNull(savedRecord);
        Assert.Equal(message.EventId, savedRecord!.EventId);
        Assert.Equal(Guid.Parse(message.UserId!), savedRecord.UserId);
        Assert.Equal(message.Email, savedRecord.UserEmail);
        Assert.Equal(message.IpAddress, savedRecord.IpAddress);
        Assert.Equal(message.CountryCode, savedRecord.Country);
        Assert.Equal(message.Success, savedRecord.Success);
    }

    [Fact]
    public async Task PublishDlqAsync_ForwardsFailedMessageToDlqTopic()
    {
        var repositoryMock = new Mock<ILoginEventRepository>();
        var capturingProducer = new CapturingKafkaProducer();
        var consumer = CreateConsumer(repositoryMock.Object, capturingProducer);

        const string message = "{\"EventId\":\"22222222-2222-2222-2222-222222222222\",\"Email\":\"user@example.com\"}";

        await InvokePrivateAsync(consumer, "PublishDlqAsync", message, "user-123", "ProcessingFailedAfterRetries", CancellationToken.None);

        Assert.Equal("login-events.dlq", capturingProducer.Topic);
        Assert.Equal("user-123", capturingProducer.Key);
        Assert.NotNull(capturingProducer.Message);

        using var json = JsonDocument.Parse(JsonSerializer.Serialize(capturingProducer.Message));
        Assert.Equal("login-events", json.RootElement.GetProperty("Topic").GetString());
        Assert.Equal("user-123", json.RootElement.GetProperty("Key").GetString());
        Assert.Equal("ProcessingFailedAfterRetries", json.RootElement.GetProperty("Reason").GetString());
        Assert.Equal(message, json.RootElement.GetProperty("Payload").GetString());
    }

    private static LoginEventConsumer CreateConsumer(ILoginEventRepository repository, IKafkaProducer? producer = null)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Kafka:BootstrapServers"] = "localhost:9092",
                ["Kafka:MaxRetryAttempts"] = "3"
            })
            .Build();

        return new LoginEventConsumer(
            configuration,
            producer ?? new CapturingKafkaProducer(),
            repository,
            NullLogger<LoginEventConsumer>.Instance);
    }

    private static async Task InvokeProtectedAsync(object instance, string methodName, params object?[] arguments)
    {
        var method = instance.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"Method {methodName} not found.");

        var result = method.Invoke(instance, arguments)
            ?? throw new InvalidOperationException($"Invocation of {methodName} returned null.");

        await (Task)result;
    }

    private static async Task InvokePrivateAsync(object instance, string methodName, params object?[] arguments)
    {
        var method = typeof(KafkaConsumerBase).GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"Method {methodName} not found.");

        var result = method.Invoke(instance, arguments)
            ?? throw new InvalidOperationException($"Invocation of {methodName} returned null.");

        await (Task)result;
    }

    private sealed class CapturingKafkaProducer : IKafkaProducer
    {
        public string? Topic { get; private set; }
        public string? Key { get; private set; }
        public object? Message { get; private set; }

        public Task ProduceAsync<T>(string topic, string key, T message)
        {
            Topic = topic;
            Key = key;
            Message = message;
            return Task.CompletedTask;
        }
    }
}