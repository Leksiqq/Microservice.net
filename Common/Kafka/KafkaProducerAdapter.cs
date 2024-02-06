using Confluent.Kafka;

namespace Net.Leksi.MicroService.Common;

public class KafkaProducerAdapter<TMessage> : KafkaProducerBase<TMessage>, IKafkaProducer<TMessage> where TMessage : class
{
    public KafkaProducerAdapter(KafkaConfig config): base(config) { }
    public async Task<List<DeliveryResult<string, string>>> ProduceAsync(string key, TMessage message, CancellationToken stoppingToken)
    {
        return await base.ProduceAsync(key, message, _config.Topics, stoppingToken);
    }
}
