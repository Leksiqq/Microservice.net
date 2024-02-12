using Confluent.Kafka;

namespace Net.Leksi.MicroService.Common;

public class KafkaProducerAdapter(IServiceProvider services, KafkaConfig config) : KafkaProducerBase(services, config), IKafkaProducer
{
    public async Task<List<DeliveryResult<string, string>>> ProduceAsync<TMessage>(TMessage message, CancellationToken stoppingToken) where TMessage : KafkaMessageBase
    {
        return await ProduceAsync<TMessage>(message, config.Topics, stoppingToken);
    }
}
