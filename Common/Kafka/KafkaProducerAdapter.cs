using Confluent.Kafka;

namespace Net.Leksi.MicroService.Common;

public class KafkaProducerAdapter(KafkaConfig config) : IKafkaProducer
{
    private IProducer<string, string> _producer = null!;
    public void Initialize()
    {
        _producer = new ProducerBuilder<string, string>(config.Properties).Build();
    }
    public async Task<List<DeliveryResult<string, string>>> ProduceAsync(Message<string, string> message, CancellationToken stoppingToken)
    {
        List<DeliveryResult<string, string>> result = [];
        foreach(string topic in config.Topics)
        {
            result.Add(await _producer.ProduceAsync(topic, message, stoppingToken));
        }
        return result;
    }
    public void Dispose()
    {
        _producer?.Dispose();
    }
}
