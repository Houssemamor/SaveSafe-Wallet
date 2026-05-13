using System.Text.Json;
using Confluent.Kafka;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace KafkaInfrastructure;

public sealed class KafkaProducer : IKafkaProducer
{
    private readonly ILogger<KafkaProducer> _logger;
    private readonly IProducer<string, string> _producer;

    public KafkaProducer(IConfiguration configuration, ILogger<KafkaProducer> logger)
        : this(CreateProducer(configuration, logger), logger)
    {
    }

    public KafkaProducer(IProducer<string, string> producer, ILogger<KafkaProducer> logger)
    {
        _producer = producer;
        _logger = logger;
    }

    private static IProducer<string, string> CreateProducer(IConfiguration configuration, ILogger<KafkaProducer> logger)
    {
        var bootstrapServers = configuration["Kafka:BootstrapServers"]
            ?? throw new InvalidOperationException("Kafka:BootstrapServers is not configured.");

        var config = new ProducerConfig
        {
            BootstrapServers = bootstrapServers,
            Acks = Acks.All,
            EnableIdempotence = true
        };

        return new ProducerBuilder<string, string>(config)
            .SetErrorHandler((_, error) =>
            {
                logger.LogWarning("Kafka producer error: {Reason}", error.Reason);
            })
            .Build();
    }

    public async Task ProduceAsync<T>(string topic, string key, T message)
    {
        try
        {
            var payload = JsonSerializer.Serialize(message);
            var result = await _producer.ProduceAsync(topic, new Message<string, string>
            {
                Key = key,
                Value = payload,
                Timestamp = Timestamp.Default
            });

            if (result.Status == PersistenceStatus.NotPersisted)
            {
                _logger.LogWarning(
                    "Kafka delivery not persisted. Topic={Topic} Key={Key} Status={Status}",
                    topic,
                    key,
                    result.Status);
                return;
            }

            var brokerTimestamp = result.Timestamp.UtcDateTime;
            _logger.LogInformation(
                "Kafka message produced. Topic={Topic} Key={Key} Partition={Partition} Offset={Offset} Timestamp={Timestamp}",
                topic,
                key,
                result.Partition.Value,
                result.Offset.Value,
                brokerTimestamp);
        }
        catch (ProduceException<string, string> ex)
        {
            _logger.LogWarning(
                ex,
                "Kafka delivery failed. Topic={Topic} Key={Key} Reason={Reason}",
                topic,
                key,
                ex.Error.Reason);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled Kafka produce exception. Topic={Topic} Key={Key}", topic, key);
        }
    }
}
