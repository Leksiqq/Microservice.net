using Confluent.Kafka;

namespace Net.Leksi.MicroService.Common;

public interface IKafkaProducer: IDisposable
{
    Task<List<DeliveryResult<string, string>>> ProduceAsync<TMessage>(TMessage message, CancellationToken stoppingToken) where TMessage : KafkaMessageBase;
}
