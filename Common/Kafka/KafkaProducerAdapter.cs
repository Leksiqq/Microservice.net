using Confluent.Kafka;

namespace Net.Leksi.MicroService.Common;

public class KafkaProducerAdapter : IKafkaProducer
{
    private readonly KafkaConfig _config;
    private readonly IProducer<string, string> _producer = null!;
    public KafkaProducerAdapter(KafkaConfig config)
    {
        _config = config;
        _producer = new ProducerBuilder<string, string>(_config.Properties).Build();
    }
    public async Task<List<DeliveryResult<string, string>>> ProduceAsync(Message<string, string> message, CancellationToken stoppingToken)
    {
        List<DeliveryResult<string, string>> result = [];
        foreach(string topic in _config.Topics)
        {
            result.Add(await _producer.ProduceAsync(topic, message, stoppingToken));
        }
        return result;
    }
    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _producer?.Dispose();
    }
}
