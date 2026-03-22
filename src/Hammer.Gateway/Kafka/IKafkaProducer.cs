namespace Hammer.Gateway.Kafka;

internal interface IKafkaProducer
{
    public Task ProduceAsync(string topic, string key, string value);
}
