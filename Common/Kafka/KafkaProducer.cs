using Confluent.Kafka;

namespace Net.Leksi.MicroService.Common;

public class KafkaProducer: KafkaProducerBase
{
    public KafkaProducer(IServiceProvider services, KafkaProducerConfig config): base(services, config) { }
    public async Task<List<DeliveryResult<string, string>>> ProduceAsync<TMessage>(TMessage message, CancellationToken stoppingToken) where TMessage : KafkaMessageBase
    {
        return await ProduceAsync<TMessage>(message, _config.Topics, stoppingToken);
    }
}
