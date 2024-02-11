using Confluent.Kafka;

namespace Net.Leksi.MicroService.Common;

public class KafkaProducerAdapter<TMessage> : KafkaProducerBase<TMessage>, IKafkaProducer<TMessage> where TMessage : class
{
    private readonly new KafkaConfig _config;
    public KafkaProducerAdapter(KafkaConfig config): base(config) 
    {
        _config = config;
    }
    public async Task<List<DeliveryResult<string, string>>> ProduceAsync(string key, TMessage message, CancellationToken stoppingToken)
    {
        return await base.ProduceAsync(key, message, _config.Topics, stoppingToken);
    }
}
