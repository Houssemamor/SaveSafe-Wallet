using Confluent.Kafka;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace KafkaInfrastructure;

public abstract class KafkaConsumerBase : BackgroundService
{
    private readonly ILogger _logger;
    private readonly IKafkaProducer _kafkaProducer;
    private readonly ConsumerConfig _consumerConfig;
    private readonly int _maxRetryAttempts;

    protected KafkaConsumerBase(
        IConfiguration configuration,
        IKafkaProducer kafkaProducer,
        ILogger logger,
        string topic,
        string groupId)
    {
        _kafkaProducer = kafkaProducer;
        _logger = logger;
        Topic = topic;

        var bootstrapServers = configuration["Kafka:BootstrapServers"]
            ?? throw new InvalidOperationException("Kafka:BootstrapServers is not configured.");

        _maxRetryAttempts = int.TryParse(configuration["Kafka:MaxRetryAttempts"], out var parsed)
            ? Math.Max(parsed, 1)
            : 3;

        _consumerConfig = new ConsumerConfig
        {
            BootstrapServers = bootstrapServers,
            GroupId = groupId,
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false
        };
    }

    protected string Topic { get; }

    protected abstract Task HandleMessageAsync(string message, string? key, CancellationToken cancellationToken);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield();

        using var consumer = new ConsumerBuilder<Ignore, string>(_consumerConfig)
            .SetErrorHandler((_, error) =>
            {
                _logger.LogWarning("Kafka consumer error. Topic={Topic} Reason={Reason}", Topic, error.Reason);
            })
            .Build();

        consumer.Subscribe(Topic);
        _logger.LogInformation("Kafka consumer subscribed. Topic={Topic} GroupId={GroupId}", Topic, _consumerConfig.GroupId);

        while (!stoppingToken.IsCancellationRequested)
        {
            ConsumeResult<Ignore, string>? consumeResult = null;

            try
            {
                consumeResult = consumer.Consume(stoppingToken);
                if (consumeResult?.Message?.Value is null)
                {
                    continue;
                }

                _logger.LogInformation(
                    "Kafka message consumed. Topic={Topic} Partition={Partition} Offset={Offset}",
                    consumeResult.TopicPartitionOffset.Topic,
                    consumeResult.Partition.Value,
                    consumeResult.Offset.Value);

                var processed = await TryHandleWithRetryAsync(consumeResult.Message.Value, null, stoppingToken);

                if (processed)
                {
                    consumer.Commit(consumeResult);
                    _logger.LogInformation(
                        "Kafka offset committed. Topic={Topic} Partition={Partition} Offset={Offset}",
                        consumeResult.TopicPartitionOffset.Topic,
                        consumeResult.Partition.Value,
                        consumeResult.Offset.Value);
                    continue;
                }

                await PublishDlqAsync(consumeResult.Message.Value, null, "ProcessingFailedAfterRetries", stoppingToken);
            }
            catch (ConsumeException ex)
            {
                _logger.LogError(ex, "Kafka consume exception. Topic={Topic}", Topic);
                if (consumeResult?.Message?.Value is not null)
                {
                    await PublishDlqAsync(consumeResult.Message.Value, null, ex.Error.Reason, stoppingToken);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled consumer exception. Topic={Topic}", Topic);
                if (consumeResult?.Message?.Value is not null)
                {
                    await PublishDlqAsync(consumeResult.Message.Value, null, ex.Message, stoppingToken);
                }
            }
        }
    }

    private async Task<bool> TryHandleWithRetryAsync(string message, string? key, CancellationToken cancellationToken)
    {
        for (var attempt = 1; attempt <= _maxRetryAttempts; attempt++)
        {
            try
            {
                await HandleMessageAsync(message, key, cancellationToken);
                return true;
            }
            catch (Exception ex)
            {
                if (attempt == _maxRetryAttempts)
                {
                    _logger.LogError(
                        ex,
                        "Consumer processing failed after max retries. Topic={Topic} Attempts={Attempts}",
                        Topic,
                        _maxRetryAttempts);
                    return false;
                }

                var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt - 1));
                _logger.LogWarning(
                    ex,
                    "Consumer processing retry. Topic={Topic} Attempt={Attempt} DelayMs={DelayMs}",
                    Topic,
                    attempt,
                    delay.TotalMilliseconds);

                await Task.Delay(delay, cancellationToken);
            }
        }

        return false;
    }

    private async Task PublishDlqAsync(string message, string? key, string reason, CancellationToken cancellationToken)
    {
        var dlqTopic = KafkaTopics.ToDlq(Topic);

        var payload = new KafkaDlqMessage
        {
            Topic = Topic,
            Key = key,
            Reason = reason,
            Payload = message,
            FailedAtUtc = DateTime.UtcNow
        };

        _logger.LogError(
            "Publishing failed message to DLQ. Topic={Topic} DlqTopic={DlqTopic} Reason={Reason}",
            Topic,
            dlqTopic,
            reason);

        await _kafkaProducer.ProduceAsync(dlqTopic, key ?? string.Empty, payload);
    }

    private sealed class KafkaDlqMessage
    {
        public string Topic { get; init; } = string.Empty;
        public string? Key { get; init; }
        public string Reason { get; init; } = string.Empty;
        public string Payload { get; init; } = string.Empty;
        public DateTime FailedAtUtc { get; init; }
    }
}
