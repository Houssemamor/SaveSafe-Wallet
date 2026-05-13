using System.Text.Json;
using Confluent.Kafka;
using KafkaInfrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AuthService.Tests;

public class KafkaProducerTests
{
    [Fact]
    public async Task ProduceAsync_SerializesMessage_AndForwardsToUnderlyingProducer()
    {
        var capturedTopic = string.Empty;
        var capturedKey = string.Empty;
        Message<string, string>? capturedMessage = null;

        var deliveryResult = new DeliveryResult<string, string>
        {
            Status = PersistenceStatus.NotPersisted,
            Topic = "login-events",
            Partition = new Partition(2),
            Offset = new Offset(18)
        };

        var producerMock = new Mock<IProducer<string, string>>();
        producerMock
            .Setup(x => x.ProduceAsync(It.IsAny<string>(), It.IsAny<Message<string, string>>(), It.IsAny<CancellationToken>()))
            .Returns((string topic, Message<string, string> message, CancellationToken cancellationToken) =>
            {
                capturedTopic = topic;
                capturedKey = message.Key ?? string.Empty;
                capturedMessage = message;
                return Task.FromResult(deliveryResult);
            });

        var producer = new KafkaProducer(producerMock.Object, NullLogger<KafkaProducer>.Instance);
        var payload = new
        {
            EventId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            UserId = "user-123",
            Email = "demo@example.com",
            Success = true
        };

        await producer.ProduceAsync("login-events", "user-123", payload);

        Assert.Equal("login-events", capturedTopic);
        Assert.Equal("user-123", capturedKey);
        Assert.NotNull(capturedMessage);

        using var json = JsonDocument.Parse(capturedMessage!.Value);
        Assert.Equal("user-123", json.RootElement.GetProperty("UserId").GetString());
        Assert.Equal("demo@example.com", json.RootElement.GetProperty("Email").GetString());
        Assert.True(json.RootElement.GetProperty("Success").GetBoolean());
    }

    [Fact]
    public async Task ProduceAsync_WhenUnderlyingProducerThrows_DoesNotThrow()
    {
        var producerMock = new Mock<IProducer<string, string>>();
        producerMock
            .Setup(x => x.ProduceAsync(It.IsAny<string>(), It.IsAny<Message<string, string>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("broker down"));

        var producer = new KafkaProducer(producerMock.Object, NullLogger<KafkaProducer>.Instance);

        var exception = await Record.ExceptionAsync(() => producer.ProduceAsync(
            "login-events",
            "user-123",
            new { UserId = "user-123", Email = "demo@example.com" }));

        Assert.Null(exception);
        producerMock.Verify(x => x.ProduceAsync(It.IsAny<string>(), It.IsAny<Message<string, string>>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}