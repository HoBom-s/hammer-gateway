using Confluent.Kafka;

namespace Hammer.Gateway.Kafka;

internal sealed class KafkaProducer : IKafkaProducer, IDisposable
{
    private readonly IProducer<string, string> _producer;
    private readonly ILogger<KafkaProducer> _logger;

    public KafkaProducer(IConfiguration configuration, ILogger<KafkaProducer> logger)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        _logger = logger;

        ProducerConfig config = new()
        {
            BootstrapServers = configuration["Kafka:BootstrapServers"],
            Acks = Acks.Leader,
            LingerMs = 5,
            BatchNumMessages = 100,
        };

        _producer = new ProducerBuilder<string, string>(config).Build();
    }

    public async Task ProduceAsync(string topic, string key, string value)
    {
        try
        {
            Message<string, string> message = new() { Key = key, Value = value };
            await _producer.ProduceAsync(topic, message);
        }
#pragma warning disable CA1031 // Fire-and-forget: Kafka failure must not break gateway
        catch (Exception ex)
#pragma warning restore CA1031
        {
            _logger.LogWarning(ex, "Failed to produce Kafka message to {Topic}", topic);
        }
    }

    public void Dispose()
    {
        _producer.Flush(TimeSpan.FromSeconds(5));
        _producer.Dispose();
    }
}
